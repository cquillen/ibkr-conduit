using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

public class OrderOperationsStatusTests
{
    private readonly FakeOrderApi _fakeApi = new();
    private readonly OrderOperations _sut;

    public OrderOperationsStatusTests()
    {
        _sut = new OrderOperations(_fakeApi, NullLogger<OrderOperations>.Instance);
    }

    [Fact]
    public async Task GetOrderStatusAsync_ReturnsOrderStatus()
    {
        _fakeApi.OrderStatusResponse = new OrderStatus(
            SubType: null,
            RequestId: "req-1",
            OrderId: 12345,
            ConidEx: "265598",
            Conid: 265598,
            Symbol: "AAPL",
            Side: "BUY",
            ContractDescription: "AAPL NASDAQ",
            ListingExchange: "NASDAQ",
            IsEventTrading: "0",
            OrderDescription: "Buy 100 AAPL MKT",
            Status: "PreSubmitted",
            OrderType: "MKT",
            Size: 100m,
            FillPrice: 0m,
            FilledQuantity: 0m,
            RemainingQuantity: 100m,
            AvgFillPrice: 0m,
            LastFillPrice: 0m,
            TotalSize: 100m,
            TotalCashSize: 0m,
            Price: null,
            Tif: "DAY",
            BgColor: "#FFFFFF",
            FgColor: "#000000",
            OrderNotEditable: false,
            EditableFields: null,
            CannotCancelOrder: false);

        var result = await _sut.GetOrderStatusAsync("12345", TestContext.Current.CancellationToken);

        result.OrderId.ShouldBe(12345);
        result.Symbol.ShouldBe("AAPL");
        result.Status.ShouldBe("PreSubmitted");
        result.Side.ShouldBe("BUY");
        result.Conid.ShouldBe(265598);
        result.RemainingQuantity.ShouldBe(100m);
    }

    private class FakeOrderApi : IIbkrOrderApi
    {
        public OrderStatus? OrderStatusResponse { get; set; }

        public Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<IApiResponse<string>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<OrdersResponse> GetLiveOrdersAsync(string? filters = null, bool? force = null, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OrderStatusResponse!);
    }
}
