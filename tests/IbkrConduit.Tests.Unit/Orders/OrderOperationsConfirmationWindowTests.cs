using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// PVR-06 / ADR-0006 §9.10: the serialized order-confirmation round and the ambiguous invalidated-reply
/// outcome. Covers lock retention across a pending confirmation (a second same-account placement waits;
/// different accounts do not), release on reply/reject/chain/timeout (fake <see cref="TimeProvider"/>),
/// the 503-on-reply → ambiguous classification (ORD-3, probe body verbatim), and the ORD-1 "every 2xx
/// reply shape classifies" net (including DOC-01's five documented reply-200 shapes). All offline.
/// </summary>
public class OrderOperationsConfirmationWindowTests
{
    // The 2026-07-07 live-probe body for a reply on an invalidated confirmation (recordings/
    // order-probe-2026-07-07.log): fully generic, no invalidation marker — recognition is contextual
    // (reply endpoint + 503).
    private const string _probeInvalidatedReplyBody = """{"error":"Service Unavailable","statusCode":503}""";

    private static OrderRequest SampleOrder() => new()
    {
        Conid = 265598,
        Side = "BUY",
        Quantity = 1,
        OrderType = "MKT",
    };

    private static List<OrderSubmissionResponse> Confirmation(string replyId, string messageId) =>
    [
        new OrderSubmissionResponse(replyId, ["Are you sure you want to submit this order?"], false, [messageId], null, null),
    ];

    private static List<OrderSubmissionResponse> Submitted(string orderId) =>
    [
        new OrderSubmissionResponse(null, null, null, null, orderId, "Submitted"),
    ];

    private static OrderOperations NewSut(ConfirmationFakeApi api, TimeProvider time, IbkrClientOptions? options = null) =>
        new(api, options ?? new IbkrClientOptions(), NullLogger<OrderOperations>.Instance, new TenantContext("test"), time);

    /// <summary>True if <paramref name="task"/> completes within <paramref name="window"/>, else false.</summary>
    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan window)
    {
        var completed = await Task.WhenAny(task, Task.Delay(window));
        return ReferenceEquals(completed, task);
    }

    // --- Serialization: a pending confirmation retains the per-account lock ---

    [Fact]
    public async Task PlaceOrderAsync_PendingConfirmation_SerializesSecondSameAccountPlacementUntilReply()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-2"));

        var first = await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        first.Value.IsT1.ShouldBeTrue("first placement returns a confirmation and retains the account lock");

        // The second same-account placement must block on the retained lock — it never reaches the wire.
        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300)))
            .ShouldBeFalse("the second placement must wait for the pending confirmation to resolve");
        api.PlaceCallCount.ShouldBe(1, "the blocked second placement must not reach the wire");

        // Resolving the round via a confirmed reply releases the lock; the second placement then proceeds.
        api.SetReply(HttpStatusCode.OK, """[{"order_id":"order-1","order_status":"Submitted"}]""");
        var reply = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);
        reply.Value.AsT0.OrderId.ShouldBe("order-1");

        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
        api.PlaceCallCount.ShouldBe(2, "the second placement proceeds once the confirmation round resolves");
    }

    [Fact]
    public async Task ReplyAsync_RejectFalse_ResolvesRoundAsRefusalAndReleasesLock()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-2"));

        await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);

        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300))).ShouldBeFalse();

        // A reply that rejects the order (definitive refusal shape) resolves the round and releases the lock.
        api.SetReply(HttpStatusCode.OK, """[{"error":"Order was rejected by the user."}]""");
        var reply = await sut.ReplyAsync("reply-1", false, TestContext.Current.CancellationToken);
        reply.Error.ShouldBeOfType<IbkrOrderRejectedError>();

        (await CompletesWithinAsync(second, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("a definitive refusal resolves the round and releases the account lock");
        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
    }

    [Fact]
    public async Task ReplyAsync_ChainedQuestion_KeepsLockHeldUntilChainResolves()
    {
        // Acceptance: a reply that chains a second question keeps the lock held until the chain resolves.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-2"));

        await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);

        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300))).ShouldBeFalse();

        // Reply to reply-1 chains a SECOND question (reply-2). The round must stay open — lock held.
        api.SetReply(HttpStatusCode.OK, """[{"id":"reply-2","message":["Please confirm again."],"messageIds":["o355"]}]""");
        var chained = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);
        chained.Value.IsT1.ShouldBeTrue("a chained reply returns another confirmation");
        chained.Value.AsT1.ReplyId.ShouldBe("reply-2");

        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300)))
            .ShouldBeFalse("the lock must stay held while the confirmation chain is open");

        // Resolving the chained question releases the lock; the second placement then proceeds.
        api.SetReply(HttpStatusCode.OK, """[{"order_id":"order-1","order_status":"Submitted"}]""");
        var resolved = await sut.ReplyAsync("reply-2", true, TestContext.Current.CancellationToken);
        resolved.Value.AsT0.OrderId.ShouldBe("order-1");

        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
    }

    [Fact]
    public async Task ConfirmationTimeout_Elapses_ReleasesLockAndSecondPlacementProceeds()
    {
        // Acceptance: a consumer that never replies — the account's next placement proceeds after
        // ConfirmationTimeout (fake clock), with the abandoned order's outcome ambiguous.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        var options = new IbkrClientOptions { ConfirmationTimeout = TimeSpan.FromSeconds(30) };
        await using var sut = NewSut(api, time, options);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-2"));

        await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);

        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300)))
            .ShouldBeFalse("the second placement waits while the confirmation is pending");

        // Advancing past the confirmation timeout releases the lock even though no reply arrived.
        time.Advance(TimeSpan.FromSeconds(30));

        (await CompletesWithinAsync(second, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("the confirmation timeout releases the lock so the account is not wedged");
        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
    }

    [Fact]
    public async Task PlaceOrderAsync_PendingConfirmationOnOneAccount_DoesNotBlockAnotherAccount()
    {
        // The confirmation lock is per-account, not global: a pending confirmation on ACCT1 must not
        // serialize a placement on ACCT2.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-acct2"));

        await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);

        var other = sut.PlaceOrderAsync("ACCT2", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(other, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("a different account must not be serialized against ACCT1's pending confirmation");
        (await other).Value.AsT0.OrderId.ShouldBe("order-acct2");
    }

    [Fact]
    public async Task ModifyOrderAsync_PendingConfirmation_RetainsLockToo()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354")); // modify returns a confirmation
        api.PlaceResponses.Enqueue(Submitted("order-2"));            // subsequent same-account placement

        var modify = await sut.ModifyOrderAsync("ACCT1", "473740665", SampleOrder(), TestContext.Current.CancellationToken);
        modify.Value.IsT1.ShouldBeTrue();

        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300)))
            .ShouldBeFalse("a modify that returns a confirmation retains the per-account lock");

        api.SetReply(HttpStatusCode.OK, """[{"order_id":"order-1","order_status":"Submitted"}]""");
        await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
    }

    // --- ORD-3: 503 on reply → ambiguous outcome (reconcile, never re-place) ---

    [Fact]
    public async Task ReplyAsync_ServiceUnavailable_ReturnsAmbiguousOrderError_WithReconcileGuidance()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.ServiceUnavailable, _probeInvalidatedReplyBody);

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var ambiguous = result.Error.ShouldBeOfType<IbkrAmbiguousOrderError>();
        ambiguous.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        ambiguous.Message.ShouldContain("reply-1", Case.Sensitive);
        ambiguous.Message.ShouldContain("Reconcile");
        ambiguous.Message.ShouldContain("never re-place");
        ambiguous.RawBody.ShouldBe(_probeInvalidatedReplyBody, "the raw probe body must survive to the error");
    }

    [Fact]
    public async Task ReplyAsync_ServiceUnavailableUnderThrowOnApiError_ThrowsAmbiguousButStillReleasesLock()
    {
        // PVR-06: under ThrowOnApiError=true a 503-invalidated reply must surface the ambiguous outcome by
        // THROWING IbkrApiException(IbkrAmbiguousOrderError) — not by mis-ordering the teardown. Critically,
        // the confirmation round is resolved (per-account lock released) BEFORE the throw propagates, so a
        // blocked same-account placement is not wedged behind a throwing reply. This is the reply-ordering
        // guarantee the ThrowOnApiError=false 503 tests do not cover.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        var options = new IbkrClientOptions { ThrowOnApiError = true };
        await using var sut = NewSut(api, time, options);

        // A placement that returns a confirmation retains the per-account lock; the follow-up placement
        // resolves once the round ends.
        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        api.PlaceResponses.Enqueue(Submitted("order-2"));

        var first = await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        first.Value.IsT1.ShouldBeTrue("a confirmation is a success outcome even under ThrowOnApiError");

        var second = sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);
        (await CompletesWithinAsync(second, TimeSpan.FromMilliseconds(300)))
            .ShouldBeFalse("the second placement waits on the retained confirmation lock");

        // The reply 503s (invalidated confirmation). Under ThrowOnApiError it throws the ambiguous error.
        api.SetReply(HttpStatusCode.ServiceUnavailable, _probeInvalidatedReplyBody);
        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken));
        var ambiguous = ex.Error.ShouldBeOfType<IbkrAmbiguousOrderError>();
        ambiguous.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        // The round resolved and released the lock even though the reply threw — reply-ordering preserved.
        (await CompletesWithinAsync(second, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("the confirmation round must release the account lock even when the reply throws");
        (await second).Value.AsT0.OrderId.ShouldBe("order-2");
    }

    [Fact]
    public async Task ReplyAsync_AfterConfirmationTimeout_ReturnsAmbiguousOrderError()
    {
        // Acceptance: a reply after timeout classifies identically to the in-window invalidated reply.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        var options = new IbkrClientOptions { ConfirmationTimeout = TimeSpan.FromSeconds(10) };
        await using var sut = NewSut(api, time, options);

        api.PlaceResponses.Enqueue(Confirmation("reply-1", "o354"));
        await sut.PlaceOrderAsync("ACCT1", SampleOrder(), TestContext.Current.CancellationToken);

        // The confirmation times out (lock released, pending round removed) before the consumer replies.
        time.Advance(TimeSpan.FromSeconds(10));

        // The late reply hits the wire and 503s (the confirmation was invalidated) → ambiguous.
        api.SetReply(HttpStatusCode.ServiceUnavailable, _probeInvalidatedReplyBody);
        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<IbkrAmbiguousOrderError>().StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    // --- ORD-1: every 2xx reply shape classifies (no raw exception escapes) ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>Service temporarily unavailable</body></html>")]
    public async Task ReplyAsync_Unclassifiable2xxBody_ClassifiesWithRawBody_NoException(string body)
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.OK, body);

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
        error.RawBody.ShouldBe(body, "the raw 2xx body must be attached to the classified error");
    }

    // --- DOC-01's five documented reply-200 shapes ---

    [Fact]
    public async Task ReplyAsync_OrderSubmitSuccessShape_ResolvesAsSubmitted()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.OK, """[{"order_id":"123","order_status":"Submitted"}]""");

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("123");
    }

    [Fact]
    public async Task ReplyAsync_ChainedOrderReplyMessageShape_ReturnsConfirmation()
    {
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.OK, """[{"id":"reply-2","message":["Confirm again?"],"messageIds":["o355"]}]""");

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.Value.AsT1.ReplyId.ShouldBe("reply-2");
    }

    [Fact]
    public async Task ReplyAsync_OrderSubmitErrorShape_ClassifiesAsError()
    {
        // DOC-01 orderSubmitError example: {"error":"Order not confirmed "}.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.OK, """{"error":"Order not confirmed "}""");

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("Order not confirmed");
    }

    [Fact]
    public async Task ReplyAsync_OrderReplyNotFoundShape_ClassifiesAsError()
    {
        // DOC-01 orderReplyNotFound example: {"error":"reply id not found: '…'"}.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        api.SetReply(HttpStatusCode.OK, """{"error":"reply id not found: 'reply-1'"}""");

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("reply id not found");
    }

    [Fact]
    public async Task ReplyAsync_AdvancedOrderRejectShape_ClassifiesAsErrorNotThrow()
    {
        // DOC-01 advancedOrderReject shape: {orderId, reqId, dismissable, text, options[], type, ...}.
        // None of its fields map to the order-submission/confirmation model, so it must classify as an
        // error carrying the raw body — never escape as a raw InvalidOperationException.
        var api = new ConfirmationFakeApi();
        var time = new FakeTimeProvider();
        await using var sut = NewSut(api, time);

        const string body =
            """{"orderId":123,"reqId":"1","dismissable":true,"text":"Reject reason","options":["Yes","No"],"type":"AOR","messageId":"o999","prompt":true}""";
        api.SetReply(HttpStatusCode.OK, body);

        var result = await sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
        error.RawBody.ShouldBe(body);
    }

    private sealed class ConfirmationFakeApi : IIbkrOrderApi
    {
        private int _placeCallCount;

        public ConcurrentQueue<List<OrderSubmissionResponse>> PlaceResponses { get; } = new();
        public HttpStatusCode ReplyStatus { get; private set; } = HttpStatusCode.OK;
        public string ReplyBody { get; private set; } = "[]";
        public int PlaceCallCount => Volatile.Read(ref _placeCallCount);

        public void SetReply(HttpStatusCode status, string body)
        {
            ReplyStatus = status;
            ReplyBody = body;
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _placeCallCount);
            PlaceResponses.TryDequeue(out var response);
            return Task.FromResult(FakeApiResponse.Success(response!));
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _placeCallCount);
            PlaceResponses.TryDequeue(out var response);
            return Task.FromResult(FakeApiResponse.Success(response!));
        }

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default)
        {
            var http = new HttpResponseMessage(ReplyStatus)
            {
                Content = new StringContent(ReplyBody),
                RequestMessage = new HttpRequestMessage(
                    HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/reply/" + replyId),
            };

            // Carry the raw body on the ApiResponse regardless of status so the classifier can attach it
            // (mirrors ResponseBodyCaptureHandler priming the raw body for all status codes in production).
            IApiResponse<string> apiResponse = new ApiResponse<string>(http, ReplyBody, new RefitSettings());
            return Task.FromResult(apiResponse);
        }

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, long? manualCancelTime = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<string>> DismissNotificationAsync(DismissNotificationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
