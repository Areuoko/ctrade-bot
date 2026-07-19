using System;
using CleanPullM15Pro.Application.Contracts;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Application.StateMachine;
using CleanPullM15Pro.Domain.Market;
using CleanPullM15Pro.Domain.Orders;
using CleanPullM15Pro.Domain.Risk;
using CleanPullM15Pro.Domain.Signals;

namespace CleanPullM15Pro.Application.Orchestration;

/// <summary>
/// Fixed configuration for one symbol's evaluation. Values come from spec
/// section 11.3 (per-symbol stop bounds) and account-level parameters.
/// </summary>
public sealed class SymbolEvaluationConfig
{
    /// <summary>Target symbol name for this evaluation configuration.</summary>
    public string SymbolName { get; init; } = string.Empty;
    /// <summary>Minimum stop distance as a multiple of ATR14 (spec section 11.3).</summary>
    public double MinStopAtr { get; init; }
    /// <summary>Maximum stop distance as a multiple of ATR14 (spec section 11.3).</summary>
    public double MaxStopAtr { get; init; }
    /// <summary>Number of M15 bars to look back for confirmed swing selection.</summary>
    public int SwingLookbackCount { get; init; } = 20;
    /// <summary>Round-turn commission per lot, included in the cost-adjusted risk.</summary>
    public double CommissionPerLotRoundTurn { get; init; }
    /// <summary>Conservative (worst-case) slippage in price units, factored into loss-per-lot.</summary>
    public double ConservativeSlippagePriceUnits { get; init; }
    /// <summary>Maximum slippage in price units allowed for the trade to proceed.</summary>
    public double MaxAllowedSlippagePriceUnits { get; init; }
}

/// <summary>
/// Orchestrates one full evaluation cycle on M15 bar close, following the
/// decision order in spec section 20. Steps 15 (basket/USD exposure) are
/// intentionally omitted — this build is scoped to a single symbol (EURUSD)
/// by explicit decision; re-add PortfolioRiskGuard calls before adding a
/// second correlated symbol.
/// </summary>
public sealed class BarEvaluationOrchestrator
{
    private readonly IExecutionPort _execution;
    private readonly ISymbolPort _symbols;
    private readonly IClockPort _clock;
    private readonly INewsCalendarPort _news;
    private readonly IStateStorePort _stateStore;
    private readonly ILogPort _log;
    private readonly SymbolEvaluationConfig _config;
    private readonly SymbolStateMachine _stateMachine;

    /// <summary>
    /// Constructs the orchestrator with its execution, market, clock, news, state-store,
    /// logging, per-symbol configuration, and state-machine dependencies.
    /// </summary>
    /// <param name="execution">Execution port used to query/submit/cancel broker orders.</param>
    /// <param name="symbols">Symbol metadata and live-pricing port.</param>
    /// <param name="clock">Time/session-window port.</param>
    /// <param name="news">News-calendar port for prohibited-window checks.</param>
    /// <param name="stateStore">Persistence port for bot state and daily/weekly counters.</param>
    /// <param name="log">Structured logging port.</param>
    /// <param name="config">Per-symbol configuration (stop bounds, costs, slippage).</param>
    /// <param name="stateMachine">Symbol state machine tracking the current per-symbol lifecycle.</param>
    public BarEvaluationOrchestrator(
        IExecutionPort execution,
        ISymbolPort symbols,
        IClockPort clock,
        INewsCalendarPort news,
        IStateStorePort stateStore,
        ILogPort log,
        SymbolEvaluationConfig config,
        SymbolStateMachine stateMachine)
    {
        _execution = execution;
        _symbols = symbols;
        _clock = clock;
        _news = news;
        _stateStore = stateStore;
        _log = log;
        _config = config;
        _stateMachine = stateMachine;
    }

    /// <summary>
    /// Runs one full M15 bar-close evaluation cycle in spec decision order (section 20):
    /// validate data/clock → risk locks → state/window/news checks → H1 trend → M15
    /// volatility → pullback signal → volume/spread → swing → entry/SL/TP → sizing →
    /// submit → confirm. Always returns an outcome (submitted, no-signal, or rejected).
    /// </summary>
    /// <param name="h1">Pre-computed H1 snapshot from closed bars.</param>
    /// <param name="m15">Pre-computed M15 snapshot, including closed candles.</param>
    /// <param name="quality">Volume/spread baselines and current values.</param>
    /// <param name="account">Account-level figures for risk and sizing.</param>
    /// <param name="symbolInfo">Validated symbol metadata.</param>
    /// <param name="barCloseTimeUtc">UTC time of the bar close being evaluated.</param>
    /// <returns>The <see cref="EvaluationOutcome"/> for this evaluation cycle.</returns>
    public EvaluationOutcome Evaluate(
        H1Snapshot h1,
        M15Snapshot m15,
        MarketQualitySnapshot quality,
        AccountSnapshot account,
        SymbolInfo symbolInfo,
        DateTime barCloseTimeUtc)
    {
        string symbol = _config.SymbolName;

        // Step 2 — Validate data and clock
        if (!symbolInfo.IsValid)
            return Reject(symbol, ReasonCode.SymbolDisabled, "Symbol metadata invalid");

        if (!h1.IsValid || !m15.IsValid)
            return Reject(symbol, ReasonCode.RejectDataInvalid, "H1 or M15 snapshot invalid/warm-up incomplete");

        // Step 3 — Global/weekly/daily risk locks
        if (_stateStore.GetKillSwitchActive())
            return Reject(symbol, ReasonCode.KillSwitch, "Kill switch active — manual re-activation required");

        if (DrawdownGuard.IsKillSwitchTriggered(account.EquityHighWaterMark, account.Equity))
        {
            _stateStore.SetKillSwitchActive(true);
            var pending = _execution.GetBrokerState(symbol);
            if (pending.HasPendingOrder && pending.OrderOrPositionId is not null)
                _execution.CancelPendingOrder(pending.OrderOrPositionId);
            return Reject(symbol, ReasonCode.KillSwitch, "Kill switch newly triggered");
        }

        var dailyReject = DrawdownGuard.ValidateDailyDrawdown(account.DailyStartEquity, account.Equity);
        if (dailyReject.HasValue)
            return Reject(symbol, dailyReject.Value, "Daily drawdown limit reached");

        var weeklyReject = DrawdownGuard.ValidateWeeklyDrawdown(account.WeeklyStartEquity, account.Equity);
        if (weeklyReject.HasValue)
            return Reject(symbol, weeklyReject.Value, "Weekly drawdown limit reached");

        var entriesReject = DrawdownGuard.ValidateDailyEntries(account.FilledEntriesToday);
        if (entriesReject.HasValue)
            return Reject(symbol, entriesReject.Value, "Max daily entries reached");

        var lossStreakReject = DrawdownGuard.ValidateConsecutiveLoss(account.ConsecutiveLossCount);
        if (lossStreakReject.HasValue)
            return Reject(symbol, lossStreakReject.Value, "Consecutive loss limit reached");

        // Step 4 — Symbol state must be READY (only one order/position per symbol)
        if (_stateMachine.Current != BotState.Ready)
            return Reject(symbol, ReasonCode.RejectDuplicateOrder, $"Symbol state is {_stateMachine.Current}, not Ready");

        // Step 5 — Trading window and news
        if (_clock.IsPastFridayNewOrderCutoff(barCloseTimeUtc))
            return Reject(symbol, ReasonCode.RejectFridayCutoff, "Past Friday new-order cutoff");

        if (_clock.IsWithinRolloverWindow(barCloseTimeUtc))
            return Reject(symbol, ReasonCode.RejectRollover, "Within rollover blackout window");

        if (!_news.IsAvailableAndFresh)
            return Reject(symbol, ReasonCode.RejectNewsCalendar, "News calendar unavailable or stale");

        if (_news.IsInProhibitedWindow(symbol, barCloseTimeUtc))
            return Reject(symbol, ReasonCode.RejectNewsWindow, "Within prohibited news window");

        // Step 7 — H1 trend
        var trend = SignalEvaluator.EvaluateH1Trend(h1.Ema50Bar1, h1.Ema200Bar1, h1.Ema50Bar6, h1.Atr14Bar1);
        if (trend == TradeDirection.None)
            return Reject(symbol, ReasonCode.TrendNeutral, "H1 trend neutral");

        // Step 8 — M15 volatility regime
        var volResult = SignalEvaluator.EvaluateVolatilityBand(m15.Atr14Bar1, m15.SmaAtr100Bar1);
        if (volResult.RejectionReason.HasValue)
            return Reject(symbol, volResult.RejectionReason.Value, "Volatility ratio outside tradeable band");

        // Step 9 — Pullback/momentum signal
        if (m15.Candles.Length == 0)
            return Reject(symbol, ReasonCode.RejectDataInvalid, "No M15 candle available");

        var signalCandle = m15.Candles[0];
        var signal = trend == TradeDirection.Buy
            ? SignalEvaluator.EvaluateBuySignal(trend, signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1)
            : SignalEvaluator.EvaluateSellSignal(trend, signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1);

        if (signal.RejectionReason.HasValue)
            return Reject(symbol, signal.RejectionReason.Value, "Pullback signal conditions not met");

        // Step 10 — Volume and spread filters
        if (!VolumeFilter.IsBaselineValid(quality.VolumeValidObservations))
            return Reject(symbol, ReasonCode.RejectVolumeBaseline, "Volume baseline has too few observations");

        if (!VolumeFilter.Passes(quality.TickVolumeBar1, quality.VolumeBaseline))
            return Reject(symbol, ReasonCode.RejectVolume, "Tick volume below baseline threshold");

        if (!SpreadFilter.Passes(quality.CurrentSpread, quality.SpreadBaseline, quality.AbsoluteSpreadCap))
            return Reject(symbol, ReasonCode.RejectSpread, "Spread above allowed threshold");

        // Step 11 — Confirmed swing
        var swing = SwingDetector.SelectSwing(m15.Candles, signal.Direction, _config.SwingLookbackCount);
        if (!swing.Found)
            return Reject(symbol, ReasonCode.RejectNoSwing, "No confirmed swing found in lookback window");

        // Step 12 — Entry, SL, TP
        double entryPrice = signal.Direction == TradeDirection.Buy
            ? OrderEntryCalculator.ComputeBuyEntry(signalCandle.High, m15.Atr14Bar1, symbolInfo.TickSize)
            : OrderEntryCalculator.ComputeSellEntry(signalCandle.Low, m15.Atr14Bar1, symbolInfo.TickSize);

        double stopLoss = StopLossCalculator.ComputeLevel(signal.Direction, swing.Price, m15.Atr14Bar1);

        // Step 13 — Stop-distance bounds
        var distanceReject = StopLossCalculator.ValidateDistance(
            entryPrice, stopLoss, m15.Atr14Bar1, _config.MinStopAtr, _config.MaxStopAtr);
        if (distanceReject.HasValue)
            return Reject(symbol, distanceReject.Value, "Stop distance outside allowed ATR bounds");

        double referencePrice = signal.Direction == TradeDirection.Buy
            ? _symbols.CurrentBid(symbol)
            : _symbols.CurrentAsk(symbol);

        var brokerLimitReject = StopLossCalculator.ValidateBrokerLimits(
            signal.Direction, stopLoss, referencePrice, symbolInfo.StopLevel);
        if (brokerLimitReject.HasValue)
            return Reject(symbol, brokerLimitReject.Value, "SL violates broker StopLevel/FreezeLevel");

        // Step 14 — Position size (single-symbol; no basket/USD exposure step here — see class doc)
        double tradeRiskMoney = PositionSizer.ComputeTradeRiskMoney(account.Equity);
        double lossPerLotAtSl = PositionSizer.ComputeLossPerLotAtSL(
            entryPrice, stopLoss, symbolInfo.TickSize, symbolInfo.TickValue,
            _config.CommissionPerLotRoundTurn, _config.ConservativeSlippagePriceUnits);

        var rawVolumeResult = PositionSizer.ComputeRawVolume(tradeRiskMoney, lossPerLotAtSl);
        if (rawVolumeResult.Rejection.HasValue)
            return Reject(symbol, rawVolumeResult.Rejection.Value, "Raw volume calculation invalid");

        var roundedVolumeResult = PositionSizer.RoundVolume(rawVolumeResult.Volume, symbolInfo.LotStep, symbolInfo.MinLot);
        if (roundedVolumeResult.Rejection.HasValue)
            return Reject(symbol, roundedVolumeResult.Rejection.Value, "Volume below minimum lot after rounding");

        var riskExceededReject = PositionSizer.ValidatePostRoundingRisk(
            roundedVolumeResult.Volume, lossPerLotAtSl, tradeRiskMoney);
        if (riskExceededReject.HasValue)
            return Reject(symbol, riskExceededReject.Value, "Post-rounding risk exceeds per-trade cap");

        if (!PositionSizer.PassesMarginCheck(EstimateRequiredMargin(roundedVolumeResult.Volume, symbolInfo, referencePrice, account.Leverage), account.FreeMargin))
            return Reject(symbol, ReasonCode.RejectInsufficientMargin, "Insufficient free margin");

        // Reserved risk cap (Rule O.2) — single symbol, so reserved risk == this trade's risk
        double thisTradeRisk = roundedVolumeResult.Volume * lossPerLotAtSl;
        if (!PortfolioRiskGuard.PassesReservedRisk(account.TotalReservedRisk + thisTradeRisk, account.Equity))
            return Reject(symbol, ReasonCode.RejectReservedRisk, "Total reserved risk cap exceeded");

        // Take-profit
        double takeProfit = TradeManagementCalculator.ComputeTakeProfit(
            signal.Direction, entryPrice, stopLoss, symbolInfo.TickSize);

        var expiry = OrderEntryCalculator.ComputeExpiry(barCloseTimeUtc);

        var orderRequest = new PendingOrderRequest
        {
            SymbolName = symbol,
            Direction = signal.Direction,
            EntryPrice = entryPrice,
            StopLoss = stopLoss,
            TakeProfit = takeProfit,
            Volume = roundedVolumeResult.Volume,
            ExpiryUtc = expiry
        };

        // Step 16/17 — Submit and confirm
        _stateMachine.TryTransition(BotState.SignalFound);
        _stateMachine.TryTransition(BotState.OrderPending);

        var submitResult = _execution.SubmitPendingOrder(orderRequest);

        if (!submitResult.Success)
        {
            _stateMachine.TryTransition(BotState.Ready);
            _stateStore.SetState(symbol, BotState.Ready);
            var details = "Broker rejected order: " + (submitResult.ErrorDescription ?? "unknown");
            _log.LogRejection(symbol, ReasonCode.RejectDataInvalid, details);
            return EvaluationOutcome.NoSignal(details);
        }

        // Step 18 — Persist state and log
        _stateStore.SetState(symbol, BotState.OrderPending);
        _log.LogDecision(symbol, signal.Direction, null,
            $"Order submitted: entry={entryPrice}, sl={stopLoss}, tp={takeProfit}, vol={roundedVolumeResult.Volume}");

        return EvaluationOutcome.Submitted(signal.Direction, orderRequest);
    }

    /// <summary>
    /// Rough margin estimate for the pre-submit check (Rule L.4): notional / leverage.
    /// This is a fallback only — Infrastructure should prefer the broker's own
    /// margin calculation (cTrader exposes Symbol.GetEstimatedMargin) when available,
    /// since real margin can depend on tiered leverage, hedging mode, etc.
    /// </summary>
    private static double EstimateRequiredMargin(double volume, SymbolInfo symbolInfo, double price, double leverage)
    {
        double notional = volume * symbolInfo.ContractSize * price;
        return leverage > 0 ? notional / leverage : notional;
    }

    private EvaluationOutcome Reject(string symbol, ReasonCode reason, string details)
    {
        _log.LogRejection(symbol, reason, details);
        return EvaluationOutcome.Rejected(reason, details);
    }
}
