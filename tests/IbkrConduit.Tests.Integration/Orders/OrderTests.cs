using System;
using System.Threading.Tasks;
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

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsT1.ShouldBeTrue("Expected OrderConfirmationRequired but got OrderSubmitted");
        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("test-reply-id-001");
        confirmation.Messages.ShouldNotBeEmpty();
        confirmation.Messages[0].ShouldContain("without market data");
        confirmation.MessageIds.ShouldContain("o354");

        var replyResult = await _harness.Client.Orders.ReplyAsync(
            confirmation.ReplyId, true, TestContext.Current.CancellationToken);

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

        var placeResult = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);
        placeResult.IsT1.ShouldBeTrue();

        var replyResult = await _harness.Client.Orders.ReplyAsync(
            placeResult.AsT1.ReplyId, true, TestContext.Current.CancellationToken);

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

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery");
        result.AsT0.OrderId.ShouldBe("123456789");

        _harness.VerifyReauthenticationOccurred();
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
