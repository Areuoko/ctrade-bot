using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Structured logging port. Every evaluated bar must be logged, even with no signal
/// (Rule spec section 27). Implemented via cBot's Print/File logging in Infrastructure.
/// </summary>
public interface ILogPort
{
    /// <summary>Logs a per-bar decision outcome (submitted, no-signal, or rejected).</summary>
    /// <param name="symbolName">Symbol being evaluated.</param>
    /// <param name="direction">Trade direction for the decision.</param>
    /// <param name="reason">Rejection reason code, if rejected; otherwise null.</param>
    /// <param name="details">Human-readable details about the decision.</param>
    void LogDecision(string symbolName, TradeDirection direction, ReasonCode? reason, string details);

    /// <summary>Logs a rejection event with the given reason and details.</summary>
    /// <param name="symbolName">Symbol being evaluated.</param>
    /// <param name="reason">Rejection reason code.</param>
    /// <param name="details">Human-readable rejection details.</param>
    void LogRejection(string symbolName, ReasonCode reason, string details);

    /// <summary>Logs an error message for the given symbol.</summary>
    /// <param name="symbolName">Symbol associated with the error.</param>
    /// <param name="message">Human-readable error message.</param>
    void LogError(string symbolName, string message);
}
