using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Domain.Risk;

/// <summary>
/// Drawdown and kill-switch guards. Rules P.1–P.6.
/// </summary>
public static class DrawdownGuard
{
    private const double DailyDrawdownLimit = 0.01;   // 1.00%
    private const double WeeklyDrawdownLimit = 0.03;  // 3.00%
    private const double KillSwitchLimit = 0.08;      // 8.00%
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
