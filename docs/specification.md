# Specification — CleanPull M15 Pro v2.0

> Atomic rules transcribed from `docs/reference/cleanpull_m15_pro_v2_0.md`.
> No rule may be implemented until its approval status changes to `Approved`.

## Legend

| Field | Description |
|---|---|
| **Inputs** | All inputs with units |
| **Condition** | Exact boolean formula or transition |
| **TF** | Primary timeframe |
| **Candle** | Candle index used (0 = current, 1 = last closed, etc.) |
| **Rounding** | How numeric values are rounded |
| **Error** | Behavior on invalid/incomplete data |
| **ReasonCode** | Stable rejection code |
| **Source** | Section in `cleanpull_m15_pro_v2_0.md` |
| **Status** | Proposed · Approved · Implemented · Verified · Blocked |

---

## A — Symbol and Contract

### A.1 — SYMBOL_DISABLED

- **Inputs:** Symbol metadata from broker (Tick Size, Tick Value, Point, Contract Size, Min Lot, Max Lot, Lot Step, Stop Level, Freeze Level, Commission, Swap, Trading Hours)
- **Condition:** Any required metadata field missing or invalid → symbol disabled
- **TF:** On bot start
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Symbol marked disabled; no order on that symbol
- **ReasonCode:** `SYMBOL_DISABLED`
- **Source:** §2
- **Status:** Proposed

---

## B — Time and Calendar

### B.1 — InternalTimeUTC

- **Inputs:** Server time, local clock
- **Condition:** All internal timestamps stored as UTC; IANA time zones used for session calculation (Europe/London, America/New_York)
- **TF:** Always
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Fixed offset from UTC is forbidden; IANA zones mandatory
- **ReasonCode:** `TIMEZONE_INVALID`
- **Source:** §3.1
- **Status:** Proposed

### B.2 — EntryWindow

- **Inputs:** Current UTC time converted to IANA zone
- **Condition:** `London 08:00–11:00` OR `NewYork 08:30–11:30` (local times)
- **TF:** M15
- **Candle:** Trigger time of pending order
- **Rounding:** N/A
- **Error:** Outside window → no new order
- **ReasonCode:** `REJECT_OUTSIDE_WINDOW`
- **Source:** §3.2
- **Status:** Proposed

### B.3 — FridayCutoff

- **Inputs:** Current UTC time in NewYork zone
- **Condition:** `NewYork >= Friday 12:00` → no new order; all pending cancelled
- **TF:** M15
- **Candle:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_FRIDAY_CUTOFF`
- **Source:** §3.3
- **Status:** Proposed

### B.4 — FridayCloseAll

- **Inputs:** Current UTC time in NewYork zone
- **Condition:** `NewYork >= Friday 15:45` → all open positions force-closed
- **TF:** M15
- **Candle:** N/A
- **Error:** N/A
- **ReasonCode:** `FRIDAY_FORCE_CLOSE`
- **Source:** §3.3
- **Status:** Proposed

### B.5 — RolloverBlackout

- **Inputs:** Broker rollover time, rollover window (default: −15 min to +15 min)
- **Condition:** Within rollover window → no new order, no modification, no activation of pending
- **TF:** M15
- **Candle:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_ROLLOVER`
- **Source:** §3.3
- **Status:** Proposed

---

## C — Data and Timeframe Alignment

### C.1 — DataQualityGate

- **Inputs:** M15 and H1 candle arrays
- **Condition:** All of: candles present, time order valid, OHLC consistent, price staleness ≤ 10 s, clock drift ≤ 2 s, indicator warmup complete
- **TF:** M15 + H1
- **Candle:** Latest closed M15 and H1
- **Rounding:** N/A
- **Error:** Any violation → no new order
- **ReasonCode:** `REJECT_DATA_INVALID`
- **Source:** §4
- **Status:** Proposed

### C.2 — WarmupMinimum

- **Inputs:** Historical candle count per timeframe
- **Condition:** H1 ≥ 300 candles AND M15 ≥ 500 candles loaded before any evaluation
- **TF:** H1, M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Insufficient → no new order
- **ReasonCode:** `REJECT_WARMUP`
- **Source:** §4
- **Status:** Proposed

---

## D — Indicators

### D.1 — ClosePriceBasis

- **Inputs:** OHLC of each candle
- **Condition:** All indicator calculations use Close price unless explicitly stated otherwise
- **TF:** H1, M15
- **Candle:** As specified per indicator
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §5
- **Status:** Proposed

### D.2 — H1_Indicators

- **Inputs:** H1 Close prices
- **Condition:** Compute EMA(50), EMA(200), ATR(14) with Wilder smoothing
- **TF:** H1
- **Candle:** [1] (last fully closed)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §5
- **Status:** Proposed

### D.3 — M15_Indicators

- **Inputs:** M15 Close prices
- **Condition:** Compute EMA(20), EMA(50), RSI(14) Wilder, ADX(14) Wilder, ATR(14) Wilder
- **TF:** M15
- **Candle:** [1] (last fully closed)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §5
- **Status:** Proposed

---

## E — H1 Trend Filter

### E.1 — TrendBuy

- **Inputs:** `EMA50[1], EMA200[1], EMA50[6], ATR14[1]` (all H1)
- **Condition:** `EMA50[1] > EMA200[1]` AND `EMA50[1] > EMA50[6]` AND `(EMA50[1] − EMA200[1]) / ATR14[1] >= 0.25`
- **TF:** H1
- **Candle:** [1] and [6] (both closed)
- **Rounding:** Division by ATR; result compared to 0.25
- **Error:** Any input missing/NaN → neutral
- **ReasonCode:** N/A (neutral is not rejection)
- **Source:** §6.1
- **Status:** Approved

### E.2 — TrendSell

- **Inputs:** `EMA50[1], EMA200[1], EMA50[6], ATR14[1]` (all H1)
- **Condition:** `EMA50[1] < EMA200[1]` AND `EMA50[1] < EMA50[6]` AND `(EMA200[1] − EMA50[1]) / ATR14[1] >= 0.25`
- **TF:** H1
- **Candle:** [1] and [6]
- **Rounding:** Division by ATR; result compared to 0.25
- **Error:** Any input missing/NaN → neutral
- **ReasonCode:** N/A
- **Source:** §6.2
- **Status:** Approved

### E.3 — TrendNeutral

- **Inputs:** Result of E.1 and E.2
- **Condition:** Neither E.1 nor E.2 is TRUE
- **TF:** H1
- **Candle:** [1]
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A (blocks new order by design)
- **Source:** §6
- **Status:** Approved

---

## F — M15 Volatility Regime

### F.1 — VolatilityRatio

- **Inputs:** `ATR14[1]` (M15), `SMA(ATR14, 100)[1]` (M15)
- **Condition:** `VolatilityRatio = ATR14[1] / SMA(ATR14, 100)[1]`
- **TF:** M15
- **Candle:** [1]
- **Rounding:** N/A
- **Error:** Denominator zero or NaN → no order
- **ReasonCode:** `REJECT_DATA_INVALID`
- **Source:** §7
- **Status:** Approved

### F.2 — VolatilityBand

- **Inputs:** VolatilityRatio from F.1
- **Condition:** `< 0.70` → reject; `>= 0.70 AND <= 1.80` → proceed; `> 1.80` → reject
- **TF:** M15
- **Candle:** [1]
- **Rounding:** Boundaries 0.70 and 1.80 are inclusive in the allowed range
- **Error:** N/A
- **ReasonCode:** `REJECT_LOW_VOL` or `REJECT_HIGH_VOL`
- **Source:** §7
- **Status:** Approved

---

## G — Pullback and Signal Candle

### G.1 — SignalDefinitions

- **Inputs:** `High[1], Low[1], Open[1], Close[1]` (M15)
- **Condition:** `Range = High[1] − Low[1]`; `Body = |Close[1] − Open[1]|`; `CLV = (Close[1] − Low[1]) / Range`
- **TF:** M15
- **Candle:** [1]
- **Rounding:** N/A
- **Error:** `Range <= 0` → signal invalid
- **ReasonCode:** `REJECT_SIGNAL_INVALID`
- **Source:** §8
- **Status:** Approved

### G.2 — BuySignal

- **Inputs:** H1 trend = BUY (E.1), `EMA20[1], EMA50[1], ADX14[1], ATR14[1], RSI14[2], RSI14[1], CLV, Body` (all M15)
- **Condition:** All must be TRUE simultaneously:
  1. H1 trend = BUY
  2. `EMA20[1] > EMA50[1]`
  3. `ADX14[1] >= 20`
  4. `Low[1] <= EMA20[1] + 0.10 × ATR14[1]`
  5. `Low[1] >= EMA20[1] − 0.35 × ATR14[1]`
  6. `Close[1] >= EMA20[1]`
  7. `RSI14[2] <= 50`
  8. `RSI14[1] > 50`
  9. `CLV >= 0.65`
  10. `Body >= 0.20 × ATR14[1]`
- **TF:** M15
- **Candle:** [1] and [2] (RSI uses two candles)
- **Rounding:** Thresholds exact as stated
- **Error:** Any input missing/NaN → FALSE (no order)
- **ReasonCode:** First failed condition code (see §20 list)
- **Source:** §8.1
- **Status:** Approved

### G.3 — SellSignal

- **Inputs:** H1 trend = SELL (E.2), `EMA20[1], EMA50[1], ADX14[1], ATR14[1], RSI14[2], RSI14[1], CLV, Body` (all M15)
- **Condition:** All must be TRUE simultaneously:
  1. H1 trend = SELL
  2. `EMA20[1] < EMA50[1]`
  3. `ADX14[1] >= 20`
  4. `High[1] >= EMA20[1] − 0.10 × ATR14[1]`
  5. `High[1] <= EMA20[1] + 0.35 × ATR14[1]`
  6. `Close[1] <= EMA20[1]`
  7. `RSI14[2] >= 50`
  8. `RSI14[1] < 50`
  9. `CLV <= 0.35`
  10. `Body >= 0.20 × ATR14[1]`
- **TF:** M15
- **Candle:** [1] and [2]
- **Rounding:** Thresholds exact as stated
- **Error:** Any input missing/NaN → FALSE (no order)
- **ReasonCode:** First failed condition code
- **Source:** §8.2
- **Status:** Approved

---

## H — Volume Filter

### H.1 — VolumeBaseline

- **Inputs:** M15 tick volumes for same 15-minute slot over previous 20 valid trading days
- **Condition:** `VolumeBaseline = Median(volume of same M15 slot over previous 20 valid trading days)`
- **TF:** M15
- **Candle:** [1] slot
- **Rounding:** Median calculation
- **Error:** Days without complete data excluded; minimum 15 valid observations required; if baseline uncomputable → no order
- **ReasonCode:** `REJECT_VOLUME_BASELINE`
- **Source:** §9
- **Status:** Proposed

### H.2 — VolumeFilter

- **Inputs:** `TickVolume[1]`, `VolumeBaseline` from H.1
- **Condition:** `TickVolume[1] >= 1.10 × VolumeBaseline`
- **TF:** M15
- **Candle:** [1]
- **Rounding:** N/A
- **Error:** Baseline uncomputable → no order
- **ReasonCode:** `REJECT_VOLUME`
- **Source:** §9
- **Status:** Proposed

---

## I — Spread Filter

### I.1 — SpreadBaseline

- **Inputs:** M15 spread for same 15-minute slot over previous 20 trading days
- **Condition:** `SpreadBaseline = Median(spread of same 15-minute slot over previous 20 trading days)`
- **TF:** M15
- **Candle:** [1] slot
- **Rounding:** Median calculation
- **Error:** N/A (baseline must be computable)
- **ReasonCode:** `REJECT_SPREAD_BASELINE`
- **Source:** §10
- **Status:** Proposed

### I.2 — SpreadFilter

- **Inputs:** `CurrentSpread`, `SpreadBaseline` from I.1, `AbsoluteSpreadCap(symbol)` [VALUE MISSING]
- **Condition:** `CurrentSpread <= 1.50 × SpreadBaseline` AND `CurrentSpread <= AbsoluteSpreadCap(symbol)`
- **TF:** M15
- **Candle:** Real-time (checked at trigger)
- **Rounding:** N/A
- **Error:** Baseline uncomputable → no order; cap undefined → no real trading
- **ReasonCode:** `REJECT_SPREAD`
- **Source:** §10
- **Status:** Proposed

### I.3 — SpreadTriggerCancel

- **Inputs:** Pending order, spread at trigger time
- **Condition:** If spread invalid at trigger → order must cancel before fill; if already filled due to delay → position managed normally (SL/TP continue)
- **TF:** M15
- **Candle:** N/A (tick-level)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_SPREAD`
- **Source:** §10
- **Status:** Proposed

---

## J — Swing and Stop Loss

### J.1 — ConfirmedSwingLow

- **Inputs:** `Low[k], Low[k−1], Low[k−2], Low[k+1], Low[k+2]` (M15)
- **Condition:** `Low[k] < Low[k−1]` AND `Low[k] < Low[k−2]` AND `Low[k] <= Low[k+1]` AND `Low[k] <= Low[k+2]`
- **TF:** M15
- **Candle:** k, k±1, k±2 — only fully closed candles; two right-side candles must be closed
- **Rounding:** N/A
- **Error:** Look-ahead forbidden; only confirmed swings
- **ReasonCode:** N/A
- **Source:** §11.1
- **Status:** Proposed

### J.2 — ConfirmedSwingHigh

- **Inputs:** `High[k], High[k−1], High[k−2], High[k+1], High[k+2]` (M15)
- **Condition:** `High[k] > High[k−1]` AND `High[k] > High[k−2]` AND `High[k] >= High[k+1]` AND `High[k] >= High[k+2]`
- **TF:** M15
- **Candle:** k, k±1, k±2
- **Rounding:** N/A
- **Error:** Look-ahead forbidden
- **ReasonCode:** N/A
- **Source:** §11.1
- **Status:** Proposed

### J.3 — SelectSwing

- **Inputs:** Confirmed swings within 20 closed M15 candles before signal candle
- **Condition:** Buy → latest ConfirmedSwingLow in [−20, −1]; Sell → latest ConfirmedSwingHigh in [−20, −1]
- **TF:** M15
- **Candle:** [−20] to [−1]
- **Rounding:** N/A
- **Error:** No valid swing found → reject
- **ReasonCode:** `REJECT_NO_SWING`
- **Source:** §11.2
- **Status:** Proposed

### J.4 — StopLossLevel

- **Inputs:** `SwingLow`, `SwingHigh`, `ATR14[1]` (M15)
- **Condition:** Buy: `SL = SwingLow − 0.15 × ATR14[1]`; Sell: `SL = SwingHigh + 0.15 × ATR14[1]`
- **TF:** M15
- **Candle:** [1]
- **Rounding:** SL must be rounded to valid Tick Size
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §11.3
- **Status:** Proposed

### J.5 — StopDistanceBounds

- **Inputs:** `EntryPrice`, `SL`, `ATR14[1]` (M15), symbol-specific bounds
- **Condition:** `StopDistanceATR = |EntryPrice − SL| / ATR14[1]`; must be within symbol bounds:
  - EURUSD: 0.80–1.80
  - XAUUSD: 1.00–2.20
  - XAGUSD: 1.20–2.60
- **TF:** M15
- **Candle:** [1]
- **Rounding:** N/A
- **Error:** Out of bounds → reject; SL not shifted to fit
- **ReasonCode:** `REJECT_STOP_TOO_WIDE` or `REJECT_STOP_TOO_NARROW`
- **Source:** §11.3
- **Status:** Proposed

### J.6 — StopBrokerLimits

- **Inputs:** `SL`, `StopLevel`, `FreezeLevel` from broker
- **Condition:** SL must respect broker StopLevel and FreezeLevel
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Violation → reject
- **ReasonCode:** `REJECT_STOP_LEVEL`
- **Source:** §11.3
- **Status:** Proposed

---

## K — Order Entry

### K.1 — EntryPrice

- **Inputs:** `High[1]`, `Low[1]`, `ATR14[1]` (M15), `TickSize`
- **Condition:** Buy: `Buffer = max(TickSize, 0.02 × ATR14[1])`; `Entry = round_up_to_tick(High[1] + Buffer)`; Order = Buy Stop. Sell: `Buffer = max(TickSize, 0.02 × ATR14[1])`; `Entry = round_down_to_tick(Low[1] − Buffer)`; Order = Sell Stop.
- **TF:** M15
- **Candle:** [1]
- **Rounding:** `round_up_to_tick` for buy, `round_down_to_tick` for sell
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §12.1
- **Status:** Proposed

### K.2 — OrderExpiry

- **Inputs:** `SignalCloseTime`
- **Condition:** `Expiry = SignalCloseTime + 30 minutes`; order cancelled if not filled by then
- **TF:** M15
- **Candle:** Signal candle + 2 M15 candles
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `EXPIRED`
- **Source:** §12.2
- **Status:** Proposed

### K.3 — PreTriggerValidation

- **Inputs:** Pending order state, market conditions at each tick
- **Condition:** Cancel pending if any of: trading window ended, news window entered, data/connection invalid, VolatilityRatio out of bounds, spread invalid, H1 trend reversed, M15 EMA crossover against position, other position/order on same symbol exists, daily/weekly/portfolio risk limit breached, order expired
- **TF:** M15
- **Candle:** N/A (tick-level)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** Specific per condition
- **Source:** §12.3
- **Status:** Proposed

### K.4 — TriggerDistanceCheck

- **Inputs:** `ExpectedFillPrice`, `EMA20_current`, `ATR14_current` (M15)
- **Condition:** `|ExpectedFillPrice − EMA20_current| <= 0.60 × ATR14_current`; if not met → cancel
- **TF:** M15
- **Candle:** Current (real-time values allowed for execution check only)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_TRIGGER_DISTANCE`
- **Source:** §12.3
- **Status:** Proposed

### K.5 — SlippageControl

- **Inputs:** Max slippage per symbol [VALUE MISSING], fill price, broker capabilities
- **Condition:** If slippage exceeds max: reject pre-fill if broker allows; if already filled: close only if risk > per-trade cap; else re-calculate SL/TP based on actual fill preserving risk
- **TF:** M15
- **Candle:** N/A (execution-time)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_SLIPPAGE`
- **Source:** §12.4
- **Status:** Proposed

---

## L — Position Sizing

### L.1 — TradeRiskMoney

- **Inputs:** `Equity` at order submission time
- **Condition:** `TradeRiskMoney = Equity × 0.003`
- **TF:** M15
- **Candle:** N/A (real-time)
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §13
- **Status:** Proposed

### L.2 — RawVolume

- **Inputs:** `TradeRiskMoney`, `LossPerLotAtSL` (includes estimated commission and conservative slippage)
- **Condition:** `RawVolume = TradeRiskMoney / LossPerLotAtSL`
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** `LossPerLotAtSL <= 0` → reject
- **ReasonCode:** `REJECT_VOLUME_INVALID`
- **Source:** §13
- **Status:** Proposed

### L.3 — VolumeRounding

- **Inputs:** `RawVolume`, `LotStep`, `MinLot`
- **Condition:** Round **down** to `LotStep`; never round up to meet MinLot
- **TF:** M15
- **Candle:** N/A
- **Rounding:** Floor to LotStep
- **Error:** Rounded volume < MinLot → reject
- **ReasonCode:** `REJECT_BELOW_MIN_LOT`
- **Source:** §13
- **Status:** Proposed

### L.4 — MarginCheck

- **Inputs:** Required margin for final volume, available free margin
- **Condition:** Free margin sufficient for required margin
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Insufficient → reject
- **ReasonCode:** `REJECT_INSUFFICIENT_MARGIN`
- **Source:** §13
- **Status:** Proposed

### L.5 — PostRoundingRisk

- **Inputs:** Final volume (after rounding), risk cap
- **Condition:** Actual risk after rounding must be ≤ per-trade cap
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Exceeds cap → reject
- **ReasonCode:** `REJECT_RISK_EXCEEDED`
- **Source:** §13
- **Status:** Proposed

---

## M — Trade Management

### M.1 — TakeProfit

- **Inputs:** `FillPrice`, `InitialSL` (from J.4 after fill)
- **Condition:** `R = |FillPrice − InitialSL|`; Buy: `TP = FillPrice + 2R`; Sell: `TP = FillPrice − 2R`
- **TF:** M15
- **Candle:** N/A
- **Rounding:** TP rounded to valid Tick Size
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §14.1
- **Status:** Proposed

### M.2 — BreakEvenDisabled

- **Inputs:** N/A
- **Condition:** Break-even auto-move is disabled in base version; model A (fixed SL, TP = 2R) is default
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §14.2
- **Status:** Proposed

### M.3 — TimeExit

- **Inputs:** Position open time, M15 candle count since entry
- **Condition:** If after 32 M15 candles (≈ 8 hours) neither SL nor TP hit → close at market on candle 32 close
- **TF:** M15
- **Candle:** 32nd candle after entry
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `TIME_EXIT`
- **Source:** §14.3
- **Status:** Proposed

### M.4 — OppositeSignalNoClose

- **Inputs:** Opposite signal detected while position open
- **Condition:** Opposite signal does NOT close existing position; no opposite order on same symbol while position open
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §14.4
- **Status:** Proposed

---

## N — News Filter

### N.1 — NewsCalendarSource

- **Inputs:** News calendar (UTC time, currency, title, importance, unique ID)
- **Condition:** Calendar must be available, fresh, and valid; currency mapping: EURUSD → EUR+USD, XAUUSD → USD, XAGUSD → USD
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Calendar unavailable/stale/invalid → no new order
- **ReasonCode:** `REJECT_NEWS_CALENDAR`
- **Source:** §15.1
- **Status:** Proposed

### N.2 — LevelANews

- **Inputs:** News event list
- **Condition:** Level A events: FOMC Rate Decision, FOMC Press Conference, US CPI, US Nonfarm Payrolls, US Core PCE, ECB Rate Decision, ECB Press Conference. ECB events apply only to EURUSD unless research proves otherwise.
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §15.2
- **Status:** Proposed

### N.3 — NewsProhibitedWindow

- **Inputs:** News event time, type
- **Condition:**
  - FOMC: 90 min before, 60 min after
  - Other Level A: 60 min before, 45 min after
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Within window → no new order
- **ReasonCode:** `REJECT_NEWS_WINDOW`
- **Source:** §15.3
- **Status:** Proposed

### N.4 — PendingCancelPreNews

- **Inputs:** Pending order, upcoming news event
- **Condition:** Pending orders cancelled 15 min before news window start
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `CANCELLED_NEWS`
- **Source:** §15.3
- **Status:** Proposed

### N.5 — ClosePreNews

- **Inputs:** Open position, upcoming news event
- **Condition:** Open positions closed at market 15 min before news window start (to avoid gap risk)
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `CLOSED_PRE_NEWS`
- **Source:** §15.3
- **Status:** Proposed

---

## O — Portfolio Risk

### O.1 — RiskPerTrade

- **Inputs:** Current Equity
- **Condition:** `RiskPerTrade = 0.30% × Equity`
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §16.1
- **Status:** Proposed

### O.2 — MaxReservedRisk

- **Inputs:** All open position risks + all pending order risks + estimated commission + conservative slippage
- **Condition:** `TotalReservedRisk <= 0.60% × Equity`; floating profit does NOT create new risk capacity; only confirmed SL reduction by broker reduces reserved risk
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Exceeds cap → reject new order
- **ReasonCode:** `REJECT_RESERVED_RISK`
- **Source:** §16.1
- **Status:** Proposed

### O.3 — MetalBasket

- **Inputs:** XAUUSD risk, XAGUSD risk
- **Condition:** `XAUUSD_risk + XAGUSD_risk <= 0.30% × Equity`; second metal order reduced to remaining capacity or rejected if below MinLot
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Exceeds cap → reduce or reject
- **ReasonCode:** `REJECT_CORRELATED_RISK`
- **Source:** §16.2
- **Status:** Proposed

### O.4 — USDDirectionalExposure

- **Inputs:** Risk of Buy EURUSD, Buy XAUUSD, Buy XAGUSD (Short USD direction); Sell EURUSD, Sell XAUUSD, Sell XAGUSD (Long USD direction)
- **Condition:** `SameDirection_USD_risk <= 0.45% × Equity`; new order reduced to capacity or rejected if below MinLot
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Exceeds cap → reduce or reject
- **ReasonCode:** `REJECT_USD_EXPOSURE`
- **Source:** §16.3
- **Status:** Proposed

---

## P — Drawdown and Kill Switch

### P.1 — DayDefinition

- **Inputs:** Time zone
- **Condition:** Day starts at NewYork 00:00; week starts at Monday NewYork 00:00; DailyStartEquity and WeeklyStartEquity persisted in state
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §17.1
- **Status:** Proposed

### P.2 — DailyDrawdown

- **Inputs:** `DailyStartEquity`, `CurrentEquity`
- **Condition:** `DailyDrawdown = (DailyStartEquity − CurrentEquity) / DailyStartEquity`; Equity includes floating P/L
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** >= 1.00% → stop entries until next day
- **ReasonCode:** `REJECT_DAILY_LOCK`
- **Source:** §17
- **Status:** Proposed

### P.3 — WeeklyDrawdown

- **Inputs:** `WeeklyStartEquity`, `CurrentEquity`
- **Condition:** `WeeklyDrawdown = (WeeklyStartEquity − CurrentEquity) / WeeklyStartEquity`
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** >= 3.00% → stop entries until next week
- **ReasonCode:** `REJECT_WEEKLY_LOCK`
- **Source:** §17
- **Status:** Proposed

### P.4 — MaxDailyEntries

- **Inputs:** Count of filled entries in current day
- **Condition:** <= 3 entries per day
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** > 3 → stop entries until next day
- **ReasonCode:** `REJECT_DAILY_ENTRIES`
- **Source:** §17
- **Status:** Proposed

### P.5 — ConsecutiveLoss

- **Inputs:** Trade result in R multiples
- **Condition:** Result < −0.05R → loss; between −0.05R and +0.05R → neutral (no counter change); > +0.05R → win (counter reset to 0). Counter >= 3 → stop entries until next trading day
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** >= 3 consecutive → lock
- **ReasonCode:** `REJECT_CONSECUTIVE_LOSS`
- **Source:** §17.2
- **Status:** Proposed

### P.6 — KillSwitch

- **Inputs:** `EquityHighWaterMark`, `CurrentEquity`
- **Condition:** `TotalDrawdown = (EquityHighWaterMark − CurrentEquity) / EquityHighWaterMark`; >= 8.00% → Kill Switch activated
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** On activation: (1) cancel all pending, (2) disable new orders, (3) existing positions managed by broker SL/TP, (4) immediate alert, (5) reactivation only by manual review + two-step command, (6) HWM not reset without manual confirmation
- **ReasonCode:** `KILL_SWITCH`
- **Source:** §17.3
- **Status:** Proposed

---

## Q — Error and Connection

### Q.1 — DisconnectBehavior

- **Inputs:** Connection state
- **Condition:** On disconnect: stop new orders; delete local unsent orders; on reconnect: re-read broker state; reconcile with internal state; mismatch → RECONCILIATION_REQUIRED; no new entries until resolved
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `RECONCILIATION_REQUIRED`
- **Source:** §18
- **Status:** Proposed

### Q.2 — StateMismatch

- **Inputs:** Internal state vs broker state (volume, SL, TP, position ID)
- **Condition:** Broker state is source of truth; new entries stopped; risk recalculated; only risk-reducing corrections allowed; full event logged
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Any mismatch → RECONCILIATION_REQUIRED
- **ReasonCode:** `RECONCILIATION_REQUIRED`
- **Source:** §18
- **Status:** Proposed

---

## R — State Machine

### R.1 — States

- **Inputs:** Per-symbol state
- **Condition:** Valid states: `DISABLED`, `READY`, `SIGNAL_FOUND`, `ORDER_PENDING`, `POSITION_OPEN`, `COOLDOWN`, `RECONCILIATION_REQUIRED`
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §19
- **Status:** Proposed

### R.2 — Transitions

- **Inputs:** State events
- **Condition:**
  - `READY → SIGNAL_FOUND` when all signal conditions pass
  - `SIGNAL_FOUND → ORDER_PENDING` when risk and execution checks pass
  - `ORDER_PENDING → POSITION_OPEN` when broker confirms fill
  - `ORDER_PENDING → READY` when cancelled or expired
  - `POSITION_OPEN → COOLDOWN` when position closes
  - `COOLDOWN → READY` after next full M15 candle closes
  - `ANY → RECONCILIATION_REQUIRED` on state mismatch
  - `ANY → DISABLED` on critical configuration or data failure
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** Per transition
- **Source:** §19
- **Status:** Proposed

### R.3 — SinglePositionPerSymbol

- **Inputs:** Symbol state
- **Condition:** At most one pending order OR one open position per symbol at any time
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** `REJECT_DUPLICATE_ORDER`
- **Source:** §19
- **Status:** Proposed

---

## S — Decision Sequence

### S.1 — CandleCloseSequence

- **Inputs:** All market and state data
- **Condition:** On each M15 close, execute in this exact order:
  1. Synchronize broker state
  2. Validate data and clock
  3. Check global/weekly/daily risk locks
  4. Check symbol state
  5. Check trading window and news
  6. Read last fully closed H1 candle
  7. Evaluate H1 trend
  8. Evaluate M15 volatility
  9. Evaluate pullback and momentum signal
  10. Evaluate volume and spread
  11. Find confirmed swing
  12. Calculate entry, SL and TP
  13. Validate stop-distance bounds
  14. Calculate position size
  15. Validate basket and USD exposure
  16. Submit broker-side pending order with SL, TP and expiry
  17. Confirm broker response
  18. Persist state and log decision
- **TF:** M15
- **Candle:** Close of each M15
- **Rounding:** N/A
- **Error:** Each rejection must have a stable ReasonCode
- **ReasonCode:** Various (see §20 list)
- **Source:** §20
- **Status:** Proposed

### S.2 — ReasonCodes

- **Inputs:** N/A
- **Condition:** Every rejection must use one of: `REJECT_SPREAD`, `REJECT_NEWS_WINDOW`, `REJECT_NO_SWING`, `REJECT_STOP_TOO_WIDE`, `REJECT_STOP_TOO_NARROW`, `REJECT_CORRELATED_RISK`, `REJECT_DAILY_LOCK`, `REJECT_DATA_INVALID` (or other specific codes defined in rules above)
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §20
- **Status:** Proposed

---

## T — Backtest Model

### T.1 — BacktestDataRequirements

- **Inputs:** Historical data
- **Condition:** Minimum 5 years; prefer tick data, fallback M1 with conservative model; bid/ask separate or reconstructed with historical spread; same broker or close-match source; historical news calendar; cover low-vol, high-vol, crisis, strong-trend periods
- **TF:** M15 + H1
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §21.1
- **Status:** Proposed

### T.2 — BacktestCosts

- **Inputs:** Spread, commission, slippage, swap, stop level, lot step, gap, fill-side rules
- **Condition:** All must be included: variable historical spread, commission, symbol/time/volatility-dependent slippage, overnight swap, stop level and lot step, gap modeling, Buy Stop fills on Ask, Sell Stop fills on Bid, SL/TP fill on correct side, pre-news and week-end exit with real cost
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §21.2
- **Status:** Proposed

### T.3 — SLTPConflictResolution

- **Inputs:** M1 data or candle data
- **Condition:** If order of SL/TP touch cannot be determined → choose worse outcome for system; optimistic resolution forbidden
- **TF:** M1
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §21.3
- **Status:** Proposed

---

## U — Walk-Forward

### U.1 — WalkForwardProtocol

- **Inputs:** Backtest results
- **Condition:** Training = 24 months, Test = 6 months, Step = 6 months, Mode = Rolling. Only pre-authorized parameters may vary: ADX threshold, VolatilityRatio bounds, Pullback depth, Stop-distance bounds, Time exit, Break-even model.
- **TF:** M15
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §22
- **Status:** Proposed

---

## V — Monte Carlo

### V.1 — MonteCarloProtocol

- **Inputs:** Out-of-sample trades
- **Condition:** Minimum 10,000 simulations; scenarios: reorder trades, resample with replacement, random spread increase, random slippage increase, remove 5% of winning trades, random entry delay, small parameter perturbation, correlated-symbol simultaneous loss
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §23
- **Status:** Proposed

### V.2 — MonteCarloOutputs

- **Inputs:** Simulation results
- **Condition:** Required outputs: net profit distribution, max drawdown distribution, longest loss chain, probability of loss after 1 year, probability of reaching Kill Switch, drawdown percentiles 50/90/95/99
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §23
- **Status:** Proposed

---

## W — Acceptance Criteria

### W.1 — AcceptanceTable

- **Inputs:** Out-of-sample results after all costs
- **Condition:**
  - Profit Factor >= 1.30
  - Expectancy >= +0.12R
  - Annualized Daily Sharpe >= 0.80
  - Max Drawdown <= 6.00%
  - MC 95th percentile Max Drawdown <= 8.00%
  - Out-of-sample trades >= 300
  - Out-of-sample trades per symbol >= 75
  - Walk-Forward profitable windows >= 65%
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Any failure → not ready for live
- **ReasonCode:** N/A
- **Source:** §24
- **Status:** Proposed

### W.2 — SharpeDefinition

- **Inputs:** Daily equity changes
- **Condition:** `DailyReturn = ΔEquity / PreviousEquity`; `Sharpe = √252 × mean(DailyReturn) / std(DailyReturn)`; RiskFreeRate = 0; if std = 0 → Sharpe invalid
- **TF:** Daily
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** std = 0 → invalid
- **ReasonCode:** N/A
- **Source:** §24
- **Status:** Proposed

### W.3 — RobustnessChecks

- **Inputs:** Multi-symbol, multi-parameter results
- **Condition:** Not fully dependent on any single symbol; positive Expectancy with 25% cost increase; no collapse in parameter neighborhood; Holdout Profit Factor >= 1.20
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §24
- **Status:** Proposed

---

## X — Ablation Tests

### X.1 — AblationComparisons

- **Inputs:** Full system vs reduced versions
- **Condition:** Compare against: no volume filter, no ADX filter, no VolatilityRatio filter, no EMA50 slope filter, no time filter, fixed SL/TP vs break-even, each symbol independently, each time window independently
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Filter that only helps in-sample but not in majority of OOS windows → removed
- **ReasonCode:** N/A
- **Source:** §25
- **Status:** Proposed

---

## Y — Pilot Execution

### Y.1 — PilotPhases

- **Inputs:** N/A
- **Condition:** Phase 1 (Backtest): all acceptance criteria met. Phase 2 (Replay): ≥100 scenarios, output match. Phase 3 (Demo): ≥3 months, ≥50 trades, fill/spread/slippage comparison. Phase 4 (Live small): risk 0.10% per trade, ≥50 trades, increase to 0.20% only after execution match, increase to 0.30% only after written risk report
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §26
- **Status:** Proposed

### Y.2 — NoRiskIncreaseOnProfit

- **Inputs:** Short-term profit
- **Condition:** Risk increase based on recent profit is forbidden; criteria is execution quality and model compliance
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §26
- **Status:** Proposed

---

## Z — Logging

### Z.1 — PerCandleLog

- **Inputs:** All computed values
- **Condition:** For every evaluated candle (even no-signal): log UTC time, session local time, all indicator values, all filter states, ReasonCode, spread and baseline, tick volume and baseline, computed entry/SL/TP, selected swing, raw and final volume, trade/metal/USD risk, next news event, broker response, slippage, execution time, result in money/%/R, code version, settings, data hash
- **TF:** M15
- **Candle:** Each
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §27
- **Status:** Proposed

### Z.2 — WeeklyReport

- **Inputs:** Weekly aggregates
- **Condition:** Include: signal/order/fill/reject counts, rejection reasons, Expectancy, Profit Factor, per-symbol/per-session performance, actual spread/slippage vs model, drawdown and correlation risk, errors and state mismatches
- **TF:** Weekly
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §27
- **Status:** Proposed

---

## AA — Parameter Classification

### AA.1 — FixedParameters

- **Inputs:** N/A
- **Condition:** Logically fixed (no change without structural reason): closed-candle usage, H1 trend + M15 entry, definitive swing, SL/TP at broker, correlated risk limit, UTC + IANA, conservative cost model and conflict resolution, Kill Switch, Reconciliation
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** N/A
- **ReasonCode:** N/A
- **Source:** §29
- **Status:** Proposed

### AA.2 — ResearchParameters

- **Inputs:** N/A
- **Condition:** Must be tested with limited sensitivity: ADX threshold, EMA gap in ATR, pullback depth, volume filter, VolatilityRatio bounds, min/max SL distance, 2R target, time exit, break-even model, trading windows
- **TF:** N/A
- **Candle:** N/A
- **Rounding:** N/A
- **Error:** Combination count must be limited and recorded before testing; post-hoc parameter addition increases overfitting risk
- **ReasonCode:** N/A
- **Source:** §29
- **Status:** Proposed
