using System.Collections.Concurrent;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations with automatic question/reply handling.
/// Uses per-account semaphore serialization to prevent concurrent order submissions.
/// </summary>
public partial class OrderOperations : IOrderOperations
{
    private const int _maxReplyIterations = 20;

    private readonly IIbkrOrderApi _orderApi;
    private readonly ILogger<OrderOperations> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    /// <summary>
    /// Creates a new <see cref="OrderOperations"/> instance.
    /// </summary>
    /// <param name="orderApi">The Refit order API client.</param>
    /// <param name="logger">The logger instance.</param>
    public OrderOperations(IIbkrOrderApi orderApi, ILogger<OrderOperations> logger)
    {
        _orderApi = orderApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderResult> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default)
    {
        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var wireModel = new OrderWireModel(
                order.Conid,
                order.Side,
                order.Quantity,
                order.OrderType,
                order.Price,
                order.AuxPrice,
                order.Tif,
                order.ManualIndicator);

            var payload = new OrdersPayload([wireModel]);
            var responses = await _orderApi.PlaceOrderAsync(accountId, payload);
            var response = responses[0];

            for (var i = 0; i < _maxReplyIterations; i++)
            {
                if (response.OrderId is not null)
                {
                    return new OrderResult(response.OrderId, response.OrderStatus ?? string.Empty);
                }

                if (response.Message is not null && response.Id is not null)
                {
                    var messageText = string.Join("; ", response.Message);
                    LogOrderQuestionAutoConfirmed(messageText);

                    var replyResponses = await _orderApi.ReplyAsync(
                        response.Id, new ReplyRequest(true));
                    response = replyResponses[0];
                }
                else
                {
                    throw new InvalidOperationException(
                        "Unexpected order submission response: no order ID and no question message.");
                }
            }

            throw new InvalidOperationException(
                $"Order question/reply loop exceeded maximum of {_maxReplyIterations} iterations.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default) =>
        _orderApi.CancelOrderAsync(accountId, orderId);

    /// <inheritdoc />
    public async Task<List<LiveOrder>> GetLiveOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _orderApi.GetLiveOrdersAsync();
        return response.Orders ?? [];
    }

    /// <inheritdoc />
    public Task<List<Trade>> GetTradesAsync(
        CancellationToken cancellationToken = default) =>
        _orderApi.GetTradesAsync();

    [LoggerMessage(Level = LogLevel.Warning, Message = "IBKR order question auto-confirmed: {Message}")]
    private partial void LogOrderQuestionAutoConfirmed(string message);
}
