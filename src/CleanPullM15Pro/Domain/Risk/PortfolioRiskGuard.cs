using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Portfolio risk guards. Rules O.1–O.4.
/// </summary>
public static class PortfolioRiskGuard
{
    private const double RiskPerTradePct = 0.003;    // 0.30%
    private const double MaxReservedRiskPct = 0.006; // 0.60%
    private const double MetalBasketPct = 0.003;     // 0.30%
    private const double UsdExposurePct = 0.0045;    // 0.45%

    /// <summary>
    /// O.1 — Per-trade risk cap.
    /// </summary>
    public static double RiskPerTrade(double equity)
        => equity * RiskPerTradePct;

    /// <summary>
    /// O.2 — Total reserved risk check.
    /// </summary>
    public static bool PassesReservedRisk(double totalReservedRisk, double equity)
    {
        if (equity <= 0) return false;
        return totalReservedRisk <= MaxReservedRiskPct * equity;
    }

    /// <summary>
    /// O.3 — Metal basket risk check.
    /// </summary>
    public static ReasonCode? ValidateMetalBasket(
        double xauUsdRisk, double xagUsdRisk, double equity)
    {
        if (equity <= 0) return ReasonCode.RejectDataInvalid;

        double combined = xauUsdRisk + xagUsdRisk;
        if (combined > MetalBasketPct * equity)
            return ReasonCode.RejectCorrelatedRisk;

        return null;
    }

    /// <summary>
    /// O.4 — USD directional exposure check.
    /// sameDirectionRisk = sum of risks in the same USD direction.
    /// </summary>
    public static ReasonCode? ValidateUsdExposure(
        double sameDirectionRisk, double equity)
    {
        if (equity <= 0) return ReasonCode.RejectDataInvalid;

        if (sameDirectionRisk > UsdExposurePct * equity)
            return ReasonCode.RejectUsdExposure;

        return null;
    }
}
