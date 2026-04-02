using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Orders;
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
        _sut = new OrderOperations(_fakeApi, NullLogger<OrderOperations>.Instance);
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

        result.AsT0.OrderId.ShouldBe("12345");
        result.AsT0.OrderStatus.ShouldBe("PreSubmitted");
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
        var confirmation = result.AsT1;
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
    }

    [Fact]
    public async Task PlaceOrderAsync_SerializesPerAccount()
    {
        var callOrder = new List<string>();
        var semaphore1 = new SemaphoreSlim(0, 1);
        var semaphore2 = new SemaphoreSlim(0, 1);

        var api = new BlockingOrderApi(callOrder, semaphore1, semaphore2);
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

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

        var confirmation = result.AsT1;
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

        result.AsT0.OrderId.ShouldBe("77777");
        result.AsT0.OrderStatus.ShouldBe("PreSubmitted");
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

        var result = await _sut.CancelOrderAsync("DU1234567", "12345", TestContext.Current.CancellationToken);

        result.Message.ShouldBe("Order cancelled");
        result.OrderId.ShouldBe(12345);
    }

    [Fact]
    public async Task GetLiveOrdersAsync_ReturnsOrdersList()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse(
        [
            new LiveOrder("111", 265598, "AAPL", "BUY", 100, "LMT", 150.00m, "PreSubmitted", 0, 100),
        ]);

        var result = await _sut.GetLiveOrdersAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].OrderId.ShouldBe("111");
    }

    [Fact]
    public async Task GetLiveOrdersAsync_NullOrders_ReturnsEmptyList()
    {
        _fakeApi.LiveOrdersResponse = new OrdersResponse(null);

        var result = await _sut.GetLiveOrdersAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTradesAsync_DelegatesToApi()
    {
        _fakeApi.TradesResponse =
        [
            new Trade("exec-1", 265598, "AAPL", "BUY", 100, 150.00m, "ref-1", "user1"),
        ];

        var result = await _sut.GetTradesAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].ExecutionId.ShouldBe("exec-1");
    }

    private class FakeOrderApi : IIbkrOrderApi
    {
        public Queue<List<OrderSubmissionResponse>> PlaceOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ModifyOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ReplyResponses { get; } = new();
        public CancelOrderResponse? CancelResponse { get; set; }
        public OrdersResponse LiveOrdersResponse { get; set; } = new(null);
        public List<Trade>? TradesResponse { get; set; }
        public WhatIfResponse? WhatIfResponse { get; set; }
        public OrderStatus? OrderStatusResponse { get; set; }
        public OrdersPayload? LastPlaceOrderPayload { get; private set; }
        public OrdersPayload? LastModifyOrderPayload { get; private set; }
        public OrdersPayload? LastWhatIfPayload { get; private set; }
        public string? LastModifyOrderId { get; private set; }
        public ReplyRequest? LastReplyRequest { get; private set; }
        public int ReplyCallCount { get; private set; }

        public Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastPlaceOrderPayload = orders;
            return Task.FromResult(PlaceOrderResponses.Dequeue());
        }

        public Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastModifyOrderPayload = orders;
            LastModifyOrderId = orderId;
            return Task.FromResult(ModifyOrderResponses.Dequeue());
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

        public Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CancelResponse!);

        public Task<OrdersResponse> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LiveOrdersResponse);

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(TradesResponse!);

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastWhatIfPayload = orders;
            return Task.FromResult(WhatIfResponse!);
        }

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderStatusResponse!);
    }

    [Fact]
    public async Task PlaceOrderAsync_DifferentAccounts_RunInParallel()
    {
        var api = new ParallelVerifyingOrderApi();
        var ops = new OrderOperations(api, NullLogger<OrderOperations>.Instance);

        var order = new OrderRequest { Conid = 265598, Side = "BUY", Quantity = 1, OrderType = "MKT" };

        // Fire two orders for DIFFERENT accounts simultaneously
        var task1 = ops.PlaceOrderAsync("ACCT1", order, TestContext.Current.CancellationToken);
        var task2 = ops.PlaceOrderAsync("ACCT2", order, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(task1, task2);

        results[0].AsT0.OrderId.ShouldBe("order-ACCT1");
        results[1].AsT0.OrderId.ShouldBe("order-ACCT2");
    }

    private class ParallelVerifyingOrderApi : IIbkrOrderApi
    {
        private readonly CountdownEvent _barrier = new(2);

        public async Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            _barrier.Signal();
            _barrier.Wait(TimeSpan.FromSeconds(2)); // Both must arrive within 2s
            await Task.CompletedTask;
            return [new OrderSubmissionResponse(null, null, null, null, $"order-{accountId}", "Submitted")];
        }

        public Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OrdersResponse> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
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

        public async Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            var semaphore = call == 1 ? _semaphore1 : _semaphore2;
            await semaphore.WaitAsync(cancellationToken);
            _callOrder.Add($"call-{call}");
            return [new OrderSubmissionResponse(null, null, null, null, $"order-{call}", "Submitted")];
        }

        public Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
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

        public Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OrdersResponse> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
