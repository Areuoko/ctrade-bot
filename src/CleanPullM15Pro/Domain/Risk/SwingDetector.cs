using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Swing detection. Rules J.1, J.2, J.3.
/// Pure domain logic on closed M15 candles.
/// </summary>
public static class SwingDetector
{
    /// <summary>
    /// J.1 — Confirmed swing low at index k.
    /// Requires candles[k-2]..candles[k+2] to exist (all closed).
    /// </summary>
    public static bool IsConfirmedSwingLow(Candle[] candles, int k)
    {
        if (k < 2 || k + 2 >= candles.Length)
            return false;

        return candles[k].Low < candles[k - 1].Low
            && candles[k].Low < candles[k - 2].Low
            && candles[k].Low <= candles[k + 1].Low
            && candles[k].Low <= candles[k + 2].Low;
    }

    /// <summary>
    /// J.2 — Confirmed swing high at index k.
    /// </summary>
    public static bool IsConfirmedSwingHigh(Candle[] candles, int k)
    {
        if (k < 2 || k + 2 >= candles.Length)
            return false;

        return candles[k].High > candles[k - 1].High
            && candles[k].High > candles[k - 2].High
            && candles[k].High >= candles[k + 1].High
            && candles[k].High >= candles[k + 2].High;
    }

    /// <summary>
    /// J.3 — Selects the latest confirmed swing within lookback.
    /// Buy → swing low; Sell → swing high.
    /// candles[0] is most recent closed candle.
    /// Returns (found, price).
    /// </summary>
    public static (bool Found, double Price) SelectSwing(
        Candle[] candles, TradeDirection direction, int lookbackCount)
    {
        // Search from most recent to oldest, leaving room for right-side confirmation
        for (int k = lookbackCount - 3; k >= 2; k--)
        {
            if (direction == TradeDirection.Buy && IsConfirmedSwingLow(candles, k))
                return (true, candles[k].Low);

            if (direction == TradeDirection.Sell && IsConfirmedSwingHigh(candles, k))
                return (true, candles[k].High);
        }

        return (false, 0);
    }
}
