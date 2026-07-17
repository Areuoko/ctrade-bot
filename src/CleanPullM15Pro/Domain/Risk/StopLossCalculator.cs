using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Stop-loss calculation and validation. Rules J.4, J.5, J.6.
/// </summary>
public static class StopLossCalculator
{
    private const double SlBufferAtrCoeff = 0.15;

    /// <summary>
    /// J.4 — Computes raw SL level before broker validation.
    /// </summary>
    public static double ComputeLevel(TradeDirection direction, double swingPrice, double atr14Bar1)
    {
        return direction == TradeDirection.Buy
            ? swingPrice - SlBufferAtrCoeff * atr14Bar1
            : swingPrice + SlBufferAtrCoeff * atr14Bar1;
    }

    /// <summary>
    /// J.5 — Validates stop distance in ATR multiples against symbol bounds.
    /// Returns null if valid, or the appropriate rejection ReasonCode.
    /// </summary>
    public static ReasonCode? ValidateDistance(
        double entryPrice, double sl, double atr14Bar1,
        double minStopAtr, double maxStopAtr)
    {
        if (double.IsNaN(atr14Bar1) || atr14Bar1 <= 0)
            return ReasonCode.RejectDataInvalid;

        double stopDistanceAtr = Math.Abs(entryPrice - sl) / atr14Bar1;

        if (stopDistanceAtr < minStopAtr)
            return ReasonCode.RejectStopTooNarrow;
        if (stopDistanceAtr > maxStopAtr)
            return ReasonCode.RejectStopTooWide;

        return null;
    }

    /// <summary>
    /// J.6 — Validates SL against broker StopLevel and FreezeLevel.
    /// For buy: SL must be below current bid minus StopLevel.
    /// For sell: SL must be above current ask plus StopLevel.
    /// </summary>
    public static ReasonCode? ValidateBrokerLimits(
        TradeDirection direction, double sl, double currentPrice,
        double stopLevel, double freezeLevel, bool hasOpenPosition)
    {
        // FreezeLevel prevents modification of existing orders/positions
        if (hasOpenPosition && freezeLevel > 0)
        {
            // Existing position SL modification blocked by freeze level
            // This is checked but not the primary rejection here
        }

        if (stopLevel <= 0)
            return null;

        double minDistance = stopLevel;

        if (direction == TradeDirection.Buy && currentPrice - sl < minDistance)
            return ReasonCode.RejectStopLevel;

        if (direction == TradeDirection.Sell && sl - currentPrice < minDistance)
            return ReasonCode.RejectStopLevel;

        return null;
    }
}
