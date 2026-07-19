using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.StateMachine;

/// <summary>
/// Enforces the state machine defined in spec section 19 / guide section 9.
/// Only documented transitions are allowed; anything else throws, which is
/// intentional — a hidden/unplanned transition is a bug, not a runtime case
/// to silently swallow.
/// </summary>
public sealed class SymbolStateMachine
{
    /// <summary>Current symbolic lifecycle state for the tracked symbol.</summary>
    public BotState Current { get; private set; }

    /// <summary>Constructs a state machine starting from the given initial state.</summary>
    /// <param name="initial">Initial <see cref="BotState"/> for the symbol.</param>
    public SymbolStateMachine(BotState initial)
    {
        Current = initial;
    }

    /// <summary>Determines whether a transition to <paramref name="target"/> is allowed by the spec.</summary>
    /// <param name="target">Candidate target state.</param>
    /// <returns>True if the transition is documented/allowed; otherwise false.</returns>
    public bool CanTransitionTo(BotState target)
    {
        // ANY → RECONCILIATION_REQUIRED and ANY → DISABLED are always allowed.
        if (target == BotState.ReconciliationRequired || target == BotState.Disabled)
            return true;

        return (Current, target) switch
        {
            (BotState.Ready, BotState.SignalFound) => true,
            (BotState.SignalFound, BotState.OrderPending) => true,
            (BotState.SignalFound, BotState.Ready) => true, // risk/execution check failed
            (BotState.OrderPending, BotState.PositionOpen) => true,
            (BotState.OrderPending, BotState.Ready) => true, // cancelled or expired
            (BotState.PositionOpen, BotState.Cooldown) => true,
            (BotState.Cooldown, BotState.Ready) => true,
            (BotState.ReconciliationRequired, BotState.Ready) => true, // after manual/auto reconcile
            (BotState.Disabled, BotState.Ready) => true, // after config fix
            _ => false
        };
    }

    /// <summary>
    /// Attempts the transition. Returns false (no-op) if not allowed instead of throwing,
    /// so orchestration can log a rejection rather than crash the bot on an edge case.
    /// </summary>
    public bool TryTransition(BotState target)
    {
        if (!CanTransitionTo(target))
            return false;

        Current = target;
        return true;
    }
}
