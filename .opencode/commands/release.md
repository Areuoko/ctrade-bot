---
description: Review, fix, rebuild and publish the current algo artifact
agent: release-manager
---
Run the full release gate. Audit against specification and traceability; check look-ahead, current-bar use, units, rounding, fail-open risk, state mismatch, API misuse, dead code, and scope. Apply only minimal certain fixes and re-audit, maximum three cycles. Run restore and Release Build. Stop on any failed gate or ambiguity. Only then copy exactly one current `.algo` to `artifacts/release/`, generate SHA256, and write a short report. Never publish stale output.
