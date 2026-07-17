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

    /// <summary>Minimum stop distance from current price.</summary>
    public double StopLevel { get; init; }

    /// <summary>Minimum distance from current price for order modification.</summary>
    public double FreezeLevel { get; init; }

    /// <summary>Commission per lot (estimated).</summary>
    public double Commission { get; init; }

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
        FreezeLevel >= 0;
}
