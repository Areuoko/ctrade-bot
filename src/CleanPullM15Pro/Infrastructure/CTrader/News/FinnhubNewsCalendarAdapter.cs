using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Orders;

namespace CleanPullM15Pro.Infrastructure.CTrader.News;

/// <summary>
/// Implements <see cref="INewsCalendarPort"/> using Finnhub's economic-calendar
/// endpoint (GET https://finnhub.io/api/v1/calendar/economic). Rule N.*.
///
/// Design notes:
/// - Refreshes in the background on a <see cref="Timer"/>, never inside
///   OnBar/OnTick with a blocking HTTP call — cBots are single-threaded, and a
///   blocked call there would stall order management on every symbol.
/// - Fail-closed per spec section 15.3: if no successful refresh has happened
///   yet, or the last successful refresh is older than
///   <c>stalenessThreshold</c>, <see cref="IsAvailableAndFresh"/> returns false
///   and the orchestrator blocks all new entries.
/// - Only classifies the fixed Level-A list from spec section 15.2 (FOMC rate
///   decision/press conference, US CPI, US Nonfarm Payrolls, US Core PCE, ECB
///   rate decision/press conference). Everything else Finnhub returns is
///   ignored — this is deliberately not a general-purpose calendar consumer.
/// - Finnhub's raw "event" text does not match spec's canonical titles, so each
///   classified event is normalized before being stored, which keeps
///   <see cref="NewsWindowCalculator.IsLevelA"/> working unchanged.
///
/// SECURITY: never hardcode the API key here or in any committed file. Pass it
/// in from a cBot parameter (or another local-only config source) at startup.
/// </summary>
public sealed class FinnhubNewsCalendarAdapter : INewsCalendarPort, IDisposable
{
    private const string BaseUrl = "https://finnhub.io/api/v1/calendar/economic";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly TimeSpan _stalenessThreshold;
    private readonly TimeSpan _lookBehind;
    private readonly TimeSpan _lookAhead;
    private readonly ILogPort? _log;
    private readonly Timer _refreshTimer;
    private readonly object _lock = new();

    private List<NewsEvent> _events = new();
    private DateTime? _lastSuccessfulFetchUtc;

    /// <summary>
    /// Creates the adapter and schedules the first background refresh immediately.
    /// </summary>
    /// <param name="apiKey">
    /// Finnhub API key. Read this from a cBot parameter or another local-only
    /// config source at startup — never hardcode it or commit it to source control.
    /// If a key is ever pasted somewhere it could be logged or shared, regenerate
    /// it in the Finnhub dashboard before using it live.
    /// </param>
    /// <param name="refreshInterval">How often to re-fetch the calendar in the background (e.g. every few hours).</param>
    /// <param name="stalenessThreshold">Maximum age of the last successful fetch before the calendar is considered unavailable (fail-closed).</param>
    /// <param name="log">Optional logging port for fetch failures.</param>
    public FinnhubNewsCalendarAdapter(
        string apiKey,
        TimeSpan refreshInterval,
        TimeSpan stalenessThreshold,
        ILogPort? log = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Finnhub API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey;
        _stalenessThreshold = stalenessThreshold;
        _lookBehind = TimeSpan.FromDays(1);
        _lookAhead = TimeSpan.FromDays(14);
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        _refreshTimer = new Timer(
            _ => _ = RefreshAsync(),
            null,
            dueTime: TimeSpan.Zero,
            period: refreshInterval);
    }

    /// <summary>
    /// Rule N.3 fail-closed: unavailable until at least one successful fetch has
    /// completed, and stale once the last successful fetch is older than the
    /// configured threshold. A slow network or an outage degrades to "no new
    /// entries" rather than to silently trading through unknown news risk.
    /// </summary>
    public bool IsAvailableAndFresh
    {
        get
        {
            lock (_lock)
            {
                if (_lastSuccessfulFetchUtc is null)
                    return false;

                return DateTime.UtcNow - _lastSuccessfulFetchUtc.Value <= _stalenessThreshold;
            }
        }
    }

    /// <inheritdoc />
    public bool IsInProhibitedWindow(string symbolName, DateTime checkTimeUtc)
    {
        var currencies = NewsWindowCalculator.GetRelevantCurrencies(symbolName);

        List<NewsEvent> snapshot;
        lock (_lock) snapshot = _events;

        foreach (var evt in snapshot)
        {
            if (!NewsWindowCalculator.IsLevelA(evt.Title))
                continue;

            if (!EventAppliesToSymbol(evt.Title, currencies))
                continue;

            if (NewsWindowCalculator.IsInProhibitedWindow(checkTimeUtc, evt.TimeUtc, evt.IsFomc))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsApproachingProhibitedWindow(string symbolName, DateTime checkTimeUtc, TimeSpan lookAhead)
        => IsInProhibitedWindow(symbolName, checkTimeUtc + lookAhead);

    /// <summary>Stops the background refresh timer and releases the HTTP client. Call from OnStop.</summary>
    public void Dispose()
    {
        _refreshTimer.Dispose();
        _http.Dispose();
    }

    private async Task RefreshAsync()
    {
        try
        {
            string from = (DateTime.UtcNow - _lookBehind).ToString("yyyy-MM-dd");
            string to = (DateTime.UtcNow + _lookAhead).ToString("yyyy-MM-dd");
            string url = $"{BaseUrl}?from={from}&to={to}&token={_apiKey}";

            using var response = await _http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _log?.LogError("NewsCalendar", $"Finnhub calendar fetch failed: HTTP {(int)response.StatusCode}");
                return; // keep serving the previous cache; the staleness clock keeps ticking
            }

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsed = ParseAndClassify(body);

            lock (_lock)
            {
                _events = parsed;
                _lastSuccessfulFetchUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            // Network/JSON failures never throw out of the timer callback. Rule N.3
            // fail-closed is enforced purely through the staleness clock in
            // IsAvailableAndFresh, not by crashing the bot.
            _log?.LogError("NewsCalendar", "Finnhub calendar fetch exception: " + ex.Message);
        }
    }

    /// <summary>
    /// Parses the Finnhub response ({"economicCalendar":[{country,event,time,...}]})
    /// and keeps only events matching spec section 15.2's Level-A list, normalized
    /// to the canonical titles NewsWindowCalculator expects.
    /// </summary>
    private static List<NewsEvent> ParseAndClassify(string json)
    {
        var result = new List<NewsEvent>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("economicCalendar", out var array) ||
            array.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            string country = GetString(item, "country");
            string rawEvent = GetString(item, "event");
            string timeRaw = GetString(item, "time");

            if (string.IsNullOrWhiteSpace(rawEvent) || string.IsNullOrWhiteSpace(timeRaw))
                continue;

            // Finnhub returns "time" as "yyyy-MM-dd HH:mm:ss" in UTC.
            if (!DateTime.TryParse(
                    timeRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timeUtc))
                continue;

            var classified = Classify(country, rawEvent);
            if (classified is null)
                continue; // not one of our Level-A events — ignored, not stored

            result.Add(new NewsEvent(timeUtc, classified.Value.Title, classified.Value.IsFomc));
        }

        return result;
    }

    /// <summary>
    /// Maps a raw Finnhub (country, event) pair onto one of spec section 15.2's
    /// canonical Level-A titles. Returns null for anything outside that fixed
    /// list — deliberately conservative; an unrecognized event is never treated
    /// as Level-A just because it looks important.
    ///
    /// NOTE: verify these keyword matches against a live response sample once you
    /// have API access — Finnhub's exact "event" wording for FOMC/CPI/NFP/PCE/ECB
    /// releases should be confirmed and this method adjusted if it differs.
    /// </summary>
    private static (string Title, bool IsFomc)? Classify(string country, string rawEvent)
    {
        bool isUs = country.Equals("US", StringComparison.OrdinalIgnoreCase)
            || country.Equals("United States", StringComparison.OrdinalIgnoreCase);
        bool isEuroArea = country.Equals("EU", StringComparison.OrdinalIgnoreCase)
            || country.Equals("EA", StringComparison.OrdinalIgnoreCase)
            || country.Equals("Euro Area", StringComparison.OrdinalIgnoreCase)
            || country.Equals("Eurozone", StringComparison.OrdinalIgnoreCase);

        string e = rawEvent.ToLowerInvariant();

        if (isUs && (e.Contains("fomc") || e.Contains("fed interest rate") || e.Contains("federal funds rate")))
        {
            return (e.Contains("press conference") || e.Contains("statement"))
                ? ("FOMC Press Conference", true)
                : ("FOMC Rate Decision", true);
        }

        if (isUs && e.Contains("cpi") && !e.Contains("core"))
            return ("US CPI", false);

        if (isUs && (e.Contains("nonfarm payroll") || e.Contains("non-farm payroll") || e.Contains("non farm payroll")))
            return ("US Nonfarm Payrolls", false);

        if (isUs && e.Contains("pce") && e.Contains("core"))
            return ("US Core PCE", false);

        if (isEuroArea && e.Contains("ecb") && e.Contains("press conference"))
            return ("ECB Press Conference", false);

        if (isEuroArea && e.Contains("ecb") && (e.Contains("rate") || e.Contains("interest")))
            return ("ECB Rate Decision", false);

        return null;
    }

    private static bool EventAppliesToSymbol(string title, string[] symbolCurrencies)
    {
        foreach (var currency in symbolCurrencies)
        {
            if (currency == "USD" && (title.StartsWith("FOMC", StringComparison.Ordinal) || title.StartsWith("US ", StringComparison.Ordinal)))
                return true;
            if (currency == "EUR" && title.StartsWith("ECB", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string GetString(JsonElement item, string property)
        => item.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;
}
