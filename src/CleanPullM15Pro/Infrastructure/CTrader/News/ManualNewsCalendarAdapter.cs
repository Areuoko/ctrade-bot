using System;
using System.Collections.Generic;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Orders;

namespace CleanPullM15Pro.Infrastructure.CTrader.News;

/// <summary>
/// Manual/stub news calendar. Useful for controlled demo testing or as a fallback
/// when no live feed is wired in. Rule N.3 fail-closed behavior:
/// - If <see cref="_events"/> is empty and <see cref="_treatEmptyAsUnavailable"/>
///   is true (the default), IsAvailableAndFresh returns false and ALL new entries
///   are blocked — the bot will never trade.
/// - Set _treatEmptyAsUnavailable=false only for controlled demo testing where
///   you accept trading through news events; this intentionally weakens a
///   documented safety filter and must not be used on a live account.
/// For a real live feed, see <see cref="FinnhubNewsCalendarAdapter"/> instead.
/// </summary>
public sealed class ManualNewsCalendarAdapter : INewsCalendarPort
{
    private readonly List<NewsEvent> _events;
    private readonly bool _treatEmptyAsUnavailable;

    /// <summary>
    /// Constructs the manual calendar. With the default <paramref name="treatEmptyAsUnavailable"/>,
    /// an empty event list blocks all new entries (fail closed per Rule N.3); set it false only
    /// for controlled demo testing.
    /// </summary>
    /// <param name="events">Manually-maintained Level-A event list.</param>
    /// <param name="treatEmptyAsUnavailable">When true an empty list reports the calendar as unavailable (fail closed).</param>
    public ManualNewsCalendarAdapter(List<NewsEvent> events, bool treatEmptyAsUnavailable = true)
    {
        _events = events;
        _treatEmptyAsUnavailable = treatEmptyAsUnavailable;
    }

    /// <summary>True when a usable calendar is available; false (fail closed) when the event list is empty and <see cref="_treatEmptyAsUnavailable"/> is set.</summary>
    public bool IsAvailableAndFresh => _events.Count > 0 || !_treatEmptyAsUnavailable;

    /// <summary>
    /// Returns true when <paramref name="checkTimeUtc"/> falls inside the prohibition window
    /// of any Level-A event relevant to <paramref name="symbolName"/>.
    /// </summary>
    /// <param name="symbolName">Symbol whose currency exposure triggers relevance.</param>
    /// <param name="checkTimeUtc">Time to test against event windows.</param>
    /// <returns>True if a relevant Level-A event prohibition window covers the time.</returns>
    public bool IsInProhibitedWindow(string symbolName, DateTime checkTimeUtc)
    {
        var currencies = NewsWindowCalculator.GetRelevantCurrencies(symbolName);
        foreach (var evt in _events)
        {
            if (!NewsWindowCalculator.IsLevelA(evt.Title))
                continue;

            if (!EventAppliesToSymbol(evt.Title, currencies))
                continue;

            if (NewsWindowCalculator.IsInProhibitedWindow(checkTimeUtc, evt.TimeUtc, evt.IsFomc))
                return true;
        }
        return false;
    }

    /// <summary>True if a relevant Level-A prohibition window starts within <paramref name="lookAhead"/> of <paramref name="checkTimeUtc"/>.</summary>
    /// <param name="symbolName">Symbol whose currency exposure triggers relevance.</param>
    /// <param name="checkTimeUtc">Reference time.</param>
    /// <param name="lookAhead">How far ahead to look for an approaching window.</param>
    /// <returns>True if a prohibition window begins within the look-ahead horizon.</returns>
    public bool IsApproachingProhibitedWindow(string symbolName, DateTime checkTimeUtc, TimeSpan lookAhead)
        => IsInProhibitedWindow(symbolName, checkTimeUtc + lookAhead);

    private static bool EventAppliesToSymbol(string title, string[] symbolCurrencies)
    {
        foreach (var currency in symbolCurrencies)
        {
            if (title.Contains(currency, StringComparison.OrdinalIgnoreCase))
                return true;
            // FOMC/CPI/NFP/PCE are USD events by convention even without "USD" in the title.
            if (currency == "USD" && (title.Contains("FOMC") || title.Contains("CPI") || title.Contains("Nonfarm") || title.Contains("PCE")))
                return true;
            if (currency == "EUR" && title.Contains("ECB"))
                return true;
        }
        return false;
    }
}
