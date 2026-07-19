using System;
using CleanPullM15Pro.Domain.Market;

namespace CleanPullM15Pro.Application.Contracts;

/// <summary>
/// Fully computed pending-order request, ready to submit to the broker.
/// All prices/volume are already validated and rounded (Rules K.1–K.2, L.*).
/// </summary>
public sealed class PendingOrderRequest
{
    /// <summary>Target symbol name for the pending order.</summary>
    public string SymbolName { get; init; } = string.Empty;
    /// <summary>Intended trade direction (long or short).</summary>
    public TradeDirection Direction { get; init; }
    /// <summary>Pending order entry (stop) price.</summary>
    public double EntryPrice { get; init; }
    /// <summary>Stop-loss price for the order.</summary>
    public double StopLoss { get; init; }
    /// <summary>Take-profit price for the order.</summary>
    public double TakeProfit { get; init; }
    /// <summary>Order volume (in lots/units).</summary>
    public double Volume { get; init; }
    /// <summary>Order expiry time (UTC); the order is cancelled if unfilled by then.</summary>
    public DateTime ExpiryUtc { get; init; }
    /// <summary>Order label used for traceability and broker tagging.</summary>
    public string Label { get; init; } = "CleanPullM15Pro";
}

/// <summary>
/// Result of a broker submission attempt. Ambiguous broker responses must
/// NOT be treated as success (Rule: execution prompt, "در پاسخ مبهم Broker
/// سفارش موفق فرض نشود").
/// </summary>
public sealed class OrderSubmitResult
{
    /// <summary>True if the broker accepted and registered the order.</summary>
    public bool Success { get; init; }
    /// <summary>Broker-assigned order id when successfully submitted.</summary>
    public string? BrokerOrderId { get; init; }
    /// <summary>Description of the failure reason when submission failed.</summary>
    public string? ErrorDescription { get; init; }

    /// <summary>Builds a successful submission result carrying the broker order id.</summary>
    /// <param name="brokerOrderId">Broker-assigned identifier for the submitted order.</param>
    /// <returns>A successful <see cref="OrderSubmitResult"/>.</returns>
    public static OrderSubmitResult Ok(string brokerOrderId) => new()
    {
        Success = true,
        BrokerOrderId = brokerOrderId
    };

    /// <summary>Builds a failed submission result carrying the error reason.</summary>
    /// <param name="reason">Human-readable description of why submission failed.</param>
    /// <returns>A failed <see cref="OrderSubmitResult"/>.</returns>
    public static OrderSubmitResult Fail(string reason) => new()
    {
        Success = false,
        ErrorDescription = reason
    };
}

/// <summary>Snapshot of an existing pending order or open position on this symbol.</summary>
public sealed class BrokerPositionState
{
    /// <summary>True when a pending (unfilled) order exists for the symbol.</summary>
    public bool HasPendingOrder { get; init; }
    /// <summary>True when an open (filled) position exists for the symbol.</summary>
    public bool HasOpenPosition { get; init; }
    /// <summary>Broker identifier for the pending order or open position, if any.</summary>
    public string? OrderOrPositionId { get; init; }
    /// <summary>Direction of the existing order/position.</summary>
    public TradeDirection Direction { get; init; }
    /// <summary>Volume of the existing order/position.</summary>
    public double Volume { get; init; }
    /// <summary>Current stop-loss of the existing order/position.</summary>
    public double StopLoss { get; init; }
    /// <summary>Current take-profit of the existing order/position.</summary>
    public double TakeProfit { get; init; }
    /// <summary>Open time (UTC) of the position, if any.</summary>
    public DateTime? OpenTimeUtc { get; init; }
}
