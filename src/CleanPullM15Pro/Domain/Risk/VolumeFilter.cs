using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Volume filter. Rules H.1, H.2, AB.5.
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
    /// H.2 — Compares tick volume against the pullback-strategy baseline (1.10×).
    /// </summary>
    public static bool Passes(long tickVolume, double volumeBaseline)
        => Passes(tickVolume, volumeBaseline, VolumeMultiplier);

    /// <summary>
    /// AB.5 — Compares tick volume against baseline using a caller-supplied multiplier
    /// (e.g. 1.25 for the Breakout strategy). Same NaN/non-positive-baseline guard as H.2.
    /// </summary>
    /// <param name="tickVolume">Tick volume of the signal candle.</param>
    /// <param name="volumeBaseline">Pre-computed baseline (median over the trailing window).</param>
    /// <param name="multiplier">Strategy-specific multiplier applied to the baseline.</param>
    public static bool Passes(long tickVolume, double volumeBaseline, double multiplier)
    {
        if (double.IsNaN(volumeBaseline) || volumeBaseline <= 0)
            return false;

        return tickVolume >= multiplier * volumeBaseline;
    }
}
