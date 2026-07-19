using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for symbol metadata and live pricing. Rule A.1.
/// </summary>
public interface ISymbolPort
{
    /// <summary>Reads and validates symbol metadata. IsValid=false → SYMBOL_DISABLED (Rule A.1).</summary>
    SymbolInfo GetSymbolInfo(string symbolName);

    /// <summary>Current bid price for the given symbol.</summary>
    /// <param name="symbolName">Symbol whose bid price is being queried.</param>
    /// <returns>The current bid price.</returns>
    double CurrentBid(string symbolName);

    /// <summary>Current ask price for the given symbol.</summary>
    /// <param name="symbolName">Symbol whose ask price is being queried.</param>
    /// <returns>The current ask price.</returns>
    double CurrentAsk(string symbolName);
}
