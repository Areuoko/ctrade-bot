# CleanPull M15 Pro project rules
Read README.md and docs/guide-fa.md before changes.

- Implement only requested approved Rule IDs.
- Never invent thresholds, fallbacks, parameters, or features.
- Do not add a file, type, interface, helper, or dependency without a current consumer.
- Do not refactor unrelated code or write future-only abstractions.
- Domain must not reference cAlgo.API; Infrastructure implements cTrader adapters.
- Use fully closed candles unless the approved specification explicitly says otherwise.
- Invalid, incomplete, or ambiguous input must fail closed and create no new order.
- Every rejection and state transition must have a stable ReasonCode.
- No dead code, executable TODO, production mock, empty catch, or silent risk clamping.

Before editing, state the goal, Rule IDs, and minimum files to touch. After editing, review the diff, run the relevant Build, fix scoped errors, review again, and report files, commands, Rule IDs, and blockers.

A failed mandatory gate stops release. Never present a stale artifact as a current Build.
