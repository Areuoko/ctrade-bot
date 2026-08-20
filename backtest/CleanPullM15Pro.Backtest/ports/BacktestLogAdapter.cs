using System;
using System.Collections.Generic;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Backtest.Ports;

/// <summary>
/// In-memory log port: tallies rejection-reason counts (directly comparable to the
/// REJECT-line breakdown already extracted from the GUI backtest's log.txt/log.md —
/// this is the cross-check spec section 26 phase 2 asks for) instead of printing to
/// cTrader's Log tab. Set <see cref="Verbose"/> to also echo lines to the console.
///
/// TRACE MODE: <see cref="ILogPort"/> doesn't carry a bar timestamp, so per-bar
/// tracing needs an ambient "current bar" set externally right before each
/// <c>BarEvaluationOrchestrator.Evaluate</c> call — see <see cref="CurrentBarTimeUtc"/>,
/// set by <c>ReplayEngine.Run</c>. When <see cref="TraceTimestamps"/> is non-null and
/// contains the current bar, the full reason/details line is captured into
/// <see cref="TraceLog"/> regardless of <see cref="Verbose"/>, so a caller (e.g.
/// Program.cs) can pinpoint exactly what happened to specific known-candidate bars
/// (identified up front via a diagnostics pass) without re-implementing the
/// orchestrator's decision logic.
/// </summary>
public sealed class BacktestLogAdapter : ILogPort
{
    /// <summary>Count of REJECT outcomes per ReasonCode across the whole run.</summary>
    public Dictionary<ReasonCode, int> RejectionCounts { get; } = new();

    /// <summary>Count of successfully submitted orders, split by direction.</summary>
    public Dictionary<TradeDirection, int> SubmittedCounts { get; } = new();

    /// <summary>Non-fatal error messages logged during the run.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>When true, also writes each decision/rejection/error line to the console — verbose, only useful for debugging a short date range.</summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// The bar close time (UTC) currently being evaluated. Set by <c>ReplayEngine.Run</c>
    /// immediately before each <c>BarEvaluationOrchestrator.Evaluate</c> call, so trace
    /// entries can be tagged with the bar they belong to even though <see cref="ILogPort"/>
    /// itself doesn't pass a timestamp.
    /// </summary>
    public DateTime CurrentBarTimeUtc { get; set; }

    /// <summary>
    /// When set, any decision or rejection logged while <see cref="CurrentBarTimeUtc"/>
    /// matches one of these timestamps is captured (in full, including the Details text)
    /// into <see cref="TraceLog"/>. Null (default) disables tracing entirely — no per-bar
    /// timestamp bookkeeping cost for normal runs.
    /// </summary>
    public HashSet<DateTime>? TraceTimestamps { get; set; }

    /// <summary>Captured trace lines for bars matching <see cref="TraceTimestamps"/>, in evaluation order.</summary>
    public List<string> TraceLog { get; } = new();

    /// <inheritdoc />
    public void LogDecision(string symbolName, TradeDirection direction, ReasonCode? reason, string details)
    {
        if (reason is null && direction != TradeDirection.None)
        {
            SubmittedCounts.TryGetValue(direction, out var count);
            SubmittedCounts[direction] = count + 1;
        }

        if (Verbose)
            Console.WriteLine($"[DECISION] dir={direction} reason={reason} — {details}");

        if (TraceTimestamps is not null && TraceTimestamps.Contains(CurrentBarTimeUtc))
            TraceLog.Add($"[{CurrentBarTimeUtc:yyyy-MM-dd HH:mm}] DECISION dir={direction} reason={reason} — {details}");
    }

    /// <inheritdoc />
    public void LogRejection(string symbolName, ReasonCode reason, string details)
    {
        RejectionCounts.TryGetValue(reason, out var count);
        RejectionCounts[reason] = count + 1;

        if (Verbose)
            Console.WriteLine($"[REJECT] {reason} — {details}");

        if (TraceTimestamps is not null && TraceTimestamps.Contains(CurrentBarTimeUtc))
            TraceLog.Add($"[{CurrentBarTimeUtc:yyyy-MM-dd HH:mm}] REJECT {reason} — {details}");
    }

    /// <inheritdoc />
    public void LogError(string symbolName, string message)
    {
        Errors.Add(message);
        Console.WriteLine("[ERROR] " + message);

        if (TraceTimestamps is not null && TraceTimestamps.Contains(CurrentBarTimeUtc))
            TraceLog.Add($"[{CurrentBarTimeUtc:yyyy-MM-dd HH:mm}] ERROR — {message}");
    }
}
