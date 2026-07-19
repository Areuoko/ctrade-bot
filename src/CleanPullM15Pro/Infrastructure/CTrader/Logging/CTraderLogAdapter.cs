using cAlgo.API;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.Logging;

/// <summary>
/// Implements ILogPort using cAlgo's Robot.Print (visible in the cBot log tab).
/// Spec section 27 requires every evaluated bar to be logged, even with no signal.
/// </summary>
public sealed class CTraderLogAdapter : ILogPort
{
    private readonly Robot _robot;

    /// <summary>Creates the log adapter bound to the host cBot's Print output.</summary>
    /// <param name="robot">The cBot whose log tab receives the printed lines.</param>
    public CTraderLogAdapter(Robot robot)
    {
        _robot = robot;
    }

    /// <summary>Logs an evaluated-bar decision line (emitted for every bar, even with no signal — spec §27).</summary>
    /// <param name="symbolName">Symbol the decision applies to.</param>
    /// <param name="direction">Signal direction, or <see cref="TradeDirection.None"/>.</param>
    /// <param name="reason">Rejection reason, or null when no rejection.</param>
    /// <param name="details">Free-text decision details.</param>
    public void LogDecision(string symbolName, TradeDirection direction, ReasonCode? reason, string details)
        => _robot.Print("[{0}] [{1}] DECISION dir={2} reason={3} — {4}",
            _robot.Server.TimeInUtc.ToString("yyyy-MM-dd HH:mm:ss"), symbolName, direction, reason, details);

    /// <summary>Logs a rejection line tagged with its stable reason code.</summary>
    /// <param name="symbolName">Symbol that was rejected.</param>
    /// <param name="reason">Stable reason code for the rejection.</param>
    /// <param name="details">Free-text rejection details.</param>
    public void LogRejection(string symbolName, ReasonCode reason, string details)
        => _robot.Print("[{0}] [{1}] REJECT {2} — {3}",
            _robot.Server.TimeInUtc.ToString("yyyy-MM-dd HH:mm:ss"), symbolName, reason, details);

    /// <summary>Logs an error line for a non-recoverable condition.</summary>
    /// <param name="symbolName">Symbol the error concerns.</param>
    /// <param name="message">Error message.</param>
    public void LogError(string symbolName, string message)
        => _robot.Print("[{0}] [{1}] ERROR — {2}",
            _robot.Server.TimeInUtc.ToString("yyyy-MM-dd HH:mm:ss"), symbolName, message);
}
