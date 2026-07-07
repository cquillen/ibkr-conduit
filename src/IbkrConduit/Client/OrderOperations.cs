using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using IbkrConduit.Serialization;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using OneOf;
using Refit;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations with caller-controlled question/reply handling.
/// Uses per-account semaphore serialization to prevent concurrent order submissions.
/// </summary>
internal partial class OrderOperations : IOrderOperations
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
    private readonly IbkrClientOptions _options;
    private readonly ILogger<OrderOperations> _logger;
    private readonly TenantContext _tenant;
    private readonly Dictionary<string, object> _logScope;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    /// <summary>
    /// Creates a new <see cref="OrderOperations"/> instance.
    /// </summary>
    /// <param name="orderApi">The Refit order API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    public OrderOperations(IIbkrOrderApi orderApi, IbkrClientOptions options, ILogger<OrderOperations> logger, TenantContext tenant)
    {
        _orderApi = orderApi;
        _options = options;
        _logger = logger;
        _tenant = tenant;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
    }

    /// <inheritdoc />
    public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Place");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        using var _ = _logger.BeginScope(_logScope);
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
            var response = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            var apiResult = ResultFactory.FromResponse(response, requestPath);
            if (!apiResult.IsSuccess)
            {
                var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            var result = ClassifyOrderResponses(apiResult.Value, ResultFactory.GetRawBody(response), requestPath);
            if (result.IsSuccess)
            {
                _submissionDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                _submissionCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                    new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            }

            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrdersAsync(
        string accountId, IReadOnlyList<OrderRequest> orders, CancellationToken cancellationToken = default)
    {
        // Validate the group shape up front — fail fast on caller error before opening a span/lock.
        ValidateOrderGroup(orders);

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.PlaceGroup");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("ibkr.order_count", orders.Count);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            var payload = new OrdersPayload(orders.Select(ToWireModel).ToList());
            var response = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            var apiResult = ResultFactory.FromResponse(response, requestPath);
            if (!apiResult.IsSuccess)
            {
                var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            // A grouped submission returns a single parent element (verified live); child
            // order ids are obtained via GetLiveOrdersAsync, correlated on the parent cOID.
            var result = ClassifyOrderResponses(apiResult.Value, ResultFactory.GetRawBody(response), requestPath);
            if (result.IsSuccess)
            {
                _submissionDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));

                // Count every leg; tag with the parent order's side/type (legs vary across the group).
                _submissionCount.Add(orders.Count,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>(LogFields.Side, orders[0].Side),
                    new KeyValuePair<string, object?>(LogFields.OrderType, orders[0].OrderType));
            }

            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Validates that a submission forms a single linked group, matching IBKR's contract:
    /// orders[0] is the parent and every child must be linked (ParentId for a bracket, or
    /// IsSingleGroup on every order for an OCA). IBKR rejects unrelated orders in bulk.
    /// </summary>
    private static void ValidateOrderGroup(IReadOnlyList<OrderRequest> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        if (orders.Count == 0)
        {
            throw new ArgumentException("At least one order must be supplied.", nameof(orders));
        }

        if (orders.Count == 1)
        {
            return;
        }

        // OCA group: every order flags isSingleGroup.
        if (orders.All(o => o.IsSingleGroup == true))
        {
            return;
        }

        // Bracket: the parent carries a cOID and every child links to it via ParentId.
        var parentCoid = orders[0].CustomerOrderId;
        if (!string.IsNullOrEmpty(parentCoid) && orders.Skip(1).All(o => o.ParentId == parentCoid))
        {
            return;
        }

        throw new ArgumentException(
            "Multiple orders must form a linked group: a bracket (the parent has CustomerOrderId and " +
            "every child's ParentId equals it) or an OCA group (IsSingleGroup set on every order). " +
            "IBKR rejects unrelated orders in bulk — call PlaceOrderAsync once per unrelated order.",
            nameof(orders));
    }

    /// <inheritdoc />
    public async Task<Result<CancelOrderResponse>> CancelOrderAsync(
        string accountId, string orderId,
        string? extOperator = null, bool? manualIndicator = null, DateTimeOffset? manualCancelTime = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Cancel");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.OrderId, orderId);
        _cancelCount.Add(1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
        var response = await _orderApi.CancelOrderAsync(accountId, orderId, extOperator, manualIndicator, manualCancelTime?.ToUnixTimeSeconds(), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<LiveOrder>>> GetLiveOrdersAsync(
        OrderStatusFilter[]? filters = null,
        bool? force = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetLiveOrders");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        var response = await _orderApi.GetLiveOrdersAsync(filters, force, cancellationToken);
        var apiResult = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        var result = apiResult.Map(r => r.Orders ?? new List<LiveOrder>());
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<Trade>>> GetTradesAsync(
        int? days = null, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetTrades");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        var response = await _orderApi.GetTradesAsync(days, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Modify");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
            var response = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            var apiResult = ResultFactory.FromResponse(response, requestPath);
            if (!apiResult.IsSuccess)
            {
                var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            var result = ClassifyOrderResponses(apiResult.Value, ResultFactory.GetRawBody(response), requestPath);
            if (result.IsSuccess)
            {
                _submissionDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
                _submissionCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId),
                    new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                    new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            }

            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Reply");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag("replyId", replyId);
        activity?.SetTag("confirmed", confirmed);

        _questionCount.Add(1,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
        LogReplyAttempt(replyId, confirmed);

        var replyApiResponse = await _orderApi.ReplyAsync(
            replyId, new ReplyRequest(confirmed), cancellationToken);

        var requestPath = replyApiResponse.RequestMessage?.RequestUri?.AbsolutePath;
        LogReplyRawContent(replyApiResponse.Content ?? string.Empty);

        // AMB-3: route the reply 2xx through the same classification every other order path uses —
        // ThrowOnSendFailure, the ADR-0003 ambiguous 401 gate, non-2xx error parsing, and bare-object
        // hidden-error detection — instead of a bespoke IsSuccessStatusCode branch. The identity parser
        // yields the raw body; classification of the parsed shapes happens below.
        var bodyResult = ResultFactory.FromResponse(replyApiResponse, static body => body, requestPath);
        if (!bodyResult.IsSuccess)
        {
            var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(bodyResult.Error);
            return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
        }

        Result<OneOf<OrderSubmitted, OrderConfirmationRequired>> result;
        try
        {
            // WIR-4: hardened deserialize; AMB-4: empty/array-error shapes classify as refusals.
            var replyResponses = DeserializeReplyResponse(bodyResult.Value);
            result = ClassifyOrderResponses(replyResponses, bodyResult.Value, requestPath);
        }
        catch (JsonException)
        {
            // WIR-4: a 2xx body that still fails typed deserialization surfaces as a classified error,
            // never an uncaught JsonException — so a consumer can map it to its ambiguous leg by type.
            result = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(
                new IbkrApiError(
                    System.Net.HttpStatusCode.OK,
                    "Reply response body could not be deserialized",
                    bodyResult.Value,
                    requestPath));
        }

        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<WhatIfResponse>> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.WhatIf");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);

        var payload = new OrdersPayload([ToWireModel(order)]);
        var response = await _orderApi.WhatIfOrderAsync(accountId, payload, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<OrderStatus>> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetStatus");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.OrderId, orderId);
        var response = await _orderApi.GetOrderStatusAsync(orderId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<string>> DismissNotificationAsync(
        int orderId, string reqId, string text,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.DismissNotification");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.OrderId, orderId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var request = new DismissNotificationRequest(orderId, reqId, text);
        var response = await _orderApi.DismissNotificationAsync(request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <summary>
    /// Classifies a deserialized order response array into a submitted-order / confirmation-required
    /// success, or a classified refusal for the unrecognized-but-plausible 200 shapes (AMB-4): an empty
    /// array or an array-wrapped <c>[{"error":"…"}]</c> reject (which bypasses bare-object hidden-error
    /// detection). Only a truly-unrecognized residual shape still throws — now carrying the raw body.
    /// </summary>
    internal static Result<OneOf<OrderSubmitted, OrderConfirmationRequired>> ClassifyOrderResponses(
        IReadOnlyList<OrderSubmissionResponse> responses, string? rawBody, string? requestPath)
    {
        // AMB-4: an empty array [] has no element to index — a refusal carrying the raw body, never an
        // ArgumentOutOfRangeException.
        if (responses.Count == 0)
        {
            return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(
                new IbkrOrderRejectedError(
                    "IBKR returned an empty order response with no order id or question.", rawBody, requestPath));
        }

        var first = responses[0];

        // AMB-4: an array-wrapped reject [{"error":"…"}] bypasses DetectHiddenError (which skips arrays);
        // classify it as a refusal carrying the reject text and raw body.
        if (first.OrderId is null && (first.Id is null || first.Message is null) && !string.IsNullOrEmpty(first.Error))
        {
            return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(
                new IbkrOrderRejectedError(first.Error, rawBody, requestPath));
        }

        return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Success(ClassifyResponse(first, rawBody));
    }

    /// <summary>
    /// Classifies an IBKR order submission response as either a confirmed order or a confirmation request.
    /// </summary>
    internal static OneOf<OrderSubmitted, OrderConfirmationRequired> ClassifyResponse(
        OrderSubmissionResponse response, string? rawBody = null)
    {
        if (response.OrderId is not null)
        {
            return new OrderSubmitted(
                response.OrderId,
                response.OrderStatus ?? string.Empty,
                response.LocalOrderId,
                response.OcaGroupId);
        }

        if (response.Id is not null && response.Message is not null)
        {
            return new OrderConfirmationRequired(
                response.Id,
                response.Message.AsReadOnly(),
                (response.MessageIds ?? []).AsReadOnly());
        }

        // AMB-4: the residual fallback carries the raw body so the broker's shape survives to the log.
        throw new InvalidOperationException(
            "Unexpected order submission response: no order ID and no question message." +
            (string.IsNullOrEmpty(rawBody) ? string.Empty : $" Raw body: {rawBody}"));
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

        // WIR-4: deserialize through the hardened shared serializer (the same one Refit uses for
        // place/modify) so the reply path shares the tolerant converters — a numeric order_id maps to a
        // string instead of throwing.
        if (trimmed[0] == '[')
        {
            return JsonSerializer.Deserialize<List<OrderSubmissionResponse>>(trimmed, IbkrRefitSettings.Options)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as array: {content}");
        }

        if (trimmed[0] == '{')
        {
            var single = JsonSerializer.Deserialize<OrderSubmissionResponse>(trimmed, IbkrRefitSettings.Options)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as object: {content}");
            return [single];
        }

        throw new InvalidOperationException(
            $"IBKR reply endpoint returned unexpected content: {content}");
    }

    private static OrderWireModel ToWireModel(OrderRequest order) =>
        new(order.Conid, order.Side, order.Quantity, order.OrderType,
            order.Price, order.AuxPrice, order.Tif, order.ManualIndicator)
        {
            CustomerOrderId = order.CustomerOrderId,
            ParentId = order.ParentId,
            IsSingleGroup = order.IsSingleGroup,
            OutsideRth = order.OutsideRth,
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "Replying to IBKR order question {ReplyId} with confirmed={Confirmed}")]
    private partial void LogReplyAttempt(string replyId, bool confirmed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IBKR reply raw content: {Content}")]
    private partial void LogReplyRawContent(string content);
}
