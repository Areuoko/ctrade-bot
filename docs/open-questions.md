# Open questions

> Each unresolved question blocks its affected Rule IDs. No default or guess may be used.
> Status: Open · Resolved.

---

## OQ-001 — AbsoluteSpreadCap value

- **Affected Rules:** I.2
- **Ambiguity:** The reference states `AbsoluteSpreadCap` must be determined per symbol and account from real spread distribution analysis, but no numeric value is provided. It also says "until this cap is determined, real trading is forbidden."
- **Trading impact:** Spread filter cannot be fully evaluated; I.2 is incomplete.
- **Safe fail-closed:** AbsoluteSpreadCap treated as unbounded until resolved → I.2 condition incomplete → no order.
- **Owner:** User
- **Status:** Open

---

## OQ-002 — Indicator calibration tolerance

- **Affected Rules:** D.2, D.3
- **Ambiguity:** §5 states indicator outputs must be calibrated against a reference file of at least 1000 candles "with a predetermined numerical tolerance" but the tolerance value is not specified.
- **Trading impact:** Cannot validate indicator implementation correctness.
- **Safe fail-closed:** Tolerance remains unspecified; implementation must document chosen tolerance for approval.
- **Owner:** User
- **Status:** Open

---

## OQ-003 — Slippage max per symbol

- **Affected Rules:** K.5
- **Ambiguity:** §12.4 says "maximum allowed slippage for entry is defined as an independent parameter per symbol" but no values are provided.
- **Trading impact:** Slippage control cannot enforce limits; rejection/adjustment logic incomplete.
- **Safe fail-closed:** Max slippage treated as 0 → reject any slippage → very conservative.
- **Owner:** User
- **Status:** Open

---

## OQ-004 — Slippage post-fill adjustment details

- **Affected Rules:** K.5
- **Ambiguity:** §12.4 says if already filled and risk is within cap, "SL and TP are re-calculated based on actual fill preserving monetary risk, subject to broker limits." The exact formula for re-calculation and broker limit interaction is not specified.
- **Trading impact:** After an unexpected fill, SL/TP adjustment behavior is ambiguous.
- **Safe fail-closed:** Do not re-calculate; close position immediately if slippage exceeded.
- **Owner:** User
- **Status:** Open

---

## OQ-005 — Break-even model selection

- **Affected Rules:** M.2
- **Ambiguity:** §14.2 describes three break-even models (A, B, C) and says "only the model that shows stable improvement in Walk-Forward and Monte Carlo enters live." Model A (disabled) is default. The exact conditions for promoting B or C are not specified.
- **Trading impact:** No impact in base version (model A is fixed); affects future parameter promotion.
- **Safe fail-closed:** Model A remains default until promotion criteria are defined and met.
- **Owner:** User
- **Status:** Open

---

## OQ-006 — News calendar unavailable behavior

- **Affected Rules:** N.1
- **Ambiguity:** §15.3 says "if the news calendar is not available, fresh, or valid, new entries are forbidden" but does not define what "fresh" means (maximum age, refresh interval, staleness threshold).
- **Trading impact:** Calendar staleness check cannot be implemented precisely.
- **Safe fail-closed:** Any calendar older than 1 hour treated as stale → no order.
- **Owner:** User
- **Status:** Open

---

## OQ-007 — Kill Switch reactivation procedure

- **Affected Rules:** P.6
- **Ambiguity:** §17.3 says reactivation requires "manual review, cause report, and two-step command" but the exact two-step mechanism is not defined.
- **Trading impact:** Kill Switch reactivation process is unspecified; could lead to accidental re-enablement.
- **Safe fail-closed:** Two-step command must be explicitly designed and approved before implementation.
- **Owner:** User
- **Status:** Open

---

## OQ-008 — Reconciliation conflict resolution rules

- **Affected Rules:** Q.1, Q.2
- **Ambiguity:** §18 says "only risk-reducing corrections are allowed" during reconciliation, but the exact rules for which corrections qualify as risk-reducing are not enumerated.
- **Trading impact:** Ambiguity in what auto-corrections are permitted during state mismatch.
- **Safe fail-closed:** No auto-corrections; all mismatches require manual resolution.
- **Owner:** User
- **Status:** Open

---

## OQ-009 — Backtest data source specification

- **Affected Rules:** T.1
- **Ambiguity:** §21.1 says "prefer tick data, fallback to M1 with conservative model" but the conservative M1 model details are not specified.
- **Trading impact:** Backtest accuracy on M1 fallback is undefined.
- **Safe fail-closed:** M1 fallback must be documented and approved before use.
- **Owner:** User
- **Status:** Open

---

## OQ-010 — Volume baseline holiday slot handling

- **Affected Rules:** H.1
- **Ambiguity:** §9 says "days without complete data are excluded" from the 20-day median but does not specify how to handle holiday slots (e.g., if a 15-minute slot had no trading on a holiday, is that slot simply absent or filled with zero?).
- **Trading impact:** Volume baseline calculation could vary depending on holiday handling.
- **Safe fail-closed:** Holiday slots excluded from median calculation (same as incomplete days).
- **Owner:** User
- **Status:** Open

---

## OQ-011 — News window cancellation timing precision

- **Affected Rules:** N.4, N.5
- **Ambiguity:** §15.3 says pending orders cancelled "15 minutes before" news window start and positions closed "15 minutes before" — but does this mean 15 minutes before the window's pre-event buffer (e.g., 15 min before FOMC's 90-min buffer = 105 min before event), or 15 minutes before the event itself?
- **Trading impact:** The actual cancellation/close time could differ by up to 75 minutes for FOMC.
- **Safe fail-closed:** Cancel/close 15 minutes before the event's pre-event buffer start (more conservative).
- **Owner:** User
- **Status:** Open

---

## OQ-012 — SL/TP fill side for pending orders

- **Affected Rules:** T.2
- **Ambiguity:** §21.2 says "SL and TP fill on correct side" but for pending orders (Buy Stop/Sell Stop), it is not explicitly stated which side (Bid vs Ask) triggers SL and TP in the cTrader context.
- **Trading impact:** Backtest fill simulation could be incorrect.
- **Safe fail-closed:** SL/TP triggered on the worse side for the system.
- **Owner:** User
- **Status:** Open

---

## OQ-013 — EMA50[6] lookback in TrendBuy/TrendSell

- **Affected Rules:** E.1, E.2
- **Ambiguity:** §6.1 uses `EMA50[6]` to check slope direction. The index [6] means 6 fully closed H1 candles back. Is this the correct lookback? The reference does not explain why [6] specifically.
- **Trading impact:** Slope check sensitivity depends on the lookback period.
- **Safe fail-closed:** [6] is used as written; no modification without explicit approval.
- **Owner:** User
- **Status:** Open

---

## OQ-014 — Time exit candle count parameterization

- **Affected Rules:** M.3
- **Ambiguity:** §14.3 says 32 M15 candles (≈ 8 hours) is default but should be tested with values 24, 32, and 40. The final production value is not confirmed.
- **Trading impact:** Time exit timing affects trade duration and P/L.
- **Safe fail-closed:** 32 candles as default; parameter to be validated in Walk-Forward.
- **Owner:** User
- **Status:** Open

---

## OQ-015 — SL/TP rounding to Tick Size precision

- **Affected Rules:** J.4, M.1, K.1
- **Ambiguity:** §12.1 specifies `round_up_to_tick` and `round_down_to_tick` for entry prices, and §14.1 says TP/SL "rounded to valid Tick Size." However, the exact rounding functions (ceiling vs floor vs nearest) for SL and TP are not fully specified — only entry is explicit.
- **Trading impact:** SL/TP prices could differ by 1 tick depending on rounding direction.
- **Safe fail-closed:** SL rounded away from price (conservative); TP rounded toward price (conservative). Must be confirmed.
- **Owner:** User
- **Status:** Open

---

## OQ-016 — Rollover window default value

- **Affected Rules:** B.5
- **Ambiguity:** §3.3 says "initial value: 15 minutes before to 15 minutes after broker's announced rollover time" but the rollover time itself is broker-dependent and not fixed.
- **Trading impact:** Rollover blackout window depends on broker configuration.
- **Safe fail-closed:** Rollover time read from broker config; if unavailable → assume worst-case (all hours are rollover-adjacent) → no order.
- **Owner:** User
- **Status:** Open

---

## OQ-017 — Symbol name mapping

- **Affected Rules:** A.1
- **Ambiguity:** §2 says "real broker symbol name like XAUUSD.a must be mapped via settings file" but the mapping mechanism (config file format, key names) is not defined.
- **Trading impact:** Cannot load symbol metadata without mapping.
- **Safe fail-closed:** If mapping fails → SYMBOL_DISABLED.
- **Owner:** User
- **Status:** Open
