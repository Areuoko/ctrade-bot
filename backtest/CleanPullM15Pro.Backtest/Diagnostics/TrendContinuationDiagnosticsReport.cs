using System;
using System.Collections.Generic;
using System.Linq;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Backtest.Diagnostics;

/// <summary>
/// Diagnostic-only aggregator for the Trend Continuation / Breakout signal conditions.
/// </summary>
public sealed class TrendContinuationDiagnosticsReport
{
    /// <summary>Fixed display/iteration order for the 4 scored conditions.</summary>
    public static readonly string[] ConditionOrder =
    {
        "AB2_Trigger",
        "AB3_Adx",
        "AB3_Clv",
        "AB3_Body"
    };

    public int TotalEligibleBars { get; private set; }
    public int BuyEligibleBars { get; private set; }
    public int SellEligibleBars { get; private set; }
    public int ZeroFailBars { get; private set; }
    public int OneFailBars { get; private set; }
    public int TwoFailBars { get; private set; }
    public int ThreeOrMoreFailBars { get; private set; }

    /// <summary>
    /// Of the <see cref="ZeroFailBars"/> (bars that pass all 4 domain-level Breakout
    /// conditions), how many ALSO fall inside the orchestrator's Step-5 window checks
    /// (session filter, Friday cutoff, rollover blackout). See
    /// <see cref="Diagnostics.PullbackDiagnosticsReport.ZeroFailAndInSessionCount"/> for the
    /// same rationale. Set externally by the caller (see
    /// <c>ReplayEngine.RunBreakoutDiagnostics</c>), 0 by default.
    /// </summary>
    public int ZeroFailAndInSessionCount { get; set; }

    public Dictionary<string, int> FailCounts { get; } = ConditionOrder.ToDictionary(k => k, _ => 0);
    public Dictionary<string, int> NearMissCounts { get; } = ConditionOrder.ToDictionary(k => k, _ => 0);
    public Dictionary<string, List<double>> NearMissShortfalls { get; } =
        ConditionOrder.ToDictionary(k => k, _ => new List<double>());

    /// <summary>
    /// Scores one eligible bar against all 4 Breakout conditions (no short-circuit).
    /// Returns the number of failing conditions (0 means every condition passed —
    /// a real domain-level signal), so callers can cross-check zero-fail bars against
    /// filters this report doesn't itself evaluate (e.g. session window).
    /// </summary>
    public int Evaluate(
        TradeDirection direction, Candle[] candles, double adx14Bar1, double atr14Bar1,
        int lookback = 10, double adxThreshold = 25.0, double clvBuy = 0.60, double clvSell = 0.40)
    {
        bool isBuy = direction == TradeDirection.Buy;
        var signalCandle = candles[0];

        TotalEligibleBars++;
        if (isBuy) BuyEligibleBars++; else SellEligibleBars++;

        var results = new[]
        {
            Trigger(isBuy, candles, atr14Bar1, lookback),
            Adx(adx14Bar1, adxThreshold),
            Clv(isBuy, signalCandle, isBuy ? clvBuy : clvSell),
            Body(signalCandle, atr14Bar1)
        };

        int failCount = 0;
        string? soleFailedKey = null;
        double soleFailedShortfall = 0;

        foreach (var (condKey, pass, shortfall) in results)
        {
            if (pass)
                continue;

            failCount++;
            soleFailedKey = condKey;
            soleFailedShortfall = shortfall;
            FailCounts[condKey]++;
        }

        switch (failCount)
        {
            case 0:
                ZeroFailBars++;
                break;
            case 1:
                OneFailBars++;
                NearMissCounts[soleFailedKey!]++;
                NearMissShortfalls[soleFailedKey!].Add(soleFailedShortfall);
                break;
            case 2:
                TwoFailBars++;
                break;
            default:
                ThreeOrMoreFailBars++;
                break;
        }

        return failCount;
    }

    public void Print()
    {
        Console.WriteLine("=== BREAKOUT CONDITION DIAGNOSTICS (no short-circuit — every eligible bar scored on all 4 conditions) ===");
        Console.WriteLine($"Eligible bars (H1 trend + M15 volatility + Range>0 passed): {TotalEligibleBars}  (Buy: {BuyEligibleBars}  Sell: {SellEligibleBars})");
        Console.WriteLine($"Zero-fail bars (all 4 passed — would clear AB.2/AB.3): {ZeroFailBars}");
        Console.WriteLine($"  ...of which also inside the entry session window (Step-5 checks): {ZeroFailAndInSessionCount}");
        Console.WriteLine($"One-fail bars (near-miss, exactly 1 condition away): {OneFailBars}");
        Console.WriteLine($"Two-fail bars: {TwoFailBars}");
        Console.WriteLine($"Three-or-more-fail bars: {ThreeOrMoreFailBars}");
        Console.WriteLine();

        Console.WriteLine("--- Failure prevalence across ALL eligible bars (how often each condition is the/a blocker) ---");
        foreach (string condKey in ConditionOrder.OrderByDescending(name => FailCounts[name]))
        {
            int fails = FailCounts[condKey];
            double pct = TotalEligibleBars > 0 ? 100.0 * fails / TotalEligibleBars : 0;
            Console.WriteLine($"{condKey,-24} failed {fails,6} / {TotalEligibleBars} ({pct,5:F1}%)");
        }
        Console.WriteLine();

        Console.WriteLine("--- Near-miss breakdown (bars where THIS was the only failing condition) ---");
        foreach (string condKey in ConditionOrder.OrderByDescending(name => NearMissCounts[name]))
        {
            int count = NearMissCounts[condKey];
            if (count == 0)
            {
                Console.WriteLine($"{condKey,-24} 0 near-misses");
                continue;
            }

            var shortfalls = NearMissShortfalls[condKey];
            shortfalls.Sort();
            double min = shortfalls[0];
            double median = shortfalls[shortfalls.Count / 2];
            double avg = shortfalls.Average();
            double max = shortfalls[^1];
            Console.WriteLine(
                $"{condKey,-24} {count,6} near-misses — shortfall {UnitFor(condKey)}: min={min:F4} median={median:F4} avg={avg:F4} max={max:F4}");
        }
    }

    private static string UnitFor(string condKey) => condKey switch
    {
        "AB2_Trigger" or "AB3_Body" => "(ATR units)",
        "AB3_Adx" => "(ADX points)",
        "AB3_Clv" => "(CLV ratio)",
        _ => ""
    };

    private static (string Key, bool Pass, double Shortfall) Trigger(bool isBuy, Candle[] candles, double atr, int lookback)
    {
        var signalCandle = candles[0];

        if (isBuy)
        {
            double breakoutHigh = double.MinValue;
            for (int k = 1; k <= lookback; k++)
                breakoutHigh = Math.Max(breakoutHigh, candles[k].High);

            bool pass = signalCandle.Close > breakoutHigh;
            double shortfall = pass ? 0 : (breakoutHigh - signalCandle.Close) / atr;
            return ("AB2_Trigger", pass, shortfall);
        }
        else
        {
            double breakoutLow = double.MaxValue;
            for (int k = 1; k <= lookback; k++)
                breakoutLow = Math.Min(breakoutLow, candles[k].Low);

            bool pass = signalCandle.Close < breakoutLow;
            double shortfall = pass ? 0 : (signalCandle.Close - breakoutLow) / atr;
            return ("AB2_Trigger", pass, shortfall);
        }
    }

    private static (string Key, bool Pass, double Shortfall) Adx(double adx14Bar1, double threshold)
    {
        bool pass = adx14Bar1 >= threshold;
        double shortfall = pass ? 0 : threshold - adx14Bar1;
        return ("AB3_Adx", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) Clv(bool isBuy, Candle candle, double threshold)
    {
        bool pass = isBuy ? candle.Clv >= threshold : candle.Clv <= threshold;
        double shortfall = pass ? 0 : (isBuy ? threshold - candle.Clv : candle.Clv - threshold);
        return ("AB3_Clv", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) Body(Candle candle, double atr)
    {
        double threshold = 0.20 * atr;
        bool pass = candle.Body >= threshold;
        double shortfall = pass ? 0 : (threshold - candle.Body) / atr;
        return ("AB3_Body", pass, shortfall);
    }
}
