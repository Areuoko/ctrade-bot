using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Orders;

/// <summary>
/// Trade management calculations. Rules M.1, M.3.
/// Pure domain logic — no cAlgo.API reference.
/// </summary>
public static class TradeManagementCalculator
{
    private const double TakeProfitRMultiple = 2.0;
    private const int TimeExitCandleCount = 32;

    /// <summary>
    /// M.1 — Take-profit level.
    /// R = |FillPrice − InitialSL|; Buy: TP = FillPrice + 2R; Sell: TP = FillPrice − 2R.
    /// TP is rounded to tick size.
    /// </summary>
    public static double ComputeTakeProfit(
        TradeDirection direction, double fillPrice, double initialSl, double tickSize)
    {
        double r = Math.Abs(fillPrice - initialSl);

        // TP must round AWAY from entry (never toward it), so the realized
        // R multiple is never smaller than 2R. Buy TP is above entry → round up.
        // Sell TP is below entry → round down.
        if (direction == TradeDirection.Buy)
        {
            double tpBuy = fillPrice + TakeProfitRMultiple * r;
            return tickSize > 0
                ? OrderEntryCalculator.RoundUpToTick(tpBuy, tickSize)
                : tpBuy;
        }

        double tpSell = fillPrice - TakeProfitRMultiple * r;
        return tickSize > 0
            ? OrderEntryCalculator.RoundDownToTick(tpSell, tickSize)
            : tpSell;
    }

    /// <summary>
    /// M.3 — Time exit check.
    /// Returns true if 32 M15 candles have elapsed since entry.
    /// Caller passes the number of completed M15 candles since entry open.
    /// </summary>
    public static bool IsTimeExitTriggered(int completedM15CandlesSinceEntry)
        => completedM15CandlesSinceEntry >= TimeExitCandleCount;
}
