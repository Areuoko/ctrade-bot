using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Position sizing. Rules L.1–L.5.
/// </summary>
public static class PositionSizer
{
    private const double RiskPerTradePct = 0.003; // 0.30%

    /// <summary>
    /// L.1 — Trade risk money = Equity × 0.30%.
    /// </summary>
    public static double ComputeTradeRiskMoney(double equity)
        => equity * RiskPerTradePct;

    /// <summary>
    /// L.2 dependency — Loss per lot if SL is hit, including a conservative estimate
    /// of commission and slippage (Rule: "Commission برآوردی و یک Slippage
    /// محافظه‌کارانه در LossPerLotAtSL لحاظ می‌شوند").
    /// </summary>
    /// <param name="entryPrice">Expected entry price.</param>
    /// <param name="stopLoss">Computed stop-loss price.</param>
    /// <param name="tickSize">Symbol tick size.</param>
    /// <param name="tickValue">Money value of one tick, one lot.</param>
    /// <param name="commissionPerLotRoundTurn">Estimated round-turn commission per lot.</param>
    /// <param name="conservativeSlippagePriceUnits">Extra price distance to assume for slippage.</param>
    public static double ComputeLossPerLotAtSL(
        double entryPrice, double stopLoss, double tickSize, double tickValue,
        double commissionPerLotRoundTurn, double conservativeSlippagePriceUnits)
    {
        if (tickSize <= 0 || tickValue <= 0)
            return 0;

        double priceDistance = System.Math.Abs(entryPrice - stopLoss) + System.Math.Max(0, conservativeSlippagePriceUnits);
        double stopLossMoneyPerLot = (priceDistance / tickSize) * tickValue;

        return stopLossMoneyPerLot + System.Math.Max(0, commissionPerLotRoundTurn);
    }

    /// <summary>
    /// L.2 — Raw volume = TradeRiskMoney / LossPerLotAtSL.
    /// Returns (volume, rejection). Null volume means rejection.
    /// </summary>
    public static (double Volume, ReasonCode? Rejection) ComputeRawVolume(
        double tradeRiskMoney, double lossPerLotAtSL)
    {
        if (lossPerLotAtSL <= 0)
            return (0, ReasonCode.RejectVolumeInvalid);

        double raw = tradeRiskMoney / lossPerLotAtSL;

        if (double.IsNaN(raw) || double.IsInfinity(raw) || raw <= 0)
            return (0, ReasonCode.RejectVolumeInvalid);

        return (raw, null);
    }

    /// <summary>
    /// L.3 — Rounds volume down to LotStep. Returns (rounded, rejection).
    /// </summary>
    public static (double Volume, ReasonCode? Rejection) RoundVolume(
        double rawVolume, double lotStep, double minLot)
    {
        if (lotStep <= 0)
            return (0, ReasonCode.RejectDataInvalid);

        double rounded = Math.Floor(rawVolume / lotStep) * lotStep;

        if (rounded < minLot)
            return (0, ReasonCode.RejectBelowMinLot);

        return (rounded, null);
    }

    /// <summary>
    /// L.4 — Margin check. Returns true if free margin is sufficient.
    /// Caller must compute required margin externally.
    /// </summary>
    public static bool PassesMarginCheck(double requiredMargin, double freeMargin)
        => freeMargin >= requiredMargin;

    /// <summary>
    /// L.5 — Post-rounding risk check.
    /// </summary>
    public static ReasonCode? ValidatePostRoundingRisk(
        double finalVolume, double lossPerLotAtSL, double perTradeCap)
    {
        double actualRisk = finalVolume * lossPerLotAtSL;

        if (actualRisk > perTradeCap)
            return ReasonCode.RejectRiskExceeded;

        return null;
    }
}
