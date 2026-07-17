using System;
using System.Collections.Generic;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for reading market candle data. Implemented by Infrastructure/CTrader.
/// </summary>
public interface IMarketDataPort
{
    /// <summary>
    /// Returns the last N fully closed candles for the given timeframe.
    /// Candle at index 0 is the most recent closed candle ([1] in specification terms).
    /// </summary>
    /// <param name="timeframeMinutes">Timeframe in minutes (15 for M15, 60 for H1).</param>
    /// <param name="count">Number of candles to return.</param>
    /// <returns>Ordered list from newest to oldest. Empty if data unavailable.</returns>
    IReadOnlyList<Candle> GetClosedBars(int timeframeMinutes, int count);

    /// <summary>
    /// Returns the current server time in UTC.
    /// </summary>
    DateTime ServerTimeUtc { get; }
}
