using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.Orders;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

public class OrderOperationsTests
{
    private readonly FakeOrderApi _fakeApi = new();
    private readonly OrderOperations _sut;

    public OrderOperationsTests()
    {
        _sut = new OrderOperations(_fakeApi, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance, new TenantContext("test"));
    }

    [Fact]
    public async Task PlaceOrderAsync_DirectConfirmation_ReturnsOrderSubmitted()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "12345", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 100,
            OrderType = "MKT",
            Tif = "DAY",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("12345");
        result.Value.AsT0.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrderAsync_WithQuestionResponse_ReturnsOrderConfirmationRequired()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                ["Are you sure you want to submit this order?"],
                false,
                ["msg-id-1"],
                null,
                null),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 50,
            OrderType = "LMT",
            Price = 150.00m,
            Tif = "GTC",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);
        var confirmation = result.Value.AsT1;
        confirmation.ReplyId.ShouldBe("reply-id-1");
        confirmation.Messages.ShouldContain("Are you sure you want to submit this order?");
        confirmation.MessageIds.ShouldContain("msg-id-1");
    }

    [Fact]
    public async Task PlaceOrderAsync_UnexpectedResponse_ThrowsInvalidOperationException()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, null, null),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Unexpected order submission response");
    }

    [Fact]
    public async Task PlaceOrderAsync_ConvertsOrderRequestToWireModel()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "111", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 100,
            OrderType = "LMT",
            Price = 150.50m,
            AuxPrice = 149.00m,
            Tif = "GTC",
            ManualIndicator = false,
            ExtOperator = "person1234",
        };

        await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        var payload = _fakeApi.LastPlaceOrderPayload;
        payload.ShouldNotBeNull();
        payload.Orders.Count.ShouldBe(1);

        var wire = payload.Orders[0];
        wire.Conid.ShouldBe(265598);
        wire.Side.ShouldBe("BUY");
        wire.Quantity.ShouldBe(100m);
        wire.OrderType.ShouldBe("LMT");
        wire.Price.ShouldBe(150.50m);
        wire.AuxPrice.ShouldBe(149.00m);
        wire.Tif.ShouldBe("GTC");
        wire.ManualIndicator.ShouldBe(false);
        wire.ExtOperator.ShouldBe("person1234");
    }

    [Fact]
    public async Task PlaceOrderAsync_ExtOperatorSetAlone_PassesThroughWithoutValidation()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "111", "PreSubmitted"),
        ]);

        // ExtOperator is a pure pass-through: setting it requires no companion field (no
        // ManualIndicator) and must trigger no TRAIL-style fail-fast validation.
        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
            ExtOperator = "person1234",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var wire = _fakeApi.LastPlaceOrderPayload!.Orders[0];
        wire.ExtOperator.ShouldBe("person1234");
        wire.ManualIndicator.ShouldBeNull();
    }

    [Fact]
    public async Task PlaceOrderAsync_TrailOrderWithBothParams_PlacesSuccessfully()
    {
        // Probe-pinned happy path (2026-07-07): trailingAmt:50, trailingType:"amt" -> PreSubmitted.
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "261920143", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "TRAIL",
            Tif = "GTC",
            TrailingAmt = 50m,
            TrailingType = "amt",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("261920143");
        var wire = _fakeApi.LastPlaceOrderPayload!.Orders[0];
        wire.TrailingAmt.ShouldBe(50m);
        wire.TrailingType.ShouldBe("amt");
    }

    [Fact]
    public async Task PlaceOrderAsync_TrailOrderMissingTrailingAmt_ThrowsBeforeWire()
    {
        // No response enqueued: if the wire were reached the fake would record a payload — so a null
        // LastPlaceOrderPayload proves the throw preceded any wire activity.
        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "TRAIL",
            Tif = "GTC",
            TrailingType = "amt",
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken));

        _fakeApi.LastPlaceOrderPayload.ShouldBeNull("validation must fail before any wire activity");
    }

    [Fact]
    public async Task PlaceOrderAsync_TrailOrderMissingTrailingType_ThrowsBeforeWire()
    {
        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "TRAIL",
            Tif = "GTC",
            TrailingAmt = 50m,
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken));

        _fakeApi.LastPlaceOrderPayload.ShouldBeNull("validation must fail before any wire activity");
    }

    [Fact]
    public async Task PlaceOrderAsync_TrailLmtOrderMissingBothParams_ThrowsBeforeWire()
    {
        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "TRAILLMT",
            Price = 150.00m,
            AuxPrice = 149.00m,
            Tif = "GTC",
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken));

        _fakeApi.LastPlaceOrderPayload.ShouldBeNull("validation must fail before any wire activity");
    }

    [Fact]
    public async Task PlaceOrderAsync_NonTrailingOrderWithNullTrailingParams_Succeeds()
    {
        // A non-trailing order type must not trip the trailing fail-fast (no false positive).
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "111", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 150.00m,
            Tif = "GTC",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var wire = _fakeApi.LastPlaceOrderPayload!.Orders[0];
        wire.TrailingAmt.ShouldBeNull();
        wire.TrailingType.ShouldBeNull();
    }

    [Fact]
    public async Task PlaceOrdersAsync_TrailChildMissingParams_ThrowsBeforeWire()
    {
        // Fail-fast applies per-leg in a group submission too, before any wire activity.
        var parent = new OrderRequest { Conid = 756733, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "Parent" };
        var trailChild = new OrderRequest { Conid = 756733, Side = "SELL", Quantity = 1, OrderType = "TRAIL", Tif = "GTC", ParentId = "Parent" };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrdersAsync("DU1234567", [parent, trailChild], TestContext.Current.CancellationToken));

        _fakeApi.LastPlaceOrderPayload.ShouldBeNull("validation must fail before any wire activity");
    }

    [Fact]
    public async Task PlaceOrderAsync_SerializesPerAccount()
    {
        var callOrder = new List<string>();
        var semaphore1 = new SemaphoreSlim(0, 1);
        var semaphore2 = new SemaphoreSlim(0, 1);

        var api = new BlockingOrderApi(callOrder, semaphore1, semaphore2);
        var ops = new OrderOperations(api, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance, new TenantContext("test"));

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var task1 = ops.PlaceOrderAsync("ACCT1", order, TestContext.Current.CancellationToken);
        var task2 = ops.PlaceOrderAsync("ACCT1", order, TestContext.Current.CancellationToken);

        // Allow first call to complete
        semaphore1.Release();
        await task1;

        // Allow second call to complete
        semaphore2.Release();
        await task2;

        callOrder.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PlaceOrderAsync_EmptyMessageArray_ReturnsOrderConfirmationRequired()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                [],
                false,
                null,
                null,
                null),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        var confirmation = result.Value.AsT1;
        confirmation.ReplyId.ShouldBe("reply-id-1");
        confirmation.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceOrderAsync_OrderIdPresent_IgnoresMessageField()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                ["Some question"],
                false,
                ["msg-id-1"],
                "77777",
                "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("77777");
        result.Value.AsT0.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrderAsync_MessagePresentButIdNull_ThrowsInvalidOperation()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                null,
                ["Some question"],
                false,
                ["msg-id-1"],
                null,
                null),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Unexpected");
    }

    [Fact]
    public async Task PlaceOrderAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "12345", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.PlaceOrderAsync("DU1234567", order, cts.Token));
    }

    [Fact]
    public async Task CancelOrderAsync_DelegatesToApi()
    {
        _fakeApi.CancelResponse = new CancelOrderResponse("Order cancelled", 12345, 265598);

        var result = await _sut.CancelOrderAsync("DU1234567", "12345", cancellationToken: TestContext.Current.CancellationToken);

        result.Value.Message.ShouldBe("Order cancelled");
        result.Value.OrderId.ShouldBe(12345);
    }

    [Fact]
    public async Task GetLiveOrdersAsync_ReturnsOrdersList()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse(
        [
            new LiveOrder("DU1234567", 265598, "265598", 111, "AAPL", "STK", "NASDAQ", "BUY", "PreSubmitted", "PreSubmitted", "LMT", 0, 100, 100, "APPLE INC", null, "DAY", null),
        ], Snapshot: true);

        var result = await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.Orders.Count.ShouldBe(1);
        result.Value.Orders[0].OrderId.ShouldBe(111);
    }

    [Fact]
    public async Task GetLiveOrdersAsync_NullOrders_ReturnsEmptyList()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse(null, Snapshot: true);

        var result = await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.Orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLiveOrdersAsync_UnprimedResponse_SurfacesIsSnapshotFalse()
    {
        // GAP1-1: snapshot:false must surface — an empty Orders here is unprimed, NOT "no orders".
        _fakeApi.LiveOrdersResponse = new OrdersResponse([], Snapshot: false);

        var result = await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.IsSnapshot.ShouldBeFalse();
        result.Value.Orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLiveOrdersAsync_PrimedResponse_SurfacesIsSnapshotTrue()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse(
        [
            new LiveOrder("DU1234567", 265598, "265598", 111, "AAPL", "STK", "NASDAQ", "BUY", "PreSubmitted", "PreSubmitted", "LMT", 0, 100, 100, "APPLE INC", null, "DAY", null),
        ], Snapshot: true);

        var result = await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.IsSnapshot.ShouldBeTrue();
        result.Value.Orders.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveOrdersAsync_SnapshotFlagAbsent_MapsToIsSnapshotFalse()
    {
        // GAP1-1: an absent `snapshot` flag maps to false (unprimed) — never a fabricated true.
        _fakeApi.LiveOrdersResponse = new OrdersResponse([], Snapshot: null);

        var result = await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.IsSnapshot.ShouldBeFalse();
    }

    [Fact]
    public async Task GetLiveOrdersAsync_WithFilters_IssuesExactlyOneForceFollowUp()
    {
        // GAP1-2 / §10.6: a filtered call is followed by exactly one force=true follow-up (no filters).
        _fakeApi.LiveOrdersResponse = new OrdersResponse([], Snapshot: false);

        await _sut.GetLiveOrdersAsync([OrderStatusFilter.Cancelled], cancellationToken: TestContext.Current.CancellationToken);

        _fakeApi.LiveOrdersCalls.Count.ShouldBe(2);
        _fakeApi.LiveOrdersCalls[0].Filters.ShouldBe(new[] { OrderStatusFilter.Cancelled });
        _fakeApi.LiveOrdersCalls[0].Force.ShouldBeNull();
        _fakeApi.LiveOrdersCalls[1].Filters.ShouldBeNull();
        _fakeApi.LiveOrdersCalls[1].Force.ShouldBe(true);
    }

    [Fact]
    public async Task GetLiveOrdersAsync_Unfiltered_IssuesNoFollowUp()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse([], Snapshot: true);

        await _sut.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        _fakeApi.LiveOrdersCalls.Count.ShouldBe(1);
        _fakeApi.LiveOrdersCalls[0].Force.ShouldBeNull();
    }

    [Fact]
    public async Task GetLiveOrdersAsync_FilteredAndAlreadyForced_IssuesNoFollowUp()
    {
        // The caller's own force=true already clears the cache — no additional follow-up.
        _fakeApi.LiveOrdersResponse = new OrdersResponse([], Snapshot: false);

        await _sut.GetLiveOrdersAsync([OrderStatusFilter.Cancelled], force: true, cancellationToken: TestContext.Current.CancellationToken);

        _fakeApi.LiveOrdersCalls.Count.ShouldBe(1);
        _fakeApi.LiveOrdersCalls[0].Force.ShouldBe(true);
    }

    [Fact]
    public async Task GetTradesAsync_DelegatesToApi()
    {
        _fakeApi.TradesResponse =
        [
            new Trade("exec-1", 265598, "AAPL", "BUY", 100, 150.00m, "ref-1", "user1"),
        ];

        var result = await _sut.GetTradesAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Value.Count.ShouldBe(1);
        result.Value[0].ExecutionId.ShouldBe("exec-1");
    }

    private class FakeOrderApi : IIbkrOrderApi
    {
        public Queue<List<OrderSubmissionResponse>> PlaceOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ModifyOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ReplyResponses { get; } = new();
        public CancelOrderResponse? CancelResponse { get; set; }
        public OrdersResponse LiveOrdersResponse { get; set; } = new(null);
        public List<(OrderStatusFilter[]? Filters, bool? Force)> LiveOrdersCalls { get; } = new();
        public List<Trade>? TradesResponse { get; set; }
        public WhatIfResponse? WhatIfResponse { get; set; }
        public OrderStatus? OrderStatusResponse { get; set; }
        public OrdersPayload? LastPlaceOrderPayload { get; private set; }
        public OrdersPayload? LastModifyOrderPayload { get; private set; }
        public OrdersPayload? LastWhatIfPayload { get; private set; }
        public string? LastModifyOrderId { get; private set; }
        public ReplyRequest? LastReplyRequest { get; private set; }
        public int ReplyCallCount { get; private set; }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastPlaceOrderPayload = orders;
            return Task.FromResult(FakeApiResponse.Success(PlaceOrderResponses.Dequeue()));
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastModifyOrderPayload = orders;
            LastModifyOrderId = orderId;
            return Task.FromResult(FakeApiResponse.Success(ModifyOrderResponses.Dequeue()));
        }

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default)
        {
            LastReplyRequest = request;
            ReplyCallCount++;
            var items = ReplyResponses.Dequeue();
            var json = JsonSerializer.Serialize(items);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            };
            IApiResponse<string> apiResponse = new ApiResponse<string>(httpResponse, json, new RefitSettings());
            return Task.FromResult(apiResponse);
        }

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, long? manualCancelTime = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(CancelResponse!));

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken cancellationToken = default)
        {
            LiveOrdersCalls.Add((filters, force));
            return Task.FromResult(FakeApiResponse.Success(LiveOrdersResponse));
        }

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(TradesResponse!));

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastWhatIfPayload = orders;
            return Task.FromResult(FakeApiResponse.Success(WhatIfResponse!));
        }

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(OrderStatusResponse!));

        public Task<IApiResponse<string>> DismissNotificationAsync(DismissNotificationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task PlaceOrdersAsync_BracketGroup_ReturnsParentResultAndSendsLinkage()
    {
        // A grouped submission returns a single parent element (verified live).
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "parent-1", "PreSubmitted"),
        ]);

        var parent = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 50, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "Parent" };
        var takeProfit = new OrderRequest { Conid = 265598, Side = "SELL", Quantity = 50, OrderType = "LMT", Price = 157.00m, Tif = "GTC", ParentId = "Parent" };
        var stop = new OrderRequest { Conid = 265598, Side = "SELL", Quantity = 50, OrderType = "STP", Price = 150.00m, Tif = "GTC", ParentId = "Parent" };

        var result = await _sut.PlaceOrdersAsync("DU1234567", [parent, takeProfit, stop], TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("parent-1");

        var payload = _fakeApi.LastPlaceOrderPayload;
        payload.ShouldNotBeNull();
        payload.Orders.Count.ShouldBe(3);
        payload.Orders[0].CustomerOrderId.ShouldBe("Parent");
        payload.Orders[1].ParentId.ShouldBe("Parent");
        payload.Orders[2].ParentId.ShouldBe("Parent");
    }

    [Fact]
    public async Task PlaceOrdersAsync_OcaGroup_ReturnsParentResult()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "leg-a", "PreSubmitted"),
        ]);

        var a = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "A", IsSingleGroup = true };
        var b = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.01m, Tif = "GTC", CustomerOrderId = "B", IsSingleGroup = true };

        var result = await _sut.PlaceOrdersAsync("DU1234567", [a, b], TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("leg-a");
    }

    [Fact]
    public async Task PlaceOrdersAsync_ConfirmationRequired_ReturnsConfirmation()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse("reply-1", ["Confirm?"], false, ["o163"], null, null),
        ]);

        var parent = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "Parent" };
        var child = new OrderRequest { Conid = 265598, Side = "SELL", Quantity = 1, OrderType = "LMT", Price = 2.00m, Tif = "GTC", ParentId = "Parent" };

        var result = await _sut.PlaceOrdersAsync("DU1234567", [parent, child], TestContext.Current.CancellationToken);

        result.Value.AsT1.ReplyId.ShouldBe("reply-1");
    }

    [Fact]
    public async Task PlaceOrdersAsync_UnrelatedOrders_ThrowsArgumentException()
    {
        // IBKR rejects unrelated bulk (400); the library pre-empts it with a clear error.
        var a = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC" };
        var b = new OrderRequest { Conid = 756733, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC" };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrdersAsync("DU1234567", [a, b], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PlaceOrdersAsync_BracketChildParentIdMismatch_ThrowsArgumentException()
    {
        var parent = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "Parent" };
        var child = new OrderRequest { Conid = 265598, Side = "SELL", Quantity = 1, OrderType = "LMT", Price = 2.00m, Tif = "GTC", ParentId = "WrongParent" };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrdersAsync("DU1234567", [parent, child], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PlaceOrdersAsync_OcaGroup_SurfacesLocalOrderIdAndOcaGroupId()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "leg-a", "PreSubmitted", "A", "oco-636441077"),
        ]);

        var a = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC", CustomerOrderId = "A", IsSingleGroup = true };
        var b = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.01m, Tif = "GTC", CustomerOrderId = "B", IsSingleGroup = true };

        var submitted = (await _sut.PlaceOrdersAsync("DU1234567", [a, b], TestContext.Current.CancellationToken)).Value.AsT0;

        submitted.OrderId.ShouldBe("leg-a");
        submitted.LocalOrderId.ShouldBe("A");
        submitted.OcaGroupId.ShouldBe("oco-636441077");
    }

    [Fact]
    public async Task PlaceOrdersAsync_EmptyList_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.PlaceOrdersAsync("DU1234567", System.Array.Empty<OrderRequest>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PlaceOrdersAsync_SingleOrder_Succeeds()
    {
        _fakeApi.PlaceOrderResponses.Enqueue([new OrderSubmissionResponse(null, null, null, null, "ord-1", "Submitted")]);

        var order = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "MKT", Tif = "DAY" };

        var result = await _sut.PlaceOrdersAsync("DU1234567", [order], TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("ord-1");
    }

    [Fact]
    public async Task PlaceOrderAsync_DifferentAccounts_RunInParallel()
    {
        var api = new ParallelVerifyingOrderApi();
        var ops = new OrderOperations(api, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance, new TenantContext("test"));

        var order = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "MKT" };

        // Fire two orders for DIFFERENT accounts simultaneously
        var task1 = ops.PlaceOrderAsync("ACCT1", order, TestContext.Current.CancellationToken);
        var task2 = ops.PlaceOrderAsync("ACCT2", order, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(task1, task2);

        results[0].Value.AsT0.OrderId.ShouldBe("order-ACCT1");
        results[1].Value.AsT0.OrderId.ShouldBe("order-ACCT2");
    }

    private class ParallelVerifyingOrderApi : IIbkrOrderApi
    {
        private readonly CountdownEvent _barrier = new(2);

        public async Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            _barrier.Signal();
            _barrier.Wait(TimeSpan.FromSeconds(2)); // Both must arrive within 2s
            await Task.CompletedTask;
            return FakeApiResponse.Success<List<OrderSubmissionResponse>>([new OrderSubmissionResponse(null, null, null, null, $"order-{accountId}", "Submitted")]);
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, long? manualCancelTime = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<string>> DismissNotificationAsync(DismissNotificationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class BlockingOrderApi : IIbkrOrderApi
    {
        private readonly List<string> _callOrder;
        private readonly SemaphoreSlim _semaphore1;
        private readonly SemaphoreSlim _semaphore2;
        private int _callCount;

        public BlockingOrderApi(
            List<string> callOrder,
            SemaphoreSlim semaphore1,
            SemaphoreSlim semaphore2)
        {
            _callOrder = callOrder;
            _semaphore1 = semaphore1;
            _semaphore2 = semaphore2;
        }

        public async Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            var semaphore = call == 1 ? _semaphore1 : _semaphore2;
            await semaphore.WaitAsync(cancellationToken);
            _callOrder.Add($"call-{call}");
            return FakeApiResponse.Success<List<OrderSubmissionResponse>>([new OrderSubmissionResponse(null, null, null, null, $"order-{call}", "Submitted")]);
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            PlaceOrderAsync(accountId, orders, cancellationToken);

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            };
            IApiResponse<string> apiResponse = new ApiResponse<string>(httpResponse, "[]", new RefitSettings());
            return Task.FromResult(apiResponse);
        }

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, string? extOperator = null, bool? manualIndicator = null, long? manualCancelTime = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(OrderStatusFilter[]? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(int? days = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<string>> DismissNotificationAsync(DismissNotificationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
