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
    public async Task Reply_401_ReturnsAmbiguousError_WithoutReplay()
    {
        // ADR-0003 (AMB-2): the reply POST is order-mutating — a 401 must NOT be replayed. Re-auth
        // still happens, but the outcome is ambiguous and surfaces as IbkrAmbiguousOrderError.
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation"));

        // 401-then-success configured; the gate must stop before the success stub is ever reached.
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

        var replyResult = await _harness.Client.Orders.ReplyAsync(
            placeResult.AsT1.ReplyId, true, TestContext.Current.CancellationToken);

        replyResult.IsSuccess.ShouldBeFalse("a 401 on the order-mutating reply POST is ambiguous, never replayed");
        var ambiguous = replyResult.Error.ShouldBeOfType<IbkrAmbiguousOrderError>();
        ambiguous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ambiguous.ReauthSucceeded.ShouldBeTrue();

        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/reply/*").UsingPost())
            .Count.ShouldBe(1, "the reply POST must be sent exactly once — no replay");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task PlaceOrder_401_ReturnsAmbiguousError_WithoutReplay()
    {
        // ADR-0003 (AMB-2): place is order-mutating — a 401 is ambiguous and must not be replayed.
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

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse("a 401 on the place POST is ambiguous, never replayed");
        var ambiguous = result.Error.ShouldBeOfType<IbkrAmbiguousOrderError>();
        ambiguous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ambiguous.ReauthSucceeded.ShouldBeTrue();

        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/*/orders").UsingPost())
            .Count.ShouldBe(1, "the place POST must be sent exactly once — no replay");
        _harness.VerifyReauthenticationOccurred();
    }

    // --- Live Orders ---

    [Fact]
    public async Task GetLiveOrders_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders"));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        snapshot.ShouldNotBeNull();
        snapshot.IsSnapshot.ShouldBeTrue("the fixture is a primed read (snapshot:true)");
        var result = snapshot.Orders;
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
        order.Price.ShouldBeNull(); // market order: IBKR sends price="" -> null
        order.TimeInForce.ShouldBe("CLOSE");
        order.OrderDescription.ShouldBe("Bought 1 SPY Market, Day");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_Unprimed_SurfacesIsSnapshotFalse()
    {
        // GAP1-1/GAP1-3: the {"orders":[],"snapshot":false} shape is IBKR's UNPRIMED response — an
        // empty list here is NOT authoritative "no orders". The fixture is named accordingly so it no
        // longer masquerades as the canonical no-orders case; IsSnapshot==false is the ground truth.
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders-unprimed-empty"));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        snapshot.ShouldNotBeNull();
        snapshot.IsSnapshot.ShouldBeFalse("snapshot:false means the cache is unprimed, not that no orders exist");
        snapshot.Orders.ShouldBeEmpty();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_PrimedEmpty_IsSnapshotTrueAndEmpty()
    {
        // GAP1-1: the distinguishing case — an empty list that IS authoritative because the read is
        // primed (snapshot:true). Only here may a consumer treat empty as "no live orders".
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders-primed-empty"));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        snapshot.IsSnapshot.ShouldBeTrue("snapshot:true means the empty set is an authoritative no-orders fact");
        snapshot.Orders.ShouldBeEmpty();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_UnprimedThenPrimed_SurfacesEachSnapshotFaithfully()
    {
        // GAP1-1/GAP1-3: pin the recorded two-call priming sequence — call one is unprimed (empty,
        // IsSnapshot=false), call two is primed (orders present, IsSnapshot=true).
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders").UsingGet())
            .InScenario("live-orders-priming")
            .WillSetStateTo("primed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders-unprimed-empty")));

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders").UsingGet())
            .InScenario("live-orders-priming")
            .WhenStateIs("primed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders")));

        var unprimed = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;
        unprimed.IsSnapshot.ShouldBeFalse();
        unprimed.Orders.ShouldBeEmpty();

        var primed = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;
        primed.IsSnapshot.ShouldBeTrue();
        primed.Orders.Count.ShouldBe(1);
        primed.Orders[0].Ticker.ShouldBe("SPY");
    }

    [Fact]
    public async Task GetLiveOrders_Filtered_IssuesExactlyOneForceFollowUp()
    {
        // GAP1-2 / §10.6: after a *filtered* call the library issues exactly one force=true follow-up
        // (no filters) through the same pipeline, so a later sor subscription still gets order details.
        // The filtered call returns the fake-empty unprimed shape; force=true returns the blank array.
        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders")
                .WithParam("filters", "cancelled").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders-unprimed-empty")));

        _harness.Server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/orders")
                .WithParam("force", "true").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders-force-cleared")));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            new[] { OrderStatusFilter.Cancelled },
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        // The consumer's filtered result stands — the follow-up never alters it.
        snapshot.IsSnapshot.ShouldBeFalse();
        snapshot.Orders.ShouldBeEmpty();

        // Exactly one force=true follow-up was issued (lowercase, per the documented wire format).
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/orders")
                .WithParam("force", "true").UsingGet())
            .Count.ShouldBe(1, "a filtered call must trigger exactly one force=true follow-up");

        // And exactly one filtered call reached the server (the follow-up carries no filters).
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/orders")
                .WithParam("filters", "cancelled").UsingGet())
            .Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveOrders_Unfiltered_IssuesNoForceFollowUp()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders"));

        await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/orders")
                .WithParam("force", "true").UsingGet())
            .Count.ShouldBe(0, "an unfiltered call must not trigger a force follow-up");
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
            cancellationToken: TestContext.Current.CancellationToken)).Value.Orders;

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
    public async Task GetTrades_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/trades",
            FixtureLoader.LoadBody("Orders", "GET-trades"));

        var result = (await _harness.Client.Orders.GetTradesAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        var trade = result[0];
        trade.ExecutionId.ShouldBe("00018fae.67890abc.01.01");
        trade.Symbol.ShouldBe("SPY");
        trade.Side.ShouldBe("BOT");
        trade.Size.ShouldBe(1.0m);
        trade.Price.ShouldBe(647.09m);
        trade.OrderRef.ShouldBe("Order123");
        trade.Submitter.ShouldBe("U1234567");
        // Newly-mapped fields
        trade.Commission.ShouldBe(1.01m);        // "1.01" (string) -> decimal
        trade.NetAmount.ShouldBe(647.09m);
        trade.TradeTime.ShouldBe("20260702-17:45:50");
        trade.TradeTimeR.ShouldBe(1783014350000);
        trade.Exchange.ShouldBe("ARCA");
        trade.Account.ShouldBe("U1234567");
        trade.AccountCode.ShouldBe("U1234567");
        trade.CompanyName.ShouldBe("SPDR S&P 500 ETF TRUST");
        trade.ContractDescription1.ShouldBe("SPY");
        trade.SecType.ShouldBe("STK");
        trade.ListingExchange.ShouldBe("ARCA");
        trade.ConidEx.ShouldBe("756733");
        trade.ClearingId.ShouldBe("IB");
        trade.ClearingName.ShouldBe("IB");
        trade.SupportsTaxOpt.ShouldBe(true);     // "1" -> true
        trade.LiquidationTrade.ShouldBe(false);  // "0" -> false
        trade.IsEventTrading.ShouldBe(false);    // "0" -> false
        // Fields IBKR returns but the documented schema omits (found via live verification).
        trade.OrderId.ShouldBe("656804954");     // JSON number -> string
        trade.Position.ShouldBe(48m);            // "48" -> decimal
        trade.AccountAllocationName.ShouldBe("U1234567");

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

    [Fact]
    public async Task GetTrades_TradeWithEmptyOrOmittedMoneyFields_SurfaceNullNotZero()
    {
        // WIR-3: IBKR sends "size":"" and omits price/order_ref/submitter on a manual/TWS trade
        // (mirrors the live "price":"" capture). Money and reference fields must surface as null
        // (absent), never a fabricated 0m / "" that a consumer would book as real fill economics.
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/trades",
            FixtureLoader.LoadBody("Orders", "GET-trades-sparse-money"));

        var result = (await _harness.Client.Orders.GetTradesAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        var trade = result.ShouldHaveSingleItem();
        trade.Size.ShouldBeNull();
        trade.Price.ShouldBeNull();
        trade.OrderRef.ShouldBeNull();
        trade.Submitter.ShouldBeNull();

        _harness.VerifyHandshakeOccurred();
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
            "U1234567", "602801486", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
            "U1234567", "602801486", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
    public async Task ModifyOrder_401_ReturnsAmbiguousError_WithoutReplay()
    {
        // ADR-0003 (AMB-2): modify is order-mutating — a 401 is ambiguous and must not be replayed.
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

        var result = await _harness.Client.Orders.ModifyOrderAsync(
            "U1234567", "473740665", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse("a 401 on the modify POST is ambiguous, never replayed");
        var ambiguous = result.Error.ShouldBeOfType<IbkrAmbiguousOrderError>();
        ambiguous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ambiguous.ReauthSucceeded.ShouldBeTrue();

        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/U1234567/order/473740665").UsingPost())
            .Count.ShouldBe(1, "the modify POST must be sent exactly once — no replay");
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

    // --- Order-outcome classification of 200-OK edge shapes (AMB-3 / AMB-4 / WIR-4) ---

    [Fact]
    public async Task PlaceOrder_ArrayWrappedError200_ReturnsClassifiedRefusalWithRawBody()
    {
        // AMB-4: an array-wrapped reject [{"error":"…"}] bypasses bare-object hidden-error detection.
        // It must classify as a refusal carrying the raw body, not throw InvalidOperationException.
        const string body = """[{"error":"We cannot accept an order at the limit price you selected."}]""";
        _harness.StubAuthenticatedPost("/v1/api/iserver/account/*/orders", body);

        var order = new OrderRequest { Conid = 756733, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC" };

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var rejected = result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldContain("cannot accept an order");
        rejected.RawBody.ShouldBe(body);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task PlaceOrder_EmptyArray200_ReturnsClassifiedFailureNotThrow()
    {
        // AMB-4: a 200 [] must classify as a failure carrying the raw body, not throw
        // ArgumentOutOfRangeException.
        _harness.StubAuthenticatedPost("/v1/api/iserver/account/*/orders", "[]");

        var order = new OrderRequest { Conid = 756733, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC" };

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task PlaceOrder_NumericOrderId200_ReturnsSubmitted()
    {
        // WIR-4: a numeric order_id on the place response must deserialize as a string (a transmitted
        // order), not collapse into a deserialization-failure Result.
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            """[{"order_id":987654321,"order_status":"Submitted"}]""");

        var order = new OrderRequest { Conid = 756733, Side = "BUY", Quantity = 1, OrderType = "LMT", Price = 1.00m, Tif = "GTC" };

        var result = (await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken)).Value;

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted for a numeric order_id");
        result.AsT0.OrderId.ShouldBe("987654321");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task Reply_HiddenError200_ReturnsClassifiedRefusal()
    {
        // AMB-3: the reply 2xx path routes through hidden-error detection like every other order path,
        // so a 200 {"error":"…"} reject surfaces the reject text instead of throwing.
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/reply/*",
            """{"error":"We cannot accept an order at the limit price you selected."}""");

        var result = await _harness.Client.Orders.ReplyAsync(
            "test-reply-id-001", true, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("cannot accept an order");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task DismissNotification_ReturnsSuccess()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/notification",
            FixtureLoader.LoadBody("Orders", "POST-dismiss-notification"));

        var result = await _harness.Client.Orders.DismissNotificationAsync(
            987654321, "12345", "Yes", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain("Request was submitted");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task DismissNotification_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/notification")
                .UsingPost())
            .InScenario("dismiss-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/notification")
                .UsingPost())
            .InScenario("dismiss-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-dismiss-notification")));

        var result = await _harness.Client.Orders.DismissNotificationAsync(
            987654321, "12345", "Yes", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        _harness.VerifyReauthenticationOccurred();
    }

    // --- Bracket / OCA group submission ---

    [Fact]
    public async Task PlaceOrders_BracketGroup_ReturnsParentResultAndSendsNativeLinkage()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/*/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-bracket-submitted"));

        var parent = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
            CustomerOrderId = "Parent",
            OutsideRth = true,
        };
        var takeProfit = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "LMT",
            Price = 2.00m,
            Tif = "GTC",
            ParentId = "Parent",
        };
        var stop = new OrderRequest
        {
            Conid = 756733,
            Side = "SELL",
            Quantity = 1,
            OrderType = "STP",
            Price = 0.50m,
            Tif = "GTC",
            ParentId = "Parent",
        };

        var result = (await _harness.Client.Orders.PlaceOrdersAsync(
            "U1234567", [parent, takeProfit, stop], TestContext.Current.CancellationToken)).Value;

        // A grouped submission returns a single parent result (verified live).
        result.IsT0.ShouldBeTrue("Expected OrderSubmitted but got OrderConfirmationRequired");
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("111");
        submitted.LocalOrderId.ShouldBe("Parent");

        var entries = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/account/*/orders").UsingPost());
        var body = entries.ShouldHaveSingleItem().RequestMessage.Body;
        body.ShouldNotBeNull();
        body.ShouldContain("\"cOID\":\"Parent\"");
        body.ShouldContain("\"parentId\":\"Parent\"");
        body.ShouldContain("\"outsideRTH\":true");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_WithOrderRef_ExposesTypedOrderRef()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders-with-ref"));

        var orders = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value.Orders;

        orders.ShouldHaveSingleItem().OrderRef.ShouldBe("Parent");

        _harness.VerifyHandshakeOccurred();
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
