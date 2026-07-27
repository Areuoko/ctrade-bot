using System;
using System.Collections.Generic;
using System.Globalization;
using CleanPullM15Pro.Domain.Orders;

namespace CleanPullM15Pro.Infrastructure.CTrader.News;

/// <summary>
/// Parses a hand-maintained Level-A news list from the cBot's text parameter into
/// <see cref="NewsEvent"/> records for <see cref="ManualNewsCalendarAdapter"/>. Used
/// when the bot runs under AccessRights.None (e.g. cTrader Cloud, which disallows
/// internet access for custom cBots — the Finnhub/ForexFactory live feeds cannot run
/// there). Format: one event per line, "yyyy-MM-ddTHH:mm:ssZ|Title|IsFomc", e.g.
///   2026-07-29T18:00:00Z|FOMC Rate Decision|true
///   2026-07-30T12:30:00Z|US Core PCE|false
/// Title must exactly match one of the canonical Level-A titles recognized by
/// NewsWindowCalculator.IsLevelA (spec section 15.2); a typo there fails closed at
/// the window-check stage, not here. Blank lines and lines starting with '#' are
/// ignored. A line that fails to parse is skipped and reported in Errors so the
/// caller can log it — parsing never throws and never stops on one bad line.
/// </summary>
public static class ManualNewsEventsParser
{
    /// <summary>Parses raw multi-line parameter text into events plus any per-line errors.</summary>
    /// <param name="rawText">Raw parameter text, one event per line.</param>
    /// <returns>Parsed events and human-readable errors for lines that failed to parse.</returns>
    public static (List<NewsEvent> Events, List<string> Errors) Parse(string rawText)
    {
        var events = new List<NewsEvent>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rawText))
            return (events, errors);

        var lines = rawText.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            var parts = line.Split('|');
            if (parts.Length != 3)
            {
                errors.Add($"Line {i + 1}: expected 3 fields separated by '|', got {parts.Length}: \"{line}\"");
                continue;
            }

            if (!DateTime.TryParse(
                    parts[0].Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timeUtc))
            {
                errors.Add($"Line {i + 1}: could not parse UTC time \"{parts[0].Trim()}\"");
                continue;
            }

            string title = parts[1].Trim();
            if (title.Length == 0)
            {
                errors.Add($"Line {i + 1}: title is empty");
                continue;
            }

            if (!bool.TryParse(parts[2].Trim(), out var isFomc))
            {
                errors.Add($"Line {i + 1}: could not parse IsFomc flag \"{parts[2].Trim()}\" (use true/false)");
                continue;
            }

            events.Add(new NewsEvent(timeUtc, title, isFomc));
        }

        return (events, errors);
    }
}