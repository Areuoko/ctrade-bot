using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;

namespace CleanPullM15Pro.Infrastructure.CTrader.MarketData;

/// <summary>
/// Rolling-window spread history, persisted via cAlgo's Robot.LocalStorage so it
/// survives restarts. Rule I.1 requires a spread baseline distinct from the current
/// reading — the previous "spreadBaseline = currentSpread" fallback made the relative
/// check "currentSpread &lt;= 1.5 * spreadBaseline" always true, a silent no-op filter.
///
/// Deliberate deviation from spec section 10's exact definition (median of the same
/// 15-minute slot over the previous 20 trading days): a single non-slotted rolling
/// window of the most recent samples is used instead (spec section 29 classifies
/// spread-baseline method as "پژوهشی", not "ثابت منطقی" — see open-questions.md).
///
/// Revision (pre-Demo-Forward-Test): window enlarged 50 → 200 samples and the
/// aggregate switched from mean to median. Rationale:
/// - EURUSD spread readings on a live feed occasionally spike (thin-liquidity
///   ticks, momentary requotes) well above the typical value; a mean lets a
///   handful of spikes drag the baseline up, which then lets a genuinely wide
///   spread pass the 1.5x-baseline check. Median is robust to that.
/// - 200 samples (vs 50) approximates a longer historical window without
///   requiring same-time-of-day slotting, trading off some recency for a more
///   stable baseline — still far short of spec's literal "20 trading days,
///   same 15-min slot" definition, which remains a known, accepted gap.
/// </summary>
public sealed class SpreadHistoryStore
{
    private const int MaxSamples = 200;
    private const string Key = "SpreadHistory";

    private readonly Robot _robot;
    private readonly string _prefix;

    /// <summary>Creates the store scoped to a single symbol using <paramref name="robot"/>'s LocalStorage.</summary>
    /// <param name="robot">The cBot instance providing LocalStorage.</param>
    /// <param name="symbolName">Symbol name used to namespace the persisted key (sanitized to Latin letters/digits only — LocalStorage keys allow no other characters).</param>
    public SpreadHistoryStore(Robot robot, string symbolName)
    {
        _robot = robot;
        _prefix = "CleanPullM15Pro" + SanitizeKeyPart(symbolName);
    }

    /// <summary>
    /// Records the current spread reading and returns the updated rolling baseline.
    /// Call once per OnBar (on the same M15 close the quality snapshot is built for) —
    /// recording on every tick would bias the baseline toward whichever moments
    /// generate more ticks rather than toward "typical spread per bar".
    /// </summary>
    /// <param name="currentSpread">Current spread reading in price units.</param>
    /// <returns>(Baseline, ValidObservations). Baseline is the median of stored samples
    /// after recording this one; 0 if none yet. Caller must check validity via
    /// <see cref="Domain.Risk.SpreadFilter.IsBaselineValid"/> before trusting Baseline.</returns>
    public (double Baseline, int ValidObservations) RecordAndGetBaseline(double currentSpread)
    {
        var samples = LoadSamples();

        samples.Add(currentSpread);
        if (samples.Count > MaxSamples)
            samples.RemoveAt(0); // drop oldest

        SaveSamples(samples);

        double baseline = samples.Count > 0 ? Median(samples) : 0;
        return (baseline, samples.Count);
    }

    private List<double> LoadSamples()
    {
        var raw = _robot.LocalStorage.GetString(_prefix + Key);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<double>();

        var result = new List<double>();
        foreach (var part in raw.Split(','))
        {
            if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                result.Add(v);
        }
        return result;
    }

    private void SaveSamples(List<double> samples)
    {
        string joined = string.Join(",", samples.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        _robot.LocalStorage.SetString(_prefix + Key, joined);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        if (n == 0) return 0;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static string SanitizeKeyPart(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var chars = new char[value.Length];
        int count = 0;
        foreach (var c in value)
            if (char.IsLetterOrDigit(c)) chars[count++] = c;
        return new string(chars, 0, count);
    }
}