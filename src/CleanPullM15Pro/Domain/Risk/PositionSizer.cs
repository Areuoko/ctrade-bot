using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Position sizing. Rules L.1–L.5, AB.6.
///
/// RISK REVISION (documented intentional deviation from spec section 16.1's base
/// values of 0.30% / 0.15%): per-trade risk raised to 1.00% (Pullback) / 0.50%
/// (Breakout) — roughly a 3.33x scale-up — for the current research/testing phase,
/// while sample sizes are still small (see docs/open-questions.md for the full
/// rationale and the trade-off being made). This is a research parameter, not a
/// "ثابت منطقی" (spec section 29) — it must be re-evaluated before any live-account
/// use, and should trend back down toward the original conservative values as
/// Demo Forward Test data accumulates.
/// </summary>
public static class PositionSizer
{
    // Revised from 0.003 (0.30%) — see class-level doc comment and open-questions.md.
    private const double RiskPerTradePct = 0.01; // 1.00% (Pullback strategy default)

    /// <summary>
    /// AB.6 — Breakout strategy's reduced per-trade risk percentage (half of Pullback's
    /// current value), pending Demo Forward Test confirmation. Revised from 0.0015 (0.15%)
    /// alongside the Pullback risk revision above.
    /// </summary>
    public const double BreakoutRiskPerTradePct = 0.005; // 0.50%

    /// <summary>
    /// L.1 — Trade risk money = Equity × 1.00% (Pullback strategy default).
    /// </summary>
    public static double ComputeTradeRiskMoney(double equity)
        => ComputeTradeRiskMoney(equity, RiskPerTradePct);

    /// <summary>
    /// L.1/AB.6 — Trade risk money = Equity × riskPct. Overload allowing a strategy-specific
    /// risk percentage (e.g. <see cref="BreakoutRiskPerTradePct"/> for the Breakout strategy).
    /// </summary>
    /// <param name="equity">Current account equity.</param>
    /// <param name="riskPct">Risk percentage to apply (e.g. 0.01 for 1.00%).</param>
    public static double ComputeTradeRiskMoney(double equity, double riskPct)
        => equity * riskPct;

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
