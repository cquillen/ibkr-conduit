using System;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Orders;
using IbkrConduit.Session;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Orders;

public class OrderTests : IAsyncLifetime, IDisposable
{
    private readonly WireMockServer _server;
    private ServiceProvider? _provider;
    private IbkrConduit.Auth.IbkrOAuthCredentials? _credentials;
    private IIbkrClient _client = null!;

    public OrderTests()
    {
        _server = WireMockServer.Start();
    }

    public async ValueTask InitializeAsync()
    {
        _credentials = TestCredentials.Create();
        var credentials = _credentials;

        MockLstServer.Register(_server, credentials);

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost()
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddIbkrClient(credentials, new IbkrClientOptions
        {
            BaseUrl = _server.Url!,
        });

        _provider = services.BuildServiceProvider();
        _client = _provider.GetRequiredService<IIbkrClient>();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task PlaceOrder_DirectSubmission_ReturnsOrderSubmitted()
    {
        // IBKR accepts the order without requiring confirmation
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost()
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*"))
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

        var result = await _client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        // Should be direct submission — OrderSubmitted (T0)
        result.IsT0.ShouldBeTrue("Expected OrderSubmitted but got OrderConfirmationRequired");
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("123456789");
        submitted.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public async Task PlaceOrder_ConfirmationRequired_ReturnsConfirmationThenSubmits()
    {
        // First call: IBKR returns a confirmation prompt
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost()
                .WithHeader("Authorization", "*HMAC-SHA256*"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation")));

        // Reply endpoint: confirming returns the submitted order
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/*")
                .UsingPost()
                .WithHeader("Authorization", "*HMAC-SHA256*"))
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

        var result = await _client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        // Should require confirmation — OrderConfirmationRequired (T1)
        result.IsT1.ShouldBeTrue("Expected OrderConfirmationRequired but got OrderSubmitted");
        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("test-reply-id-001");
        confirmation.Messages.ShouldNotBeEmpty();
        confirmation.Messages[0].ShouldContain("without market data");
        confirmation.MessageIds.ShouldContain("o354");

        // Now confirm the order via ReplyAsync
        var replyResult = await _client.Orders.ReplyAsync(
            confirmation.ReplyId, true, TestContext.Current.CancellationToken);

        // Reply should return OrderSubmitted
        replyResult.IsT0.ShouldBeTrue("Expected OrderSubmitted after confirmation");
        var submitted = replyResult.AsT0;
        submitted.OrderId.ShouldBe("987654321");
        submitted.OrderStatus.ShouldBe("PreSubmitted");

        // Verify the reply endpoint was actually called
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/reply/*").UsingPost())
            .Count.ShouldBe(1, "Reply endpoint should have been called exactly once");
    }

    [Fact]
    public async Task Reply_401Recovery_ReauthenticatesAndRetries()
    {
        // PlaceOrder returns confirmation (no 401 here)
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation")));

        // First reply call returns 401 (token expired between place and reply)
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/reply/*")
                .UsingPost())
            .InScenario("reply-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        // After re-auth, second reply call succeeds
        _server.Given(
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

        // Place order — gets confirmation
        var placeResult = await _client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);
        placeResult.IsT1.ShouldBeTrue();
        var confirmation = placeResult.AsT1;

        // Reply — first call 401s, re-auth, retry succeeds
        var replyResult = await _client.Orders.ReplyAsync(
            confirmation.ReplyId, true, TestContext.Current.CancellationToken);

        replyResult.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery on reply");
        replyResult.AsT0.OrderId.ShouldBe("987654321");

        // Verify re-authentication occurred
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(2,
                "LST handshake should have been called at least twice (initial + re-auth)");
    }

    [Fact]
    public async Task PlaceOrder_401Recovery_ReauthenticatesAndRetries()
    {
        // First call returns 401
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost())
            .InScenario("order-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        // After re-auth, second call succeeds with direct submission
        _server.Given(
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

        var result = await _client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsT0.ShouldBeTrue("Expected OrderSubmitted after 401 recovery");
        result.AsT0.OrderId.ShouldBe("123456789");

        // Verify re-authentication occurred
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(2,
                "LST handshake should have been called at least twice (initial + re-auth)");
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(2,
                "Session init should have been called at least twice (initial + re-auth)");
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _server.Dispose();
        _credentials?.Dispose();
        GC.SuppressFinalize(this);
    }
}
