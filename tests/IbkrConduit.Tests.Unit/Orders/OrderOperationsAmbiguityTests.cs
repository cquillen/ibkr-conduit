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
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// VCR-04: order-outcome classification through the facade. Covers the AMB-3 (reply routes through
/// hidden-error detection), AMB-4 (array-wrapped / empty 200 shapes classify as refusals), and WIR-4
/// (numeric order_id, 2xx-unparseable → classified error) guarantees. The 401 replay gate (AMB-2) is
/// exercised end-to-end in the integration suite (WireMock) and at the handler level in
/// <c>TokenRefreshHandlerTests</c>.
/// </summary>
public class OrderOperationsAmbiguityTests
{
    private readonly ConfigurableOrderApi _fakeApi = new();
    private readonly OrderOperations _sut;

    public OrderOperationsAmbiguityTests()
    {
        _sut = new OrderOperations(_fakeApi, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance, new TenantContext("test"));
    }

    private static OrderRequest SampleOrder() => new()
    {
        Conid = 265598,
        Side = "BUY",
        Quantity = 1,
        OrderType = "MKT",
    };

    // --- AMB-4: place/modify unrecognized 200 shapes classify as refusals, never throw ---

    [Fact]
    public async Task PlaceOrderAsync_ArrayWrappedErrorBody_ReturnsClassifiedRefusal()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, null, null)
            {
                Error = "We cannot accept an order at the limit price you selected.",
            },
        ]);

        var result = await _sut.PlaceOrderAsync("DU1234567", SampleOrder(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var rejected = result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldContain("cannot accept an order");
    }

    [Fact]
    public async Task PlaceOrderAsync_EmptyArray_ReturnsClassifiedRefusalNotThrow()
    {
        _fakeApi.PlaceOrderResponses.Enqueue([]);

        var result = await _sut.PlaceOrderAsync("DU1234567", SampleOrder(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
    }

    [Fact]
    public async Task ModifyOrderAsync_ArrayWrappedErrorBody_ReturnsClassifiedRefusal()
    {
        _fakeApi.ModifyOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, null, null) { Error = "Order rejected." },
        ]);

        var result = await _sut.ModifyOrderAsync("DU1234567", "473740665", SampleOrder(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>().RejectionMessage.ShouldBe("Order rejected.");
    }

    // --- AMB-3 / ERR-4: reply 2xx routes through order-mutating hidden-error detection ---

    [Fact]
    public async Task ReplyAsync_BareObjectErrorBody_ReturnsOrderRejectedError()
    {
        // ERR-4 / §9.9 (breaking-behavioral): a bare-object 200-with-error on the reply endpoint (an
        // order-mutating surface) now classifies as the order-rejection subtype, NOT the generic
        // hidden-error subtype which is reserved for non-order surfaces.
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = """{"error":"We cannot accept an order at the limit price you selected."}""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var rejected = result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldContain("cannot accept an order");
    }

    [Fact]
    public async Task ReplyAsync_ArrayWrappedErrorBody_ReturnsClassifiedRefusal()
    {
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = """[{"error":"Order rejected by broker."}]""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>().RejectionMessage.ShouldBe("Order rejected by broker.");
    }

    [Fact]
    public async Task ReplyAsync_EmptyArray_ReturnsClassifiedRefusalNotThrow()
    {
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = "[]";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
    }

    [Fact]
    public async Task ReplyAsync_NumericOrderId_ReturnsOrderSubmitted()
    {
        // WIR-4 on the reply path.
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = """[{"order_id":987654321,"order_status":"Submitted"}]""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AsT0.OrderId.ShouldBe("987654321");
    }

    [Fact]
    public async Task ReplyAsync_Non2xxNon503_ReturnsClassifiedApiError()
    {
        // A non-2xx reply that is NOT a 503 stays a generic classified IbkrApiError — only the reply
        // endpoint's 503 (an invalidated confirmation) reclassifies to ambiguous (ADR-0006 §9.10).
        _fakeApi.ReplyStatus = HttpStatusCode.InternalServerError;
        _fakeApi.ReplyBody = """{"error":"timeout"}""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrApiError>().StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ReplyAsync_503_ReturnsAmbiguousOrderError()
    {
        // ADR-0006 §9.10 (breaking-behavioral): a 503 on the reply endpoint is an invalidated
        // confirmation — the order may still have gone live — so it surfaces as an ambiguous outcome,
        // NOT a generic/transient IbkrApiError(503) a consumer might blindly retry.
        _fakeApi.ReplyStatus = HttpStatusCode.ServiceUnavailable;
        _fakeApi.ReplyBody = """{"error":"Service Unavailable","statusCode":503}""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrAmbiguousOrderError>()
            .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ReplyAsync_SuccessBody_ReturnsOrderSubmitted()
    {
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = """[{"order_id":"111","order_status":"PreSubmitted"}]""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AsT0.OrderId.ShouldBe("111");
    }

    [Fact]
    public async Task ReplyAsync_UnparseableJsonBody_ReturnsClassifiedErrorNotThrow()
    {
        // WIR-4: a 2xx body that still fails typed deserialization (order_id as an object) surfaces as a
        // classified error, never an uncaught JsonException.
        _fakeApi.ReplyStatus = HttpStatusCode.OK;
        _fakeApi.ReplyBody = """[{"order_id":{"unexpected":true}}]""";

        var result = await _sut.ReplyAsync("reply-1", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrApiError>().StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class ConfigurableOrderApi : IIbkrOrderApi
    {
        public Queue<List<OrderSubmissionResponse>> PlaceOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ModifyOrderResponses { get; } = new();
        public HttpStatusCode ReplyStatus { get; set; } = HttpStatusCode.OK;
        public string ReplyBody { get; set; } = "[]";

        public Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(PlaceOrderResponses.Dequeue()));

        public Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(ModifyOrderResponses.Dequeue()));

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default)
        {
            var http = new HttpResponseMessage(ReplyStatus)
            {
                Content = new StringContent(ReplyBody),
                RequestMessage = new HttpRequestMessage(
                    HttpMethod.Post, "https://api.ibkr.com/v1/api/iserver/reply/" + replyId),
            };

            // Mirror Refit's shape: Content carries the raw body on success; on a non-2xx it is null
            // and the pipeline reads the status. (FakeApiResponse.Failure models the same.)
            var content = ReplyStatus == HttpStatusCode.OK ? ReplyBody : null;
            IApiResponse<string> apiResponse = new ApiResponse<string>(http, content, new RefitSettings());
            return Task.FromResult(apiResponse);
        }

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, long? manualCancelTime = null, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<string>> DismissNotificationAsync(DismissNotificationRequest request, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
    }
}
