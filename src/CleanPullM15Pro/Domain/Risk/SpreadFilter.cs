using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Spread filter. Rules I.1, I.2, I.3.
/// Baseline must be computed externally (infrastructure).
/// </summary>
public static class SpreadFilter
{
    private const double SpreadMultiplier = 1.50;
    private const int MinObservations = 20;

    /// <summary>
    /// I.1 — Validates that the rolling spread baseline has enough observations
    /// before it can be trusted. Below this threshold, callers must fail closed
    /// (reject new entries) per Rule N.3-style philosophy, rather than using an
    /// under-sampled or degenerate baseline.
    /// </summary>
    public static bool IsBaselineValid(int validObservations)
        => validObservations >= MinObservations;

    /// <summary>
    /// I.2 — Checks spread against baseline and absolute cap.
    /// </summary>
    public static bool Passes(double currentSpread, double spreadBaseline, double absoluteCap)
    {
        if (double.IsNaN(spreadBaseline) || spreadBaseline <= 0)
            return false;

        if (double.IsNaN(absoluteCap) || absoluteCap <= 0)
            return false;

        bool withinBaseline = currentSpread <= SpreadMultiplier * spreadBaseline;
        bool withinCap = currentSpread <= absoluteCap;

        return withinBaseline && withinCap;
    }
}
