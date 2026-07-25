using cAlgo.API;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.Symbols;

/// <summary>
/// Implements ISymbolPort using cAlgo's Robot.Symbols accessor. Rule A.1.
/// </summary>
public sealed class CTraderSymbolAdapter : ISymbolPort
{
    private readonly Robot _robot;

    /// <summary>Creates the symbol adapter bound to the host cBot's Symbols accessor.</summary>
    /// <param name="robot">The cBot supplying the symbol metadata/quotes.</param>
    public CTraderSymbolAdapter(Robot robot)
    {
        _robot = robot;
    }

    /// <summary>
    /// Builds the domain symbol-info snapshot for <paramref name="symbolName"/> (Rule A.1),
    /// converting cSymbol tick value to "money per lot per tick" by multiplying by lot size.
    /// A missing broker symbol yields an info object carrying only the symbol name.
    /// </summary>
    /// <param name="symbolName">Symbol to inspect.</param>
    /// <returns>The domain symbol info, or a name-only info when the symbol is not found.</returns>
   public Domain.Market.SymbolInfo GetSymbolInfo(string symbolName)
{
    var s = _robot.Symbols.GetSymbol(symbolName);
    if (s is null)
    {
        return new Domain.Market.SymbolInfo { SymbolName = symbolName };
    }

    // Rule A.1 / spec §11.3 — StopLevel must reflect the broker's real minimum
    // stop-loss distance. Confirmed live via a standalone probe cBot against
    // LiteFinance demo EURUSD (2026-07-25, Log tab output):
    //   MinDistanceType = Pips, MinStopLossDistance = 0, MinTakeProfitDistance = 0
    // Pips converts to price units safely via PipSize (fixed ratio, no live price
    // needed). Percentage is NOT converted here — see IsMinDistanceUnsupported doc.
    double stopLevelPriceUnits;
    bool minDistanceUnsupported;

    if (s.MinDistanceType == SymbolMinDistanceType.Pips)
    {
        stopLevelPriceUnits = s.MinStopLossDistance * s.PipSize;
        minDistanceUnsupported = false;
    }
    else
    {
        stopLevelPriceUnits = 0;
        // Only a real problem if the broker is actually enforcing something;
        // a Percentage type with value 0 is harmless.
        minDistanceUnsupported = s.MinStopLossDistance > 0;
    }

    return new Domain.Market.SymbolInfo
    {
        SymbolName = symbolName,
        TickSize = s.TickSize,
        // NOTE: verify against cAlgo docs at build time — Symbol.TickValue in cAlgo is the
        // money value of one tick move for one unit of volume (not one lot). PositionSizer
        // expects "money per lot per tick", so this multiplies by LotSize. If build errors
        // or live results don't match Symbol.TickValue's actual definition, adjust here.
        TickValue = s.TickValue * s.LotSize,
        Point = s.TickSize,
        ContractSize = s.LotSize,
        MinLot = s.VolumeInUnitsMin > 0 ? s.VolumeInUnitsToQuantity(s.VolumeInUnitsMin) : 0,
        MaxLot = s.VolumeInUnitsMax > 0 ? s.VolumeInUnitsToQuantity(s.VolumeInUnitsMax) : 0,
        LotStep = s.VolumeInUnitsStep > 0 ? s.VolumeInUnitsToQuantity(s.VolumeInUnitsStep) : 0,
        StopLevel = stopLevelPriceUnits,
        FreezeLevel = 0, // cAlgo.API has no Freeze Level equivalent — confirmed, not guessed.
        Commission = s.Commission,
        IsMinDistanceUnsupported = minDistanceUnsupported
    };
}

    /// <summary>Current bid for <paramref name="symbolName"/>; 0 when the symbol is unavailable.</summary>
    /// <param name="symbolName">Symbol to quote.</param>
    /// <returns>Current bid price, or 0.</returns>
    public double CurrentBid(string symbolName) => _robot.Symbols.GetSymbol(symbolName)?.Bid ?? 0;

    /// <summary>Current ask for <paramref name="symbolName"/>; 0 when the symbol is unavailable.</summary>
    /// <param name="symbolName">Symbol to quote.</param>
    /// <returns>Current ask price, or 0.</returns>
    public double CurrentAsk(string symbolName) => _robot.Symbols.GetSymbol(symbolName)?.Ask ?? 0;
}
