using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using Refit;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

public class OrderOperationsModifyTests
{
    private readonly FakeOrderApi _fakeApi = new();
    private readonly OrderOperations _sut;

    public OrderOperationsModifyTests()
    {
        _sut = new OrderOperations(_fakeApi, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance);
    }

    [Fact]
    public async Task ModifyOrderAsync_DirectConfirmation_ReturnsOrderSubmitted()
    {
        _fakeApi.ModifyOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "12345", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 100,
            OrderType = "LMT",
            Price = 155.00m,
            Tif = "DAY",
        };

        var result = await _sut.ModifyOrderAsync("DU1234567", "99999", order, TestContext.Current.CancellationToken);

        result.Value.AsT0.OrderId.ShouldBe("12345");
        result.Value.AsT0.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task ModifyOrderAsync_WithQuestionResponse_ReturnsOrderConfirmationRequired()
    {
        _fakeApi.ModifyOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                ["Are you sure you want to modify this order?"],
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
            Price = 160.00m,
            Tif = "GTC",
        };

        var result = await _sut.ModifyOrderAsync("DU1234567", "99999", order, TestContext.Current.CancellationToken);

        var confirmation = result.Value.AsT1;
        confirmation.ReplyId.ShouldBe("reply-id-1");
        confirmation.Messages.ShouldContain("Are you sure you want to modify this order?");
        confirmation.MessageIds.ShouldContain("msg-id-1");
    }

    [Fact]
    public async Task ModifyOrderAsync_ConvertsOrderRequestToWireModel()
    {
        _fakeApi.ModifyOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(null, null, null, null, "111", "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "SELL",
            Quantity = 200,
            OrderType = "LMT",
            Price = 145.50m,
            AuxPrice = 144.00m,
            Tif = "GTC",
            ManualIndicator = false,
        };

        await _sut.ModifyOrderAsync("DU1234567", "99999", order, TestContext.Current.CancellationToken);

        var payload = _fakeApi.LastModifyOrderPayload;
        payload.ShouldNotBeNull();
        payload.Orders.Count.ShouldBe(1);

        var wire = payload.Orders[0];
        wire.Conid.ShouldBe(265598);
        wire.Side.ShouldBe("SELL");
        wire.Quantity.ShouldBe(200m);
        wire.OrderType.ShouldBe("LMT");
        wire.Price.ShouldBe(145.50m);
        wire.AuxPrice.ShouldBe(144.00m);
        wire.Tif.ShouldBe("GTC");
        wire.ManualIndicator.ShouldBe(false);
        _fakeApi.LastModifyOrderId.ShouldBe("99999");
    }

    [Fact]
    public async Task ModifyOrderAsync_SerializesPerAccount()
    {
        var callOrder = new List<string>();
        var semaphore1 = new SemaphoreSlim(0, 1);
        var semaphore2 = new SemaphoreSlim(0, 1);

        var api = new BlockingModifyOrderApi(callOrder, semaphore1, semaphore2);
        var ops = new OrderOperations(api, new IbkrClientOptions(), NullLogger<OrderOperations>.Instance);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var task1 = ops.ModifyOrderAsync("ACCT1", "order-1", order, TestContext.Current.CancellationToken);
        var task2 = ops.ModifyOrderAsync("ACCT1", "order-2", order, TestContext.Current.CancellationToken);

        semaphore1.Release();
        await task1;

        semaphore2.Release();
        await task2;

        callOrder.Count.ShouldBe(2);
    }

    private class FakeOrderApi : IIbkrOrderApi
    {
        public Queue<List<OrderSubmissionResponse>> PlaceOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ModifyOrderResponses { get; } = new();
        public Queue<List<OrderSubmissionResponse>> ReplyResponses { get; } = new();
        public OrdersPayload? LastModifyOrderPayload { get; private set; }
        public string? LastModifyOrderId { get; private set; }
        public int ReplyCallCount { get; private set; }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(PlaceOrderResponses.Dequeue()));

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

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
    }

    private class BlockingModifyOrderApi : IIbkrOrderApi
    {
        private readonly List<string> _callOrder;
        private readonly SemaphoreSlim _semaphore1;
        private readonly SemaphoreSlim _semaphore2;
        private int _callCount;

        public BlockingModifyOrderApi(
            List<string> callOrder,
            SemaphoreSlim semaphore1,
            SemaphoreSlim semaphore2)
        {
            _callOrder = callOrder;
            _semaphore1 = semaphore1;
            _semaphore2 = semaphore2;
        }

        public Task<IApiResponse<List<OrderSubmissionResponse>>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public async Task<IApiResponse<List<OrderSubmissionResponse>>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            var semaphore = call == 1 ? _semaphore1 : _semaphore2;
            await semaphore.WaitAsync(cancellationToken);
            _callOrder.Add($"call-{call}");
            return FakeApiResponse.Success<List<OrderSubmissionResponse>>([new OrderSubmissionResponse(null, null, null, null, $"order-{call}", "Submitted")]);
        }

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

        public Task<IApiResponse<CancelOrderResponse>> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrdersResponse>> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<List<Trade>>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<WhatIfResponse>> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<OrderStatus>> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
    }
}
