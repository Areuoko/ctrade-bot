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
///
/// BUILD FIX: <see cref="Application.Orchestration.BarEvaluationOrchestrator"/> now
/// calls the reserved-risk check with an explicit
/// <c>SymbolEvaluationConfig.MaxReservedRiskPct</c> value (config-driven, so
/// Walk-Forward/backtest runs can vary it), which requires a 3-argument overload of
/// <see cref="PassesReservedRisk(double, double, double)"/>. The original 2-argument
/// overload is kept — falling back to the <see cref="MaxReservedRiskPct"/> constant
/// below — for any direct/test callers that don't have a config value on hand.
/// </summary>
public static class PortfolioRiskGuard
{
    private const double RiskPerTradePct = 0.01;     // 1.00% — see class doc comment
    // Revised from 0.006 (0.60%) — kept at the same 2x ratio to per-trade risk
    // that the original spec used (0.60% / 0.30% = 2x), applied to the new 1.00%.
    // Used only as the fallback default for the 2-argument PassesReservedRisk
    // overload below; the orchestrator's live/backtest calls pass
    // SymbolEvaluationConfig.MaxReservedRiskPct explicitly instead.
    private const double MaxReservedRiskPct = 0.02;  // 2.00%
    private const double MetalBasketPct = 0.003;     // 0.30% — unused (single-symbol build)
    private const double UsdExposurePct = 0.0045;    // 0.45% — unused (single-symbol build)

    /// <summary>
    /// O.1 — Per-trade risk cap.
    /// </summary>
    public static double RiskPerTrade(double equity)
        => equity * RiskPerTradePct;

    /// <summary>
    /// O.2 — Total reserved risk check, using a caller-supplied cap (e.g. from
    /// <c>SymbolEvaluationConfig.MaxReservedRiskPct</c> so Walk-Forward/backtest runs
    /// can vary it per parameter combination).
    /// </summary>
    /// <param name="totalReservedRisk">Sum of reserved risk (open + pending) in account currency.</param>
    /// <param name="equity">Current account equity.</param>
    /// <param name="maxReservedRiskPct">Maximum allowed reserved risk as a fraction of equity (e.g. 0.02 for 2.00%).</param>
    public static bool PassesReservedRisk(double totalReservedRisk, double equity, double maxReservedRiskPct)
    {
        if (equity <= 0) return false;
        return totalReservedRisk <= maxReservedRiskPct * equity;
    }

    /// <summary>
    /// O.2 — Total reserved risk check using the internal default cap
    /// (<see cref="MaxReservedRiskPct"/>). Kept for callers without a config value
    /// on hand; the orchestrator uses the 3-argument overload above instead.
    /// </summary>
    /// <param name="totalReservedRisk">Sum of reserved risk (open + pending) in account currency.</param>
    /// <param name="equity">Current account equity.</param>
    public static bool PassesReservedRisk(double totalReservedRisk, double equity)
        => PassesReservedRisk(totalReservedRisk, equity, MaxReservedRiskPct);

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
