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
    /// Cancels an existing order.
    /// </summary>
    Task<Result<CancelOrderResponse>> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders for the current session.
    /// </summary>
    Task<Result<List<LiveOrder>>> GetLiveOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    Task<Result<List<Trade>>> GetTradesAsync(CancellationToken cancellationToken = default);

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
}
