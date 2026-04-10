using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Orders;

public class OrderTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task PlaceOrder_DirectSubmission_ReturnsOrderSubmitted()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-order-submitted"));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted but got OrderConfirmationRequired");
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("123456789");
        submitted.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrder_ConfirmationRequired_ReturnsConfirmationThenSubmits()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation"));

        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/reply/*",
            FixtureLoader.LoadBody("Orders", "POST-reply-submitted"));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.IsT1.ShouldBeTrue("Expected OrderConfirmationRequired but got OrderSubmitted");
        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("test-reply-id-001");
        confirmation.Messages.ShouldNotBeEmpty();
        confirmation.Messages[0].ShouldContain("without market data");
        confirmation.MessageIds.ShouldContain("o354");

        var replyResult = (await _harness.Client.Orders.ReplyAsync(
            confirmation.ReplyId, true, TestContext.Current.CancellationToken)).Value;

        replyResult.IsT0.ShouldBeTrue("Expected OrderSubmitted after confirmation");
        var submitted = replyResult.AsT0;
        submitted.OrderId.ShouldBe("987654321");
        submitted.OrderStatus.ShouldBe("PreSubmitted");

        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/reply/*").UsingPost())
            .Count.ShouldBe(1, "Reply endpoint should have been called exactly once");
    }

    [Fact]
    public async Task Reply_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation"));

        // First reply call returns 401
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/*")
                .UsingPost())
            .InScenario("reply-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        // After re-auth, second reply succeeds
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/*")
                .UsingPost())
            .InScenario("reply-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-reply-submitted")));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var placeResult = (await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;
        placeResult.IsT1.ShouldBeTrue();

        var replyResult = (await _harness.Client.Orders.ReplyAsync(
            placeResult.AsT1.ReplyId, true, TestContext.Current.CancellationToken)).Value;

        replyResult.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery on reply");
        replyResult.AsT0.OrderId.ShouldBe("987654321");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task PlaceOrder_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost())
            .InScenario("order-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost())
            .InScenario("order-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-place-order-submitted")));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery");
        result.AsT0.OrderId.ShouldBe("123456789");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Live Orders ---

    [Fact]
    public async Task GetLiveOrders_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders"));

        var result = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        var order = result[0];
        order.Account.ShouldBe("U1234567");
        order.Conid.ShouldBe(756733);
        order.ConidEx.ShouldBe("756733");
        order.OrderId.ShouldBe(473740665);
        order.Ticker.ShouldBe("SPY");
        order.SecType.ShouldBe("STK");
        order.ListingExchange.ShouldBe("ARCA");
        order.Side.ShouldBe("BUY");
        order.Status.ShouldBe("Filled");
        order.OrderCcpStatus.ShouldBe("Filled");
        order.OrderType.ShouldBe("Market");
        order.FilledQuantity.ShouldBe(1.0m);
        order.RemainingQuantity.ShouldBe(0.0m);
        order.TotalSize.ShouldBe(1.0m);
        order.CompanyName.ShouldBe("SS SPDR S&P 500 ETF TRUST-US");
        order.AvgPrice.ShouldBe("647.09");
        order.TimeInForce.ShouldBe("CLOSE");
        order.OrderDescription.ShouldBe("Bought 1 SPY Market, Day");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_EmptyOrders_ReturnsEmptyList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders-empty"));

        var result = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .InScenario("live-orders-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .InScenario("live-orders-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders")));

        var result = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Ticker.ShouldBe("SPY");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Order Status ---

    [Fact]
    public async Task GetOrderStatus_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/order/status/473740665",
            FixtureLoader.LoadBody("Orders", "GET-order-status"));

        var result = (await _harness.Client.Orders.GetOrderStatusAsync(
            "473740665", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.OrderId.ShouldBe(473740665);
        result.Conid.ShouldBe(756733);
        result.ConidEx.ShouldBe("756733");
        result.Symbol.ShouldBe("SPY");
        result.Side.ShouldBe("BUY");
        result.Status.ShouldBe("Filled");
        result.OrderType.ShouldBe("Market");
        result.OrderDescription.ShouldBe("Bought 1 SPY Market, Day");
        result.ListingExchange.ShouldBe("ARCA");
        result.FilledQuantity.ShouldBe(1.0m);
        result.RemainingQuantity.ShouldBe(0.0m);
        result.FillPrice.ShouldBe(647.09m);
        result.AvgFillPrice.ShouldBe(647.09m);
        result.Tif.ShouldBe("DAY");
        result.OrderNotEditable.ShouldBe(true);
        result.CannotCancelOrder.ShouldBe(true);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetOrderStatus_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/order/status/473740665")
                .UsingGet())
            .InScenario("order-status-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/order/status/473740665")
                .UsingGet())
            .InScenario("order-status-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-order-status")));

        var result = (await _harness.Client.Orders.GetOrderStatusAsync(
            "473740665", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.OrderId.ShouldBe(473740665);
        result.Symbol.ShouldBe("SPY");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Trades ---

    [Fact]
    public async Task GetTrades_EmptyResponse_ReturnsEmptyList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/trades",
            FixtureLoader.LoadBody("Orders", "GET-trades-empty"));

        var result = (await _harness.Client.Orders.GetTradesAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetTrades_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/trades")
                .UsingGet())
            .InScenario("trades-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/trades")
                .UsingGet())
            .InScenario("trades-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-trades")));

        var result = (await _harness.Client.Orders.GetTradesAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Symbol.ShouldBe("SPY");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Cancel Order ---

    [Fact]
    public async Task CancelOrder_ReturnsAllFields()
    {
        _harness.StubAuthenticated(
            HttpMethod.Delete,
            "/v1/api/iserver/account/U1234567/order/602801486",
            FixtureLoader.LoadBody("Orders", "DELETE-cancel-order"));

        var result = (await _harness.Client.Orders.CancelOrderAsync(
            "U1234567", "602801486", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Message.ShouldBe("Request was submitted");
        result.OrderId.ShouldBe(602801486);
        result.Conid.ShouldBe(-1);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task CancelOrder_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/order/602801486")
                .UsingDelete())
            .InScenario("cancel-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/order/602801486")
                .UsingDelete())
            .InScenario("cancel-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "DELETE-cancel-order")));

        var result = (await _harness.Client.Orders.CancelOrderAsync(
            "U1234567", "602801486", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Message.ShouldBe("Request was submitted");
        result.OrderId.ShouldBe(602801486);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- WhatIf Order ---

    [Fact]
    public async Task WhatIfOrder_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/U1234567/orders/whatif",
            FixtureLoader.LoadBody("Orders", "POST-whatif-order"));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.WhatIfOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();

        result.Amount.ShouldNotBeNull();
        result.Amount!.Amount.ShouldBe("1 USD (1 Shares)");
        result.Amount.Commission.ShouldBe("0.01 USD");
        result.Amount.Total.ShouldBe("1.01 USD");

        result.Equity.ShouldNotBeNull();
        result.Equity!.Current.ShouldBe("1,006,413");
        result.Equity.Change.ShouldBe("163");
        result.Equity.After.ShouldBe("1,006,576");

        result.Initial.ShouldNotBeNull();
        result.Initial!.Current.ShouldBe("8,637");
        result.Initial.Change.ShouldBe("164");
        result.Initial.After.ShouldBe("8,801");

        result.Maintenance.ShouldNotBeNull();
        result.Maintenance!.Current.ShouldBe("8,637");
        result.Maintenance.Change.ShouldBe("164");
        result.Maintenance.After.ShouldBe("8,801");

        result.Warning.ShouldNotBeNull();
        result.Warning.ShouldContain("price exceeds");
        result.Error.ShouldBeNull();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task WhatIfOrder_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/orders/whatif")
                .UsingPost())
            .InScenario("whatif-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/orders/whatif")
                .UsingPost())
            .InScenario("whatif-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-whatif-order")));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.WhatIfOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Amount.ShouldNotBeNull();
        result.Amount!.Commission.ShouldBe("0.01 USD");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Modify Order ---

    [Fact]
    public async Task ModifyOrder_DirectSubmission_ReturnsOrderSubmitted()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/U1234567/order/473740665",
            FixtureLoader.LoadBody("Orders", "POST-modify-order-submitted"));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 2,
            OrderType = "LMT",
            Price = 2.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.ModifyOrderAsync(
            "U1234567", "473740665", order, TestContext.Current.CancellationToken)).Value;

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted but got OrderConfirmationRequired");
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("555666777");
        submitted.OrderStatus.ShouldBe("PreSubmitted");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task ModifyOrder_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/order/473740665")
                .UsingPost())
            .InScenario("modify-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/order/473740665")
                .UsingPost())
            .InScenario("modify-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-modify-order-submitted")));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 2,
            OrderType = "LMT",
            Price = 2.00m,
            Tif = "GTC",
        };

        var result = (await _harness.Client.Orders.ModifyOrderAsync(
            "U1234567", "473740665", order, TestContext.Current.CancellationToken)).Value;

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery");
        result.AsT0.OrderId.ShouldBe("555666777");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_ServerError_ReturnsFailureResult()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.Orders.GetLiveOrdersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
