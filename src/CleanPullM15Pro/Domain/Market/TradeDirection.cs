namespace CleanPullM15Pro.Domain.Market;

/// <summary>
/// Trade direction. None means no position or signal.
/// </summary>
public enum TradeDirection
{
    /// <summary>No position or signal.</summary>
    None = 0,

    /// <summary>Buy (long) direction.</summary>
    Buy = 1,

    /// <summary>Sell (short) direction.</summary>
    Sell = 2
}
