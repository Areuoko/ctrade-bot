using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Portfolio risk guards. Rules O.1–O.4.
///
/// RISK REVISION: <see cref="MaxReservedRiskPct"/> was scaled up alongside the
/// per-trade risk revision in <see cref="PositionSizer"/>, keeping the same 2x
/// ratio to per-trade risk that the original spec used (0.60% / 0.30% = 2x).
/// <see cref="RiskPerTradePct"/> below is retained only for <see cref="RiskPerTrade"/>
/// (O.1, currently unused by the orchestrator directly — sizing goes through
/// <see cref="PositionSizer"/> instead) and is NOT the authoritative per-trade risk
/// value; see <see cref="PositionSizer"/> for the actual value used in sizing.
/// <see cref="MetalBasketPct"/> and <see cref="UsdExposurePct"/> are single-symbol
/// (EURUSD-only) build placeholders, unused in this build — see
/// <c>BarEvaluationOrchestrator</c>'s class doc comment.
/// </summary>
public static class PortfolioRiskGuard
{
    private const double RiskPerTradePct = 0.01;     // 1.00% — see class doc comment
    // Revised from 0.006 (0.60%) — kept at the same 2x ratio to per-trade risk
    // that the original spec used (0.60% / 0.30% = 2x), applied to the new 1.00%.
    private const double MaxReservedRiskPct = 0.02;  // 2.00%
    private const double MetalBasketPct = 0.003;     // 0.30% — unused (single-symbol build)
    private const double UsdExposurePct = 0.0045;    // 0.45% — unused (single-symbol build)

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
