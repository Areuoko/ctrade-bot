using CleanPullM15Pro.Application.Contracts;

namespace CleanPullM15Pro.Application.Ports;

/// <summary>
/// Port for submitting and managing broker-side orders/positions.
/// Implemented by Infrastructure/CTrader. Rules K.*, M.*.
/// </summary>
public interface IExecutionPort
{
    /// <summary>Submits a pending stop order with SL/TP/expiry attached at broker side.</summary>
    OrderSubmitResult SubmitPendingOrder(PendingOrderRequest request);

    /// <summary>Cancels a pending order by id. No-op if already gone.</summary>
    bool CancelPendingOrder(string orderId);

    /// <summary>Closes an open position at market.</summary>
    bool CloseMarket(string positionId);

    /// <summary>Modifies SL/TP of an open position (e.g. Fill-based retargeting after slippage).</summary>
    bool ModifyStops(string positionId, double stopLoss, double takeProfit);

    /// <summary>Reads current broker-side state for this symbol (Rule S.9 reconciliation source).</summary>
    BrokerPositionState GetBrokerState(string symbolName);
}
