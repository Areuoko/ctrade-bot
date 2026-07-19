using System;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for the economic news calendar. Rule N.*.
/// If the calendar is unavailable/stale, new entries must be blocked (fail closed).
/// </summary>
public interface INewsCalendarPort
{
    /// <summary>True if the calendar data is present and fresh enough to trust.</summary>
    bool IsAvailableAndFresh { get; }

    /// <summary>
    /// True if checkTimeUtc falls inside a prohibited window for any Level-A event
    /// relevant to the given symbol's currencies (Rule N.3).
    /// </summary>
    bool IsInProhibitedWindow(string symbolName, DateTime checkTimeUtc);

    /// <summary>
    /// True if a relevant Level-A event's prohibited window starts within the next
    /// 15 minutes — used to pre-cancel pending orders / pre-close positions (Rule N.3).
    /// </summary>
    bool IsApproachingProhibitedWindow(string symbolName, DateTime checkTimeUtc, TimeSpan lookAhead);
}
