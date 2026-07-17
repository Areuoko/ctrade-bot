using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Orders;

/// <summary>
/// Order entry calculations. Rules K.1, K.2, K.4.
/// Pure domain logic — no cAlgo.API reference.
/// </summary>
public static class OrderEntryCalculator
{
    private const double EntryBufferAtrCoeff = 0.02;
    private const double TriggerDistanceAtrCoeff = 0.60;
    private const int ExpiryMinutes = 30;

    /// <summary>
    /// K.1 — Computes buy stop entry price.
    /// Buffer = max(TickSize, 0.02 × ATR14[1]); Entry = round_up(High[1] + Buffer).
    /// </summary>
    public static double ComputeBuyEntry(double highBar1, double atr14Bar1, double tickSize)
    {
        double buffer = Math.Max(tickSize, EntryBufferAtrCoeff * atr14Bar1);
        return RoundUpToTick(highBar1 + buffer, tickSize);
    }

    /// <summary>
    /// K.1 — Computes sell stop entry price.
    /// Buffer = max(TickSize, 0.02 × ATR14[1]); Entry = round_down(Low[1] − Buffer).
    /// </summary>
    public static double ComputeSellEntry(double lowBar1, double atr14Bar1, double tickSize)
    {
        double buffer = Math.Max(tickSize, EntryBufferAtrCoeff * atr14Bar1);
        return RoundDownToTick(lowBar1 - buffer, tickSize);
    }

    /// <summary>
    /// K.2 — Order expiry = signal close time + 30 minutes.
    /// </summary>
    public static DateTime ComputeExpiry(DateTime signalCloseTimeUtc)
        => signalCloseTimeUtc.AddMinutes(ExpiryMinutes);

    /// <summary>
    /// K.4 — Trigger distance check.
    /// Returns true if |ExpectedFillPrice − EMA20_current| &lt;= 0.60 × ATR14_current.
    /// </summary>
    public static bool PassesTriggerDistance(
        double expectedFillPrice, double ema20Current, double atr14Current)
    {
        if (double.IsNaN(expectedFillPrice) || double.IsNaN(ema20Current) || double.IsNaN(atr14Current))
            return false;

        if (atr14Current <= 0)
            return false;

        double distance = Math.Abs(expectedFillPrice - ema20Current);
        return distance <= TriggerDistanceAtrCoeff * atr14Current;
    }

    /// <summary>
    /// Rounds price up to the nearest tick size.
    /// </summary>
    public static double RoundUpToTick(double price, double tickSize)
    {
        if (tickSize <= 0) return price;
        return Math.Ceiling(price / tickSize) * tickSize;
    }

    /// <summary>
    /// Rounds price down to the nearest tick size.
    /// </summary>
    public static double RoundDownToTick(double price, double tickSize)
    {
        if (tickSize <= 0) return price;
        return Math.Floor(price / tickSize) * tickSize;
    }
}
