using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Portfolio;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Portfolio;

public class PortfolioAccountsTests : IDisposable
{
    private readonly WireMockServer _server;

    public PortfolioAccountsTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetAccountsAsync_WithMockedServer_ReturnsAccountList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet()
                .WithHeader("Authorization", "*"))
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

        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

        var accounts = await api.GetAccountsAsync();

        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");
        accounts[0].AccountTitle.ShouldBe("Paper Trading");
    }

    [Fact]
    public async Task GetAccountsAsync_Unauthorized_ThrowsApiException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var lstBytes = new byte[20];
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

        await Should.ThrowAsync<Refit.ApiException>(() => api.GetAccountsAsync());
    }

    /// <summary>
    /// End-to-end test against a real IBKR paper account.
    /// Requires IBKR_* environment variables to be set.
    /// Run manually: dotnet test --filter "PortfolioAccounts_WithPaperAccount_ReturnsAccountList"
    /// </summary>
    [Fact(Skip = "Requires real IBKR paper account credentials in environment variables")]
    public async Task PortfolioAccounts_WithPaperAccount_ReturnsAccountList()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();

        using var lstHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };
        var lstClient = new LiveSessionTokenClient(lstHttpClient);
        var tokenProvider = new SessionTokenProvider(creds, lstClient);

        var signingHandler = new OAuthSigningHandler(tokenProvider, creds.ConsumerKey, creds.AccessToken)
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

        var accounts = await api.GetAccountsAsync();

        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
