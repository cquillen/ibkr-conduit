using Refit;

namespace IbkrConduit.Orders;

/// <summary>
/// Refit interface for IBKR order management endpoints.
/// </summary>
public interface IIbkrOrderApi
{
    /// <summary>
    /// Submits one or more orders for the specified account.
    /// </summary>
    [Post("/v1/api/iserver/account/{accountId}/orders")]
    Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
        string accountId, [Body] OrdersPayload orders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replies to an order confirmation question with a confirmed/rejected answer.
    /// </summary>
    [Post("/v1/api/iserver/reply/{replyId}")]
    Task<List<OrderSubmissionResponse>> ReplyAsync(
        string replyId, [Body] ReplyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    [Delete("/v1/api/iserver/account/{accountId}/order/{orderId}")]
    Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders for the current session.
    /// </summary>
    [Get("/v1/api/iserver/account/orders")]
    Task<OrdersResponse> GetLiveOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    [Get("/v1/api/iserver/account/trades")]
    Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing order for the specified account.
    /// </summary>
    [Post("/v1/api/iserver/account/{accountId}/order/{orderId}")]
    Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
        string accountId, string orderId, [Body] OrdersPayload orders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the commission and margin impact of an order without placing it.
    /// </summary>
    [Post("/v1/api/iserver/account/{accountId}/orders/whatif")]
    Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, [Body] OrdersPayload orders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed status for a single order.
    /// </summary>
    [Get("/v1/api/iserver/account/order/status/{orderId}")]
    Task<OrderStatus> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default);
}
