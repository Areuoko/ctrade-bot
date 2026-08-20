// ================================================================================
// FILE: backtest\CleanPullM15Pro.Backtest\Engine\ReplayEngine.cs
// ================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using CleanPullM15Pro.Application.Contracts;
using CleanPullM15Pro.Application.Orchestration;
using CleanPullM15Pro.Application.StateMachine;
using CleanPullM15Pro.Backtest.Data;
using CleanPullM15Pro.Backtest.Diagnostics;
using CleanPullM15Pro.Backtest.Indicators;
using CleanPullM15Pro.Backtest.Ports;
using CleanPullM15Pro.Domain.Market;
using CleanPullM15Pro.Domain.Signals;

namespace CleanPullM15Pro.Backtest.Engine;

/// <summary>
/// Replays historical M15 and H1 candles through the BarEvaluationOrchestrator and execution simulation.
/// Supports BreakEven, Scale-Out (Partial Close), and Dynamic ATR Trailing Stops.
/// </summary>
public sealed class ReplayEngine
{
    private const int H1WarmupMinimum = 300;
    private const int M15WarmupMinimum = 500;
    private const int SwingLookbackCount = 20;
    private const double CommissionPerLotRoundTurn = 5.0;
    private const double ConservativeSlippagePriceUnits = 0.00005;

    private readonly Candle[] _m15;
    private readonly Candle[] _h1;
    private readonly SpreadModel _spreadModel;
    private readonly double _initialEquity;

    private readonly double[] _ema20M15;
    private readonly double[] _ema50M15;
    private readonly double[] _rsi14M15;
    private readonly double[] _adx14M15;
    private readonly double[] _atr14M15;
    private readonly double[] _smaAtr100M15;

    private readonly double[] _ema50H1;
    private readonly double[] _ema200H1;
    private readonly double[] _atr14H1;

    private readonly BacktestClockAdapter _clock;
    private readonly BacktestSymbolAdapter _symbol;
    private readonly BacktestNewsCalendarAdapter _news;
    private readonly BacktestStateStoreAdapter _stateStore;
    private readonly BacktestLogAdapter _log;
    private readonly BacktestExecutionAdapter _execution;
    private readonly SymbolStateMachine _stateMachine;
    private readonly BarEvaluationOrchestrator _orchestrator;
    private readonly SymbolEvaluationConfig _config;

    private double _equity;

    /// <summary>Gets the backtest log adapter.</summary>
    public BacktestLogAdapter Log => _log;

    /// <summary>Gets the list of executed trades.</summary>
    public IReadOnlyList<TradeRecord> Trades => _execution.Trades;

    /// <summary>Gets the recorded equity curve points over time.</summary>
    public List<(DateTime TimeUtc, double Equity)> EquityCurve { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayEngine"/> class.
    /// </summary>
    public ReplayEngine(
        Candle[] m15Chronological,
        Candle[] h1Chronological,
        SpreadModel spreadModel,
        double initialEquity,
        int rolloverHourUtc,
        SymbolEvaluationConfig? customConfig = null)
    {
        _m15 = m15Chronological;
        _h1 = h1Chronological;
        _spreadModel = spreadModel;
        _initialEquity = initialEquity;
        _equity = initialEquity;

        var m15Closes = _m15.Select(c => c.Close).ToArray();
        _ema20M15 = WilderIndicators.Ema(m15Closes, 20);
        _ema50M15 = WilderIndicators.Ema(m15Closes, 50);
        _rsi14M15 = WilderIndicators.Rsi(m15Closes, 14);
        _adx14M15 = WilderIndicators.Adx(_m15, 14);
        _atr14M15 = WilderIndicators.Atr(_m15, 14);
        _smaAtr100M15 = WilderIndicators.Sma(_atr14M15, 100);

        var h1Closes = _h1.Select(c => c.Close).ToArray();
        _ema50H1 = WilderIndicators.Ema(h1Closes, 50);
        _ema200H1 = WilderIndicators.Ema(h1Closes, 200);
        _atr14H1 = WilderIndicators.Atr(_h1, 14);

        _clock = new BacktestClockAdapter(TimeSpan.FromHours(rolloverHourUtc));
        _symbol = new BacktestSymbolAdapter();
        _news = new BacktestNewsCalendarAdapter();
        _stateStore = new BacktestStateStoreAdapter();
        _log = new BacktestLogAdapter();
        _execution = new BacktestExecutionAdapter();
        _stateMachine = new SymbolStateMachine(BotState.Ready);

        _config = customConfig ?? new SymbolEvaluationConfig
        {
            SymbolName = "EURUSD",
            MinStopAtr = 0.80,
            MaxStopAtr = 1.80,
            SwingLookbackCount = SwingLookbackCount,
            CommissionPerLotRoundTurn = CommissionPerLotRoundTurn,
            ConservativeSlippagePriceUnits = ConservativeSlippagePriceUnits,
            MaxAllowedSlippagePriceUnits = ConservativeSlippagePriceUnits * 2,
            RiskPerTradePct = 0.015,
            MaxReservedRiskPct = 0.030,
            EnableBreakout = false,
            EnableBreakeven = true,
            BreakevenTriggerR = 1.0,
            BreakevenOffsetPriceUnits = 0.0,
            EnableScaleOut = false,
            ScaleOutRatio = 0.50,
            ScaleOutTriggerR = 1.50,
            EnableAtrTrailing = false,
            TrailingAtrMultiplier = 2.50,
            PullbackAdxThreshold = 16.0,
            PullbackUpperBoundAtr = 0.20,
            PullbackLowerBoundAtr = 0.35,
            PullbackRsiPriorBuyThreshold = 55.0,
            PullbackRsiPriorSellThreshold = 45.0,
            PullbackClvBuyThreshold = 0.55,
            PullbackClvSellThreshold = 0.45,
            PullbackBodyAtrCoeff = 0.20,
            BreakoutLookback = 10,
            BreakoutAdxThreshold = 25.0,
            BreakoutClvBuyThreshold = 0.60,
            BreakoutClvSellThreshold = 0.40,
            BreakoutMaxExtensionAtr = 2.00,
            BreakoutVolumeMultiplier = 1.10
        };

        _execution.ConfigureTradeManagement(
            _config.EnableBreakeven,
            _config.BreakevenTriggerR,
            _config.BreakevenOffsetPriceUnits,
            _config.EnableScaleOut,
            _config.ScaleOutRatio,
            _config.ScaleOutTriggerR,
            _config.EnableAtrTrailing,
            _config.TrailingAtrMultiplier);

        _orchestrator = new BarEvaluationOrchestrator(
            _execution, _symbol, _clock, _news, _stateStore, _log, _config, _stateMachine);

        _stateStore.DailyStartEquity = initialEquity;
        _stateStore.WeeklyStartEquity = initialEquity;
        _stateStore.EquityHighWaterMark = initialEquity;
    }

    /// <summary>
    /// Executes the backtest replay bar by bar over the entire dataset.
    /// </summary>
    public void Run()
    {
        int h1Index = -1;

        for (int i = 0; i < _m15.Length; i++)
        {
            var candle = _m15[i];
            var barCloseUtc = candle.TimestampUtc.AddMinutes(15);

            while (h1Index + 1 < _h1.Length && _h1[h1Index + 1].TimestampUtc.AddHours(1) <= barCloseUtc)
                h1Index++;

            RollDailyWeeklyCounters(barCloseUtc);

            double currentM15Atr = double.IsNaN(_atr14M15[i]) ? 0.0 : _atr14M15[i];
            _execution.AdvanceBar(candle, currentM15Atr, _symbol.Info, CommissionPerLotRoundTurn);

            if (_execution.LastClosedRMultiple is double r)
            {
                _equity += _execution.Trades[^1].PnLMoney;
                if (r < -0.05) _stateStore.ConsecutiveLossCount++;
                else if (r > 0.05) _stateStore.ConsecutiveLossCount = 0;
                _execution.ClearLastClosedMarker();
            }
            SyncState();

            EquityCurve.Add((barCloseUtc, _equity));
            if (_equity > _stateStore.EquityHighWaterMark)
                _stateStore.EquityHighWaterMark = _equity;

            bool warmupOk = h1Index >= H1WarmupMinimum - 1 && h1Index - 6 >= 0 && i >= M15WarmupMinimum - 1 && i >= 1;
            if (!warmupOk)
                continue;

            if (_stateMachine.Current != BotState.Ready)
                continue;

            var h1Snapshot = BuildH1Snapshot(h1Index);
            var m15Snapshot = BuildM15Snapshot(i);
            var quality = BuildQualitySnapshot(i, candle);
            var account = new AccountSnapshot
            {
                Equity = _equity,
                FreeMargin = _equity,
                Leverage = 100,
                DailyStartEquity = _stateStore.DailyStartEquity,
                WeeklyStartEquity = _stateStore.WeeklyStartEquity,
                EquityHighWaterMark = _stateStore.EquityHighWaterMark,
                FilledEntriesToday = _stateStore.FilledEntriesToday,
                ConsecutiveLossCount = _stateStore.ConsecutiveLossCount,
                TotalReservedRisk = 0
            };

            _symbol.CurrentMidPrice = candle.Close;
            _symbol.CurrentSpreadPriceUnits = quality.CurrentSpread;

            _orchestrator.Evaluate(h1Snapshot, m15Snapshot, quality, account, _symbol.Info, barCloseUtc);
        }
    }

    /// <summary>
    /// Evaluates diagnostic metrics for Pullback conditions across all eligible bars.
    /// Cross-checks each true zero-fail bar (all 9 domain conditions passed) against
    /// the same session/Friday/rollover clock gates the orchestrator's Step 5 applies,
    /// setting <see cref="PullbackDiagnosticsReport.ZeroFailAndInSessionCount"/> so a
    /// "0 trades" result can be attributed to the window filter vs. the signal itself.
    /// </summary>
    public PullbackDiagnosticsReport RunPullbackDiagnostics()
    {
        var report = new PullbackDiagnosticsReport();
        int h1Index = -1;
        int zeroFailAndInSession = 0;

        for (int i = 0; i < _m15.Length; i++)
        {
            var candle = _m15[i];
            var barCloseUtc = candle.TimestampUtc.AddMinutes(15);

            while (h1Index + 1 < _h1.Length && _h1[h1Index + 1].TimestampUtc.AddHours(1) <= barCloseUtc)
                h1Index++;

            bool warmupOk = h1Index >= H1WarmupMinimum - 1 && h1Index - 6 >= 0 && i >= M15WarmupMinimum - 1 && i >= 1;
            if (!warmupOk)
                continue;

            double ema50H1Bar1 = _ema50H1[h1Index];
            double ema200H1Bar1 = _ema200H1[h1Index];
            double ema50H1Bar6 = _ema50H1[h1Index - 6];
            double atr14H1Bar1 = _atr14H1[h1Index];

            double ema20Bar1 = _ema20M15[i];
            double ema50Bar1 = _ema50M15[i];
            double rsi1 = _rsi14M15[i];
            double rsi2 = _rsi14M15[i - 1];
            double adx1 = _adx14M15[i];
            double atr1 = _atr14M15[i];
            double smaAtr100 = _smaAtr100M15[i];

            bool h1Valid = AllFinite(ema50H1Bar1, ema200H1Bar1, ema50H1Bar6, atr14H1Bar1) && atr14H1Bar1 > 0;
            bool m15Valid = AllFinite(ema20Bar1, ema50Bar1, rsi1, rsi2, adx1, atr1, smaAtr100) && atr1 > 0 && smaAtr100 > 0;
            if (!h1Valid || !m15Valid)
                continue;

            var trend = SignalEvaluator.EvaluateH1Trend(ema50H1Bar1, ema200H1Bar1, ema50H1Bar6, atr14H1Bar1);
            if (trend == TradeDirection.None)
                continue;

            double volRatio = atr1 / smaAtr100;
            if (volRatio < 0.70 || volRatio > 1.80)
                continue;

            if (candle.Range <= 0)
                continue;

            int failCount = report.Evaluate(
                trend, candle, ema20Bar1, ema50Bar1, adx1, atr1, rsi2, rsi1,
                _config.PullbackAdxThreshold,
                _config.PullbackUpperBoundAtr,
                _config.PullbackLowerBoundAtr,
                _config.PullbackClvBuyThreshold,
                _config.PullbackClvSellThreshold,
                _config.PullbackRsiPriorBuyThreshold,
                _config.PullbackRsiPriorSellThreshold);

            if (failCount == 0 && IsWithinStep5Gates(barCloseUtc))
                zeroFailAndInSession++;
        }

        report.ZeroFailAndInSessionCount = zeroFailAndInSession;
        return report;
    }

    /// <summary>
    /// Evaluates diagnostic metrics for Breakout conditions across all eligible bars.
    /// Cross-checks each true zero-fail bar the same way as
    /// <see cref="RunPullbackDiagnostics"/> — see that method's doc comment.
    /// </summary>
    public TrendContinuationDiagnosticsReport RunBreakoutDiagnostics()
    {
        var report = new TrendContinuationDiagnosticsReport();
        int h1Index = -1;
        int lookback = _config.BreakoutLookback;
        int zeroFailAndInSession = 0;

        for (int i = 0; i < _m15.Length; i++)
        {
            var candle = _m15[i];
            var barCloseUtc = candle.TimestampUtc.AddMinutes(15);

            while (h1Index + 1 < _h1.Length && _h1[h1Index + 1].TimestampUtc.AddHours(1) <= barCloseUtc)
                h1Index++;

            bool warmupOk = h1Index >= H1WarmupMinimum - 1 && h1Index - 6 >= 0 && i >= M15WarmupMinimum - 1 && i >= lookback;
            if (!warmupOk)
                continue;

            double ema50H1Bar1 = _ema50H1[h1Index];
            double ema200H1Bar1 = _ema200H1[h1Index];
            double ema50H1Bar6 = _ema50H1[h1Index - 6];
            double atr14H1Bar1 = _atr14H1[h1Index];

            double adx1 = _adx14M15[i];
            double atr1 = _atr14M15[i];
            double smaAtr100 = _smaAtr100M15[i];

            bool h1Valid = AllFinite(ema50H1Bar1, ema200H1Bar1, ema50H1Bar6, atr14H1Bar1) && atr14H1Bar1 > 0;
            bool m15Valid = AllFinite(adx1, atr1, smaAtr100) && atr1 > 0 && smaAtr100 > 0;
            if (!h1Valid || !m15Valid)
                continue;

            var trend = SignalEvaluator.EvaluateH1Trend(ema50H1Bar1, ema200H1Bar1, ema50H1Bar6, atr14H1Bar1);
            if (trend == TradeDirection.None)
                continue;

            double volRatio = atr1 / smaAtr100;
            if (volRatio < 0.70 || volRatio > 1.80)
                continue;

            if (candle.Range <= 0)
                continue;

            var candles = new Candle[lookback + 1];
            for (int k = 0; k <= lookback; k++)
                candles[k] = _m15[i - k];

            int failCount = report.Evaluate(
                trend, candles, adx1, atr1,
                _config.BreakoutLookback,
                _config.BreakoutAdxThreshold,
                _config.BreakoutClvBuyThreshold,
                _config.BreakoutClvSellThreshold);

            if (failCount == 0 && IsWithinStep5Gates(barCloseUtc))
                zeroFailAndInSession++;
        }

        report.ZeroFailAndInSessionCount = zeroFailAndInSession;
        return report;
    }

    /// <summary>
    /// Mirrors the orchestrator's Step-5 window checks (session filter, Friday cutoff,
    /// rollover blackout) so the diagnostics loops can report how many raw domain-level
    /// signals would have actually survived to reach signal evaluation in the real
    /// <see cref="BarEvaluationOrchestrator"/> flow. News is intentionally excluded here
    /// since <see cref="BacktestNewsCalendarAdapter"/> never blocks (see its doc comment).
    /// </summary>
    private bool IsWithinStep5Gates(DateTime barCloseUtc)
    {
        if (_config.EnableSessionFilter && !_clock.IsWithinEntryWindow(barCloseUtc))
            return false;

        if (_clock.IsPastFridayNewOrderCutoff(barCloseUtc))
            return false;

        if (_clock.IsWithinRolloverWindow(barCloseUtc))
            return false;

        return true;
    }

    private void SyncState()
    {
        var broker = _execution.GetBrokerState("EURUSD");

        switch (_stateMachine.Current)
        {
            case BotState.OrderPending when broker.HasOpenPosition:
                _stateMachine.TryTransition(BotState.PositionOpen);
                _stateStore.FilledEntriesToday++;
                break;

            case BotState.OrderPending when !broker.HasPendingOrder && !broker.HasOpenPosition:
                _stateMachine.TryTransition(BotState.Ready);
                break;

            case BotState.PositionOpen when !broker.HasOpenPosition:
                _stateMachine.TryTransition(BotState.Cooldown);
                break;

            case BotState.Cooldown:
                _stateMachine.TryTransition(BotState.Ready);
                break;
        }
    }

    private void RollDailyWeeklyCounters(DateTime barCloseUtc)
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var nyNow = TimeZoneInfo.ConvertTimeFromUtc(barCloseUtc, newYork);
        string today = nyNow.ToString("yyyy-MM-dd");

        if (_stateStore.LastCountersResetDate == today)
            return;

        _stateStore.DailyStartEquity = _equity;
        _stateStore.FilledEntriesToday = 0;
        _stateStore.ConsecutiveLossCount = 0;

        if (nyNow.DayOfWeek == DayOfWeek.Monday || string.IsNullOrEmpty(_stateStore.LastCountersResetDate))
            _stateStore.WeeklyStartEquity = _equity;

        _stateStore.LastCountersResetDate = today;
    }

    private H1Snapshot BuildH1Snapshot(int h1Index)
    {
        double ema50Bar1 = _ema50H1[h1Index];
        double ema200Bar1 = _ema200H1[h1Index];
        double ema50Bar6 = _ema50H1[h1Index - 6];
        double atr14Bar1 = _atr14H1[h1Index];

        bool valid = AllFinite(ema50Bar1, ema200Bar1, ema50Bar6, atr14Bar1) && atr14Bar1 > 0;

        return new H1Snapshot
        {
            Ema50Bar1 = ema50Bar1,
            Ema200Bar1 = ema200Bar1,
            Ema50Bar6 = ema50Bar6,
            Atr14Bar1 = atr14Bar1,
            IsValid = valid
        };
    }

    private M15Snapshot BuildM15Snapshot(int i)
    {
        double ema20Bar1 = _ema20M15[i];
        double ema50Bar1 = _ema50M15[i];
        double rsi14Bar1 = _rsi14M15[i];
        double rsi14Bar2 = _rsi14M15[i - 1];
        double adx14Bar1 = _adx14M15[i];
        double atr14Bar1 = _atr14M15[i];
        double smaAtr100Bar1 = _smaAtr100M15[i];

        int lookback = Math.Min(i + 1, SwingLookbackCount + 5 + 20);
        var candles = new Candle[lookback];
        for (int k = 0; k < lookback; k++)
            candles[k] = _m15[i - k];

        bool valid = AllFinite(ema20Bar1, ema50Bar1, rsi14Bar1, rsi14Bar2, adx14Bar1, atr14Bar1, smaAtr100Bar1)
            && atr14Bar1 > 0 && smaAtr100Bar1 > 0;

        return new M15Snapshot
        {
            Candles = candles,
            Ema20Bar1 = ema20Bar1,
            Ema50Bar1 = ema50Bar1,
            Rsi14Bar1 = rsi14Bar1,
            Rsi14Bar2 = rsi14Bar2,
            Adx14Bar1 = adx14Bar1,
            Atr14Bar1 = atr14Bar1,
            SmaAtr100Bar1 = smaAtr100Bar1,
            Ema20Current = ema20Bar1,
            Atr14Current = atr14Bar1,
            IsValid = valid
        };
    }

    private MarketQualitySnapshot BuildQualitySnapshot(int i, Candle candle)
    {
        var slot = candle.TimestampUtc.TimeOfDay;
        var sameSlotVolumes = new List<double>();
        for (int k = i - 1; k >= 0 && sameSlotVolumes.Count < 20 * 3; k--)
        {
            if (_m15[k].TimestampUtc.TimeOfDay == slot)
                sameSlotVolumes.Add(_m15[k].TickVolume);
            if (sameSlotVolumes.Count >= 20)
                break;
        }
        double volumeBaseline = sameSlotVolumes.Count > 0 ? Median(sameSlotVolumes) : 0;
        double modeledSpread = _spreadModel.GetSpread(candle.TimestampUtc);

        return new MarketQualitySnapshot
        {
            TickVolumeBar1 = candle.TickVolume,
            VolumeBaseline = volumeBaseline,
            VolumeValidObservations = sameSlotVolumes.Count,
            CurrentSpread = modeledSpread,
            SpreadBaseline = modeledSpread,
            SpreadValidObservations = 200,
            AbsoluteSpreadCap = 0.00020
        };
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static bool AllFinite(params double[] values)
    {
        foreach (var v in values)
            if (double.IsNaN(v) || double.IsInfinity(v))
                return false;
        return true;
    }
}
