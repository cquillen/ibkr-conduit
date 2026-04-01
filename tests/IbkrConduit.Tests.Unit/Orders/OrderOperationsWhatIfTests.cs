using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

public class OrderOperationsWhatIfTests
{
    private readonly FakeOrderApi _fakeApi = new();
    private readonly OrderOperations _sut;

    public OrderOperationsWhatIfTests()
    {
        _sut = new OrderOperations(_fakeApi, NullLogger<OrderOperations>.Instance);
    }

    [Fact]
    public async Task WhatIfOrderAsync_ReturnsWhatIfResponse()
    {
        _fakeApi.WhatIfResponse = new WhatIfResponse(
            new WhatIfAmount("15000.00", "1.50", "15001.50"),
            new WhatIfEquity("100000.00", "-15001.50", "84998.50"),
            new WhatIfMargin("5000.00", "3000.00", "8000.00"),
            new WhatIfMargin("4000.00", "2500.00", "6500.00"),
            null,
            null);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 100,
            OrderType = "LMT",
            Price = 150.00m,
            Tif = "DAY",
        };

        var result = await _sut.WhatIfOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        result.Amount.ShouldNotBeNull();
        result.Amount.Commission.ShouldBe("1.50");
        result.Equity.ShouldNotBeNull();
        result.Equity.Current.ShouldBe("100000.00");
        result.Initial.ShouldNotBeNull();
        result.Initial.After.ShouldBe("8000.00");
        result.Maintenance.ShouldNotBeNull();
        result.Maintenance.After.ShouldBe("6500.00");
    }

    [Fact]
    public async Task WhatIfOrderAsync_ConvertsOrderRequestToWireModel()
    {
        _fakeApi.WhatIfResponse = new WhatIfResponse(null, null, null, null, null, null);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 50,
            OrderType = "MKT",
            Tif = "DAY",
            ManualIndicator = false,
        };

        await _sut.WhatIfOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);

        var payload = _fakeApi.LastWhatIfPayload;
        payload.ShouldNotBeNull();
        payload.Orders.Count.ShouldBe(1);

        var wire = payload.Orders[0];
        wire.Conid.ShouldBe(265598);
        wire.Side.ShouldBe("BUY");
        wire.Quantity.ShouldBe(50m);
        wire.OrderType.ShouldBe("MKT");
        wire.ManualIndicator.ShouldBe(false);
    }

    private class FakeOrderApi : IIbkrOrderApi
    {
        public WhatIfResponse? WhatIfResponse { get; set; }
        public OrdersPayload? LastWhatIfPayload { get; private set; }

        public Task<List<OrderSubmissionResponse>> PlaceOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<List<OrderSubmissionResponse>> ModifyOrderAsync(
            string accountId, string orderId, OrdersPayload orders, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<List<OrderSubmissionResponse>> ReplyAsync(
            string replyId, ReplyRequest request, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<CancelOrderResponse> CancelOrderAsync(string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<OrdersResponse> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();

        public Task<WhatIfResponse> WhatIfOrderAsync(
            string accountId, OrdersPayload orders, CancellationToken cancellationToken = default)
        {
            LastWhatIfPayload = orders;
            return Task.FromResult(WhatIfResponse!);
        }

        public Task<OrderStatus> GetOrderStatusAsync(
            string orderId, CancellationToken cancellationToken = default) =>
            throw new System.NotImplementedException();
    }
}
