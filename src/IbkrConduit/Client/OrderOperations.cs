using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using IbkrConduit.Diagnostics;
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

    private static readonly Histogram<double> _submissionDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.order.submission.duration", "ms");

    private static readonly Counter<long> _submissionCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.submission.count");

    private static readonly Counter<long> _cancelCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.cancel.count");

    private static readonly Counter<long> _questionCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.question.count");

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
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Place");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);
        activity?.SetTag(LogFields.Side, order.Side);
        activity?.SetTag(LogFields.OrderType, order.OrderType);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
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
            var responses = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var result = await HandleQuestionReplyLoopAsync(responses, cancellationToken);

            _submissionDuration.Record(sw.Elapsed.TotalMilliseconds);
            _submissionCount.Add(1,
                new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Cancel");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.OrderId, orderId);
        _cancelCount.Add(1);
        return await _orderApi.CancelOrderAsync(accountId, orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<LiveOrder>> GetLiveOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetLiveOrders");
        var response = await _orderApi.GetLiveOrdersAsync(cancellationToken);
        return response.Orders ?? [];
    }

    /// <inheritdoc />
    public async Task<List<Trade>> GetTradesAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetTrades");
        return await _orderApi.GetTradesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderResult> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Modify");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.OrderId, orderId);
        activity?.SetTag(LogFields.Conid, order.Conid);
        activity?.SetTag(LogFields.Side, order.Side);
        activity?.SetTag(LogFields.OrderType, order.OrderType);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
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
            var responses = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
            var result = await HandleQuestionReplyLoopAsync(responses, cancellationToken);

            _submissionDuration.Record(sw.Elapsed.TotalMilliseconds);
            _submissionCount.Add(1,
                new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.WhatIf");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);

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
        return await _orderApi.WhatIfOrderAsync(accountId, payload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderStatus> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetStatus");
        activity?.SetTag(LogFields.OrderId, orderId);
        return await _orderApi.GetOrderStatusAsync(orderId, cancellationToken);
    }

    private async Task<OrderResult> HandleQuestionReplyLoopAsync(
        List<OrderSubmissionResponse> responses, CancellationToken cancellationToken)
    {
        var response = responses[0];

        for (var i = 0; i < _maxReplyIterations; i++)
        {
            if (response.OrderId is not null)
            {
                return new OrderResult(response.OrderId, response.OrderStatus ?? string.Empty);
            }

            if (response.Message is not null && response.Id is not null)
            {
                _questionCount.Add(1);
                var messageText = string.Join("; ", response.Message);
                LogOrderQuestionAutoConfirmed(messageText);

                var replyResponses = await _orderApi.ReplyAsync(
                    response.Id, new ReplyRequest(true), cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "IBKR order question auto-confirmed: {Message}")]
    private partial void LogOrderQuestionAutoConfirmed(string message);
}
