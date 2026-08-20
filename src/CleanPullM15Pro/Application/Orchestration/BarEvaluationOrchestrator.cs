// ================================================================================
// FILE: src\CleanPullM15Pro\Application\Orchestration\BarEvaluationOrchestrator.cs
// ================================================================================

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
/// Configuration parameters for symbol evaluation, strategy rules, and trade management.
/// </summary>
public sealed record SymbolEvaluationConfig
{
    /// <summary>Symbol name (e.g. EURUSD).</summary>
    public string SymbolName { get; init; } = string.Empty;

    /// <summary>Minimum stop distance in ATR units.</summary>
    public double MinStopAtr { get; init; }

    /// <summary>Maximum stop distance in ATR units.</summary>
    public double MaxStopAtr { get; init; }

    /// <summary>Swing lookback candle count.</summary>
    public int SwingLookbackCount { get; init; } = 20;

    /// <summary>Commission per lot round-turn.</summary>
    public double CommissionPerLotRoundTurn { get; init; }

    /// <summary>Conservative slippage in price units.</summary>
    public double ConservativeSlippagePriceUnits { get; init; }

    /// <summary>Maximum allowed slippage in price units.</summary>
    public double MaxAllowedSlippagePriceUnits { get; init; }

    /// <summary>Per-trade risk percentage of equity. Default 1.50% (0.015).</summary>
    public double RiskPerTradePct { get; init; } = 0.015;

    /// <summary>Maximum allowed total reserved risk percentage. Default 3.00% (0.030).</summary>
    public double MaxReservedRiskPct { get; init; } = 0.030;

    /// <summary>Whether to evaluate the Breakout strategy when Pullback conditions are not met.</summary>
    public bool EnableBreakout { get; init; } = false;

    // --- Session Hours Filter ---
    /// <summary>Whether to enforce London and New York entry session windows.</summary>
    public bool EnableSessionFilter { get; init; } = false;

    // --- BreakEven & Scale-Out Trade Management ---
    /// <summary>Whether to activate the BreakEven mechanism on open positions.</summary>
    public bool EnableBreakeven { get; init; } = true;

    /// <summary>R-multiple gain required to move Stop Loss to BreakEven (e.g. 1.0 for +1.0R).</summary>
    public double BreakevenTriggerR { get; init; } = 1.0;

    /// <summary>Optional buffer distance in price units added to entry when moving to BreakEven.</summary>
    public double BreakevenOffsetPriceUnits { get; init; } = 0.0;

    /// <summary>Whether to execute a partial close at target R.</summary>
    public bool EnableScaleOut { get; init; } = false;

    /// <summary>Ratio of volume to close on scale-out (e.g. 0.50 for 50%).</summary>
    public double ScaleOutRatio { get; init; } = 0.50;

    /// <summary>Favorable R-multiple required to trigger partial scale-out.</summary>
    public double ScaleOutTriggerR { get; init; } = 1.50;

    /// <summary>Whether to manage runner position with dynamic ATR trailing stop.</summary>
    public bool EnableAtrTrailing { get; init; } = false;

    /// <summary>ATR multiplier for runner trailing stop.</summary>
    public double TrailingAtrMultiplier { get; init; } = 2.50;

    // --- Pullback Thresholds ---
    /// <summary>Pullback C3 — ADX minimum threshold. Calibrated to 16.0.</summary>
    public double PullbackAdxThreshold { get; init; } = 16.0;

    /// <summary>Pullback C4 — Upper bound distance from EMA20 in ATR units.</summary>
    public double PullbackUpperBoundAtr { get; init; } = 0.20;

    /// <summary>Pullback C5 — Lower bound distance from EMA20 in ATR units. Calibrated to 0.35.</summary>
    public double PullbackLowerBoundAtr { get; init; } = 0.35;

    /// <summary>Pullback C7 — RSI prior threshold for buy signals before crossover. Calibrated to 55.0.</summary>
    public double PullbackRsiPriorBuyThreshold { get; init; } = 55.0;

    /// <summary>Pullback C7 — RSI prior threshold for sell signals before crossover. Calibrated to 45.0.</summary>
    public double PullbackRsiPriorSellThreshold { get; init; } = 45.0;

    /// <summary>Pullback C9 — Close Location Value threshold for buy signals.</summary>
    public double PullbackClvBuyThreshold { get; init; } = 0.55;

    /// <summary>Pullback C9 — Close Location Value threshold for sell signals.</summary>
    public double PullbackClvSellThreshold { get; init; } = 0.45;

    /// <summary>Pullback C10 — Minimum candle body size relative to ATR. Calibrated to 0.20.</summary>
    public double PullbackBodyAtrCoeff { get; init; } = 0.20;

    // --- Breakout Thresholds ---
    /// <summary>Breakout AB.2 — Lookback candle count K for range calculation.</summary>
    public int BreakoutLookback { get; init; } = 10;

    /// <summary>Breakout AB.3 — ADX minimum threshold.</summary>
    public double BreakoutAdxThreshold { get; init; } = 25.0;

    /// <summary>Breakout AB.3 — Close Location Value threshold for buy signals.</summary>
    public double BreakoutClvBuyThreshold { get; init; } = 0.60;

    /// <summary>Breakout AB.3 — Close Location Value threshold for sell signals.</summary>
    public double BreakoutClvSellThreshold { get; init; } = 0.40;

    /// <summary>Breakout AB.4 — Maximum entry extension distance from EMA20 in ATR units.</summary>
    public double BreakoutMaxExtensionAtr { get; init; } = 2.00;

    /// <summary>Breakout AB.5 — Tick volume baseline multiplier threshold.</summary>
    public double BreakoutVolumeMultiplier { get; init; } = 1.10;
}

/// <summary>
/// Orchestrates the bar evaluation lifecycle across H1 trend, M15 filters, risk sizing, and execution.
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

    private const string PullbackLabel = "CleanPullM15Pro";
    private const string BreakoutLabel = "CleanPullM15Pro_Breakout";

    /// <summary>
    /// Initializes a new instance of the <see cref="BarEvaluationOrchestrator"/> class.
    /// </summary>
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
    /// Evaluates the market conditions on bar close and submits orders when all criteria match.
    /// </summary>
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

        // Step 4 — Symbol state must be READY
        if (_stateMachine.Current != BotState.Ready)
            return Reject(symbol, ReasonCode.RejectDuplicateOrder, $"Symbol state is {_stateMachine.Current}, not Ready");

        // Step 5 — Trading window and news
        if (_config.EnableSessionFilter && !_clock.IsWithinEntryWindow(barCloseTimeUtc))
            return Reject(symbol, ReasonCode.RejectOutsideWindow, "Outside allowed 4-hour trading session window");

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
        {
            string trendDiag = SignalEvaluator.DescribeH1TrendDiagnostics(
                h1.Ema50Bar1, h1.Ema200Bar1, h1.Ema50Bar6, h1.Atr14Bar1);
            return Reject(symbol, ReasonCode.TrendNeutral, "H1 trend neutral — " + trendDiag);
        }

        // Step 8 — M15 volatility regime
        var volResult = SignalEvaluator.EvaluateVolatilityBand(m15.Atr14Bar1, m15.SmaAtr100Bar1);
        if (volResult.RejectionReason.HasValue)
            return Reject(symbol, volResult.RejectionReason.Value, "Volatility ratio outside tradeable band");

        // Step 9 — Pullback signal, with optional Breakout fallback
        if (m15.Candles.Length == 0)
            return Reject(symbol, ReasonCode.RejectDataInvalid, "No M15 candle available");

        var signalCandle = m15.Candles[0];
        var signal = trend == TradeDirection.Buy
            ? SignalEvaluator.EvaluateBuySignal(
                trend, signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1,
                m15.Rsi14Bar2, m15.Rsi14Bar1,
                _config.PullbackAdxThreshold, _config.PullbackLowerBoundAtr,
                _config.PullbackUpperBoundAtr, _config.PullbackClvBuyThreshold,
                _config.PullbackRsiPriorBuyThreshold, _config.PullbackBodyAtrCoeff)
            : SignalEvaluator.EvaluateSellSignal(
                trend, signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1,
                m15.Rsi14Bar2, m15.Rsi14Bar1,
                _config.PullbackAdxThreshold, _config.PullbackLowerBoundAtr,
                _config.PullbackUpperBoundAtr, _config.PullbackClvSellThreshold,
                _config.PullbackRsiPriorSellThreshold, _config.PullbackBodyAtrCoeff);

        bool isBreakout = false;

        if (signal.RejectionReason.HasValue)
        {
            if (!_config.EnableBreakout)
            {
                string pullbackDiag = trend == TradeDirection.Buy
                    ? SignalEvaluator.DescribeBuyDiagnostics(signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1)
                    : SignalEvaluator.DescribeSellDiagnostics(signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1);
                return Reject(symbol, signal.RejectionReason.Value, "Pullback signal conditions not met — " + pullbackDiag);
            }

            var breakoutSignal = trend == TradeDirection.Buy
                ? TrendContinuationSignalEvaluator.EvaluateBuySignal(
                    trend, m15.Candles, m15.Adx14Bar1, m15.Atr14Bar1,
                    _config.BreakoutAdxThreshold, _config.BreakoutClvBuyThreshold,
                    _config.BreakoutLookback)
                : TrendContinuationSignalEvaluator.EvaluateSellSignal(
                    trend, m15.Candles, m15.Adx14Bar1, m15.Atr14Bar1,
                    _config.BreakoutAdxThreshold, _config.BreakoutClvSellThreshold,
                    _config.BreakoutLookback);

            if (breakoutSignal.RejectionReason.HasValue)
            {
                string pullbackDiag = trend == TradeDirection.Buy
                    ? SignalEvaluator.DescribeBuyDiagnostics(signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1)
                    : SignalEvaluator.DescribeSellDiagnostics(signalCandle, m15.Ema20Bar1, m15.Ema50Bar1, m15.Adx14Bar1, m15.Atr14Bar1, m15.Rsi14Bar2, m15.Rsi14Bar1);
                return Reject(symbol, breakoutSignal.RejectionReason.Value,
                    "Neither Pullback nor Breakout signal conditions met — Pullback: " + pullbackDiag);
            }

            signal = breakoutSignal;
            isBreakout = true;
        }

        // Step 10 — Volume and spread filters
        if (!VolumeFilter.IsBaselineValid(quality.VolumeValidObservations))
            return Reject(symbol, ReasonCode.RejectVolumeBaseline, "Volume baseline has too few observations");

        bool volumePasses = isBreakout
            ? VolumeFilter.Passes(quality.TickVolumeBar1, quality.VolumeBaseline, _config.BreakoutVolumeMultiplier)
            : VolumeFilter.Passes(quality.TickVolumeBar1, quality.VolumeBaseline);

        if (!volumePasses)
            return Reject(symbol,
                isBreakout ? ReasonCode.RejectBreakoutVolume : ReasonCode.RejectVolume,
                "Tick volume below baseline threshold");

        if (!SpreadFilter.IsBaselineValid(quality.SpreadValidObservations))
            return Reject(symbol, ReasonCode.RejectSpreadBaseline, "Spread baseline has too few observations");

        if (!SpreadFilter.Passes(quality.CurrentSpread, quality.SpreadBaseline, quality.AbsoluteSpreadCap))
            return Reject(symbol, ReasonCode.RejectSpread, "Spread above allowed threshold");

        // Step 11 — Confirmed swing
        var swing = SwingDetector.SelectSwing(m15.Candles, signal.Direction, _config.SwingLookbackCount);
        if (!swing.Found)
            return Reject(symbol, ReasonCode.RejectNoSwing, "No confirmed swing found in lookback window");

        // Step 12 — Entry
        double entryPrice = signal.Direction == TradeDirection.Buy
            ? OrderEntryCalculator.ComputeBuyEntry(signalCandle.High, m15.Atr14Bar1, symbolInfo.TickSize)
            : OrderEntryCalculator.ComputeSellEntry(signalCandle.Low, m15.Atr14Bar1, symbolInfo.TickSize);

        // AB.4 — Extension filter (Breakout only)
        if (isBreakout)
        {
            var extensionReject = TrendContinuationSignalEvaluator.ValidateExtension(
                entryPrice, m15.Ema20Bar1, m15.Atr14Bar1, _config.BreakoutMaxExtensionAtr);
            if (extensionReject.HasValue)
                return Reject(symbol, extensionReject.Value, "Breakout entry too extended from EMA20 relative to ATR");
        }

        double stopLoss = StopLossCalculator.ComputeLevel(signal.Direction, swing.Price, m15.Atr14Bar1);

        // Step 13 — Stop-distance bounds
        var distanceReject = StopLossCalculator.ValidateDistance(
            entryPrice, stopLoss, m15.Atr14Bar1, _config.MinStopAtr, _config.MaxStopAtr);
        if (distanceReject.HasValue)
        {
            // Diagnostic-only: recompute the same ratio ValidateDistance used internally,
            // purely for the rejection log — does not affect the accept/reject decision itself.
            double stopDistanceAtrDebug = m15.Atr14Bar1 > 0
                ? Math.Abs(entryPrice - stopLoss) / m15.Atr14Bar1
                : double.NaN;

            return Reject(symbol, distanceReject.Value,
                $"Stop distance outside allowed ATR bounds — computed={stopDistanceAtrDebug:F3} ATR " +
                $"(allowed [{_config.MinStopAtr:F2}, {_config.MaxStopAtr:F2}]), " +
                $"swing={swing.Price:F5}, entry={entryPrice:F5}, sl={stopLoss:F5}, atr14Bar1={m15.Atr14Bar1:F5}");
        }

        double referencePrice = signal.Direction == TradeDirection.Buy
            ? _symbols.CurrentBid(symbol)
            : _symbols.CurrentAsk(symbol);

        var brokerLimitReject = StopLossCalculator.ValidateBrokerLimits(
            signal.Direction, stopLoss, referencePrice, symbolInfo.StopLevel);
        if (brokerLimitReject.HasValue)
            return Reject(symbol, brokerLimitReject.Value, "SL violates broker StopLevel/FreezeLevel");

        // Step 14 — Position size (1.50% configured risk)
        double tradeRiskMoney = isBreakout
            ? PositionSizer.ComputeTradeRiskMoney(account.Equity, _config.RiskPerTradePct / 2.0)
            : PositionSizer.ComputeTradeRiskMoney(account.Equity, _config.RiskPerTradePct);

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

        double thisTradeRisk = roundedVolumeResult.Volume * lossPerLotAtSl;
        if (!PortfolioRiskGuard.PassesReservedRisk(account.TotalReservedRisk + thisTradeRisk, account.Equity, _config.MaxReservedRiskPct))
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
            ExpiryUtc = expiry,
            Label = isBreakout ? BreakoutLabel : PullbackLabel
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
            $"Order submitted ({(isBreakout ? "Breakout" : "Pullback")}): entry={entryPrice}, sl={stopLoss}, tp={takeProfit}, vol={roundedVolumeResult.Volume}");

        return EvaluationOutcome.Submitted(signal.Direction, orderRequest);
    }

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