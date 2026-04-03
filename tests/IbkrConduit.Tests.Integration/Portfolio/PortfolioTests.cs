using System;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Portfolio;

public class PortfolioTests : IAsyncLifetime, IDisposable
{
    private readonly WireMockServer _server;
    private ServiceProvider? _provider;
    private IbkrConduit.Auth.IbkrOAuthCredentials? _credentials;
    private IIbkrClient _client = null!;

    public PortfolioTests()
    {
        _server = WireMockServer.Start();
    }

    public async ValueTask InitializeAsync()
    {
        // Create synthetic credentials (disposed in Dispose)
        _credentials = TestCredentials.Create();
        var credentials = _credentials;

        // Register the LST handshake handler (server-side DH exchange)
        MockLstServer.Register(_server, credentials);

        // Stub ssodh/init (session initialization)
        // Expect: correct consumer key, access token, HMAC-SHA256, and JSON body
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost()
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", $"*oauth_token=\"{TestCredentials.AccessToken}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*")
                .WithBody(b => b != null && b.Contains("publish")))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

        // Build the full DI pipeline pointing at WireMock
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
    public async Task GetAccounts_ReturnsAllFields()
    {
        // Expect: correct consumer key, access token, and HMAC-SHA256
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet()
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{TestCredentials.ConsumerKey}\"*")
                .WithHeader("Authorization", $"*oauth_token=\"{TestCredentials.AccessToken}\"*")
                .WithHeader("Authorization", "*HMAC-SHA256*"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-accounts")));

        var accounts = await _client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        accounts.ShouldNotBeEmpty();
        var account = accounts[0];
        account.Id.ShouldBe("U1234567");
        account.AccountId.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Test User");
        account.DisplayName.ShouldBe("Test User");
        account.Currency.ShouldBe("USD");
        account.Type.ShouldBe("DEMO");
        account.TradingType.ShouldBe("STKNOPT");
        account.BusinessType.ShouldBe("INDEPENDENT");
        account.IbEntity.ShouldBe("IBLLC-US");
        account.BrokerageAccess.ShouldBeTrue();
        account.Faclient.ShouldBeFalse();
        account.ClearingStatus.ShouldBe("O");
        account.Covestor.ShouldBeFalse();
        account.NoClientTrading.ShouldBeFalse();
        account.TrackVirtualFXPortfolio.ShouldBeFalse();
        account.Parent.ShouldNotBeNull();
        account.Parent!.IsMParent.ShouldBeFalse();
        account.Parent!.IsMultiplex.ShouldBeFalse();

        // Verify the full handshake occurred
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBeGreaterThan(0, "Live Session Token handshake should have been called");
        _server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .Count.ShouldBeGreaterThan(0, "Session init should have been called");
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
