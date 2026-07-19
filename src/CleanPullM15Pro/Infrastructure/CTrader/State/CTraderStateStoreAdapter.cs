using System;
using System.Globalization;
using cAlgo.API;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.State;

/// <summary>
/// Implements IStateStorePort using cAlgo's Robot.LocalStorage (persists across
/// restarts, scoped to this bot instance). Rule S.*, P.1.
/// NOTE: verify LocalStorage.SetString/GetString signatures against your SDK —
/// some versions require a LocalStorageScope parameter (Instance vs Robot).
/// </summary>
public sealed class CTraderStateStoreAdapter : IStateStorePort
{
    private readonly Robot _robot;
    private readonly string _prefix;

    /// <summary>Constructs the state-store adapter scoped to a single symbol using <paramref name="robot"/>'s LocalStorage.</summary>
    /// <param name="robot">The cBot instance providing LocalStorage.</param>
    /// <param name="symbolName">Symbol name used to namespace persisted keys.</param>
    public CTraderStateStoreAdapter(Robot robot, string symbolName)
    {
        _robot = robot;
        _prefix = "CleanPullM15Pro_" + symbolName + "_";
    }

    /// <summary>Loads the persisted bot state for <paramref name="symbolName"/>, defaulting to <see cref="BotState.Ready"/>.</summary>
    /// <param name="symbolName">Symbol whose state is being read.</param>
    /// <returns>The persisted <see cref="BotState"/>, or <see cref="BotState.Ready"/> if absent/invalid.</returns>
    public BotState GetState(string symbolName)
    {
        var raw = _robot.LocalStorage.GetString(_prefix + "State");
        if (string.IsNullOrEmpty(raw) || !Enum.TryParse<BotState>(raw, out var state))
            return BotState.Ready;
        return state;
    }

    /// <summary>Persists the bot state for <paramref name="symbolName"/>.</summary>
    /// <param name="symbolName">Symbol whose state is being written.</param>
    /// <param name="state">The state to persist.</param>
    public void SetState(string symbolName, BotState state)
        => _robot.LocalStorage.SetString(_prefix + "State", state.ToString());

    /// <summary>Gets the daily-start equity, defaulting to 0 when unset.</summary>
    /// <returns>The persisted daily-start equity.</returns>
    public double GetDailyStartEquity() => GetDouble("DailyStartEquity", 0);
    /// <summary>Persists the daily-start equity.</summary>
    /// <param name="value">The daily-start equity to store.</param>
    public void SetDailyStartEquity(double value) => SetDouble("DailyStartEquity", value);

    /// <summary>Gets the weekly-start equity, defaulting to 0 when unset.</summary>
    /// <returns>The persisted weekly-start equity.</returns>
    public double GetWeeklyStartEquity() => GetDouble("WeeklyStartEquity", 0);
    /// <summary>Persists the weekly-start equity.</summary>
    /// <param name="value">The weekly-start equity to store.</param>
    public void SetWeeklyStartEquity(double value) => SetDouble("WeeklyStartEquity", value);

    /// <summary>Gets the equity high-water mark, defaulting to 0 when unset.</summary>
    /// <returns>The persisted equity high-water mark.</returns>
    public double GetEquityHighWaterMark() => GetDouble("EquityHwm", 0);
    /// <summary>Persists the equity high-water mark.</summary>
    /// <param name="value">The equity high-water mark to store.</param>
    public void SetEquityHighWaterMark(double value) => SetDouble("EquityHwm", value);

    /// <summary>Gets the filled-entries-today counter, defaulting to 0 when unset.</summary>
    /// <returns>The persisted filled-entries-today count.</returns>
    public int GetFilledEntriesToday() => GetInt("FilledEntriesToday", 0);
    /// <summary>Persists the filled-entries-today counter.</summary>
    /// <param name="value">The count to store.</param>
    public void SetFilledEntriesToday(int value) => SetInt("FilledEntriesToday", value);

    /// <summary>Gets the consecutive-loss count, defaulting to 0 when unset.</summary>
    /// <returns>The persisted consecutive-loss count.</returns>
    public int GetConsecutiveLossCount() => GetInt("ConsecutiveLossCount", 0);
    /// <summary>Persists the consecutive-loss count.</summary>
    /// <param name="value">The count to store.</param>
    public void SetConsecutiveLossCount(int value) => SetInt("ConsecutiveLossCount", value);

    /// <summary>Gets whether the kill switch is active (persists as "1"/"0").</summary>
    /// <returns>True if the kill switch is active; otherwise false.</returns>
    public bool GetKillSwitchActive()
    {
        var raw = _robot.LocalStorage.GetString(_prefix + "KillSwitch");
        return raw == "1";
    }

    /// <summary>Persists the kill-switch state.</summary>
    /// <param name="value">The kill-switch state to store.</param>
    public void SetKillSwitchActive(bool value)
        => _robot.LocalStorage.SetString(_prefix + "KillSwitch", value ? "1" : "0");

    /// <summary>Gets the last counters-reset date string, defaulting to empty when unset.</summary>
    /// <returns>The persisted last-reset date string, or <see cref="string.Empty"/> if absent.</returns>
    public string GetLastCountersResetDate()
        => _robot.LocalStorage.GetString(_prefix + "LastResetDate") ?? string.Empty;

    /// <summary>Persists the last counters-reset date string.</summary>
    /// <param name="date">The date string (yyyy-MM-dd) to store.</param>
    public void SetLastCountersResetDate(string date)
        => _robot.LocalStorage.SetString(_prefix + "LastResetDate", date);

    private double GetDouble(string key, double fallback)
    {
        var raw = _robot.LocalStorage.GetString(_prefix + key);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private void SetDouble(string key, double value)
        => _robot.LocalStorage.SetString(_prefix + key, value.ToString(CultureInfo.InvariantCulture));

    private int GetInt(string key, int fallback)
    {
        var raw = _robot.LocalStorage.GetString(_prefix + key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private void SetInt(string key, int value)
        => _robot.LocalStorage.SetString(_prefix + key, value.ToString(CultureInfo.InvariantCulture));
}
