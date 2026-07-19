using System;
using System.Linq;
using cAlgo.API;
using CleanPullM15Pro.Application.Contracts;
using CleanPullM15Pro.Application.Ports;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Infrastructure.CTrader.Execution;

/// <summary>
/// Implements IExecutionPort using cAlgo's Robot trading API.
/// NOTE: this file is the most likely to need small adjustments after the first
/// build — cAlgo's PlaceStopOrder overload set and TradeResult/PendingOrder
/// members can differ slightly by SDK version. The logic (SL/TP attached at
/// submit time, ambiguous responses never treated as success) must be kept
/// if anything here is adjusted.
/// </summary>
public sealed class CTraderExecutionAdapter : IExecutionPort
{
    private readonly Robot _robot;
    private const string Label = "CleanPullM15Pro";

    /// <summary>Creates the execution adapter bound to the host cBot's trade API.</summary>
    /// <param name="robot">The cBot whose Symbols/PendingOrders/Positions collections are driven.</param>
    public CTraderExecutionAdapter(Robot robot)
    {
        _robot = robot;
    }

    /// <summary>
    /// Submits a stop order with absolute SL/TP attached at submit time (Rules J.4, M.1).
    /// An ambiguous or unsuccessful broker response is never treated as success.
    /// </summary>
    /// <param name="request">Absolute-price pending-order request.</param>
    /// <returns>Ok with the broker order id, or Fail with the broker error/null-response reason.</returns>
    public OrderSubmitResult SubmitPendingOrder(PendingOrderRequest request)
    {
        var symbol = _robot.Symbols.GetSymbol(request.SymbolName);
        if (symbol is null)
            return OrderSubmitResult.Fail("Symbol not found: " + request.SymbolName);

        var tradeType = request.Direction == TradeDirection.Buy ? TradeType.Buy : TradeType.Sell;

        double volumeInUnits = symbol.QuantityToVolumeInUnits(request.Volume);

        // request.StopLoss / request.TakeProfit are already absolute price levels
        // (Rules J.4, M.1), so ProtectionType.Absolute is used — not pips.
        var result = _robot.PlaceStopOrder(
            tradeType,
            symbol.Name,
            volumeInUnits,
            request.EntryPrice,
            request.Label,
            request.StopLoss,
            request.TakeProfit,
            ProtectionType.Absolute,
            request.ExpiryUtc);

        // An ambiguous or unsuccessful broker response must never be treated as success.
        if (result is null || !result.IsSuccessful || result.PendingOrder is null)
        {
            string reason = result?.Error?.ToString() ?? "null/ambiguous broker response";
            return OrderSubmitResult.Fail(reason);
        }

        return OrderSubmitResult.Ok(result.PendingOrder.Id.ToString());
    }

    /// <summary>Cancels the pending order with id <paramref name="orderId"/>. A missing order returns true (already gone).</summary>
    /// <param name="orderId">Broker id of the pending order to cancel.</param>
    /// <returns>True if the order is gone (already-cancelled or cancellation confirmed); false if cancellation failed.</returns>
    public bool CancelPendingOrder(string orderId)
    {
        var order = _robot.PendingOrders.FirstOrDefault(o => o.Id.ToString() == orderId);
        if (order is null)
            return true; // already gone — treat as success, nothing to cancel

        var result = _robot.CancelPendingOrder(order);
        return result is not null && result.IsSuccessful;
    }

    /// <summary>Closes the open position identified by <paramref name="positionId"/>.</summary>
    /// <param name="positionId">Broker id of the position to close.</param>
    /// <returns>True if the position was open and the close succeeded; false if missing or close failed.</returns>
    public bool CloseMarket(string positionId)
    {
        var position = _robot.Positions.FirstOrDefault(p => p.Id.ToString() == positionId);
        if (position is null)
            return false;

        var result = _robot.ClosePosition(position);
        return result is not null && result.IsSuccessful;
    }

    /// <summary>Modifies the SL and TP of an open position using absolute price levels.</summary>
    /// <param name="positionId">Broker id of the position to modify.</param>
    /// <param name="stopLoss">New absolute stop-loss price.</param>
    /// <param name="takeProfit">New absolute take-profit price.</param>
    /// <returns>True if the modification succeeded; false if the position is missing or the modify failed.</returns>
    public bool ModifyStops(string positionId, double stopLoss, double takeProfit)
    {
        var position = _robot.Positions.FirstOrDefault(p => p.Id.ToString() == positionId);
        if (position is null)
            return false;

        var result = _robot.ModifyPosition(position, stopLoss, takeProfit, ProtectionType.Absolute);
        return result is not null && result.IsSuccessful;
    }

    /// <summary>
    /// Reads the broker's current position/order state for <paramref name="symbolName"/>,
    /// preferring an open position over a pending order, else reporting flat.
    /// </summary>
    /// <param name="symbolName">Symbol whose broker state to read.</param>
    /// <returns>An open-position snapshot, a pending-order snapshot, or a flat (no position, no order) state.</returns>
    public BrokerPositionState GetBrokerState(string symbolName)
    {
        var position = _robot.Positions.FirstOrDefault(p => p.SymbolName == symbolName);
        if (position is not null)
        {
            return new BrokerPositionState
            {
                HasOpenPosition = true,
                HasPendingOrder = false,
                OrderOrPositionId = position.Id.ToString(),
                Direction = position.TradeType == TradeType.Buy ? TradeDirection.Buy : TradeDirection.Sell,
                Volume = position.Symbol.VolumeInUnitsToQuantity(position.VolumeInUnits),
                StopLoss = position.StopLoss ?? 0,
                TakeProfit = position.TakeProfit ?? 0,
                OpenTimeUtc = position.EntryTime
            };
        }

        var pending = _robot.PendingOrders.FirstOrDefault(o => o.SymbolName == symbolName);
        if (pending is not null)
        {
            return new BrokerPositionState
            {
                HasOpenPosition = false,
                HasPendingOrder = true,
                OrderOrPositionId = pending.Id.ToString(),
                Direction = pending.TradeType == TradeType.Buy ? TradeDirection.Buy : TradeDirection.Sell,
                Volume = pending.Symbol.VolumeInUnitsToQuantity(pending.VolumeInUnits),
                StopLoss = pending.StopLoss ?? 0,
                TakeProfit = pending.TakeProfit ?? 0,
                OpenTimeUtc = null
            };
        }

        return new BrokerPositionState { HasOpenPosition = false, HasPendingOrder = false };
    }
}
