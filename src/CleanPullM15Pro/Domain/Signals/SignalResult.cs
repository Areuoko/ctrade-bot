using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Signals;

/// <summary>
/// Signal evaluation outcome. Direction = None means no signal; ReasonCode explains why.
/// Consumed by SignalEvaluator and downstream application layer.
/// </summary>
public readonly record struct SignalResult
{
    /// <summary>Signal direction. None = no signal.</summary>
    public TradeDirection Direction { get; init; }

    /// <summary>Rejection code. None when signal is valid.</summary>
    public ReasonCode? RejectionReason { get; init; }

    /// <summary>Creates a valid signal result.</summary>
    public static SignalResult Of(TradeDirection direction) => new()
    {
        Direction = direction,
        RejectionReason = null
    };

    /// <summary>Creates a rejection result (Direction = None).</summary>
    public static SignalResult Rejected(ReasonCode reason) => new()
    {
        Direction = TradeDirection.None,
        RejectionReason = reason
    };
}
