using IbkrConduit.Orders;
using OneOf;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations on the IBKR API.
/// </summary>
public interface IOrderOperations
{
    /// <summary>
    /// Places an order for the specified account. Returns either a confirmed submission
    /// or a confirmation-required response that the caller must handle via <see cref="ReplyAsync"/>.
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders (cancelled, filled, submitted) across sessions.
    /// Note: IBKR requires two calls — the first primes the endpoint and may return empty results.
    /// </summary>
    /// <param name="filters">Optional comma-separated status filters (e.g., "submitted", "filled", "cancelled").</param>
    /// <param name="force">When true, clears cached order data and forces a fresh request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<LiveOrder>> GetLiveOrdersAsync(
        string? filters = null, bool? force = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing order. Returns either a confirmed submission
    /// or a confirmation-required response that the caller must handle via <see cref="ReplyAsync"/>.
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replies to an order confirmation question. Returns either a confirmed submission
    /// or another confirmation-required response (IBKR can chain confirmations).
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the commission and margin impact of an order without placing it.
    /// </summary>
    Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed status for a single order.
    /// </summary>
    Task<OrderStatus> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default);
}
