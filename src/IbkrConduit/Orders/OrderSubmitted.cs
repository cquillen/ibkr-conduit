using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Orders;

/// <summary>
/// Confirms the order was accepted by IBKR.
/// </summary>
/// <param name="OrderId">The IBKR order identifier.</param>
/// <param name="OrderStatus">The status of the placed order (e.g., "Submitted", "PreSubmitted").</param>
[ExcludeFromCodeCoverage]
public sealed record OrderSubmitted(string OrderId, string OrderStatus);
