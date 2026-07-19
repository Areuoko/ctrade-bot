using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Spread filter. Rules I.1, I.2, I.3.
/// Baseline must be computed externally (infrastructure).
/// </summary>
public static class SpreadFilter
{
    private const double SpreadMultiplier = 1.50;

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
