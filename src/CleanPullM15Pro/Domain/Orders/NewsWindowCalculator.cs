using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Orders;

/// <summary>
/// News window calculations. Rules N.2, N.3.
/// Pure domain logic — no cAlgo.API reference.
/// </summary>
public static class NewsWindowCalculator
{
    // N.3 — Prohibited window minutes
    private const int FomcBeforeMinutes = 90;
    private const int FomcAfterMinutes = 60;
    private const int OtherLevelABeforeMinutes = 60;
    private const int OtherLevelAAfterMinutes = 45;

    /// <summary>
    /// N.2 — Determines if a news event is Level A (high impact).
    /// </summary>
    public static bool IsLevelA(string eventTitle)
    {
        if (string.IsNullOrWhiteSpace(eventTitle))
            return false;

        return eventTitle.Contains("FOMC Rate Decision")
            || eventTitle.Contains("FOMC Press Conference")
            || eventTitle.Contains("US CPI")
            || eventTitle.Contains("US Nonfarm Payrolls")
            || eventTitle.Contains("US Core PCE")
            || eventTitle.Contains("ECB Rate Decision")
            || eventTitle.Contains("ECB Press Conference");
    }

    /// <summary>
    /// N.2 — Maps a symbol to its relevant news currencies.
    /// EURUSD → EUR+USD; XAUUSD → USD; XAGUSD → USD.
    /// Returns empty array if no mapping exists.
    /// </summary>
    public static string[] GetRelevantCurrencies(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
            return Array.Empty<string>();

        string upper = symbolName.ToUpperInvariant();

        if (upper.Contains("EUR") && upper.Contains("USD"))
            return new[] { "EUR", "USD" };
        if (upper.Contains("XAU") || upper.Contains("XAG"))
            return new[] { "USD" };

        return Array.Empty<string>();
    }

    /// <summary>
    /// N.3 — Computes the prohibited trading window around a Level A news event.
    /// Returns (windowStart, windowEnd).
    /// </summary>
    public static (DateTime WindowStart, DateTime WindowEnd) ComputeProhibitedWindow(
        DateTime eventTimeUtc, bool isFomc)
    {
        int beforeMinutes = isFomc ? FomcBeforeMinutes : OtherLevelABeforeMinutes;
        int afterMinutes = isFomc ? FomcAfterMinutes : OtherLevelAAfterMinutes;

        return (
            eventTimeUtc.AddMinutes(-beforeMinutes),
            eventTimeUtc.AddMinutes(afterMinutes)
        );
    }

    /// <summary>
    /// N.3 — Checks if a given time falls within the prohibited window.
    /// </summary>
    public static bool IsInProhibitedWindow(
        DateTime checkTimeUtc, DateTime eventTimeUtc, bool isFomc)
    {
        var (start, end) = ComputeProhibitedWindow(eventTimeUtc, isFomc);
        return checkTimeUtc >= start && checkTimeUtc <= end;
    }
}
