// ================================================================================
// FILE: backtest\CleanPullM15Pro.Backtest\Program.cs
// ================================================================================

using System;
using System.IO;
using System.Linq;
using CleanPullM15Pro.Backtest.Data;
using CleanPullM15Pro.Backtest.Engine;
using CleanPullM15Pro.Backtest.WalkForward;

namespace CleanPullM15Pro.Backtest;

public static class Program
{
    private const double InitialEquity = 10_000;
    private const int RolloverHourUtc = 21;

    public static int Main(string[] args)
    {
        string dataDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "data");

        string m15Path = Path.Combine(dataDir, "EURUSD_M15.csv");
        string h1Path = Path.Combine(dataDir, "EURUSD_H1.csv");
        string spreadPath = Path.Combine(dataDir, "EURUSD_Spread_M15.csv");

        if (!File.Exists(m15Path) || !File.Exists(h1Path) || !File.Exists(spreadPath))
        {
            Console.WriteLine("Missing one or more input CSVs in: " + dataDir);
            Console.WriteLine("Expected: EURUSD_M15.csv, EURUSD_H1.csv, EURUSD_Spread_M15.csv");
            return 1;
        }

        Console.WriteLine("Loading data from: " + dataDir);
        var m15 = CsvBarLoader.Load(m15Path);
        var h1 = CsvBarLoader.Load(h1Path);
        var spreadModel = SpreadModel.Load(spreadPath);

        Console.WriteLine($"M15 bars: {m15.Length} ({m15.First().TimestampUtc:yyyy-MM-dd} to {m15.Last().TimestampUtc:yyyy-MM-dd})");
        Console.WriteLine($"H1 bars: {h1.Length} ({h1.First().TimestampUtc:yyyy-MM-dd} to {h1.Last().TimestampUtc:yyyy-MM-dd})");
        Console.WriteLine($"Spread sample window: {spreadModel.SampledRangeStartUtc:yyyy-MM-dd} to {spreadModel.SampledRangeEndUtc:yyyy-MM-dd}");
        Console.WriteLine();

        // Spec-aligned baseline (OQ-P.6-1: Risk 1.00%/0.50%, reserved-risk cap 2.00%;
        // spec section 8.1/8.2 original Pullback thresholds ADX>=20/RSI@50/CLV 0.65-0.35;
        // 4-hour session windows and Break-even @+1.0R per explicit decision — see
        // WalkForwardHarness.CreateSpecAlignedBaseConfig doc comment for full rationale).
        var baselineConfig = WalkForwardHarness.CreateSpecAlignedBaseConfig();

        // --- Run 1: Spec-aligned baseline sanity check (single fixed parameter set) ---
        Console.WriteLine("================================================================================");
        Console.WriteLine(">>> 1. FULL REPLAY: SPEC-ALIGNED BASELINE (ADX=20, LB=0.35, RISK=1.00%/0.50%, TP=2.0R)");
        Console.WriteLine("================================================================================");
        var baselineEngine = new ReplayEngine(m15, h1, spreadModel, InitialEquity, RolloverHourUtc, baselineConfig);
        baselineEngine.Run();
        PrintReport(baselineEngine, InitialEquity);
        Console.WriteLine();

        // --- Run 2: 18-combination research grid (Pullback ADX x LowerBound x Breakout preset) ---
        var wf = new WalkForwardHarness(m15, h1, spreadModel, InitialEquity, RolloverHourUtc);
        var researchGrid = WalkForwardHarness.BuildResearchGrid18();

        Console.WriteLine("================================================================================");
        Console.WriteLine($">>> 2. WALK-FORWARD RESEARCH GRID ({researchGrid.Count} combinations)");
        Console.WriteLine("================================================================================");

        var results = wf.Run(researchGrid, WalkForwardHarness.DefaultWindows);
        WalkForwardHarness.PrintReport("18-COMBINATION RESEARCH GRID (PULLBACK ADX x LOWERBOUND x BREAKOUT PRESET)", results, WalkForwardHarness.DefaultWindows);

        return 0;
    }

    private static void PrintReport(ReplayEngine engine, double initialEquity)
    {
        var trades = engine.Trades;

        Console.WriteLine("=== SUBMITTED SIGNALS ===");
        foreach (var (direction, count) in engine.Log.SubmittedCounts)
            Console.WriteLine($"{direction}: {count}");
        Console.WriteLine();

        Console.WriteLine($"=== TRADES: {trades.Count} ===");
        if (trades.Count == 0)
        {
            Console.WriteLine("No trades completed.");
            return;
        }

        int wins = trades.Count(t => t.RMultiple > 0.05);
        int losses = trades.Count(t => t.RMultiple < -0.05);
        int beTrades = trades.Count(t => t.ExitReason == "BE" || Math.Abs(t.RMultiple) <= 0.05);
        double winRate = (double)wins / trades.Count;

        double grossProfit = trades.Where(t => t.PnLMoney > 0).Sum(t => t.PnLMoney);
        double grossLoss = -trades.Where(t => t.PnLMoney < 0).Sum(t => t.PnLMoney);
        double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : double.PositiveInfinity;
        double expectancyR = trades.Average(t => t.RMultiple);
        double netPnL = trades.Sum(t => t.PnLMoney);

        Console.WriteLine($"Wins: {wins}  Losses: {losses}  BreakEvens: {beTrades}  WinRate: {winRate:P1}");
        Console.WriteLine($"Net P&L: {netPnL:F2} USD  Profit Factor: {profitFactor:F2}  Expectancy: {expectancyR:F3}R");
        Console.WriteLine();

        Console.WriteLine("=== EQUITY / DRAWDOWN ===");
        double peak = initialEquity;
        double maxDrawdownPct = 0;
        foreach (var (_, equity) in engine.EquityCurve)
        {
            if (equity > peak) peak = equity;
            double dd = peak > 0 ? (peak - equity) / peak : 0;
            if (dd > maxDrawdownPct) maxDrawdownPct = dd;
        }
        double finalEquity = engine.EquityCurve.Count > 0 ? engine.EquityCurve[^1].Equity : initialEquity;
        Console.WriteLine($"Initial: {initialEquity:F2}  Final: {finalEquity:F2}  Max Drawdown: {maxDrawdownPct:P2}");
    }
}
