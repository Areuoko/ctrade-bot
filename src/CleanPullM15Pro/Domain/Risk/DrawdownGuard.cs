using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Drawdown and kill-switch guards. Rules P.1–P.6.
///
/// RISK REVISION (documented intentional deviation from spec section 17's base
/// values): Daily/Weekly limits were scaled up proportionally to the per-trade
/// risk revision in <see cref="PositionSizer"/> (0.30% → 1.00%, roughly 3.33x).
/// The Kill Switch limit was NOT scaled linearly — see the constant's own doc
/// comment for why. See docs/open-questions.md for full rationale; these values
/// must be re-evaluated (and likely brought back down) before any live-account use.
/// </summary>
public static class DrawdownGuard
{
    // Revised from 0.01 (1.00%) — scaled ~3x alongside the per-trade risk revision
    // (1.00% new per-trade risk vs 0.30% original ≈ 3.33x; rounded to a clean 3x
    // multiple of the original daily limit for a round number).
    private const double DailyDrawdownLimit = 0.03;   // 3.00%

    // Revised from 0.03 (3.00%) — kept at the same 3x ratio to the daily limit
    // that the original spec used (3.00% / 1.00% = 3x), applied to the new daily limit.
    private const double WeeklyDrawdownLimit = 0.09;  // 9.00%

    // Revised from 0.08 (8.00%) — deliberately NOT scaled by the same ~3.33x factor
    // as per-trade/daily/weekly risk (which would give ~26.6%). The Kill Switch is
    // the last line of capital-preservation defense, not a multiple of per-trade
    // risk sizing; allowing over a quarter of the account to erode before it fires
    // would defeat its purpose. 15.00% is a deliberate, more conservative compromise
    // — wider than the original 8.00% (to avoid tripping on ordinary drawdown swings
    // at the larger position size) but well short of a fully linear scale-up.
    private const double KillSwitchLimit = 0.15;      // 15.00%

    // Unchanged — Rules P.4/P.5 are behavioral/streak locks, not position-size-dependent.
    private const int MaxDailyEntries = 3;
    private const int MaxConsecutiveLosses = 3;

    // P.1 — Day/week start is New York 00:00
    // Time-zone conversion handled by caller (infrastructure).

    /// <summary>
    /// P.2 — Daily drawdown check.
    /// </summary>
    public static ReasonCode? ValidateDailyDrawdown(double dailyStartEquity, double currentEquity)
    {
        if (dailyStartEquity <= 0) return ReasonCode.RejectDataInvalid;

        double drawdown = (dailyStartEquity - currentEquity) / dailyStartEquity;

        if (drawdown >= DailyDrawdownLimit)
            return ReasonCode.RejectDailyLock;

        return null;
    }

    /// <summary>
    /// P.3 — Weekly drawdown check.
    /// </summary>
    public static ReasonCode? ValidateWeeklyDrawdown(double weeklyStartEquity, double currentEquity)
    {
        if (weeklyStartEquity <= 0) return ReasonCode.RejectDataInvalid;

        double drawdown = (weeklyStartEquity - currentEquity) / weeklyStartEquity;

        if (drawdown >= WeeklyDrawdownLimit)
            return ReasonCode.RejectWeeklyLock;

        return null;
    }

    /// <summary>
    /// P.4 — Max daily entries check.
    /// </summary>
    public static ReasonCode? ValidateDailyEntries(int filledEntriesToday)
    {
        if (filledEntriesToday >= MaxDailyEntries)
            return ReasonCode.RejectDailyEntries;

        return null;
    }

    /// <summary>
    /// P.5 — Consecutive loss check.
    /// lossCount: number of consecutive losses (result &lt; −0.05R).
    /// </summary>
    public static ReasonCode? ValidateConsecutiveLoss(int lossCount)
    {
        if (lossCount >= MaxConsecutiveLosses)
            return ReasonCode.RejectConsecutiveLoss;

        return null;
    }

    /// <summary>
    /// P.6 — Kill switch (total drawdown from equity high-water mark).
    /// </summary>
    public static bool IsKillSwitchTriggered(double equityHighWaterMark, double currentEquity)
    {
        if (equityHighWaterMark <= 0) return true;

        double drawdown = (equityHighWaterMark - currentEquity) / equityHighWaterMark;

        return drawdown >= KillSwitchLimit;
    }
}
