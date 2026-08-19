// ================================================================================
// FILE: backtest\CleanPullM15Pro.Backtest\WalkForward\WalkForwardHarness.cs
// ================================================================================

using System;
using System.Collections.Generic;
using CleanPullM15Pro.Application.Orchestration;
using CleanPullM15Pro.Backtest.Data;
using CleanPullM15Pro.Backtest.Engine;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Backtest.WalkForward;

/// <summary>
/// Defines a single time window for walk-forward evaluation.
/// </summary>
public readonly record struct WalkForwardWindow(string Label, DateTime TestStartUtc, DateTime TestEndUtc);

/// <summary>
/// Pairs a descriptive test label with a strategy evaluation configuration.
/// </summary>
public readonly record struct ParameterCombo(string Label, SymbolEvaluationConfig Config);

/// <summary>
/// Aggregates performance and risk metrics across a single evaluation window.
/// </summary>
public sealed class WindowMetrics
{
    /// <summary>Total closed trade count.</summary>
    public int TradeCount { get; private set; }
    /// <summary>Winning trade count (> +0.05R).</summary>
    public int Wins { get; private set; }
    /// <summary>Losing trade count (&lt; -0.05R).</summary>
    public int Losses { get; private set; }
    /// <summary>BreakEven trade count (between -0.05R and +0.05R).</summary>
    public int Breakevens { get; private set; }

    private double _sumR;
    private double _grossProfitR;
    private double _grossLossR;
    private double _cumulativeR;
    private double _peakR;
    private double _maxDrawdownR;

    /// <summary>Records a completed trade into the metrics accumulator.</summary>
    public void Add(TradeRecord trade)
    {
        double r = trade.RMultiple;
        TradeCount++;
        _sumR += r;

        if (r > 0.05)
        {
            Wins++;
            _grossProfitR += r;
        }
        else if (r < -0.05)
        {
            Losses++;
            _grossLossR += Math.Abs(r);
        }
        else
        {
            Breakevens++;
        }

        _cumulativeR += r;
        if (_cumulativeR > _peakR)
            _peakR = _cumulativeR;

        double dd = _peakR - _cumulativeR;
        if (dd > _maxDrawdownR)
            _maxDrawdownR = dd;
    }

    /// <summary>Average R-multiple per trade.</summary>
    public double ExpectancyR => TradeCount > 0 ? _sumR / TradeCount : 0;
    /// <summary>Profit factor based on R-multiples.</summary>
    public double ProfitFactor => _grossLossR > 0 ? _grossProfitR / _grossLossR : (_grossProfitR > 0 ? double.PositiveInfinity : 0);
    /// <summary>Gross profit in R.</summary>
    public double GrossProfitR => _grossProfitR;
    /// <summary>Gross loss in R.</summary>
    public double GrossLossR => _grossLossR;
    /// <summary>Win rate percentage.</summary>
    public double WinRate => TradeCount > 0 ? (double)Wins / TradeCount : 0;
    /// <summary>Maximum drawdown in R.</summary>
    public double MaxDrawdownR => _maxDrawdownR;
}

/// <summary>
/// Harness for multi-window Walk-Forward optimization and grid evaluation.
/// </summary>
public sealed class WalkForwardHarness
{
    private readonly Candle[] _m15;
    private readonly Candle[] _h1;
    private readonly SpreadModel _spreadModel;
    private readonly double _initialEquity;
    private readonly int _rolloverHourUtc;

    /// <summary>Default evaluation windows across the historical dataset.</summary>
    public static readonly WalkForwardWindow[] DefaultWindows =
    {
        new("W1", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
        new("W2", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
        new("W3", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
    };

    /// <summary>Initializes a new instance of the <see cref="WalkForwardHarness"/> class.</summary>
    public WalkForwardHarness(
        Candle[] m15Chronological,
        Candle[] h1Chronological,
        SpreadModel spreadModel,
        double initialEquity,
        int rolloverHourUtc)
    {
        _m15 = m15Chronological;
        _h1 = h1Chronological;
        _spreadModel = spreadModel;
        _initialEquity = initialEquity;
        _rolloverHourUtc = rolloverHourUtc;
    }

    /// <summary>
    /// Creates the spec-aligned baseline configuration used as the fixed axis for the
    /// research grid (see <see cref="BuildResearchGrid18"/>). Values follow the original
    /// specification (cleanpull_m15_pro_v2_0.md) except where an explicit, documented
    /// decision has been made to deviate:
    ///
    /// - RiskPerTradePct = 1.00% / Breakout = 0.50% (orchestrator computes Breakout as
    ///   RiskPerTradePct/2.0): revised from spec's 0.30%/0.15% — see OQ-P.6-1 in
    ///   docs/open-questions.md.
    /// - MaxReservedRiskPct = 2.00%: revised from spec's 0.60%, same 2x ratio kept — see
    ///   OQ-P.6-1.
    /// - EnableSessionFilter = true with 4-hour London/NY windows (08:00–12:00 /
    ///   08:30–12:30): revised from spec section 3.2's original 3-hour windows — decision
    ///   made alongside this grid (2026-08), matches <c>CTraderClockAdapter</c>.
    /// - EnableBreakeven = true, BreakevenTriggerR = 1.0: revised from spec section 14.2's
    ///   Model A (no breakeven) — explicit decision to keep breakeven ON for this
    ///   Walk-Forward round rather than treating it as a separate ablation axis.
    /// - PullbackAdxThreshold (20.0), PullbackLowerBoundAtr (0.35): spec section 8.1/8.2
    ///   original values — these are the two axes varied by the grid; this baseline value
    ///   is overwritten per-combo in <see cref="BuildResearchGrid18"/>.
    /// - PullbackUpperBoundAtr (0.10), PullbackRsiPriorBuyThreshold/SellThreshold (50.0/
    ///   50.0), PullbackClvBuyThreshold/SellThreshold (0.65/0.35): spec section 8.1/8.2
    ///   original values, reverted from the looser "Sniper" calibration (0.20, 55/45,
    ///   0.55/0.45) used in an earlier session — see chat history for that comparison.
    /// - BreakoutAdxThreshold (25.0), BreakoutClvBuyThreshold/SellThreshold (0.60/0.40):
    ///   spec section AB.3 original values; these three fields (ADX + both CLV thresholds,
    ///   moved together as a Loose/Tight preset) are the second pair of axes varied by the
    ///   grid — this baseline value is overwritten per-combo in
    ///   <see cref="BuildResearchGrid18"/>.
    /// </summary>
    public static SymbolEvaluationConfig CreateSpecAlignedBaseConfig() => new()
    {
        SymbolName = "EURUSD",
        MinStopAtr = 0.80,
        MaxStopAtr = 1.80,
        SwingLookbackCount = 20,
        CommissionPerLotRoundTurn = 5.0,
        ConservativeSlippagePriceUnits = 0.00005,
        MaxAllowedSlippagePriceUnits = 0.00010,

        // OQ-P.6-1 — see doc comment above.
        RiskPerTradePct = 0.01,
        MaxReservedRiskPct = 0.02,

        EnableBreakout = true,
        EnableSessionFilter = true, // 4-hour London/NY sessions — see CTraderClockAdapter

        // Break-even kept ON at +1.0R per explicit decision — NOT spec section 14.2's
        // Model A baseline; see doc comment above.
        EnableBreakeven = true,
        BreakevenTriggerR = 1.0,
        BreakevenOffsetPriceUnits = 0.0,

        EnableScaleOut = false,
        ScaleOutRatio = 0.50,
        ScaleOutTriggerR = 1.50,
        EnableAtrTrailing = false,
        TrailingAtrMultiplier = 2.50,

        // --- Pullback: spec section 8.1/8.2 original values ---
        // PullbackAdxThreshold and PullbackLowerBoundAtr are the grid's Pullback axes;
        // BuildResearchGrid18 overwrites both per combo.
        PullbackAdxThreshold = 20.0,
        PullbackUpperBoundAtr = 0.10,
        PullbackLowerBoundAtr = 0.35,
        PullbackRsiPriorBuyThreshold = 50.0,
        PullbackRsiPriorSellThreshold = 50.0,
        PullbackClvBuyThreshold = 0.65,
        PullbackClvSellThreshold = 0.35,
        PullbackBodyAtrCoeff = 0.20,

        // --- Breakout: spec section AB.2-AB.5 original values ---
        // BreakoutAdxThreshold/BreakoutClvBuyThreshold/BreakoutClvSellThreshold are the
        // grid's Breakout preset axis; BuildResearchGrid18 overwrites all three per combo.
        BreakoutLookback = 10,
        BreakoutAdxThreshold = 25.0,
        BreakoutClvBuyThreshold = 0.60,
        BreakoutClvSellThreshold = 0.40,
        BreakoutMaxExtensionAtr = 2.00,
        BreakoutVolumeMultiplier = 1.10
    };

    /// <summary>
    /// Builds the agreed 18-combination research grid: 3 Pullback ADX levels
    /// (18/20/22) x 3 Pullback LowerBound levels (0.30/0.35/0.40) = 9 Pullback
    /// combinations, each crossed with 2 Breakout ADX+CLV presets (Loose/Tight) = 18
    /// total. All other fields come from <see cref="CreateSpecAlignedBaseConfig"/>.
    ///
    /// Breakout ADX and both CLV thresholds are varied together as a single "preset"
    /// axis (rather than a separate full-factorial axis for each) to keep the grid at
    /// 18 combinations instead of exploding to dozens — Breakout is the secondary
    /// (fallback) strategy, evaluated only when Pullback is rejected for that candle.
    /// </summary>
    public static List<ParameterCombo> BuildResearchGrid18()
    {
        var baseConfig = CreateSpecAlignedBaseConfig();

        double[] pullbackAdxLevels = { 18.0, 20.0, 22.0 };
        double[] pullbackLowerBoundLevels = { 0.30, 0.35, 0.40 };

        var breakoutPresets = new (string Label, double Adx, double ClvBuy, double ClvSell)[]
        {
            ("Loose", 23.0, 0.55, 0.45),
            ("Tight", 27.0, 0.65, 0.35),
        };

        var combos = new List<ParameterCombo>();

        foreach (var adx in pullbackAdxLevels)
        {
            foreach (var lowerBound in pullbackLowerBoundLevels)
            {
                foreach (var preset in breakoutPresets)
                {
                    var config = baseConfig with
                    {
                        PullbackAdxThreshold = adx,
                        PullbackLowerBoundAtr = lowerBound,
                        BreakoutAdxThreshold = preset.Adx,
                        BreakoutClvBuyThreshold = preset.ClvBuy,
                        BreakoutClvSellThreshold = preset.ClvSell,
                    };

                    string label =
                        $"PB(ADX={adx:F0}, LB={lowerBound:F2}) x BRK-{preset.Label}(ADX={preset.Adx:F0}, CLV={preset.ClvBuy:F2}/{preset.ClvSell:F2})";

                    combos.Add(new ParameterCombo(label, config));
                }
            }
        }

        return combos;
    }

    /// <summary>Runs the Walk-Forward evaluation over the supplied parameter combinations.</summary>
    public List<(ParameterCombo Combo, Dictionary<string, WindowMetrics> ByWindow)> Run(
        IEnumerable<ParameterCombo> combos, IReadOnlyList<WalkForwardWindow> windows)
    {
        var results = new List<(ParameterCombo, Dictionary<string, WindowMetrics>)>();

        foreach (var combo in combos)
        {
            var engine = new ReplayEngine(
                _m15, _h1, _spreadModel, _initialEquity, _rolloverHourUtc, combo.Config);

            engine.Run();

            var byWindow = new Dictionary<string, WindowMetrics>();
            foreach (var w in windows)
                byWindow[w.Label] = new WindowMetrics();

            foreach (var trade in engine.Trades)
            {
                foreach (var w in windows)
                {
                    if (trade.SignalTimeUtc >= w.TestStartUtc && trade.SignalTimeUtc < w.TestEndUtc)
                    {
                        byWindow[w.Label].Add(trade);
                        break;
                    }
                }
            }

            results.Add((combo, byWindow));
        }

        return results;
    }

    /// <summary>Prints formatted Walk-Forward summary metrics to the console.</summary>
    public static void PrintReport(
        string title,
        List<(ParameterCombo Combo, Dictionary<string, WindowMetrics> ByWindow)> results,
        IReadOnlyList<WalkForwardWindow> windows)
    {
        Console.WriteLine($"=== WALK-FORWARD: {title} ===");
        Console.WriteLine("(R-based metrics — evaluates cross-window consistency and edge decay.)");
        Console.WriteLine();

        foreach (var (combo, byWindow) in results)
        {
            Console.WriteLine(combo.Label);

            int allCount = 0;
            double allSumR = 0, allGrossProfitR = 0, allGrossLossR = 0;

            foreach (var w in windows)
            {
                var m = byWindow[w.Label];
                Console.WriteLine(
                    $"    {w.Label,-4} n={m.TradeCount,4}  ExpR={m.ExpectancyR,7:F3}  PF={FormatPf(m.ProfitFactor),7}  WinRate={m.WinRate,6:P1} (BE={m.Breakevens})  MaxDD(R)={m.MaxDrawdownR,6:F2}");

                allCount += m.TradeCount;
                allSumR += m.ExpectancyR * m.TradeCount;
                allGrossProfitR += m.GrossProfitR;
                allGrossLossR += m.GrossLossR;
            }

            double allExpR = allCount > 0 ? allSumR / allCount : 0;
            double allPf = allGrossLossR > 0 ? allGrossProfitR / allGrossLossR : (allGrossProfitR > 0 ? double.PositiveInfinity : 0);
            Console.WriteLine($"    {"ALL",-4} n={allCount,4}  ExpR={allExpR,7:F3}  PF={FormatPf(allPf),7}");
            Console.WriteLine();
        }
    }

    private static string FormatPf(double pf) => double.IsPositiveInfinity(pf) ? "inf" : pf.ToString("F2");
}
