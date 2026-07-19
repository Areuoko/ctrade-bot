using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Contracts;

/// <summary>
/// Everything the orchestrator needs from the H1 timeframe for one evaluation.
/// All values are pre-computed by Infrastructure from fully closed bars only.
/// Index convention matches spec: Bar1 = [1] (last closed), Bar6 = [6].
/// </summary>
public sealed class H1Snapshot
{
    /// <summary>H1 EMA(50) evaluated at the last closed bar ([1]).</summary>
    public double Ema50Bar1 { get; init; }
    /// <summary>H1 EMA(200) evaluated at the last closed bar ([1]).</summary>
    public double Ema200Bar1 { get; init; }
    /// <summary>H1 EMA(50) evaluated at bar [6], for the slope/alignment trend filter.</summary>
    public double Ema50Bar6 { get; init; }
    /// <summary>H1 ATR(14) evaluated at the last closed bar ([1]), used for volatility context.</summary>
    public double Atr14Bar1 { get; init; }

    /// <summary>True if H1 data passed warm-up and quality checks (Rule D.*).</summary>
    public bool IsValid { get; init; }
}

/// <summary>
/// Everything the orchestrator needs from the M15 timeframe for one evaluation.
/// </summary>
public sealed class M15Snapshot
{
    /// <summary>
    /// Closed M15 candles, newest first. Index 0 = signal candle ([1] in spec).
    /// Must contain at least enough history for swing lookback (20) + confirmation (2).
    /// </summary>
    public Candle[] Candles { get; init; } = System.Array.Empty<Candle>();

    /// <summary>M15 EMA(20) evaluated at the last closed bar ([1]).</summary>
    public double Ema20Bar1 { get; init; }
    /// <summary>M15 EMA(50) evaluated at the last closed bar ([1]).</summary>
    public double Ema50Bar1 { get; init; }
    /// <summary>M15 RSI(14) evaluated at the last closed bar ([1]).</summary>
    public double Rsi14Bar1 { get; init; }
    /// <summary>M15 RSI(14) evaluated at bar [2], for pullback/turn confirmation.</summary>
    public double Rsi14Bar2 { get; init; }
    /// <summary>M15 ADX(14) evaluated at the last closed bar ([1]), for trend strength.</summary>
    public double Adx14Bar1 { get; init; }
    /// <summary>M15 ATR(14) evaluated at the last closed bar ([1]), the sizing/reference ATR.</summary>
    public double Atr14Bar1 { get; init; }

    /// <summary>SMA(ATR14, 100) evaluated at [1], for the volatility ratio (Rule F.1).</summary>
    public double SmaAtr100Bar1 { get; init; }

    /// <summary>Live (current, non-closed-bar) EMA20 for trigger-time distance check (Rule K.4).</summary>
    public double Ema20Current { get; init; }

    /// <summary>Live (current) ATR14 for trigger-time distance check (Rule K.4).</summary>
    public double Atr14Current { get; init; }

    /// <summary>True if M15 data passed warm-up and quality checks (Rule D.*).</summary>
    public bool IsValid { get; init; }
}

/// <summary>
/// Volume and spread baselines. Computed externally (Infrastructure) per Rules H.1, I.1.
/// </summary>
public sealed class MarketQualitySnapshot
{
    /// <summary>Tick volume of the last closed bar ([1]), for the volume filter (Rule H.1).</summary>
    public long TickVolumeBar1 { get; init; }
    /// <summary>Baseline (e.g. median/SMA) tick volume used to gauge the current bar (Rule H.1).</summary>
    public double VolumeBaseline { get; init; }
    /// <summary>Count of valid volume observations contributing to the baseline.</summary>
    public int VolumeValidObservations { get; init; }

    /// <summary>Current symbol spread in pips, for the spread filter (Rule I.1).</summary>
    public double CurrentSpread { get; init; }
    /// <summary>Baseline spread used to gauge the current spread (Rule I.1).</summary>
    public double SpreadBaseline { get; init; }
    /// <summary>Absolute spread cap above which entries are rejected (Rule I.1).</summary>
    public double AbsoluteSpreadCap { get; init; }
}

/// <summary>
/// Account-level figures needed for risk and sizing (Rules L.*, O.*, P.*).
/// </summary>
public sealed class AccountSnapshot
{
    /// <summary>Current account equity.</summary>
    public double Equity { get; init; }
    /// <summary>Free (available) margin at the moment of evaluation.</summary>
    public double FreeMargin { get; init; }
    /// <summary>Account leverage used in margin/risk checks.</summary>
    public double Leverage { get; init; } = 1;
    /// <summary>Equity recorded at the start of the trading day, for daily drawdown guard (Rule L.*).</summary>
    public double DailyStartEquity { get; init; }
    /// <summary>Equity recorded at the start of the trading week, for weekly drawdown guard (Rule L.*).</summary>
    public double WeeklyStartEquity { get; init; }
    /// <summary>High-water mark of equity, for trailing/profit lock checks.</summary>
    public double EquityHighWaterMark { get; init; }
    /// <summary>Number of positions filled (entered) today, for the daily entry cap.</summary>
    public int FilledEntriesToday { get; init; }
    /// <summary>Consecutive loss count, for the loss-streak kill switch (Rule L.*).</summary>
    public int ConsecutiveLossCount { get; init; }
    /// <summary>Total reserved (open) risk across all positions, for portfolio risk guard (Rule O.*).</summary>
    public double TotalReservedRisk { get; init; }
}
