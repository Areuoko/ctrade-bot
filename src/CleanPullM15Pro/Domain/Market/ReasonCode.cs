namespace CleanPullM15Pro.Domain.Market;

/// <summary>
/// Stable rejection and event codes. Rule S.2.
/// Every rejection must use one of these codes.
/// </summary>
public enum ReasonCode
{
    /// <summary>Data quality check failed.</summary>
    RejectDataInvalid = 0,

    /// <summary>Insufficient warmup candles.</summary>
    RejectWarmup = 1,

    /// <summary>Outside allowed trading window.</summary>
    RejectOutsideWindow = 10,

    /// <summary>Friday cutoff reached.</summary>
    RejectFridayCutoff = 11,

    /// <summary>Within rollover blackout window.</summary>
    RejectRollover = 12,

    /// <summary>H1 trend neutral — no buy or sell.</summary>
    TrendNeutral = 20,

    /// <summary>Volatility ratio below minimum.</summary>
    RejectLowVol = 30,

    /// <summary>Volatility ratio above maximum.</summary>
    RejectHighVol = 31,

    /// <summary>Signal candle definition invalid (Range ≤ 0).</summary>
    RejectSignalInvalid = 40,

    /// <summary>ADX14[1] below minimum threshold (condition, not data error).</summary>
    RejectAdxTooLow = 41,

    /// <summary>RSI14 crossover condition not met (condition, not data error).</summary>
    RejectRsiCondition = 42,

    /// <summary>Volume baseline uncomputable.</summary>
    RejectVolumeBaseline = 50,

    /// <summary>Tick volume below threshold.</summary>
    RejectVolume = 51,

    /// <summary>Spread baseline uncomputable.</summary>
    RejectSpreadBaseline = 60,

    /// <summary>Spread exceeds allowed cap.</summary>
    RejectSpread = 61,

    /// <summary>No confirmed swing found in lookback window.</summary>
    RejectNoSwing = 70,

    /// <summary>Stop loss distance exceeds maximum ATR.</summary>
    RejectStopTooWide = 80,

    /// <summary>Stop loss distance below minimum ATR.</summary>
    RejectStopTooNarrow = 81,

    /// <summary>Stop loss violates broker StopLevel/FreezeLevel.</summary>
    RejectStopLevel = 82,

    /// <summary>Trigger-time distance check failed.</summary>
    RejectTriggerDistance = 90,

    /// <summary>Slippage exceeds allowed maximum.</summary>
    RejectSlippage = 91,

    /// <summary>Raw volume calculation invalid (LossPerLotAtSL ≤ 0).</summary>
    RejectVolumeInvalid = 100,

    /// <summary>Rounded volume below minimum lot.</summary>
    RejectBelowMinLot = 101,

    /// <summary>Insufficient free margin.</summary>
    RejectInsufficientMargin = 102,

    /// <summary>Post-rounding risk exceeds per-trade cap.</summary>
    RejectRiskExceeded = 103,

    /// <summary>Metal basket risk limit exceeded.</summary>
    RejectCorrelatedRisk = 110,

    /// <summary>USD directional exposure limit exceeded.</summary>
    RejectUsdExposure = 111,

    /// <summary>Total reserved risk limit exceeded.</summary>
    RejectReservedRisk = 112,

    /// <summary>Daily drawdown lock triggered.</summary>
    RejectDailyLock = 120,

    /// <summary>Weekly drawdown lock triggered.</summary>
    RejectWeeklyLock = 121,

    /// <summary>Maximum daily entries reached.</summary>
    RejectDailyEntries = 122,

    /// <summary>Consecutive loss limit reached.</summary>
    RejectConsecutiveLoss = 123,

    /// <summary>Kill switch activated.</summary>
    KillSwitch = 124,

    /// <summary>News calendar unavailable or stale.</summary>
    RejectNewsCalendar = 130,

    /// <summary>Within prohibited news window.</summary>
    RejectNewsWindow = 131,

    /// <summary>Symbol metadata invalid or missing.</summary>
    SymbolDisabled = 140,

    /// <summary>Duplicate order on same symbol.</summary>
    RejectDuplicateOrder = 150,

    /// <summary>State mismatch with broker.</summary>
    ReconciliationRequired = 151,

    /// <summary>Order expired before fill.</summary>
    Expired = 160,

    /// <summary>Time-based exit triggered.</summary>
    TimeExit = 161,

    /// <summary>Friday force-close of all positions.</summary>
    FridayForceClose = 162,

    /// <summary>Pending order cancelled before news.</summary>
    CancelledNews = 163,

    /// <summary>Position closed before news window.</summary>
    ClosedPreNews = 164
}
