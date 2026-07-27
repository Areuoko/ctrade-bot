using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Orders;

namespace CleanPullM15Pro.Infrastructure.CTrader.News;

/// <summary>
/// Implements <see cref="INewsCalendarPort"/> using ForexFactory's free, unauthenticated
/// weekly calendar feed (GET ff_calendar_thisweek.json). Replaces
/// <see cref="FinnhubNewsCalendarAdapter"/> as the default live feed, since Finnhub's
/// economic-calendar endpoint is not included in the free plan (confirmed by its "You
/// don't have access to this resource" response — a plan-tier error, not a network/
/// AccessRights problem).
///
/// Design notes / how this addresses the concerns raised in earlier sessions, and what a
/// live test against the real feed (2026-07-25) confirmed or corrected:
///
/// 1) Classification robustness (Rule N.2): <see cref="Classify"/> gates on the feed's own
///    "impact"=="High" field FIRST, then matches canonical wording. Gating on impact avoids
///    misclassifying a Low/Medium item (e.g. a regional Fed speaker) that happens to share
///    a keyword. Verified against a live fetch (week of 2026-07-19): root shape, field names,
///    "country"-holds-currency-code, impact values, and the offset-aware date format all
///    matched what this file assumes. "Main Refinancing Rate" / "ECB Press Conference" /
///    "Monetary Policy Statement" (EUR) all classify correctly. That sample had no FOMC/US
///    CPI/NFP/Core PCE row, so the USD keyword branches are still unverified against a live
///    sample — re-check title wording next time one of those releases falls inside the
///    fetch window before trusting this in a live account.
///
/// 2) Week-boundary blind spot (Rule N.3): an earlier version of this class also fetched
///    "ff_calendar_nextweek.json" to cover a Monday event checked from the preceding
///    Friday/Saturday. That URL returns HTTP 404 — ForexFactory does not publish a public
///    next-week feed under this scheme — so that fetch was removed rather than left calling
///    a dead endpoint every refresh. The blind spot is real but narrow in practice: the bot
///    does not trade over the weekend anyway (Friday force-close, no Saturday/Sunday entry
///    window), and "thisweek.json" itself rolls over to the new week's contents before the
///    London/NY Monday sessions open. This is a documented, accepted limitation, not a
///    solved one — flag it in open-questions.md.
///
/// 3) Silent feed breakage: tracks (a) raw JSON row count per fetch — zero rows is a
///    stronger signal of a format change (or of ForexFactory's undocumented rate limit,
///    see point 5) than zero Level-A rows — and (b) consecutive successful fetches that
///    classify zero Level-A events, which is unusual given how often FOMC/CPI/NFP/Core
///    PCE/ECB releases occur. Both surface as queued diagnostic messages (see next point)
///    rather than silently doing nothing.
///
/// 4) Thread safety: the refresh <see cref="Timer"/> callback runs on a ThreadPool thread.
///    This class NEVER calls into ILogPort (and therefore never into cAlgo's Robot.Print)
///    from that thread. Diagnostic messages are queued in a thread-safe
///    <see cref="ConcurrentQueue{T}"/> instead; the host MUST call
///    <see cref="DrainDiagnostics"/> from its own main-thread callback (e.g. OnBar) and
///    forward the results to ILogPort itself. This was an unresolved concern in
///    <see cref="FinnhubNewsCalendarAdapter"/> (which does call ILogPort directly from the
///    timer thread) — it is fixed here, not there; FinnhubNewsCalendarAdapter is unchanged.
///
/// 5) Undocumented rate limit: as of an August 2024 policy change, ForexFactory limits
///    weekly-export downloads (json/xml/csv/ics) to roughly 2 requests per 5 minutes per
///    source; exceeding it returns an HTML "Request Denied" page instead of JSON, still
///    with a 200-range status in some reports. That response fails JSON parsing and is
///    caught by the try/catch in <see cref="FetchAndParseAsync"/>, so it degrades to
///    a failed fetch (logged, cache preserved, staleness clock keeps ticking) rather than a
///    crash. The default 2-hour refresh interval stays far under this limit under normal
///    operation; this note exists so a future change to a shorter interval doesn't
///    reintroduce the problem silently.
/// </summary>
public sealed class ForexFactoryNewsCalendarAdapter : INewsCalendarPort, IDisposable
{
    private const string ThisWeekUrl = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

    // Consecutive successful-but-zero-Level-A-events fetches before we start warning.
    // Chosen loosely: with the default 2-hour refresh interval this is ~6 hours of
    // "quiet" calendar before we flag it as suspicious rather than genuinely quiet.
    private const int ConsecutiveZeroLevelAWarningThreshold = 3;

    private readonly HttpClient _http;
    private readonly TimeSpan _stalenessThreshold;
    private readonly Timer _refreshTimer;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<string> _pendingDiagnostics = new();

    private List<NewsEvent> _events = new();
    private DateTime? _lastSuccessfulFetchUtc;
    private int _consecutiveZeroLevelAFetches;

    /// <summary>
    /// Creates the adapter and schedules the first background refresh immediately.
    /// No API key is required — ForexFactory's weekly JSON feeds are public.
    /// </summary>
    /// <param name="refreshInterval">How often to re-fetch the calendar feed in the background.</param>
    /// <param name="stalenessThreshold">Maximum age of the last successful fetch before the calendar is considered unavailable (fail-closed per Rule N.3).</param>
    public ForexFactoryNewsCalendarAdapter(TimeSpan refreshInterval, TimeSpan stalenessThreshold)
    {
        _stalenessThreshold = stalenessThreshold;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        _refreshTimer = new Timer(
            _ => _ = RefreshAsync(),
            null,
            dueTime: TimeSpan.Zero,
            period: refreshInterval);
    }

    /// <summary>
    /// Rule N.3 fail-closed: unavailable until at least one successful fetch has completed,
    /// and stale once the last successful fetch is older than the configured threshold.
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

    /// <summary>
    /// Drains diagnostic messages accumulated on the background refresh thread (fetch
    /// failures, feed-format anomalies, zero-Level-A-event streaks). MUST be called from
    /// the host's own main/event thread (e.g. OnBar) — this method itself is just a
    /// thread-safe dequeue and never touches cAlgo's API. Call the returned messages into
    /// ILogPort yourself, from that same main-thread call site.
    /// </summary>
    /// <returns>Zero or more queued diagnostic messages, oldest first. Empty if nothing pending.</returns>
    public IReadOnlyList<string> DrainDiagnostics()
    {
        var list = new List<string>();
        while (_pendingDiagnostics.TryDequeue(out var msg))
            list.Add(msg);
        return list;
    }

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
            var fetch = await FetchAndParseAsync(ThisWeekUrl).ConfigureAwait(false);

            if (!fetch.Success)
                return; // request failed (network, HTTP error, rate-limited, or malformed body)
                        // — keep serving the previous cache; the staleness clock keeps ticking

            lock (_lock)
            {
                _events = fetch.Events;
                _lastSuccessfulFetchUtc = DateTime.UtcNow;
            }

            if (fetch.Events.Count == 0)
            {
                _consecutiveZeroLevelAFetches++;
                if (_consecutiveZeroLevelAFetches >= ConsecutiveZeroLevelAWarningThreshold
                    && _consecutiveZeroLevelAFetches % ConsecutiveZeroLevelAWarningThreshold == 0)
                {
                    _pendingDiagnostics.Enqueue(
                        $"ForexFactory calendar: {_consecutiveZeroLevelAFetches} consecutive successful fetches " +
                        "classified zero Level-A events. FOMC/CPI/NFP/Core PCE/ECB releases occur most weeks — " +
                        "this is unusual enough to be worth checking for a feed format change rather than assuming " +
                        "a genuinely quiet calendar.");
                }
            }
            else
            {
                _consecutiveZeroLevelAFetches = 0;
            }
        }
        catch (Exception ex)
        {
            // Never throw out of the timer callback. Fail-closed behavior is enforced purely
            // through the staleness clock in IsAvailableAndFresh, not by crashing the bot.
            _pendingDiagnostics.Enqueue("ForexFactory calendar refresh exception: " + ex.Message);
        }
    }

    private readonly record struct FetchResult(bool Success, List<NewsEvent> Events);

    private async Task<FetchResult> FetchAndParseAsync(string url)
    {
        try
        {
            using var response = await _http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _pendingDiagnostics.Enqueue($"ForexFactory fetch failed ({url}): HTTP {(int)response.StatusCode}");
                return new FetchResult(false, new List<NewsEvent>());
            }

            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var events = ParseAndClassify(body, out int rawRowCount);

            if (rawRowCount == 0)
            {
                // Zero rows in the raw JSON (before any Level-A filtering) is a much stronger
                // signal of a broken/changed feed than "zero Level-A rows out of many rows".
                _pendingDiagnostics.Enqueue(
                    $"ForexFactory feed ({url}) parsed to zero raw rows — likely an empty response or a " +
                    "root-shape/field-name change, not just a quiet calendar week.");
            }

            return new FetchResult(true, events);
        }
        catch (Exception ex)
        {
            _pendingDiagnostics.Enqueue($"ForexFactory fetch exception ({url}): {ex.Message}");
            return new FetchResult(false, new List<NewsEvent>());
        }
    }

    /// <summary>
    /// Parses the ForexFactory weekly feed — a plain JSON array of
    /// { title, country, date, impact, forecast, previous } objects, where "country" is
    /// actually the currency code (e.g. "USD", "EUR") — and keeps only High-impact rows
    /// matching spec section 15.2's fixed Level-A list, normalized to the canonical titles
    /// <see cref="NewsWindowCalculator.IsLevelA"/> expects.
    /// </summary>
    /// <param name="json">Raw JSON response body.</param>
    /// <param name="rawRowCount">Total rows found in the JSON array, before any filtering — used to detect a broken/changed feed.</param>
    private static List<NewsEvent> ParseAndClassify(string json, out int rawRowCount)
    {
        var result = new List<NewsEvent>();
        rawRowCount = 0;

        using var doc = JsonDocument.Parse(json);

        // NOTE: verify this root shape against a live response at build/runtime — the public
        // ForexFactory mirror has historically returned a bare JSON array. If a wrapping
        // object shows up instead, adjust this root-element check accordingly.
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            rawRowCount++;

            string currency = GetString(item, "country"); // field is named "country" but holds a currency code
            string title = GetString(item, "title");
            string dateRaw = GetString(item, "date");
            string impact = GetString(item, "impact");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dateRaw))
                continue;

            // Level-A events in spec section 15.2 are always High-impact releases. Gating on
            // impact first avoids classifying a Low/Medium row that happens to share wording
            // (e.g. a regional Fed speech mentioning "rate") as one of the canonical titles.
            if (!impact.Equals("High", StringComparison.OrdinalIgnoreCase))
                continue;

            // The feed includes an explicit UTC offset, so DateTimeOffset handles both DST
            // and non-DST dates correctly without a manual timezone table.
            if (!DateTimeOffset.TryParse(
                    dateRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
                continue;

            var classified = Classify(currency, title);
            if (classified is null)
                continue; // High-impact but outside our fixed Level-A list — ignored, not stored

            result.Add(new NewsEvent(dto.UtcDateTime, classified.Value.Title, classified.Value.IsFomc));
        }

        return result;
    }

    /// <summary>
    /// Maps a (currency, title) pair from the ForexFactory feed onto one of spec section
    /// 15.2's canonical Level-A titles. Returns null for anything outside that fixed list —
    /// deliberately conservative; an unrecognized High-impact row is never treated as
    /// Level-A just because it looks important.
    ///
    /// OPEN QUESTION carried over from the Finnhub adapter: spec section 15.2 lists a single
    /// "US CPI" line without distinguishing headline vs. core. This keeps the same choice
    /// already made there — headline CPI only (title contains "cpi" but not "core") — for
    /// consistency. Revisit if that reading turns out to be wrong.
    ///
    /// NOTE: verify these keyword matches against a live response sample — ForexFactory's
    /// exact title wording for FOMC/CPI/NFP/PCE/ECB releases should be confirmed and this
    /// method adjusted if it differs (see class-level doc comment, concern #1).
    /// </summary>
    private static (string Title, bool IsFomc)? Classify(string currency, string rawTitle)
    {
        string cur = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        string t = rawTitle?.ToLowerInvariant() ?? string.Empty;

        bool isUsd = cur == "USD";
        bool isEur = cur == "EUR";

        if (isUsd && (t.Contains("fomc") || t.Contains("federal funds rate")))
        {
            // "FOMC Statement" and "Federal Funds Rate" are the rate-decision release;
            // only an explicit "press conference" row is the separate press conference
            // event. Verified against a live feed sample (2026-07-29 FOMC): ForexFactory
            // publishes "Federal Funds Rate", "FOMC Statement", and "FOMC Press
            // Conference" as three distinct rows at the same/adjacent timestamps.
            return t.Contains("press conference")
                ? ("FOMC Press Conference", true)
                : ("FOMC Rate Decision", true);
        }

        if (isUsd && t.Contains("cpi") && !t.Contains("core"))
            return ("US CPI", false);

        if (isUsd && (t.Contains("non-farm employment change") || t.Contains("nonfarm payroll") || t.Contains("non farm payroll")))
            return ("US Nonfarm Payrolls", false);

        if (isUsd && t.Contains("pce") && t.Contains("core"))
            return ("US Core PCE", false);

        if (isEur && t.Contains("press conference"))
            return ("ECB Press Conference", false);

        // "Monetary Policy Statement" is ForexFactory's generic title for the ECB's rate
        // statement — confirmed against a live sample where it appears alongside
        // "Main Refinancing Rate" at the same timestamp (2026-07-23 EUR meeting). It has no
        // "ecb"/"rate" wording of its own, so it needs an explicit check rather than falling
        // out of the keyword matches below.
        if (isEur && t.Contains("monetary policy statement"))
            return ("ECB Rate Decision", false);

        if (isEur && (t.Contains("main refinancing rate") || t.Contains("deposit facility rate") || t.Contains("interest rate decision")))
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
