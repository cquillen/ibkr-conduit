using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using IbkrConduit.Diagnostics;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging;
using OneOf;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations with caller-controlled question/reply handling.
/// Uses per-account semaphore serialization to prevent concurrent order submissions.
/// </summary>
public partial class OrderOperations : IOrderOperations
{
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
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
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
            var payload = new OrdersPayload([ToWireModel(order)]);
            var responses = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var result = ClassifyResponse(responses[0]);

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
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
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
            var payload = new OrdersPayload([ToWireModel(order)]);
            var responses = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
            var result = ClassifyResponse(responses[0]);

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
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Reply");
        activity?.SetTag("replyId", replyId);
        activity?.SetTag("confirmed", confirmed);

        _questionCount.Add(1);
        LogReplyAttempt(replyId, confirmed);

        var replyApiResponse = await _orderApi.ReplyAsync(
            replyId, new ReplyRequest(confirmed), cancellationToken);

        if (!replyApiResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"IBKR reply endpoint returned HTTP {(int)replyApiResponse.StatusCode}: {replyApiResponse.Error?.Content}");
        }

        LogReplyRawContent(replyApiResponse.Content ?? string.Empty);
        var replyResponses = DeserializeReplyResponse(replyApiResponse.Content!);
        return ClassifyResponse(replyResponses[0]);
    }

    /// <inheritdoc />
    public async Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.WhatIf");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);

        var payload = new OrdersPayload([ToWireModel(order)]);
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

    /// <summary>
    /// Classifies an IBKR order submission response as either a confirmed order or a confirmation request.
    /// </summary>
    internal static OneOf<OrderSubmitted, OrderConfirmationRequired> ClassifyResponse(
        OrderSubmissionResponse response)
    {
        if (response.OrderId is not null)
        {
            return new OrderSubmitted(response.OrderId, response.OrderStatus ?? string.Empty);
        }

        if (response.Id is not null && response.Message is not null)
        {
            return new OrderConfirmationRequired(
                response.Id,
                response.Message.AsReadOnly(),
                (response.MessageIds ?? []).AsReadOnly());
        }

        throw new InvalidOperationException(
            "Unexpected order submission response: no order ID and no question message.");
    }

    /// <summary>
    /// Deserializes an IBKR reply response that may be either a JSON array or a bare JSON object.
    /// </summary>
    internal static List<OrderSubmissionResponse> DeserializeReplyResponse(string content)
    {
        var trimmed = content.AsSpan().Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException(
                "IBKR reply endpoint returned an empty response body.");
        }

        if (trimmed[0] == '[')
        {
            return JsonSerializer.Deserialize<List<OrderSubmissionResponse>>(trimmed)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as array: {content}");
        }

        if (trimmed[0] == '{')
        {
            var single = JsonSerializer.Deserialize<OrderSubmissionResponse>(trimmed)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as object: {content}");
            return [single];
        }

        throw new InvalidOperationException(
            $"IBKR reply endpoint returned unexpected content: {content}");
    }

    private static OrderWireModel ToWireModel(OrderRequest order) =>
        new(order.Conid, order.Side, order.Quantity, order.OrderType,
            order.Price, order.AuxPrice, order.Tif, order.ManualIndicator);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replying to IBKR order question {ReplyId} with confirmed={Confirmed}")]
    private partial void LogReplyAttempt(string replyId, bool confirmed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IBKR reply raw content: {Content}")]
    private partial void LogReplyRawContent(string content);
}
