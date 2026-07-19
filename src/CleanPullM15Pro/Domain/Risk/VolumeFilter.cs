using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Volume filter. Rules H.1, H.2.
/// Baseline must be computed externally (infrastructure).
/// </summary>
public static class VolumeFilter
{
    private const double VolumeMultiplier = 1.10;
    private const int MinObservations = 15;

    /// <summary>
    /// H.1 — Validates that the baseline has enough observations.
    /// Caller must provide pre-computed median baseline.
    /// </summary>
    public static bool IsBaselineValid(int validObservations)
        => validObservations >= MinObservations;

    /// <summary>
    /// H.2 — Compares tick volume against baseline.
    /// </summary>
    public static bool Passes(long tickVolume, double volumeBaseline)
    {
        if (double.IsNaN(volumeBaseline) || volumeBaseline <= 0)
            return false;

        return tickVolume >= VolumeMultiplier * volumeBaseline;
    }
}
