using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Signals;

/// <summary>
/// Pure domain evaluator for H1 trend, M15 volatility band, and M15 signal conditions.
/// Rules E.1–E.3, F.1–F.2, G.1–G.3.
/// No state; no cAlgo.API reference.
/// </summary>
public static class SignalEvaluator
{
    private const double TrendAtrThreshold = 0.25;
    private const double AdxMinimum = 20.0;
    private const double PullbackUpperCoeff = 0.10;
    private const double PullbackLowerCoeff = 0.35;
    private const double RsiMidline = 50.0;
    private const double ClvBuyThreshold = 0.65;
    private const double ClvSellThreshold = 0.35;
    private const double BodyAtrCoeff = 0.20;
    private const double VolBandLow = 0.70;
    private const double VolBandHigh = 1.80;

    /// <summary>
    /// E.1/E.2/E.3 — Evaluate H1 trend direction from closed-bar indicators.
    /// Returns Buy, Sell, or None (neutral). NaN or non-positive ATR → neutral.
    /// </summary>
    public static TradeDirection EvaluateH1Trend(
        double ema50Bar1, double ema200Bar1,
        double ema50Bar6, double atr14Bar1)
    {
        if (double.IsNaN(ema50Bar1) || double.IsNaN(ema200Bar1) ||
            double.IsNaN(ema50Bar6) || double.IsNaN(atr14Bar1) ||
            atr14Bar1 <= 0)
            return TradeDirection.None;

        // E.1 — TrendBuy
        if (ema50Bar1 > ema200Bar1
            && ema50Bar1 > ema50Bar6
            && (ema50Bar1 - ema200Bar1) / atr14Bar1 >= TrendAtrThreshold)
            return TradeDirection.Buy;

        // E.2 — TrendSell
        if (ema50Bar1 < ema200Bar1
            && ema50Bar1 < ema50Bar6
            && (ema200Bar1 - ema50Bar1) / atr14Bar1 >= TrendAtrThreshold)
            return TradeDirection.Sell;

        // E.3 — TrendNeutral
        return TradeDirection.None;
    }

    /// <summary>
    /// F.1/F.2 — Evaluate M15 volatility band.
    /// Ratio in [0.70, 1.80] → proceed. Otherwise → rejection.
    /// Denominator zero or NaN → REJECT_DATA_INVALID.
    /// </summary>
    public static SignalResult EvaluateVolatilityBand(
        double atr14Bar1, double smaAtr100Bar1)
    {
        if (double.IsNaN(atr14Bar1) || double.IsNaN(smaAtr100Bar1) || smaAtr100Bar1 <= 0)
            return SignalResult.Rejected(ReasonCode.RejectDataInvalid);

        double ratio = atr14Bar1 / smaAtr100Bar1;

        if (ratio < VolBandLow)
            return SignalResult.Rejected(ReasonCode.RejectLowVol);
        if (ratio > VolBandHigh)
            return SignalResult.Rejected(ReasonCode.RejectHighVol);

        return SignalResult.Of(TradeDirection.None);
    }

    /// <summary>
    /// G.2 — Evaluate M15 buy signal. All 10 conditions must pass.
    /// Returns first failing condition as rejection.
    /// </summary>
    public static SignalResult EvaluateBuySignal(
        TradeDirection h1Trend,
        Candle m15Candle,
        double ema20Bar1, double ema50Bar1,
        double adx14Bar1, double atr14Bar1,
        double rsi14Bar2, double rsi14Bar1)
    {
        // G.1: Range must be positive
        if (m15Candle.Range <= 0)
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // NaN guard — any missing input → no order
        if (double.IsNaN(ema20Bar1) || double.IsNaN(ema50Bar1) ||
            double.IsNaN(adx14Bar1) || double.IsNaN(atr14Bar1) ||
            double.IsNaN(rsi14Bar2) || double.IsNaN(rsi14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectDataInvalid);

        // C1: H1 trend = BUY
        if (h1Trend != TradeDirection.Buy)
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        // C2: EMA20[1] > EMA50[1]
        if (!(ema20Bar1 > ema50Bar1))
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        // C3: ADX14[1] >= 20
        if (!(adx14Bar1 >= AdxMinimum))
            return SignalResult.Rejected(ReasonCode.RejectAdxTooLow);

        // C4: Low[1] <= EMA20[1] + 0.10 × ATR14[1]
        if (!(m15Candle.Low <= ema20Bar1 + PullbackUpperCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C5: Low[1] >= EMA20[1] − 0.35 × ATR14[1]
        if (!(m15Candle.Low >= ema20Bar1 - PullbackLowerCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C6: Close[1] >= EMA20[1]
        if (!(m15Candle.Close >= ema20Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C7: RSI14[2] <= 50
        if (!(rsi14Bar2 <= RsiMidline))
            return SignalResult.Rejected(ReasonCode.RejectRsiCondition);

        // C8: RSI14[1] > 50
        if (!(rsi14Bar1 > RsiMidline))
            return SignalResult.Rejected(ReasonCode.RejectRsiCondition);

        // C9: CLV >= 0.65
        if (!(m15Candle.Clv >= ClvBuyThreshold))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C10: Body >= 0.20 × ATR14[1]
        if (!(m15Candle.Body >= BodyAtrCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        return SignalResult.Of(TradeDirection.Buy);
    }

    /// <summary>
    /// G.3 — Evaluate M15 sell signal. All 10 conditions must pass.
    /// Returns first failing condition as rejection.
    /// </summary>
    public static SignalResult EvaluateSellSignal(
        TradeDirection h1Trend,
        Candle m15Candle,
        double ema20Bar1, double ema50Bar1,
        double adx14Bar1, double atr14Bar1,
        double rsi14Bar2, double rsi14Bar1)
    {
        // G.1: Range must be positive
        if (m15Candle.Range <= 0)
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // NaN guard — any missing input → no order
        if (double.IsNaN(ema20Bar1) || double.IsNaN(ema50Bar1) ||
            double.IsNaN(adx14Bar1) || double.IsNaN(atr14Bar1) ||
            double.IsNaN(rsi14Bar2) || double.IsNaN(rsi14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectDataInvalid);

        // C1: H1 trend = SELL
        if (h1Trend != TradeDirection.Sell)
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        // C2: EMA20[1] < EMA50[1]
        if (!(ema20Bar1 < ema50Bar1))
            return SignalResult.Rejected(ReasonCode.TrendNeutral);

        // C3: ADX14[1] >= 20
        if (!(adx14Bar1 >= AdxMinimum))
            return SignalResult.Rejected(ReasonCode.RejectAdxTooLow);

        // C4: High[1] >= EMA20[1] − 0.10 × ATR14[1]
        if (!(m15Candle.High >= ema20Bar1 - PullbackUpperCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C5: High[1] <= EMA20[1] + 0.35 × ATR14[1]
        if (!(m15Candle.High <= ema20Bar1 + PullbackLowerCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C6: Close[1] <= EMA20[1]
        if (!(m15Candle.Close <= ema20Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C7: RSI14[2] >= 50
        if (!(rsi14Bar2 >= RsiMidline))
            return SignalResult.Rejected(ReasonCode.RejectRsiCondition);

        // C8: RSI14[1] < 50
        if (!(rsi14Bar1 < RsiMidline))
            return SignalResult.Rejected(ReasonCode.RejectRsiCondition);

        // C9: CLV <= 0.35
        if (!(m15Candle.Clv <= ClvSellThreshold))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        // C10: Body >= 0.20 × ATR14[1]
        if (!(m15Candle.Body >= BodyAtrCoeff * atr14Bar1))
            return SignalResult.Rejected(ReasonCode.RejectSignalInvalid);

        return SignalResult.Of(TradeDirection.Sell);
    }
}
