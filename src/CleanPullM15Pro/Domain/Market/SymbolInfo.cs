namespace CleanPullM15Pro.Domain.Market;

/// <summary>
/// Symbol metadata required at startup. Rule A.1.
/// </summary>
public readonly record struct SymbolInfo
{
    /// <summary>Symbol name as provided by broker.</summary>
    public string SymbolName { get; init; }

    /// <summary>Minimum price increment.</summary>
    public double TickSize { get; init; }

    /// <summary>Value of one tick in account currency.</summary>
    public double TickValue { get; init; }

    /// <summary>Point value (price unit).</summary>
    public double Point { get; init; }

    /// <summary>Contract size for lot calculation.</summary>
    public double ContractSize { get; init; }

    /// <summary>Minimum allowed lot size.</summary>
    public double MinLot { get; init; }

    /// <summary>Maximum allowed lot size.</summary>
    public double MaxLot { get; init; }

    /// <summary>Lot size increment step.</summary>
    public double LotStep { get; init; }

    /// <summary>
    /// Minimum stop distance from current price, in price units. Sourced from
    /// cAlgo's Symbol.MinStopLossDistance (Rule A.1, spec §11.3). Verified live
    /// against LiteFinance demo EURUSD (2026-07-25): MinDistanceType = Pips,
    /// value = 0 (broker imposes no restriction on this symbol/account).
    /// </summary>
    public double StopLevel { get; init; }

    /// <summary>
    /// Minimum distance from current price for order modification. cAlgo.API has
    /// no equivalent to MT4/5's Freeze Level (confirmed against the current API
    /// reference, 2026-07) — left at 0 deliberately, not as an unresolved gap.
    /// </summary>
    public double FreezeLevel { get; init; }

    /// <summary>Commission per lot (estimated).</summary>
    public double Commission { get; init; }

    /// <summary>
    /// True when the broker reports Symbol.MinDistanceType = Percentage with a
    /// non-zero MinStopLossDistance. This adapter only converts the Pips case to
    /// price units (a fixed, safe conversion); Percentage needs the live price at
    /// trigger time, not the one-time startup read, so it fails closed here
    /// instead of guessing. IsValid folds this into SYMBOL_DISABLED (spec §2)
    /// until the conversion is implemented and verified against a real broker.
    /// </summary>
    public bool IsMinDistanceUnsupported { get; init; }

    /// <summary>
    /// Validates that all required fields are present and positive where required.
    /// Rule A.1: any missing or invalid field → symbol disabled.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(SymbolName) &&
        TickSize > 0 &&
        TickValue > 0 &&
        Point > 0 &&
        ContractSize > 0 &&
        MinLot > 0 &&
        MaxLot >= MinLot &&
        LotStep > 0 &&
        StopLevel >= 0 &&
        FreezeLevel >= 0 &&
        !IsMinDistanceUnsupported;
}