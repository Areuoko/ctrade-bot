using System;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for time/session logic. All windows computed with IANA time zones
/// so DST is handled automatically (Rule B.1). Rules B.2, B.3.
/// </summary>
public interface IClockPort
{
    /// <summary>Current UTC time used for all session/window evaluations.</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// True if UtcNow falls inside one of the allowed entry windows
    /// (London open 08:00–11:00 Europe/London, NY morning 08:30–11:30 America/New_York).
    /// Rule B.2. Checked at order-activation time, not just signal time.
    /// </summary>
    bool IsWithinEntryWindow(DateTime checkTimeUtc);

    /// <summary>True from Friday 12:00 New York onward — no new orders. Rule B.3.</summary>
    bool IsPastFridayNewOrderCutoff(DateTime checkTimeUtc);

    /// <summary>True at/after Friday 15:45 New York — force-close all trades. Rule B.3.</summary>
    bool IsPastFridayForceCloseTime(DateTime checkTimeUtc);

    /// <summary>True if inside the broker rollover blackout window. Rule B.3.</summary>
    bool IsWithinRolloverWindow(DateTime checkTimeUtc);

    /// <summary>Server time freshness/clock-skew check inputs (Rule D.1).</summary>
    bool IsClockReliable(DateTime lastPriceTimeUtc, DateTime localTimeUtc);
}
