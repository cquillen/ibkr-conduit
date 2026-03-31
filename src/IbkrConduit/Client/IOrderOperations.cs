using IbkrConduit.Orders;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations on the IBKR API.
/// </summary>
public interface IOrderOperations
{
    /// <summary>
    /// Places an order for the specified account. Automatically confirms any
    /// follow-up questions from IBKR by replying with confirmed=true.
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders for the current session.
    /// </summary>
    Task<List<LiveOrder>> GetLiveOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);
}
