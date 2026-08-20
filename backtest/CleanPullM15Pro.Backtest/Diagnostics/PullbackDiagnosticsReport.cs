using System;
using System.Collections.Generic;
using System.Linq;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Backtest.Diagnostics;

/// <summary>
/// Diagnostic-only aggregator for the Pullback signal's 9 scored conditions.
/// </summary>
public sealed class PullbackDiagnosticsReport
{
    /// <summary>Fixed display/iteration order for the 9 scored conditions.</summary>
    public static readonly string[] ConditionOrder =
    {
        "C2_EmaAlignment",
        "C3_Adx",
        "C4_PullbackUpperBound",
        "C5_PullbackLowerBound",
        "C6_CloseSide",
        "C7_RsiPriorSide",
        "C8_RsiCross",
        "C9_Clv",
        "C10_Body"
    };

    public int TotalEligibleBars { get; private set; }
    public int BuyEligibleBars { get; private set; }
    public int SellEligibleBars { get; private set; }
    public int ZeroFailBars { get; private set; }
    public int OneFailBars { get; private set; }
    public int TwoFailBars { get; private set; }
    public int ThreeOrMoreFailBars { get; private set; }

    /// <summary>
    /// Of the <see cref="ZeroFailBars"/> (bars that pass all 9 domain-level signal
    /// conditions), how many ALSO fall inside the orchestrator's Step-5 window checks
    /// (session filter, Friday cutoff, rollover blackout). This report evaluates the
    /// 9 signal conditions independently of those Step-5 checks — a bar can be a
    /// "real" domain-level signal yet still never reach signal evaluation in the actual
    /// orchestrator because it arrives outside the allowed entry window. Set externally
    /// by the caller (see <c>ReplayEngine.RunPullbackDiagnostics</c>), 0 by default.
    /// </summary>
    public int ZeroFailAndInSessionCount { get; set; }

    public Dictionary<string, int> FailCounts { get; } = ConditionOrder.ToDictionary(k => k, _ => 0);
    public Dictionary<string, int> NearMissCounts { get; } = ConditionOrder.ToDictionary(k => k, _ => 0);
    public Dictionary<string, List<double>> NearMissShortfalls { get; } =
        ConditionOrder.ToDictionary(k => k, _ => new List<double>());

    /// <summary>
    /// Scores one eligible bar against all 9 Pullback conditions (no short-circuit).
    /// Returns the number of failing conditions (0 means every condition passed —
    /// a real domain-level signal), so callers can cross-check zero-fail bars against
    /// filters this report doesn't itself evaluate (e.g. session window).
    /// </summary>
    public int Evaluate(
        TradeDirection direction, Candle candle,
        double ema20Bar1, double ema50Bar1, double adx14Bar1, double atr14Bar1,
        double rsi14Bar2, double rsi14Bar1,
        double adxThreshold = 20.0,
        double upperBoundAtr = 0.20,
        double lowerBoundAtr = 0.50,
        double clvBuyThreshold = 0.55,
        double clvSellThreshold = 0.45,
        double rsiPriorBuy = 53.0,
        double rsiPriorSell = 47.0)
    {
        bool isBuy = direction == TradeDirection.Buy;

        TotalEligibleBars++;
        if (isBuy) BuyEligibleBars++; else SellEligibleBars++;

        var results = new[]
        {
            EmaAlignment(isBuy, ema20Bar1, ema50Bar1),
            Adx(adx14Bar1, adxThreshold),
            PullbackUpperBound(isBuy, candle, ema20Bar1, atr14Bar1, upperBoundAtr),
            PullbackLowerBound(isBuy, candle, ema20Bar1, atr14Bar1, lowerBoundAtr),
            CloseSide(isBuy, candle, ema20Bar1),
            RsiPriorSide(isBuy, rsi14Bar2, isBuy ? rsiPriorBuy : rsiPriorSell),
            RsiCross(isBuy, rsi14Bar1),
            Clv(isBuy, candle, isBuy ? clvBuyThreshold : clvSellThreshold),
            Body(candle, atr14Bar1)
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
        Console.WriteLine("=== PULLBACK CONDITION DIAGNOSTICS (no short-circuit — every eligible bar scored on all 9 conditions) ===");
        Console.WriteLine($"Eligible bars (H1 trend + M15 volatility passed): {TotalEligibleBars}  (Buy: {BuyEligibleBars}  Sell: {SellEligibleBars})");
        Console.WriteLine($"Zero-fail bars (all 9 passed — would be a real Pullback signal): {ZeroFailBars}");
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
        "C4_PullbackUpperBound" or "C5_PullbackLowerBound" or "C10_Body" => "(ATR units)",
        "C3_Adx" => "(ADX points)",
        "C7_RsiPriorSide" or "C8_RsiCross" => "(RSI points)",
        "C9_Clv" => "(CLV ratio)",
        "C2_EmaAlignment" or "C6_CloseSide" => "(price units)",
        _ => ""
    };

    private static (string Key, bool Pass, double Shortfall) EmaAlignment(bool isBuy, double ema20, double ema50)
    {
        bool pass = isBuy ? ema20 > ema50 : ema20 < ema50;
        double shortfall = pass ? 0 : Math.Abs(ema20 - ema50);
        return ("C2_EmaAlignment", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) Adx(double adx14Bar1, double threshold)
    {
        bool pass = adx14Bar1 >= threshold;
        double shortfall = pass ? 0 : threshold - adx14Bar1;
        return ("C3_Adx", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) PullbackUpperBound(
        bool isBuy, Candle candle, double ema20, double atr, double coeff)
    {
        if (isBuy)
        {
            double threshold = ema20 + coeff * atr;
            bool pass = candle.Low <= threshold;
            double shortfall = pass ? 0 : (candle.Low - threshold) / atr;
            return ("C4_PullbackUpperBound", pass, shortfall);
        }
        else
        {
            double threshold = ema20 - coeff * atr;
            bool pass = candle.High >= threshold;
            double shortfall = pass ? 0 : (threshold - candle.High) / atr;
            return ("C4_PullbackUpperBound", pass, shortfall);
        }
    }

    private static (string Key, bool Pass, double Shortfall) PullbackLowerBound(
        bool isBuy, Candle candle, double ema20, double atr, double coeff)
    {
        if (isBuy)
        {
            double threshold = ema20 - coeff * atr;
            bool pass = candle.Low >= threshold;
            double shortfall = pass ? 0 : (threshold - candle.Low) / atr;
            return ("C5_PullbackLowerBound", pass, shortfall);
        }
        else
        {
            double threshold = ema20 + coeff * atr;
            bool pass = candle.High <= threshold;
            double shortfall = pass ? 0 : (candle.High - threshold) / atr;
            return ("C5_PullbackLowerBound", pass, shortfall);
        }
    }

    private static (string Key, bool Pass, double Shortfall) CloseSide(bool isBuy, Candle candle, double ema20)
    {
        bool pass = isBuy ? candle.Close >= ema20 : candle.Close <= ema20;
        double shortfall = pass ? 0 : Math.Abs(candle.Close - ema20);
        return ("C6_CloseSide", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) RsiPriorSide(bool isBuy, double rsi14Bar2, double threshold)
    {
        bool pass = isBuy ? rsi14Bar2 <= threshold : rsi14Bar2 >= threshold;
        double shortfall = pass ? 0 : Math.Abs(rsi14Bar2 - threshold);
        return ("C7_RsiPriorSide", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) RsiCross(bool isBuy, double rsi14Bar1)
    {
        const double midline = 50.0;
        bool pass = isBuy ? rsi14Bar1 > midline : rsi14Bar1 < midline;
        double shortfall = pass ? 0 : Math.Abs(rsi14Bar1 - midline);
        return ("C8_RsiCross", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) Clv(bool isBuy, Candle candle, double threshold)
    {
        bool pass = isBuy ? candle.Clv >= threshold : candle.Clv <= threshold;
        double shortfall = pass ? 0 : (isBuy ? threshold - candle.Clv : candle.Clv - threshold);
        return ("C9_Clv", pass, shortfall);
    }

    private static (string Key, bool Pass, double Shortfall) Body(Candle candle, double atr)
    {
        double threshold = 0.20 * atr;
        bool pass = candle.Body >= threshold;
        double shortfall = pass ? 0 : (threshold - candle.Body) / atr;
        return ("C10_Body", pass, shortfall);
    }
}
