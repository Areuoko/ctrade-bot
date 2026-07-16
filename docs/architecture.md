# Architecture

`Host -> Application -> Domain` and `Infrastructure/cTrader -> Application Ports`.

- Domain: pure market, indicator, trend, signal, risk, order, and position rules.
- Application: orchestration, state machine, and ports.
- Infrastructure: cTrader market data, execution, symbols, clock, and logging.
- Host: Robot lifecycle adapter only.

Target folders: `src/CleanPullM15Pro/{Host,Application,Domain,Infrastructure/cTrader}` and `tests/CleanPullM15Pro.Tests`.
Domain must not reference cAlgo.API. Invalid data and reconciliation mismatch block new orders.
