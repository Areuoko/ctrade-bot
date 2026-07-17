using System;

namespace CleanPullM15Pro.Domain.Market;

/// <summary>
/// Single OHLCV candle. Rule G.1 input.
/// </summary>
public readonly record struct Candle
{
    /// <summary>Candle open timestamp in UTC.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Open price.</summary>
    public double Open { get; init; }

    /// <summary>High price.</summary>
    public double High { get; init; }

    /// <summary>Low price.</summary>
    public double Low { get; init; }

    /// <summary>Close price.</summary>
    public double Close { get; init; }

    /// <summary>Tick volume for this candle.</summary>
    public long TickVolume { get; init; }

    /// <summary>
    /// Range = High − Low. Rule G.1.
    /// </summary>
    public double Range => High - Low;

    /// <summary>
    /// Body = |Close − Open|. Rule G.1.
    /// </summary>
    public double Body => Math.Abs(Close - Open);

    /// <summary>
    /// CLV = (Close − Low) / Range. Rule G.1.
    /// Returns 0 when Range is zero.
    /// </summary>
    public double Clv => Range > 0 ? (Close - Low) / Range : 0.0;
}
