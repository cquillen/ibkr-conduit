using IbkrConduit.Errors;
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
    Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a single linked order group — a bracket or OCA group — in one submission.
    /// The first order is the parent; every child must be linked: set
    /// <see cref="OrderRequest.ParentId"/> (equal to the parent's
    /// <see cref="OrderRequest.CustomerOrderId"/>) on each child for a bracket, or
    /// <see cref="OrderRequest.IsSingleGroup"/> on every order for an OCA group.
    /// IBKR returns a single result for the group (the parent's outcome): either a confirmed
    /// submission or a confirmation-required response to handle via <see cref="ReplyAsync"/>.
    /// Child order ids are NOT returned here — query <see cref="GetLiveOrdersAsync"/> and
    /// correlate on the parent's cOID/order_ref. For unrelated orders, call
    /// <see cref="PlaceOrderAsync"/> once per order (IBKR rejects unrelated bulk).
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="orders">The linked group to submit (parent first). At least one order; multiple orders must form a valid bracket or OCA group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrdersAsync(
        string accountId, IReadOnlyList<OrderRequest> orders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="extOperator">External operator identifier.</param>
    /// <param name="manualIndicator">Required for US Futures; indicates manual vs automated cancel.</param>
    /// <param name="manualCancelTime">Timestamp of manual cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<CancelOrderResponse>> CancelOrderAsync(
        string accountId, string orderId,
        string? extOperator = null, bool? manualIndicator = null, DateTimeOffset? manualCancelTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders for the current session, together with IBKR's priming indicator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result's <see cref="LiveOrdersSnapshot.IsSnapshot"/> flag distinguishes an authoritative
    /// order set from an unprimed one: IBKR's <c>/iserver/account/orders</c> endpoint returns
    /// <c>snapshot:false</c> (often with an empty list) on a cold read even when orders exist, and
    /// primes on a follow-up call. When <see cref="LiveOrdersSnapshot.IsSnapshot"/> is <c>false</c>,
    /// an empty <see cref="LiveOrdersSnapshot.Orders"/> is an unprimed artifact — NOT proof that no
    /// orders exist. Call again (or key any absence decision on <c>IsSnapshot == true</c>). See
    /// design doc §10.6 (findings GAP1-1/GAP1-2).
    /// </para>
    /// </remarks>
    /// <param name="filters">Optional array of order status filters. Only orders matching the
    /// specified statuses are returned. Include <see cref="OrderStatusFilter.SortByTime"/> to
    /// sort results chronologically.
    /// <para>
    /// <b>Quirk handled for you (§10.6):</b> IBKR suppresses order-detail frames on the WebSocket
    /// <c>sor</c> topic after a <em>filtered</em> live-orders call until a <c>force=true</c> follow-up
    /// clears the cached behavior. This library issues that <c>force=true</c> follow-up itself after
    /// any filtered call, so a later <see cref="IStreamingOperations.OrderUpdatesAsync"/> subscription
    /// still receives order details. Consumers need not send the follow-up.
    /// </para></param>
    /// <param name="force">When <c>true</c>, clears IBKR's order cache and fetches fresh data
    /// from the backend. The response will be an empty array — call again without <c>force</c>
    /// to get the refreshed orders (two-call priming pattern).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<LiveOrdersSnapshot>> GetLiveOrdersAsync(
        OrderStatusFilter[]? filters = null,
        bool? force = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    /// <param name="days">Number of prior days to include (1-7). Default is current day only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<Trade>>> GetTradesAsync(
        int? days = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing order. Returns either a confirmed submission
    /// or a confirmation-required response that the caller must handle via <see cref="ReplyAsync"/>.
    /// </summary>
    Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replies to an order confirmation question. Returns either a confirmed submission
    /// or another confirmation-required response (IBKR can chain confirmations).
    /// </summary>
    Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the commission and margin impact of an order without placing it.
    /// </summary>
    Task<Result<WhatIfResponse>> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed status for a single order.
    /// </summary>
    Task<Result<OrderStatus>> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses a server prompt received via the WebSocket <c>ntf</c> notification topic.
    /// Call this to respond to interactive prompts delivered through the streaming connection.
    /// </summary>
    /// <param name="orderId">IB-assigned order identifier from the <c>ntf</c> WebSocket message.</param>
    /// <param name="reqId">IB-assigned request identifier from the <c>ntf</c> WebSocket message.</param>
    /// <param name="text">The selected value from the prompt's <c>options</c> array (e.g., "Yes", "No").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<string>> DismissNotificationAsync(
        int orderId, string reqId, string text,
        CancellationToken cancellationToken = default);
}
