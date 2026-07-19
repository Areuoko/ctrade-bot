using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using CleanPullM15Pro.Application.Contracts;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.MarketData;

/// <summary>
/// Builds Application-layer snapshots from cAlgo's Bars + Indicators API.
/// NOTE: indicator overload names/signatures are per the cTrader.Automate SDK
/// referenced in the .csproj. If the compiler reports a different overload,
/// adjust the calls below — the *values* required (EMA/ATR/RSI/ADX, Wilder
/// method per spec section 5) do not change, only the exact API surface might.
/// </summary>
public sealed class CTraderSnapshotBuilder
{
    private const int H1WarmupMinimum = 300;
    private const int M15WarmupMinimum = 500;
    private const int VolumeBaselineDays = 20;
    private const int VolumeBaselineMinObservations = 15;

    private readonly Robot _robot;
    private readonly CTraderMarketDataAdapter _marketData;

    /// <summary>Creates the snapshot builder bound to the host cBot and its market-data adapter.</summary>
    /// <param name="robot">The cBot supplying MarketData and Indicators.</param>
    /// <param name="marketData">Market-data adapter used by the builder.</param>
    public CTraderSnapshotBuilder(Robot robot, CTraderMarketDataAdapter marketData)
    {
        _robot = robot;
        _marketData = marketData;
    }

    /// <summary>
    /// Builds the H1 trend snapshot from Wilder ATR14 and EMA50/EMA200 on the most recent
    /// closed hourly bars. Returns a snapshot with <see cref="H1Snapshot.IsValid"/> = false
    /// when insufficient warmup bars or NaN indicator values are present.
    /// </summary>
    /// <returns>The H1 trend snapshot for the last closed hour.</returns>
    public H1Snapshot BuildH1Snapshot()
    {
        var h1Bars = _robot.MarketData.GetBars(TimeFrame.Hour);

        if (h1Bars.Count < H1WarmupMinimum)
            return new H1Snapshot { IsValid = false };

        var ema50 = _robot.Indicators.ExponentialMovingAverage(h1Bars.ClosePrices, 50);
        var ema200 = _robot.Indicators.ExponentialMovingAverage(h1Bars.ClosePrices, 200);
        var atr14 = _robot.Indicators.AverageTrueRange(h1Bars, 14, MovingAverageType.WilderSmoothing);

        // Bars.Last(0) is the current, unclosed bar. [1] = Last(1), [6] = Last(6).
        int idx1 = h1Bars.Count - 2; // Last(1)
        int idx6 = h1Bars.Count - 7; // Last(6)

        if (idx6 < 0)
            return new H1Snapshot { IsValid = false };

        double ema50Bar1 = ema50.Result[idx1];
        double ema200Bar1 = ema200.Result[idx1];
        double ema50Bar6 = ema50.Result[idx6];
        double atr14Bar1 = atr14.Result[idx1];

        bool valid = AllFinite(ema50Bar1, ema200Bar1, ema50Bar6, atr14Bar1) && atr14Bar1 > 0;

        return new H1Snapshot
        {
            Ema50Bar1 = ema50Bar1,
            Ema200Bar1 = ema200Bar1,
            Ema50Bar6 = ema50Bar6,
            Atr14Bar1 = atr14Bar1,
            IsValid = valid
        };
    }

    /// <summary>
    /// Builds the M15 signal snapshot (EMA20/EMA50, RSI14, ADX14, Wilder ATR14, SMA100 of
    /// ATR, and the closed-candle history used for swing detection) from the most recent
    /// bars. Returns a snapshot with <see cref="M15Snapshot.IsValid"/> = false on insufficient
    /// warmup or NaN indicator values.
    /// </summary>
    /// <param name="swingLookbackWithMargin">Closed-candle count to extract for swing lookback plus confirmation margin.</param>
    /// <returns>The M15 signal snapshot for the last closed bar.</returns>
    public M15Snapshot BuildM15Snapshot(int swingLookbackWithMargin)
    {
        var m15Bars = _robot.MarketData.GetBars(TimeFrame.Minute15);

        if (m15Bars.Count < M15WarmupMinimum)
            return new M15Snapshot { IsValid = false };

        var ema20 = _robot.Indicators.ExponentialMovingAverage(m15Bars.ClosePrices, 20);
        var ema50 = _robot.Indicators.ExponentialMovingAverage(m15Bars.ClosePrices, 50);
        var rsi14 = _robot.Indicators.RelativeStrengthIndex(m15Bars.ClosePrices, 14);
        var adxSystem = _robot.Indicators.DirectionalMovementSystem(m15Bars, 14);
        var atr14 = _robot.Indicators.AverageTrueRange(m15Bars, 14, MovingAverageType.WilderSmoothing);
        var smaAtr100 = _robot.Indicators.SimpleMovingAverage(atr14.Result, 100);

        int idx1 = m15Bars.Count - 2; // Last(1)
        int idx2 = m15Bars.Count - 3; // Last(2)

        if (idx2 < 0)
            return new M15Snapshot { IsValid = false };

        double ema20Bar1 = ema20.Result[idx1];
        double ema50Bar1 = ema50.Result[idx1];
        double rsi14Bar1 = rsi14.Result[idx1];
        double rsi14Bar2 = rsi14.Result[idx2];
        double adx14Bar1 = adxSystem.ADX[idx1];
        double atr14Bar1 = atr14.Result[idx1];
        double smaAtr100Bar1 = smaAtr100.Result[idx1];

        // "Live" values for trigger-time distance re-check (Rule K.4) — read from the
        // current (possibly unclosed) bar index, used ONLY for execution gating,
        // never for signal generation.
        int idxCurrent = m15Bars.Count - 1;
        double ema20Current = ema20.Result[idxCurrent];
        double atr14Current = atr14.Result[idxCurrent];

        var candles = ExtractCandles(m15Bars, swingLookbackWithMargin);

        bool valid = AllFinite(ema20Bar1, ema50Bar1, rsi14Bar1, rsi14Bar2, adx14Bar1, atr14Bar1, smaAtr100Bar1)
            && atr14Bar1 > 0 && smaAtr100Bar1 > 0
            && candles.Length > 0;

        return new M15Snapshot
        {
            Candles = candles,
            Ema20Bar1 = ema20Bar1,
            Ema50Bar1 = ema50Bar1,
            Rsi14Bar1 = rsi14Bar1,
            Rsi14Bar2 = rsi14Bar2,
            Adx14Bar1 = adx14Bar1,
            Atr14Bar1 = atr14Bar1,
            SmaAtr100Bar1 = smaAtr100Bar1,
            Ema20Current = ema20Current,
            Atr14Current = atr14Current,
            IsValid = valid
        };
    }

    /// <summary>
    /// Volume baseline (Rule H.*): median tick volume of the same M15 slot over the
    /// previous 20 valid trading days. "Same slot" = same hour:minute-of-day.
    /// </summary>
    public MarketQualitySnapshot BuildQualitySnapshot(double absoluteSpreadCap)
    {
        var m15Bars = _robot.MarketData.GetBars(TimeFrame.Minute15);
        int idx1 = m15Bars.Count - 2;

        if (idx1 < 0)
            return new MarketQualitySnapshot();

        var signalTime = m15Bars.OpenTimes[idx1];
        var slotOfDay = signalTime.TimeOfDay;

        var sameSlotVolumes = new List<double>();
        // Walk back through bars looking for the same time-of-day slot, up to ~45 calendar
        // days back to comfortably find 20 valid trading days even with weekends/holidays.
        for (int i = idx1 - 1; i >= 0 && sameSlotVolumes.Count < VolumeBaselineDays * 3; i--)
        {
            if (m15Bars.OpenTimes[i].TimeOfDay == slotOfDay)
                sameSlotVolumes.Add(m15Bars.TickVolumes[i]);

            if (sameSlotVolumes.Count >= VolumeBaselineDays)
                break;
        }

        double volumeBaseline = sameSlotVolumes.Count > 0 ? Median(sameSlotVolumes) : 0;

        double currentSpread = _robot.Symbol.Spread;

        // Spread baseline uses the same same-slot-median approach as volume, on recent spread.
        // cAlgo does not retain historical spread series by default, so this uses a simple
        // rolling estimate seeded from current spread until a real historical spread feed
        // is wired in. See open-questions in README — this is a known simplification.
        double spreadBaseline = currentSpread;

        return new MarketQualitySnapshot
        {
            TickVolumeBar1 = (long)m15Bars.TickVolumes[idx1],
            VolumeBaseline = volumeBaseline,
            VolumeValidObservations = sameSlotVolumes.Count,
            CurrentSpread = currentSpread,
            SpreadBaseline = spreadBaseline,
            AbsoluteSpreadCap = absoluteSpreadCap
        };
    }

    private Candle[] ExtractCandles(Bars bars, int count)
    {
        var list = new List<Candle>(count);
        for (int i = 1; i <= count; i++)
        {
            int idx = bars.Count - 1 - i;
            if (idx < 0) break;

            list.Add(new Candle
            {
                TimestampUtc = bars.OpenTimes[idx],
                Open = bars.OpenPrices[idx],
                High = bars.HighPrices[idx],
                Low = bars.LowPrices[idx],
                Close = bars.ClosePrices[idx],
                TickVolume = (long)bars.TickVolumes[idx]
            });
        }
        return list.ToArray();
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        if (n == 0) return 0;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static bool AllFinite(params double[] values)
    {
        foreach (var v in values)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                return false;
        }
        return true;
    }
}
