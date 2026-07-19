using System;
using cAlgo.API;
using CleanPullM15Pro.Application.Ports;

namespace CleanPullM15Pro.Infrastructure.CTrader.Clock;

/// <summary>
/// Implements IClockPort using IANA time zones for DST-safe session windows.
/// Rules B.1–B.3.
/// .NET 6+ resolves IANA ids ("Europe/London", "America/New_York") on Windows
/// via ICU; if FindSystemTimeZoneById throws on your runtime, this falls back
/// to the equivalent Windows time zone id.
/// </summary>
public sealed class CTraderClockAdapter : IClockPort
{
    private readonly Robot _robot;
    private readonly TimeZoneInfo _london;
    private readonly TimeZoneInfo _newYork;

    /// <summary>Time-of-day (UTC, broker-declared) the rollover window is centered on.</summary>
    private readonly TimeSpan _rolloverUtcTimeOfDay;
    private readonly TimeSpan _rolloverMargin = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan LondonOpenStart = new(8, 0, 0);
    private static readonly TimeSpan LondonOpenEnd = new(11, 0, 0);
    private static readonly TimeSpan NyMorningStart = new(8, 30, 0);
    private static readonly TimeSpan NyMorningEnd = new(11, 30, 0);
    private static readonly TimeSpan FridayCutoff = new(12, 0, 0);
    private static readonly TimeSpan FridayForceClose = new(15, 45, 0);

    /// <summary>
    /// Creates the adapter bound to the host cBot and a rollover time-of-day.
    /// </summary>
    /// <param name="robot">The cBot instance supplying broker server time.</param>
    /// <param name="rolloverUtcTimeOfDay">UTC time-of-day the daily rollover window is centered on.</param>
    public CTraderClockAdapter(Robot robot, TimeSpan rolloverUtcTimeOfDay)
    {
        _robot = robot;
        _rolloverUtcTimeOfDay = rolloverUtcTimeOfDay;
        _london = ResolveTimeZone("Europe/London", "GMT Standard Time");
        _newYork = ResolveTimeZone("America/New_York", "Eastern Standard Time");
    }

    /// <summary>Current broker-reported UTC time.</summary>
    public DateTime UtcNow => _robot.Server.TimeInUtc;

    /// <summary>
    /// Rule B.2/B.3 — true when <paramref name="checkTimeUtc"/> is inside the London open
    /// (08:00–11:00) or New York morning (08:30–11:30) local session windows.
    /// </summary>
    /// <param name="checkTimeUtc">Time to evaluate.</param>
    /// <returns>True inside an allowed entry session; otherwise false.</returns>
    public bool IsWithinEntryWindow(DateTime checkTimeUtc)
    {
        var londonLocal = TimeZoneInfo.ConvertTimeFromUtc(checkTimeUtc, _london).TimeOfDay;
        if (londonLocal >= LondonOpenStart && londonLocal <= LondonOpenEnd)
            return true;

        var nyLocal = TimeZoneInfo.ConvertTimeFromUtc(checkTimeUtc, _newYork).TimeOfDay;
        if (nyLocal >= NyMorningStart && nyLocal <= NyMorningEnd)
            return true;

        return false;
    }

    /// <summary>
    /// Rule B.3 — true when <paramref name="checkTimeUtc"/> is past Friday's New York
    /// new-order cutoff (12:00 NY). Saturday/Sunday return true (market shut regardless).
    /// </summary>
    /// <param name="checkTimeUtc">Time to evaluate.</param>
    /// <returns>True when no new orders may be opened after this time on Friday.</returns>
    public bool IsPastFridayNewOrderCutoff(DateTime checkTimeUtc)
    {
        var nyTime = TimeZoneInfo.ConvertTimeFromUtc(checkTimeUtc, _newYork);
        if (nyTime.DayOfWeek < DayOfWeek.Friday) return false;
        if (nyTime.DayOfWeek > DayOfWeek.Friday) return true; // Saturday/Sunday — market shut anyway
        return nyTime.TimeOfDay >= FridayCutoff;
    }

    /// <summary>
    /// Rule B.3 — true when <paramref name="checkTimeUtc"/> is past Friday's New York
    /// force-close time (15:45 NY), at which open positions must be closed.
    /// </summary>
    /// <param name="checkTimeUtc">Time to evaluate.</param>
    /// <returns>True when existing positions must be force-closed.</returns>
    public bool IsPastFridayForceCloseTime(DateTime checkTimeUtc)
    {
        var nyTime = TimeZoneInfo.ConvertTimeFromUtc(checkTimeUtc, _newYork);
        if (nyTime.DayOfWeek < DayOfWeek.Friday) return false;
        if (nyTime.DayOfWeek > DayOfWeek.Friday) return true;
        return nyTime.TimeOfDay >= FridayForceClose;
    }

    /// <summary>
    /// Rule P.1 — true when <paramref name="checkTimeUtc"/> falls within the ±15-minute
    /// rollover window centered on the configured rollover UTC time-of-day (today's
    /// or yesterday's, to cover the wrap-around near midnight).
    /// </summary>
    /// <param name="checkTimeUtc">Time to evaluate.</param>
    /// <returns>True inside the daily rollover window; otherwise false.</returns>
    public bool IsWithinRolloverWindow(DateTime checkTimeUtc)
    {
        var todayRollover = checkTimeUtc.Date + _rolloverUtcTimeOfDay;
        var windowStart = todayRollover - _rolloverMargin;
        var windowEnd = todayRollover + _rolloverMargin;

        if (checkTimeUtc >= windowStart && checkTimeUtc <= windowEnd)
            return true;

        // Handle the case where checkTimeUtc is just after midnight and rollover
        // was actually "yesterday's" rollover time near 24:00.
        var yesterdayRollover = todayRollover.AddDays(-1);
        return checkTimeUtc >= yesterdayRollover - _rolloverMargin && checkTimeUtc <= yesterdayRollover + _rolloverMargin;
    }

    /// <summary>
    /// Rule A.2 — true when the broker clock is not stale: <paramref name="localTimeUtc"/>
    /// is within 10 seconds of the last price tick <paramref name="lastPriceTimeUtc"/>.
    /// </summary>
    /// <param name="lastPriceTimeUtc">Timestamp of the most recent price update.</param>
    /// <param name="localTimeUtc">Current broker UTC time.</param>
    /// <returns>True when the clock is considered reliable; false (fail closed) when stale.</returns>
    public bool IsClockReliable(DateTime lastPriceTimeUtc, DateTime localTimeUtc)
    {
        var staleness = (localTimeUtc - lastPriceTimeUtc).Duration();
        return staleness <= TimeSpan.FromSeconds(10);
    }

    private static TimeZoneInfo ResolveTimeZone(string ianaId, string windowsFallbackId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsFallbackId);
        }
    }
}
