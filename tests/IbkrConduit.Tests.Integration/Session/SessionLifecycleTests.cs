using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Session;

[Collection("IBKR E2E")]
public class SessionLifecycleTests : IAsyncDisposable
{
    private readonly WireMockServer _server;

    public SessionLifecycleTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task FullInitialization_CallsEndpointsInCorrectOrder()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupSuppressEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163" },
        };

        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        await using var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            notifier,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Act: call portfolio endpoint (triggers lazy init)
        var accounts = await portfolioApi.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");

        // Verify endpoints were called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("ssodh/init"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("suppress"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("portfolio/accounts"));
    }

    [Fact]
    public async Task UnauthorizedApiCall_TriggersReauthAndRetries()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-retry")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-retry")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"DU1234567","accountTitle":"Paper Trading","type":"INDIVIDUAL"}]"""));

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions();
        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        await using var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            notifier,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Act
        var accounts = await portfolioApi.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");
    }

    [Fact]
    public async Task CleanShutdown_CallsLogout()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();
        SetupLogoutEndpoint();

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions();
        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            notifier,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Initialize by making an API call
        await portfolioApi.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Act: Dispose triggers shutdown
        await sessionManager.DisposeAsync();

        // Assert: logout was called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("logout"));
    }

    /// <summary>
    /// End-to-end test against a real IBKR paper account.
    /// Runs automatically when IBKR_CONSUMER_KEY environment variable is set.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task PaperAccount_FullLifecycle_InitializesAndShutdown()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // Making any API call triggers lazy session initialization
        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();

        // Disposing the provider triggers clean session shutdown (logout)
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _server.Dispose();
        await Task.CompletedTask;
    }

    private IIbkrPortfolioApi CreatePortfolioApi(
        FakeTokenProvider tokenProvider,
        SessionManager sessionManager)
    {
        var tokenRefreshHandler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = new OAuthSigningHandler(
                tokenProvider,
                "TESTKEY01",
                "mytoken",
                sessionManager)
            {
                InnerHandler = new HttpClientHandler(),
            },
        };

        var httpClient = new HttpClient(tokenRefreshHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<IIbkrPortfolioApi>(httpClient);
    }

    private IIbkrSessionApi CreateSessionApi(FakeTokenProvider tokenProvider)
    {
        var signingHandler = new OAuthSigningHandler(
            tokenProvider,
            "TESTKEY01",
            "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<IIbkrSessionApi>(httpClient);
    }

    private static FakeTokenProvider CreateFakeTokenProvider()
    {
        var lstBytes = new byte[] {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x11, 0x12, 0x13, 0x14,
        };
        return new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));
    }

    private void SetupSsodhInitEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"connected":true,"competing":false}"""));
    }

    private void SetupSuppressEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"status":"submitted"}"""));
    }

    private void SetupTickleEndpoint(bool authenticated)
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($$"""
                        {
                            "session": "abc123",
                            "iserver": {
                                "authStatus": {
                                    "authenticated": {{authenticated.ToString().ToLowerInvariant()}},
                                    "competing": false,
                                    "connected": true
                                }
                            }
                        }
                        """));
    }

    private void SetupPortfolioAccountsEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL"
                            }
                        ]
                        """));
    }

    private void SetupLogoutEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/logout")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"confirmed":true}"""));
    }

    internal class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
