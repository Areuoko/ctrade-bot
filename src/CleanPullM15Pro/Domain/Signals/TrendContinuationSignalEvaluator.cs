using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Signals;

/// <summary>
/// Pure domain evaluator for the Trend Continuation / Breakout strategy (spec section AB).
/// Independent of <see cref="SignalEvaluator"/> (Pullback) — no shared state, no cAlgo.API
/// reference. Rules AB.2–AB.4. AB.5 (volume) is handled by
/// <see cref="Risk.VolumeFilter"/>'s 3-argument overload; AB.6 (swing/SL/TP/expiry/label/
/// risk%) is handled by existing Pullback calculators plus orchestrator-level config —
/// nothing here duplicates that.
/// </summary>
public static class TrendContinuationSignalEvaluator
{
    /// <summary>AB.2 — K: number of closed M15 candles before the signal candle used for the breakout range.</summary>
    public const int BreakoutLookback = 10;

    /// <summary>AB.5 — Breakout-specific volume baseline multiplier, passed to VolumeFilter.Passes' 3-arg overload.</summary>
    public const double VolumeMultiplier = 1.25;

    private const double AdxMinimum = 25.0;
    private const double ClvBuyThreshold = 0.60;
    private const double ClvSellThreshold = 0.40;
    private const double BodyAtrCoeff = 0.20;
    private const double MaxExtensionAtr = 1.00;

    /// <summary>
    /// AB.2/AB.3 — Evaluate the M15 Breakout buy signal. Caller must supply at least
    /// <see cref="BreakoutLookback"/> + 1 closed candles, index 0 = signal candle ([1] in
    /// spec terms), indices 1..10 = candles [2]..[11] used for the range. Only call this
    /// when the Pullback buy signal (SignalEvaluator.EvaluateBuySignal) was already rejected
    /// for the same candle (AB.1) — this method does not check that itself.
    /// </summary>
    /// <param name="h1Trend">H1 trend result (must be Buy for this signal to fire).</param>
    /// <param name="candles">Closed M15 candles, newest first; index 0 is the signal candle.</param>
    /// <param name="adx14Bar1">M15 ADX(14) at the signal candle.</param>
    /// <param name="atr14Bar1">M15 ATR(14) at the signal candle.</param>
    /// <returns>Buy on success, or a rejection with the first failing AB.2/AB.3 condition.</returns>
    public static SignalResult EvaluateBuySignal(
        TradeDirection h1Trend, Candle[] candles, double adx14Bar1, double atr14Bar1)
    {
        if (candles is null || candles.Length < BreakoutLookback + 1)
            return SignalResult.Rejected(ReasonCode.RejectBreakoutRangeInvalid);

        if (double.IsNaN(adx14Bar1) || double.IsNaN(atr14Bar1) || atr14Bar1 <= 0)
            return SignalResult.Rejected(ReasonCode.RejectDataInvalid);

        if (h1Trend != TradeDirection.Buy)
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        var signalCandle = candles[0];
        if (signalCandle.Range <= 0)
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // AB.2 — BreakoutHigh = max(High[2..K+1]) = max(candles[1..K])
        double breakoutHigh = double.MinValue;
        for (int i = 1; i <= BreakoutLookback; i++)
            breakoutHigh = Math.Max(breakoutHigh, candles[i].High);

        if (!(signalCandle.Close > breakoutHigh))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutTriggerNotMet);

        // AB.3 — supplementary conditions
        if (!(adx14Bar1 >= AdxMinimum))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutAdxTooLow);

        if (!(signalCandle.Clv >= ClvBuyThreshold))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutClv);

        if (!(signalCandle.Body >= BodyAtrCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        return SignalResult.Of(TradeDirection.Buy);
    }

    /// <summary>
    /// AB.2/AB.3 — Evaluate the M15 Breakout sell signal. Mirror of
    /// <see cref="EvaluateBuySignal"/>; see its documentation for candle-array conventions.
    /// </summary>
    /// <param name="h1Trend">H1 trend result (must be Sell for this signal to fire).</param>
    /// <param name="candles">Closed M15 candles, newest first; index 0 is the signal candle.</param>
    /// <param name="adx14Bar1">M15 ADX(14) at the signal candle.</param>
    /// <param name="atr14Bar1">M15 ATR(14) at the signal candle.</param>
    /// <returns>Sell on success, or a rejection with the first failing AB.2/AB.3 condition.</returns>
    public static SignalResult EvaluateSellSignal(
        TradeDirection h1Trend, Candle[] candles, double adx14Bar1, double atr14Bar1)
    {
        if (candles is null || candles.Length < BreakoutLookback + 1)
            return SignalResult.Rejected(ReasonCode.RejectBreakoutRangeInvalid);

        if (double.IsNaN(adx14Bar1) || double.IsNaN(atr14Bar1) || atr14Bar1 <= 0)
            return SignalResult.Rejected(ReasonCode.RejectDataInvalid);

        if (h1Trend != TradeDirection.Sell)
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        var signalCandle = candles[0];
        if (signalCandle.Range <= 0)
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // AB.2 — BreakoutLow = min(Low[2..K+1]) = min(candles[1..K])
        double breakoutLow = double.MaxValue;
        for (int i = 1; i <= BreakoutLookback; i++)
            breakoutLow = Math.Min(breakoutLow, candles[i].Low);

        if (!(signalCandle.Close < breakoutLow))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutTriggerNotMet);

        // AB.3 — supplementary conditions
        if (!(adx14Bar1 >= AdxMinimum))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutAdxTooLow);

        if (!(signalCandle.Clv <= ClvSellThreshold))
            return SignalResult.Rejected(ReasonCode.RejectBreakoutClv);

        if (!(signalCandle.Body >= BodyAtrCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        return SignalResult.Of(TradeDirection.Sell);
    }

    /// <summary>
    /// AB.4 — Extension filter. MUST be called with <paramref name="entryPrice"/> already
    /// computed via <c>OrderEntryCalculator.ComputeBuyEntry</c>/<c>ComputeSellEntry</c> —
    /// never with Close[1] — because the entry price already includes the K.1 buffer beyond
    /// the signal candle's high/low. Evaluation order is therefore: compute EntryPrice first,
    /// then call this — the reverse of the Pullback ordering in section G.
    /// </summary>
    /// <param name="entryPrice">Computed pending-order entry price (K.1).</param>
    /// <param name="ema20Bar1">M15 EMA(20) at the signal candle.</param>
    /// <param name="atr14Bar1">M15 ATR(14) at the signal candle.</param>
    /// <returns>Null if within the allowed extension; otherwise <see cref="ReasonCode.RejectBreakoutExtension"/> (or RejectDataInvalid on bad input).</returns>
    public static ReasonCode? ValidateExtension(double entryPrice, double ema20Bar1, double atr14Bar1)
    {
        if (double.IsNaN(entryPrice) || double.IsNaN(ema20Bar1) || double.IsNaN(atr14Bar1) || atr14Bar1 <= 0)
            return ReasonCode.RejectDataInvalid;

        double extensionAtr = Math.Abs(entryPrice - ema20Bar1) / atr14Bar1;
        return extensionAtr <= MaxExtensionAtr ? null : ReasonCode.RejectBreakoutExtension;
    }
}
