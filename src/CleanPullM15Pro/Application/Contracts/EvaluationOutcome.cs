using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Contracts;

/// <summary>
/// Result of one full bar-close evaluation cycle. Always produced, even on
/// rejection or "no signal" — spec section 27 requires every evaluated bar
/// to be logged.
/// </summary>
public sealed class EvaluationOutcome
{
    /// <summary>True if a pending order was submitted for this bar.</summary>
    public bool OrderSubmitted { get; init; }
    /// <summary>Trade direction evaluated/submitted for this bar.</summary>
    public TradeDirection Direction { get; init; }
    /// <summary>Rejection reason code, if the evaluation was rejected.</summary>
    public ReasonCode? Reason { get; init; }
    /// <summary>Human-readable details explaining the outcome.</summary>
    public string Details { get; init; } = string.Empty;
    /// <summary>The submitted order, if one was placed; otherwise null.</summary>
    public PendingOrderRequest? SubmittedOrder { get; init; }

    /// <summary>Builds a rejected evaluation outcome with the given reason and details.</summary>
    /// <param name="reason">Reason code for the rejection.</param>
    /// <param name="details">Optional human-readable rejection details.</param>
    /// <returns>A rejected <see cref="EvaluationOutcome"/>.</returns>
    public static EvaluationOutcome Rejected(ReasonCode reason, string details = "") => new()
    {
        OrderSubmitted = false,
        Direction = TradeDirection.None,
        Reason = reason,
        Details = details
    };

    /// <summary>Builds a "no signal" outcome when no setup was detected for the bar.</summary>
    /// <param name="details">Optional human-readable details.</param>
    /// <returns>A no-signal <see cref="EvaluationOutcome"/>.</returns>
    public static EvaluationOutcome NoSignal(string details = "") => new()
    {
        OrderSubmitted = false,
        Direction = TradeDirection.None,
        Reason = null,
        Details = details
    };

    /// <summary>Builds a submitted-order outcome for the given direction and order.</summary>
    /// <param name="direction">Direction of the submitted order.</param>
    /// <param name="order">The submitted pending-order request.</param>
    /// <returns>A submitted <see cref="EvaluationOutcome"/>.</returns>
    public static EvaluationOutcome Submitted(TradeDirection direction, PendingOrderRequest order) => new()
    {
        OrderSubmitted = true,
        Direction = direction,
        Reason = null,
        SubmittedOrder = order,
        Details = "Order submitted"
    };
}
