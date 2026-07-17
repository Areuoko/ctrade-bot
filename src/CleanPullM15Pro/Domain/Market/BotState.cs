namespace CleanPullM15Pro.Domain.Market;

/// <summary>
/// Per-symbol bot state. Rule R.1.
/// </summary>
public enum BotState
{
    /// <summary>Bot disabled for this symbol.</summary>
    Disabled = 0,

    /// <summary>Ready to evaluate signals.</summary>
    Ready = 1,

    /// <summary>Signal found, pending risk and execution checks.</summary>
    SignalFound = 2,

    /// <summary>Order submitted to broker, awaiting confirmation.</summary>
    OrderPending = 3,

    /// <summary>Position open and managed by broker.</summary>
    PositionOpen = 4,

    /// <summary>Position closed, waiting for next M15 candle.</summary>
    Cooldown = 5,

    /// <summary>State mismatch with broker detected.</summary>
    ReconciliationRequired = 6
}
