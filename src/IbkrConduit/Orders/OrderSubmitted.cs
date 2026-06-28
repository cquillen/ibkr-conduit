using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Orders;

/// <summary>
/// Confirms the order was accepted by IBKR.
/// </summary>
/// <param name="OrderId">The IBKR order identifier.</param>
/// <param name="OrderStatus">The status of the placed order (e.g., "Submitted", "PreSubmitted").</param>
/// <param name="LocalOrderId">The customer order id (cOID) echoed back by IBKR, when one was set; otherwise null.</param>
/// <param name="OcaGroupId">The OCA group identifier IBKR assigns to a one-cancels-all group; otherwise null.</param>
[ExcludeFromCodeCoverage]
public sealed record OrderSubmitted(string OrderId, string OrderStatus, string? LocalOrderId = null, string? OcaGroupId = null);
