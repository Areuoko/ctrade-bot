using System;

namespace CleanPullM15Pro.Domain.Orders;

/// <summary>
/// A single Level-A economic event as consumed by INewsCalendarPort implementations.
/// Shared between <c>ManualNewsCalendarAdapter</c> (manual/testing) and
/// <c>FinnhubNewsCalendarAdapter</c> (live feed) so both produce the same shape
/// that <see cref="NewsWindowCalculator"/> understands. Rule N.2, N.3.
/// </summary>
/// <param name="TimeUtc">Scheduled event time in UTC.</param>
/// <param name="Title">
/// Canonical event title, matched against <see cref="NewsWindowCalculator.IsLevelA"/>.
/// Producers (manual list, Finnhub adapter, etc.) are responsible for normalizing
/// their raw event names into one of the fixed titles from spec section 15.2
/// before constructing this record — this type does not validate the title itself.
/// </param>
/// <param name="IsFomc">
/// True if this is an FOMC event, which uses the wider prohibition window
/// (90 min before / 60 min after) per spec section 15.3, instead of the
/// standard Level-A window (60 min before / 45 min after).
/// </param>
public readonly record struct NewsEvent(DateTime TimeUtc, string Title, bool IsFomc);
