using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for persisting state across restarts. Rule S.*, P.1.
/// Implemented via cBot's local/robot storage in Infrastructure.
/// </summary>
public interface IStateStorePort
{
    /// <summary>Loads the persisted bot state for the given symbol.</summary>
    /// <param name="symbolName">Symbol whose state is being read.</param>
    /// <returns>The persisted <see cref="BotState"/> for the symbol.</returns>
    BotState GetState(string symbolName);
    /// <summary>Persists the bot state for the given symbol.</summary>
    /// <param name="symbolName">Symbol whose state is being written.</param>
    /// <param name="state">The state to persist.</param>
    void SetState(string symbolName, BotState state);

    /// <summary>Gets the equity recorded at the start of the trading day.</summary>
    /// <returns>The daily-start equity value.</returns>
    double GetDailyStartEquity();
    /// <summary>Sets the equity recorded at the start of the trading day.</summary>
    /// <param name="value">The daily-start equity value to persist.</param>
    void SetDailyStartEquity(double value);

    /// <summary>Gets the equity recorded at the start of the trading week.</summary>
    /// <returns>The weekly-start equity value.</returns>
    double GetWeeklyStartEquity();
    /// <summary>Sets the equity recorded at the start of the trading week.</summary>
    /// <param name="value">The weekly-start equity value to persist.</param>
    void SetWeeklyStartEquity(double value);

    /// <summary>Gets the high-water mark of equity.</summary>
    /// <returns>The equity high-water-mark value.</returns>
    double GetEquityHighWaterMark();
    /// <summary>Sets the high-water mark of equity.</summary>
    /// <param name="value">The equity high-water-mark value to persist.</param>
    void SetEquityHighWaterMark(double value);

    /// <summary>Gets the number of entries filled today.</summary>
    /// <returns>The count of filled entries today.</returns>
    int GetFilledEntriesToday();
    /// <summary>Sets the number of entries filled today.</summary>
    /// <param name="value">The filled-entries-today count to persist.</param>
    void SetFilledEntriesToday(int value);

    /// <summary>Gets the current consecutive-loss count.</summary>
    /// <returns>The consecutive loss count.</returns>
    int GetConsecutiveLossCount();
    /// <summary>Sets the current consecutive-loss count.</summary>
    /// <param name="value">The consecutive-loss count to persist.</param>
    void SetConsecutiveLossCount(int value);

    /// <summary>Gets whether the kill switch is currently active.</summary>
    /// <returns>True if the kill switch is active; otherwise false.</returns>
    bool GetKillSwitchActive();
    /// <summary>Sets whether the kill switch is active.</summary>
    /// <param name="value">The kill-switch state to persist.</param>
    void SetKillSwitchActive(bool value);

    /// <summary>Last UTC date (yyyy-MM-dd, New York session date) counters were reset for.</summary>
    string GetLastCountersResetDate();
    /// <summary>Sets the last UTC date the daily counters were reset for.</summary>
    /// <param name="date">The date string (yyyy-MM-dd) to persist.</param>
    void SetLastCountersResetDate(string date);
}
