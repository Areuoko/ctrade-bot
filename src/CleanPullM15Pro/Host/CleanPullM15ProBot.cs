using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using CleanPullM15Pro.Application.Contracts;
using CleanPullM15Pro.Application.Orchestration;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Application.StateMachine;
using CleanPullM15Pro.Domain.Market;
using CleanPullM15Pro.Domain.Orders;
using CleanPullM15Pro.Domain.Risk;
using CleanPullM15Pro.Infrastructure.CTrader.Clock;
using CleanPullM15Pro.Infrastructure.CTrader.Execution;
using CleanPullM15Pro.Infrastructure.CTrader.Logging;
using CleanPullM15Pro.Infrastructure.CTrader.MarketData;
using CleanPullM15Pro.Infrastructure.CTrader.News;
using CleanPullM15Pro.Infrastructure.CTrader.State;
using CleanPullM15Pro.Infrastructure.CTrader.Symbols;

namespace CleanPullM15Pro.Host;

/// <summary>
/// cBot lifecycle adapter. Single-symbol build (EURUSD) — the chart/instance this
/// cBot is attached to determines the traded symbol; add a separate instance per
/// symbol in cTrader if you want more than one, but portfolio-level risk sharing
/// (metal basket, USD exposure — Rules O.3/O.4) is NOT implemented in this build.
/// </summary>
[Robot(AccessRights = AccessRights.None)]
public class CleanPullM15ProBot : Robot
{
    /// <summary>Finnhub API key for the live economic-calendar feed. Leave empty to fall back to the manual/disabled calendar. Never commit a real key — set this per-instance in the cTrader UI only.</summary>
    [Parameter("Finnhub API Key (leave empty to disable live feed)", DefaultValue = "")]
    public string FinnhubApiKey { get; set; } = "";
     
    /// <summary>UTC hour (0–23) at which the daily counter rollover window is centered (Rule P.1 rollover reference).</summary>
    [Parameter("Rollover hour (UTC)", DefaultValue = 21, MinValue = 0, MaxValue = 23)]
    public int RolloverHourUtc { get; set; }

    /// <summary>Minimum stop distance as a multiple of ATR14[1] (Rule J.2 lower bound on the stop).</summary>
    [Parameter("Min stop distance (ATR)", DefaultValue = 0.80)]
    public double MinStopAtr { get; set; }

    /// <summary>Maximum stop distance as a multiple of ATR14[1] (Rule J.2 upper bound on the stop).</summary>
    [Parameter("Max stop distance (ATR)", DefaultValue = 1.80)]
    public double MaxStopAtr { get; set; }

    /// <summary>Absolute spread cap in price units; entry rejected when current spread exceeds this (Rule F.3).</summary>
    [Parameter("Absolute spread cap (price units)", DefaultValue = 0.00020)]
    public double AbsoluteSpreadCap { get; set; }

    /// <summary>Estimated round-turn commission per lot, folded into LossPerLotAtSL (Rule L.2).</summary>
    [Parameter("Commission per lot round-turn", DefaultValue = 0)]
    public double CommissionPerLotRoundTurn { get; set; }

    /// <summary>Conservative extra price distance assumed as slippage when sizing loss per lot (Rule L.2).</summary>
    [Parameter("Conservative slippage (price units)", DefaultValue = 0.00005)]
    public double ConservativeSlippagePriceUnits { get; set; }

    /// <summary>TESTING ONLY — when true the news filter (Rule N.*) is bypassed. Never enable on a live account; see open-questions.md.</summary>
    [Parameter("Disable news filter (TESTING ONLY — see open-questions.md)", DefaultValue = true)]
    public bool DisableNewsFilter { get; set; }

    private BarEvaluationOrchestrator _orchestrator = null!;
    private CTraderSnapshotBuilder _snapshotBuilder = null!;
    private CTraderStateStoreAdapter _stateStore = null!;
    private CTraderExecutionAdapter _execution = null!;
    private CTraderClockAdapter _clock = null!;
    private CTraderLogAdapter _log = null!;
    private CTraderSymbolAdapter _symbols = null!;
    private SymbolStateMachine _stateMachine = null!;
    private SymbolEvaluationConfig _config = null!;
    private FinnhubNewsCalendarAdapter? _finnhubNews;

    private const int SwingLookbackCount = 20;
    private const int SwingLookbackMargin = 5; // extra candles so right-side confirmation has room

    /// <summary>Called once when the robot starts: wires all cTrader adapters, loads persisted state, builds the evaluation orchestrator, and subscribes to position-closed events.</summary>
    protected override void OnStart()
    {
        string symbolName = SymbolName;

        var marketData = new CTraderMarketDataAdapter(this);
        _snapshotBuilder = new CTraderSnapshotBuilder(this, marketData);
        _stateStore = new CTraderStateStoreAdapter(this, symbolName);
        _execution = new CTraderExecutionAdapter(this);
        _clock = new CTraderClockAdapter(this, TimeSpan.FromHours(RolloverHourUtc));
        _log = new CTraderLogAdapter(this);
        _symbols = new CTraderSymbolAdapter(this);

        INewsCalendarPort news;
        if (!string.IsNullOrWhiteSpace(FinnhubApiKey))
        {
            _finnhubNews = new FinnhubNewsCalendarAdapter(
                FinnhubApiKey,
                refreshInterval: TimeSpan.FromHours(4),
                stalenessThreshold: TimeSpan.FromHours(8),
                log: _log);
            news = _finnhubNews;
            _log.LogDecision(symbolName, TradeDirection.None, null, "News calendar: Finnhub live feed enabled");
        }
        else
        {
            news = new ManualNewsCalendarAdapter(new List<NewsEvent>(), treatEmptyAsUnavailable: !DisableNewsFilter);
            if (DisableNewsFilter)
                _log.LogError(symbolName, "News filter is DISABLED (testing mode) — Rule N.* is not enforced. Do not use on a live account.");
        }

        _config = new SymbolEvaluationConfig
        {
            SymbolName = symbolName,
            MinStopAtr = MinStopAtr,
            MaxStopAtr = MaxStopAtr,
            SwingLookbackCount = SwingLookbackCount,
            CommissionPerLotRoundTurn = CommissionPerLotRoundTurn,
            ConservativeSlippagePriceUnits = ConservativeSlippagePriceUnits,
            MaxAllowedSlippagePriceUnits = ConservativeSlippagePriceUnits * 2
        };

        var initialState = _stateStore.GetState(symbolName);
        _stateMachine = new SymbolStateMachine(initialState);

        _orchestrator = new BarEvaluationOrchestrator(
            _execution, _symbols, _clock, news, _stateStore, _log, _config, _stateMachine);

        EnsureEquityMarksInitialized();

        Positions.Closed += OnPositionClosed;

        _log.LogDecision(symbolName, TradeDirection.None, null, "Bot started, state=" + _stateMachine.Current);
    }

    /// <summary>Called on each closed bar: rolls counters, reconciles broker state, and — when <see cref="BotState.Ready"/> — builds the H1/M15/quality/account snapshots and runs the evaluation orchestrator.</summary>
    protected override void OnBar()
    {
        string symbolName = SymbolName;

        try
        {
            RollDailyWeeklyCountersIfNeeded();
            SyncStateWithBroker();

            if (_stateMachine.Current != BotState.Ready)
                return; // orchestrator itself re-checks, but skip snapshot work if obviously not applicable

            var h1 = _snapshotBuilder.BuildH1Snapshot();
            var m15 = _snapshotBuilder.BuildM15Snapshot(SwingLookbackCount + SwingLookbackMargin);
            var quality = _snapshotBuilder.BuildQualitySnapshot(AbsoluteSpreadCap);
            var symbolInfo = _symbols.GetSymbolInfo(symbolName);
            var account = BuildAccountSnapshot();

            _orchestrator.Evaluate(h1, m15, quality, account, symbolInfo, Server.TimeInUtc);
        }
        catch (Exception ex)
        {
            _log.LogError(symbolName, "Unhandled exception in OnBar: " + ex);
        }
    }

    /// <summary>Re-validates a pending order on every tick (Rule K.3) and cancels it if past the Friday new-order cutoff or inside the rollover window.</summary>
    protected override void OnTick()
    {
        // Rule K.3 — pending order re-validation on every tick.
        if (_stateMachine.Current != BotState.OrderPending)
            return;

        try
        {
            string symbolName = SymbolName;
            var brokerState = _execution.GetBrokerState(symbolName);

            if (!brokerState.HasPendingOrder)
                return; // filled, expired, or cancelled — reconciled on next OnBar/position event

            bool shouldCancel =
                _clock.IsPastFridayNewOrderCutoff(Server.TimeInUtc) ||
                _clock.IsWithinRolloverWindow(Server.TimeInUtc);

            if (shouldCancel && brokerState.OrderOrPositionId is not null)
            {
                _execution.CancelPendingOrder(brokerState.OrderOrPositionId);
                _stateStore.SetState(symbolName, BotState.Ready);
                _stateMachine.TryTransition(BotState.Ready);
                _log.LogDecision(symbolName, TradeDirection.None, ReasonCode.Expired, "Pending order cancelled on tick re-validation");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(SymbolName, "Unhandled exception in OnTick: " + ex);
        }
    }

    /// <summary>Called when the robot stops: detaches the position-closed event handler.</summary>
    protected override void OnStop()
    {
        Positions.Closed -= OnPositionClosed;
        _finnhubNews?.Dispose();
    }

    private void OnPositionClosed(PositionClosedEventArgs args)
    {
        if (args.Position.SymbolName != SymbolName)
            return;

        // Rule P.7 — win/loss/neutral classification by R multiple.
        double r = 0;
        if (args.Position.StopLoss is double sl && args.Position.EntryPrice != sl)
        {
            double riskPerUnit = Math.Abs(args.Position.EntryPrice - sl);
            double pnlPerUnit = args.Position.NetProfit / Math.Max(args.Position.VolumeInUnits, 1);
            r = riskPerUnit > 0 ? pnlPerUnit / riskPerUnit : 0;
        }

        int lossCount = _stateStore.GetConsecutiveLossCount();
        if (r < -0.05)
            lossCount += 1;
        else if (r > 0.05)
            lossCount = 0;
        _stateStore.SetConsecutiveLossCount(lossCount);

        _stateStore.SetState(SymbolName, BotState.Cooldown);
        _stateMachine.TryTransition(BotState.Cooldown);
        _log.LogDecision(SymbolName, TradeDirection.None, null, $"Position closed, R={r:F2}, lossStreak={lossCount}, state=Cooldown");
    }

    private void SyncStateWithBroker()
    {
        var brokerState = _execution.GetBrokerState(SymbolName);

        switch (_stateMachine.Current)
        {
            case BotState.OrderPending when brokerState.HasOpenPosition:
                _stateMachine.TryTransition(BotState.PositionOpen);
                _stateStore.SetState(SymbolName, BotState.PositionOpen);
                _stateStore.SetFilledEntriesToday(_stateStore.GetFilledEntriesToday() + 1);
                break;

            case BotState.OrderPending when !brokerState.HasPendingOrder && !brokerState.HasOpenPosition:
                _stateMachine.TryTransition(BotState.Ready);
                _stateStore.SetState(SymbolName, BotState.Ready);
                break;

            case BotState.PositionOpen when !brokerState.HasOpenPosition:
                _stateMachine.TryTransition(BotState.Cooldown);
                _stateStore.SetState(SymbolName, BotState.Cooldown);
                break;

            case BotState.Cooldown:
                _stateMachine.TryTransition(BotState.Ready);
                _stateStore.SetState(SymbolName, BotState.Ready);
                break;

            case BotState.Ready when brokerState.HasOpenPosition || brokerState.HasPendingOrder:
                _stateMachine.TryTransition(BotState.ReconciliationRequired);
                _stateStore.SetState(SymbolName, BotState.ReconciliationRequired);
                _log.LogError(SymbolName, "State mismatch: internal=Ready but broker has an order/position. RECONCILIATION_REQUIRED.");
                break;
        }
    }

    private AccountSnapshot BuildAccountSnapshot()
    {
        return new AccountSnapshot
        {
            Equity = Account.Equity,
            FreeMargin = Account.FreeMargin,
            Leverage = Account.PreciseLeverage > 0 ? Account.PreciseLeverage : 1,
            DailyStartEquity = _stateStore.GetDailyStartEquity(),
            WeeklyStartEquity = _stateStore.GetWeeklyStartEquity(),
            EquityHighWaterMark = _stateStore.GetEquityHighWaterMark(),
            FilledEntriesToday = _stateStore.GetFilledEntriesToday(),
            ConsecutiveLossCount = _stateStore.GetConsecutiveLossCount(),
            TotalReservedRisk = 0
        };
    }

    private void EnsureEquityMarksInitialized()
    {
        if (_stateStore.GetDailyStartEquity() <= 0)
            _stateStore.SetDailyStartEquity(Account.Equity);

        if (_stateStore.GetWeeklyStartEquity() <= 0)
            _stateStore.SetWeeklyStartEquity(Account.Equity);

        if (_stateStore.GetEquityHighWaterMark() <= 0)
            _stateStore.SetEquityHighWaterMark(Account.Equity);
        else if (Account.Equity > _stateStore.GetEquityHighWaterMark())
            _stateStore.SetEquityHighWaterMark(Account.Equity);
    }

    private void RollDailyWeeklyCountersIfNeeded()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var nyNow = TimeZoneInfo.ConvertTimeFromUtc(Server.TimeInUtc, newYork);
        string today = nyNow.ToString("yyyy-MM-dd");

        string lastReset = _stateStore.GetLastCountersResetDate();
        if (lastReset == today)
        {
            if (Account.Equity > _stateStore.GetEquityHighWaterMark())
                _stateStore.SetEquityHighWaterMark(Account.Equity);
            return;
        }

        _stateStore.SetDailyStartEquity(Account.Equity);
        _stateStore.SetFilledEntriesToday(0);

        if (nyNow.DayOfWeek == DayOfWeek.Monday || string.IsNullOrEmpty(lastReset))
            _stateStore.SetWeeklyStartEquity(Account.Equity);

        if (Account.Equity > _stateStore.GetEquityHighWaterMark())
            _stateStore.SetEquityHighWaterMark(Account.Equity);

        _stateStore.SetLastCountersResetDate(today);
    }
}