using System;
using System.Collections.Generic;
using cAlgo.API;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.MarketData;

/// <summary>
/// Implements IMarketDataPort using cAlgo's Bars API for the robot's attached symbol.
/// Rule G.1/B.1: every timestamp is converted to UTC before leaving this class.
/// </summary>
public sealed class CTraderMarketDataAdapter : IMarketDataPort
{
    private readonly Robot _robot;

    /// <summary>Creates the market-data adapter bound to the host cBot's attached symbol.</summary>
    /// <param name="robot">The cBot whose MarketData/Server are read.</param>
    public CTraderMarketDataAdapter(Robot robot)
    {
        _robot = robot;
    }

    /// <summary>
    /// NOTE: verify at build time that cAlgo.API.Server exposes TimeInUtc on your SDK
    /// version. If it does not, replace with Server.Time and a manually configured
    /// fixed broker-UTC offset from your broker's specification sheet.
    /// </summary>
    public DateTime ServerTimeUtc => _robot.Server.TimeInUtc;

    /// <summary>
    /// Returns the last <paramref name="count"/> fully closed candles, newest first.
    /// Result[0] = spec's [1] (last closed candle). Bars.Last(0) — the current,
    /// possibly still-open bar — is never read.
    /// </summary>
    public IReadOnlyList<Candle> GetClosedBars(int timeframeMinutes, int count)
    {
        var tf = ToTimeFrame(timeframeMinutes);
        var bars = _robot.MarketData.GetBars(tf);

        // Constant offset between broker server clock and UTC, derived once per call
        // from the two current-time readings. Applied to each historical bar's
        // OpenTime, which cAlgo reports in broker server time.
        var utcOffset = _robot.Server.TimeInUtc - _robot.Server.Time;

        var result = new List<Candle>(count);
        for (int i = 1; i <= count; i++)
        {
            int idx = bars.Count - 1 - i;
            if (idx < 0) break;

            result.Add(new Candle
            {
                TimestampUtc = bars.OpenTimes[idx] + utcOffset,
                Open = bars.OpenPrices[idx],
                High = bars.HighPrices[idx],
                Low = bars.LowPrices[idx],
                Close = bars.ClosePrices[idx],
                TickVolume = (long)bars.TickVolumes[idx]
            });
        }

        return result;
    }

    private static TimeFrame ToTimeFrame(int minutes) => minutes switch
    {
        15 => TimeFrame.Minute15,
        60 => TimeFrame.Hour,
        _ => throw new ArgumentOutOfRangeException(nameof(minutes), $"Unsupported timeframe: {minutes} minutes")
    };
}
