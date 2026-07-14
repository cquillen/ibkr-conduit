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

// ADR-0008 §9.11: the per-leg outcome of a single group leg, and the whole-group placement outcome
// (a per-leg outcome list, or a group-wide confirmation-required). Aliased here to keep the
// PlaceOrdersAsync signature and classifier readable; the public IOrderOperations surface spells the
// same type out in full.
using GroupLegOutcome = OneOf.OneOf<IbkrConduit.Orders.OrderSubmitted, IbkrConduit.Errors.IbkrOrderRejectedError, IbkrConduit.Errors.IbkrAmbiguousOrderError>;
using GroupPlacementOutcome = OneOf.OneOf<System.Collections.Generic.IReadOnlyList<OneOf.OneOf<IbkrConduit.Orders.OrderSubmitted, IbkrConduit.Errors.IbkrOrderRejectedError, IbkrConduit.Errors.IbkrAmbiguousOrderError>>, IbkrConduit.Orders.OrderConfirmationRequired>;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations with caller-controlled question/reply handling.
/// Uses per-account semaphore serialization to prevent concurrent order submissions.
/// </summary>
internal partial class OrderOperations : IOrderOperations, IAsyncDisposable
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
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, object> _logScope;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    /// <summary>
    /// Confirmation rounds whose per-account order lock is retained (ADR-0006, §9.10), keyed by the
    /// current reply id. A placement that returns a confirmation registers one here and does NOT
    /// release the account lock; <see cref="ReplyAsync"/> resolves (or re-keys, on a chained question)
    /// the round, and a <see cref="IbkrClientOptions.ConfirmationTimeout"/> timer bounds retention.
    /// </summary>
    private readonly ConcurrentDictionary<string, PendingConfirmation> _pendingConfirmations = new();

    /// <summary>
    /// Cancels in-flight §10.6 force-clear follow-ups on disposal so a blocked follow-up unwinds
    /// promptly rather than holding up teardown. Scoped to this operations instance — never a caller's
    /// token — because the follow-up outlives the filtered call that spawned it (PVR-18, ORD-2).
    /// </summary>
    private readonly CancellationTokenSource _followUpCts = new();

    /// <summary>Guards <see cref="_followUpTasks"/> and <see cref="_disposed"/>.</summary>
    private readonly object _followUpLock = new();

    /// <summary>
    /// Tracks the background force-clear follow-up tasks so <see cref="DisposeAsync"/> can await or
    /// cancel every one — no fire-and-forget task outlives the operations instance, no exception goes
    /// unobserved. Completed tasks self-remove via a continuation.
    /// </summary>
    private readonly HashSet<Task> _followUpTasks = new();

    /// <summary>Set once disposal begins; blocks new follow-ups from being spawned. Guarded by <see cref="_followUpLock"/>.</summary>
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="OrderOperations"/> instance.
    /// </summary>
    /// <param name="orderApi">The Refit order API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    /// <param name="timeProvider">
    /// Clock used to arm the <see cref="IbkrClientOptions.ConfirmationTimeout"/> that bounds a retained
    /// confirmation round (ADR-0006). Defaults to <see cref="TimeProvider.System"/> when null; tests
    /// inject a fake clock to drive the timeout deterministically.
    /// </param>
    public OrderOperations(
        IIbkrOrderApi orderApi, IbkrClientOptions options, ILogger<OrderOperations> logger,
        TenantContext tenant, TimeProvider? timeProvider = null)
    {
        _orderApi = orderApi;
        _options = options;
        _logger = logger;
        _tenant = tenant;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };
    }

    /// <inheritdoc />
    public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default)
    {
        // Fail fast on caller error before opening a span/lock or touching the wire (§9.7).
        ValidateOrder(order);

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
        var retainLock = false;
        try
        {
            var payload = new OrdersPayload([ToWireModel(order)]);
            var response = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            // ERR-4 / §9.9: place is order-mutating — a 200-with-error classifies as IbkrOrderRejectedError.
            var apiResult = ResultFactory.FromResponse(response, requestPath, orderMutating: true);
            if (!apiResult.IsSuccess)
            {
                var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            var result = ClassifyOrderResponses(apiResult.Value, ResultFactory.GetRawBody(response), requestPath);

            // ADR-0006 §9.10: a placement that returns a confirmation retains the per-account lock until
            // the round resolves (reply/dismiss/timeout) — the finally must NOT release it here.
            retainLock = TryRetainForConfirmation(accountId, result, semaphore);

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
            if (!retainLock)
            {
                semaphore.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<GroupPlacementOutcome>> PlaceOrdersAsync(
        string accountId, IReadOnlyList<OrderRequest> orders, CancellationToken cancellationToken = default)
    {
        // Validate the group shape up front — fail fast on caller error before opening a span/lock.
        ValidateOrderGroup(orders);
        foreach (var order in orders)
        {
            ValidateOrder(order);
        }

        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.PlaceGroup");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("ibkr.order_count", orders.Count);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        var retainLock = false;
        try
        {
            var payload = new OrdersPayload(orders.Select(ToWireModel).ToList());
            var response = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            // ERR-4 / §9.9: a grouped placement is order-mutating too.
            var apiResult = ResultFactory.FromResponse(response, requestPath, orderMutating: true);
            if (!apiResult.IsSuccess)
            {
                // No-array hard rejection (ADR-0008 §9.11): a bare {"error": …} object is caught by
                // ResultFactory's whole-call classification (AMB-4) before it ever reaches the per-leg
                // classifier — there is no per-leg data to break down.
                var failResult = Result<GroupPlacementOutcome>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            // ADR-0008 §9.11: classify each leg of the group's response independently (never collapse the
            // array to a single group-level outcome, never key on array position). A rejected child — even
            // one carrying a real-looking order_id under a terminal status — never surfaces as OrderSubmitted.
            var result = ClassifyGroupResponses(
                apiResult.Value, orders, ResultFactory.GetRawBody(response), requestPath);

            // ADR-0006 §9.10: retain the per-account lock while a group's confirmation round is pending. The
            // confirmation arm (T1) is unchanged by per-leg classification, so reuse the shared retention
            // helper via a confirmation-shaped view of the outcome.
            if (result.IsSuccess && result.Value.IsT1)
            {
                retainLock = TryRetainForConfirmation(
                    accountId,
                    Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Success(result.Value.AsT1),
                    semaphore);
            }

            if (result.IsSuccess)
            {
                // Telemetry stays gated on the outer Result's success — IBKR accepted the request —
                // independent of what each leg's per-leg outcome turned out to be (§9.11).
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
            if (!retainLock)
            {
                semaphore.Release();
            }
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

    /// <summary>
    /// Fail-fast validation of a single order before any wire activity (design doc §9.7). IBKR
    /// requires both <c>trailingAmt</c> and <c>trailingType</c> for TRAIL and TRAILLMT orders ("You
    /// must specify both trailingType and trailingAmt for TRAIL and TRAILLMT order", DOC-03), so a
    /// trailing order missing either is rejected here — before the rate limiter or HTTP is touched —
    /// rather than deferring an unparameterized trailing order to an ambiguous wire refusal.
    /// </summary>
    /// <param name="order">The order to validate.</param>
    private static void ValidateOrder(OrderRequest order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (RequiresTrailingParameters(order.OrderType) &&
            (order.TrailingAmt is null || string.IsNullOrEmpty(order.TrailingType)))
        {
            throw new ArgumentException(
                $"A {order.OrderType} order requires both TrailingAmt and TrailingType (IBKR: " +
                "\"You must specify both trailingType and trailingAmt for TRAIL and TRAILLMT order\").",
                nameof(order));
        }
    }

    /// <summary>
    /// Whether the order type is a trailing type (TRAIL/TRAILLMT) that requires trailing parameters.
    /// Case-insensitive so a case variant of the wire code cannot bypass the trailing fail-fast.
    /// </summary>
    private static bool RequiresTrailingParameters(string orderType) =>
        orderType.Equals("TRAIL", StringComparison.OrdinalIgnoreCase) ||
        orderType.Equals("TRAILLMT", StringComparison.OrdinalIgnoreCase);

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
    public async Task<Result<LiveOrdersSnapshot>> GetLiveOrdersAsync(
        OrderStatusFilter[]? filters = null,
        bool? force = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetLiveOrders");
        activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
        var filtered = filters is { Length: > 0 };
        activity?.SetTag("ibkr.filtered", filtered);
        var response = await _orderApi.GetLiveOrdersAsync(filters, force, cancellationToken);
        var apiResult = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);

        // GAP1-1 / §10.6: carry IBKR's `snapshot` flag through instead of discarding it. IsSnapshot ==
        // false means the cache is unprimed — an empty Orders is NOT an authoritative "no orders".
        var result = apiResult.Map(r =>
            new LiveOrdersSnapshot(r.Orders ?? new List<LiveOrder>(), r.Snapshot ?? false));

        // GAP1-2 / §10.6: a *filtered* live-orders call suppresses `sor` order-detail frames until a
        // force=true follow-up clears IBKR's cached filter behavior. The library owns the quirk: issue
        // that follow-up so a later OrderUpdatesAsync still delivers order details. The follow-up's
        // result is discarded and its failures are logged — the consumer's filtered result is
        // authoritative and unaffected.
        //
        // PVR-18 (ORD-2): the follow-up runs as a BACKGROUND-tracked task, NOT awaited here — the
        // already-computed filtered result returns immediately instead of paying up to a rate-limiter
        // window of latency. It still travels the normal endpoint rate limiter (§8 wait-not-fail) via
        // the same Refit client; it is just decoupled from the caller's result.
        //
        // PVR-18 (ORD-5): the old filters+force exemption is DROPPED. Single-call sufficiency is
        // unpinned in every doc tier (§10.6), so §10.6's defensive posture applies to EVERY filtered
        // call — always follow up, even when the caller already passed force=true.
        if (filtered)
        {
            StartForceClearFollowUp();
        }

        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <summary>
    /// Starts the §10.6 force-clear follow-up as a background-tracked task (PVR-18). The filtered caller
    /// returns immediately; the follow-up runs on its own through the normal endpoint rate limiter, is
    /// logged on failure, and is tracked so <see cref="DisposeAsync"/> awaits or cancels it. Uses the
    /// operations-scoped dispose token — never the caller's — since the caller's request has already
    /// returned and its token may be torn down.
    /// </summary>
    private void StartForceClearFollowUp()
    {
        lock (_followUpLock)
        {
            // Disposal has begun — do not spawn new background work that would outlive teardown.
            if (_disposed)
            {
                return;
            }

            // Capture the token under the lock (where _disposed is false, so _followUpCts is live);
            // DisposeAsync disposes the source only after awaiting every tracked task, so the token
            // stays valid for the task's lifetime.
            var token = _followUpCts.Token;
            var task = Task.Run(() => IssueForceClearFollowUpAsync(token));
            _followUpTasks.Add(task);

            // Self-clean the tracking set on completion so a long-lived instance does not accumulate
            // finished tasks; runs synchronously on the completing thread.
            _ = task.ContinueWith(
                RemoveTrackedFollowUp,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void RemoveTrackedFollowUp(Task task)
    {
        lock (_followUpLock)
        {
            _followUpTasks.Remove(task);
        }
    }

    /// <summary>
    /// Issues the §10.6 <c>force=true</c> follow-up that clears IBKR's cached filter behavior after a
    /// filtered live-orders call, so a subsequent <c>sor</c> subscription still receives order details.
    /// Best-effort: IBKR returns a blank array by design, so the response is discarded; a transport
    /// failure is logged. Cancellation (disposal) is expected and swallowed quietly so teardown stays
    /// clean and no exception goes unobserved.
    /// </summary>
    private async Task IssueForceClearFollowUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogForceClearFollowUp();
            _ = await _orderApi.GetLiveOrdersAsync(null, true, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Disposal cancelled the in-flight follow-up (Refit may wrap the cancellation) — expected
            // on teardown, nothing to surface.
        }
        catch (Exception ex)
        {
            LogForceClearFollowUpFailed(ex);
        }
    }

    /// <summary>
    /// Cancels and awaits any in-flight §10.6 force-clear follow-up so no background task outlives the
    /// operations instance and no exception goes unobserved (PVR-18, ORD-2). Idempotent — the container
    /// owns this singleton and disposes it on provider teardown.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        List<Task> pending;
        List<PendingConfirmation> pendingConfirmations;
        lock (_followUpLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pending = new List<Task>(_followUpTasks);
            pendingConfirmations = new List<PendingConfirmation>(_pendingConfirmations.Values);
            _pendingConfirmations.Clear();
        }

        // Tear down any retained confirmation round (ADR-0006 §9.10): dispose its timer so no callback
        // fires after disposal, and release the held per-account lock so no waiter stays wedged. Guarded
        // by the round's gate + Resolved flag so a racing timeout/reply cannot double-release.
        foreach (var confirmation in pendingConfirmations)
        {
            lock (confirmation.Gate)
            {
                if (!confirmation.Resolved)
                {
                    confirmation.Resolved = true;
                    confirmation.Timer?.Dispose();
                    confirmation.Semaphore.Release();
                }
            }
        }

        // Signal cancellation so an in-flight follow-up's HTTP call unwinds promptly rather than
        // blocking teardown for its full latency.
        await _followUpCts.CancelAsync();

        // Await every tracked follow-up. Each swallows its own outcome (cancellation or logged
        // failure), so Task.WhenAll completes without faulting and teardown stays clean.
        await Task.WhenAll(pending);

        _followUpCts.Dispose();
        GC.SuppressFinalize(this);
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
        // Fail fast on caller error before opening a span/lock or touching the wire (§9.7).
        ValidateOrder(order);

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
        var retainLock = false;
        try
        {
            var payload = new OrdersPayload([ToWireModel(order)]);
            var response = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
            var requestPath = response.RequestMessage?.RequestUri?.AbsolutePath;
            // ERR-4 / §9.9: modify is order-mutating — a 200-with-error classifies as IbkrOrderRejectedError.
            var apiResult = ResultFactory.FromResponse(response, requestPath, orderMutating: true);
            if (!apiResult.IsSuccess)
            {
                var failResult = Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(apiResult.Error);
                return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
            }

            var result = ClassifyOrderResponses(apiResult.Value, ResultFactory.GetRawBody(response), requestPath);

            // ADR-0006 §9.10: a modify that returns a confirmation retains the per-account lock too.
            retainLock = TryRetainForConfirmation(accountId, result, semaphore);

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
            if (!retainLock)
            {
                semaphore.Release();
            }
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

        var result = ClassifyReplyResponse(replyApiResponse, replyId, requestPath);

        // ADR-0006 §9.10: resolve (or, on a chained question, re-key) the retained confirmation round so
        // the per-account order lock releases exactly when the round ends — not while it is still open.
        ResolveConfirmationRound(replyId, result);

        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <summary>
    /// Classifies a reply endpoint response into the order-outcome model (ADR-0006 §9.10). Two rules on
    /// top of the shared order classification: a <b>503 on the reply endpoint</b> is an <em>invalidated
    /// confirmation</em> — the 2026-07-07 probe pinned that the body is fully generic and, critically,
    /// that the "refused" order can still go live — so it surfaces as an <see cref="IbkrAmbiguousOrderError"/>
    /// (reconcile before resubmitting, ADR-0003 family), never a generic/transient 503 and never a
    /// definitive refusal (re-placing would double-submit); and <b>every 2xx reply shape classifies</b>
    /// (ORD-1) — an empty, whitespace, or non-JSON body surfaces as a classified error carrying the raw
    /// body rather than an uncaught exception.
    /// </summary>
    private Result<OneOf<OrderSubmitted, OrderConfirmationRequired>> ClassifyReplyResponse(
        IApiResponse<string> replyApiResponse, string replyId, string? requestPath)
    {
        // ORD-3 / ADR-0006: reply endpoint + 503 (contextual recognition — no wire marker) = ambiguous.
        if (replyApiResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            LogReplyInvalidated(replyId);
            var rawBody = ResultFactory.GetRawBody(replyApiResponse) ?? replyApiResponse.Content;
            return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(
                new IbkrAmbiguousOrderError(
                    System.Net.HttpStatusCode.ServiceUnavailable,
                    $"The reply to order confirmation '{replyId}' failed with 503 (Service Unavailable). " +
                    "The confirmation was invalidated, but the order may still have gone live (2026-07-07 " +
                    "probe). Reconcile via GetLiveOrdersAsync/GetTradesAsync (matching your cOID) before " +
                    "resubmitting — never re-place, which can double-submit.",
                    rawBody,
                    requestPath,
                    ReauthSucceeded: false));
        }

        // AMB-3: route the reply through the same classification every other order path uses —
        // ThrowOnSendFailure, the ADR-0003 ambiguous 401 gate, non-2xx error parsing, and bare-object
        // hidden-error detection. The identity parser yields the raw body; the parsed shapes classify below.
        // ERR-4 / §9.9: reply is order-mutating — a bare-object 200-with-error classifies as an order rejection.
        var bodyResult = ResultFactory.FromResponse(replyApiResponse, static body => body, requestPath, orderMutating: true);
        if (!bodyResult.IsSuccess)
        {
            return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(bodyResult.Error);
        }

        try
        {
            // WIR-4: hardened deserialize; AMB-4: empty/array-error shapes classify as refusals.
            var replyResponses = DeserializeReplyResponse(bodyResult.Value);
            return ClassifyOrderResponses(replyResponses, bodyResult.Value, requestPath);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // ORD-1 / WIR-4: EVERY 2xx reply shape must classify. Empty/whitespace/non-JSON bodies throw
            // InvalidOperationException from DeserializeReplyResponse (and the ClassifyResponse residual);
            // a malformed JSON body throws JsonException. Both surface as a classified error carrying the
            // raw body — never an uncaught exception a consumer cannot map to its ambiguous leg by type.
            return Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>.Failure(
                new IbkrApiError(
                    System.Net.HttpStatusCode.OK,
                    "Reply response body could not be classified",
                    bodyResult.Value,
                    requestPath));
        }
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
    /// Terminal, non-transmitting <c>order_status</c> values (ADR-0008 §9.11): a leg carrying one of these
    /// did NOT transmit, even when it also carries a real-looking <c>order_id</c> (observed on a rejected
    /// bracket's parent row). Compared case-insensitively so a case variant of the wire value cannot bypass
    /// the money-safety rejection check (same rationale as the trailing-parameter fail-fast). Deliberately
    /// open/extensible — wire-observed values only (<c>"Failed"</c>, <c>"Inactive"</c>); an unrecognized
    /// future status is caught by the classifier's defensive fallback (degrades to
    /// <see cref="IbkrAmbiguousOrderError"/>), never silently treated as success.
    /// </summary>
    private static readonly HashSet<string> _terminalNonTransmittingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Failed", "Inactive" };

    /// <summary>
    /// ADR-0008 §9.11: classifies a bracket/OCA group's <see cref="PlaceOrdersAsync"/> response into a
    /// <b>per-leg outcome list</b> (one <see cref="GroupLegOutcome"/> per leg, in wire order) or a
    /// group-wide <see cref="OrderConfirmationRequired"/>. Unlike <see cref="ClassifyOrderResponses"/>
    /// (single-order path, unchanged), this never collapses the response array to a single group-level
    /// outcome and <b>never keys on array position</b> — each row is classified by its own field signature,
    /// so a rejected child (even one carrying a real-looking <c>order_id</c> under a terminal
    /// <c>order_status</c>) never surfaces as an <see cref="OrderSubmitted"/>.
    /// </summary>
    /// <param name="responses">IBKR's deserialized per-leg response array.</param>
    /// <param name="orders">The originally-requested group, used to detect a leg-count shortfall.</param>
    /// <param name="rawBody">The raw response body, carried into every classified outcome.</param>
    /// <param name="requestPath">The order endpoint path, carried into every classified outcome.</param>
    internal static Result<GroupPlacementOutcome> ClassifyGroupResponses(
        IReadOnlyList<OrderSubmissionResponse> responses,
        IReadOnlyList<OrderRequest> orders,
        string? rawBody,
        string? requestPath)
    {
        // Step 1 (AMB-4, unchanged): an empty array is a whole-call refusal carrying the raw body — never
        // an index-out-of-range, and no per-leg data to break down.
        if (responses.Count == 0)
        {
            return Result<GroupPlacementOutcome>.Failure(
                new IbkrOrderRejectedError(
                    "IBKR returned an empty order response with no order id or question.", rawBody, requestPath));
        }

        // Step 2 (unchanged): a single question-shaped element blocks every leg alike — one confirmation
        // for the whole group; there is nothing to break down per-leg yet.
        if (responses.Count == 1)
        {
            var only = responses[0];
            if (only.OrderId is null && only.Id is not null && only.Message is not null)
            {
                return Result<GroupPlacementOutcome>.Success(
                    new OrderConfirmationRequired(
                        only.Id, only.Message.AsReadOnly(), (only.MessageIds ?? []).AsReadOnly()));
            }
        }

        // Step 3: classify every element independently, preserving wire order.
        var legs = new List<GroupLegOutcome>(orders.Count);
        foreach (var element in responses)
        {
            legs.Add(ClassifyLeg(element, rawBody, requestPath));
        }

        // Step 4: leg-count shortfall — a requested leg with no corresponding response entry is ambiguous
        // (sent, outcome unknown; reconcile, never re-place). Known limitation: for a group with more than
        // two legs this cannot identify WHICH requested leg is missing, only that some count is — the wire
        // evidence covers only 2-leg groups (1 parent + 1 child). A future 3+-leg finding may refine this.
        for (var i = responses.Count; i < orders.Count; i++)
        {
            legs.Add(new IbkrAmbiguousOrderError(
                null,
                "This requested order leg had no corresponding entry in IBKR's group response; its outcome " +
                "is unknown. Reconcile via GetLiveOrdersAsync/GetTradesAsync (matching your cOID) before " +
                "resubmitting — never re-place, which can double-submit.",
                rawBody, requestPath, ReauthSucceeded: false));
        }

        return Result<GroupPlacementOutcome>.Success(legs.AsReadOnly());
    }

    /// <summary>
    /// Classifies a single group-response leg by its own field signature (ADR-0008 §9.11), never by its
    /// array position.
    /// </summary>
    private static GroupLegOutcome ClassifyLeg(OrderSubmissionResponse element, string? rawBody, string? requestPath)
    {
        // A leg carrying an explicit error field is a definite rejection (covers an array-wrapped
        // [{"error":"…"}] element that bypasses bare-object hidden-error detection, AMB-4).
        if (element.Error is not null)
        {
            return new IbkrOrderRejectedError(element.Error, rawBody, requestPath);
        }

        // A terminal non-transmitting status is a definite rejection EVEN with a real-looking order_id —
        // the dangerous case ADR-0008 fixes (a rejected parent must never surface as OrderSubmitted just
        // because it carries an order_id). Checked before the order_id branch so the status wins.
        if (element.OrderStatus is not null && _terminalNonTransmittingStatuses.Contains(element.OrderStatus))
        {
            return new IbkrOrderRejectedError(
                $"Order not transmitted (status: {element.OrderStatus}).", rawBody, requestPath);
        }

        // A real order_id with a non-terminal status is a transmitted leg (existing OrderSubmitted mapping).
        if (element.OrderId is not null)
        {
            return new OrderSubmitted(
                element.OrderId, element.OrderStatus ?? string.Empty, element.LocalOrderId, element.OcaGroupId);
        }

        // Defensive (not wire-observed): a row matching none of the above degrades to ambiguous — the safe
        // direction — never a silent OrderSubmitted with empty fields and never an exception.
        return new IbkrAmbiguousOrderError(
            null, "Unrecognized order-leg response shape.", rawBody, requestPath, ReauthSucceeded: false);
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

    /// <summary>
    /// ADR-0006 §9.10: if <paramref name="result"/> is a confirmation-required outcome, retains the
    /// per-account order lock by registering a <see cref="PendingConfirmation"/> keyed by its reply id
    /// and arming the <see cref="IbkrClientOptions.ConfirmationTimeout"/>. Returns <c>true</c> when the
    /// lock is retained (the caller's <c>finally</c> must then NOT release it); <c>false</c> otherwise —
    /// including once disposal has begun, so no timer outlives teardown.
    /// </summary>
    private bool TryRetainForConfirmation(
        string accountId,
        Result<OneOf<OrderSubmitted, OrderConfirmationRequired>> result,
        SemaphoreSlim semaphore)
    {
        if (!result.IsSuccess || !result.Value.IsT1)
        {
            return false;
        }

        var replyId = result.Value.AsT1.ReplyId;
        lock (_followUpLock)
        {
            if (_disposed)
            {
                return false;
            }

            var pending = new PendingConfirmation(accountId, semaphore, replyId);

            // Arm with an infinite due time first so the Timer field is assigned before the callback can
            // ever run (a faked/fast clock could otherwise fire before the assignment), then start it.
            var timer = _timeProvider.CreateTimer(
                OnConfirmationTimeout, pending, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            pending.Timer = timer;
            _pendingConfirmations[replyId] = pending;
            timer.Change(_options.ConfirmationTimeout, Timeout.InfiniteTimeSpan);
            LogConfirmationRetained(accountId, replyId, _options.ConfirmationTimeout);
            return true;
        }
    }

    /// <summary>
    /// Resolves the retained confirmation round for the replied-to <paramref name="replyId"/> based on
    /// the reply's classified <paramref name="result"/>: a reply that returns another question re-keys
    /// the round (the lock stays held until the chain resolves); any terminal outcome — submitted,
    /// refused, ambiguous, or unclassifiable — releases the per-account lock. A reply for which no
    /// in-process round is pending (a stale/cross-process/already-timed-out reply) is a no-op — the
    /// classification stands and no lock is touched.
    /// </summary>
    private void ResolveConfirmationRound(
        string replyId, Result<OneOf<OrderSubmitted, OrderConfirmationRequired>> result)
    {
        if (!_pendingConfirmations.TryGetValue(replyId, out var pending))
        {
            return;
        }

        if (result.IsSuccess && result.Value.IsT1)
        {
            RekeyPendingConfirmation(pending, result.Value.AsT1.ReplyId);
        }
        else
        {
            ResolvePendingConfirmation(pending);
        }
    }

    /// <summary>
    /// Timer callback for an expired confirmation round (ADR-0006 §9.10): releases the retained
    /// per-account lock so the account's order flow is never wedged by a consumer that never replied.
    /// That order's outcome is ambiguous — logged as such. Idempotent against the reply-resolution path
    /// via the round's gate + <see cref="PendingConfirmation.Resolved"/> flag (the semaphore is released
    /// exactly once).
    /// </summary>
    private void OnConfirmationTimeout(object? state)
    {
        var pending = (PendingConfirmation)state!;
        lock (pending.Gate)
        {
            if (pending.Resolved)
            {
                return;
            }

            pending.Resolved = true;
            _pendingConfirmations.TryRemove(
                new KeyValuePair<string, PendingConfirmation>(pending.ReplyId, pending));
            pending.Timer?.Dispose();
            pending.Semaphore.Release();
        }

        LogConfirmationTimedOut(pending.AccountId, pending.ReplyId, _options.ConfirmationTimeout);
    }

    /// <summary>Terminally resolves a round: disposes the timer and releases the held lock exactly once.</summary>
    private void ResolvePendingConfirmation(PendingConfirmation pending)
    {
        lock (pending.Gate)
        {
            if (pending.Resolved)
            {
                return;
            }

            pending.Resolved = true;
            _pendingConfirmations.TryRemove(
                new KeyValuePair<string, PendingConfirmation>(pending.ReplyId, pending));
            pending.Timer?.Dispose();
            pending.Semaphore.Release();
        }
    }

    /// <summary>
    /// Re-keys a round to a chained question's new reply id and restarts the confirmation timeout,
    /// keeping the per-account lock held until the chain resolves. A no-op if the round already resolved
    /// (the timeout fired between the wire reply and here — an extreme edge; the chain is not re-serialized).
    /// </summary>
    private void RekeyPendingConfirmation(PendingConfirmation pending, string newReplyId)
    {
        lock (pending.Gate)
        {
            if (pending.Resolved)
            {
                return;
            }

            _pendingConfirmations.TryRemove(
                new KeyValuePair<string, PendingConfirmation>(pending.ReplyId, pending));
            pending.ReplyId = newReplyId;
            _pendingConfirmations[newReplyId] = pending;
            pending.Timer?.Change(_options.ConfirmationTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private static OrderWireModel ToWireModel(OrderRequest order) =>
        new(order.Conid, order.Side, order.Quantity, order.OrderType,
            order.Price, order.AuxPrice, order.Tif, order.ManualIndicator)
        {
            CustomerOrderId = order.CustomerOrderId,
            ParentId = order.ParentId,
            IsSingleGroup = order.IsSingleGroup,
            OutsideRth = order.OutsideRth,
            ExtOperator = order.ExtOperator,
            TrailingAmt = order.TrailingAmt,
            TrailingType = order.TrailingType,
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "Replying to IBKR order question {ReplyId} with confirmed={Confirmed}")]
    private partial void LogReplyAttempt(string replyId, bool confirmed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IBKR reply raw content: {Content}")]
    private partial void LogReplyRawContent(string content);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Issuing force=true live-orders follow-up to clear IBKR's cached filter behavior (§10.6) after a filtered call")]
    private partial void LogForceClearFollowUp();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The force=true live-orders follow-up (§10.6) failed; IBKR's filter cache may not be cleared and sor order-detail frames may be suppressed until the next unfiltered/forced call")]
    private partial void LogForceClearFollowUpFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retaining per-account order lock for account {AccountId} while confirmation {ReplyId} is pending (ADR-0006 §9.10; releases on reply/dismiss or after {Timeout})")]
    private partial void LogConfirmationRetained(string accountId, string replyId, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order confirmation {ReplyId} for account {AccountId} was not answered within {Timeout}; releasing the per-account order lock — that order's outcome is AMBIGUOUS, reconcile via live-order/trade queries before resubmitting (ADR-0006 §9.10)")]
    private partial void LogConfirmationTimedOut(string accountId, string replyId, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reply to order confirmation {ReplyId} failed with 503 (invalidated confirmation); classifying as an ambiguous order outcome — the order may still have gone live, reconcile before resubmitting (ADR-0006 §9.10)")]
    private partial void LogReplyInvalidated(string replyId);

    /// <summary>
    /// A confirmation round whose per-account order lock is retained (ADR-0006 §9.10) from the placement
    /// that returned the confirmation until the round resolves — a reply/dismiss or the
    /// <see cref="IbkrClientOptions.ConfirmationTimeout"/>. Its <see cref="Gate"/> serializes the
    /// reply-resolution path against the timeout callback so the held semaphore is released exactly once.
    /// </summary>
    private sealed class PendingConfirmation
    {
        public PendingConfirmation(string accountId, SemaphoreSlim semaphore, string replyId)
        {
            AccountId = accountId;
            Semaphore = semaphore;
            ReplyId = replyId;
        }

        /// <summary>Guards <see cref="ReplyId"/>, <see cref="Resolved"/>, and the release of <see cref="Semaphore"/>.</summary>
        public object Gate { get; } = new();

        /// <summary>Account whose order lock this round holds.</summary>
        public string AccountId { get; }

        /// <summary>The retained per-account order semaphore, released exactly once when the round ends.</summary>
        public SemaphoreSlim Semaphore { get; }

        /// <summary>The current reply id (updated when a chained question re-keys the round).</summary>
        public string ReplyId { get; set; }

        /// <summary>The confirmation-timeout timer; disposed when the round resolves.</summary>
        public ITimer? Timer { get; set; }

        /// <summary>Set true by whichever of reply-resolution or timeout resolves the round first.</summary>
        public bool Resolved { get; set; }
    }
}
