using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Accounts;

public class AccountEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public AccountEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountsList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "accounts": ["DU1234567", "DU7654321"],
                            "selectedAccount": "DU1234567"
                        }
                        """));

        var api = CreateRefitClient<IIbkrAccountApi>();

        var result = await api.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.Count.ShouldBe(2);
        result.Accounts[0].ShouldBe("DU1234567");
        result.SelectedAccount.ShouldBe("DU1234567");
    }

    [Fact]
    public async Task SwitchAccountAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"set":true,"selectedAccount":"DU7654321"}"""));

        var api = CreateRefitClient<IIbkrAccountApi>();

        var result = await api.SwitchAccountAsync(
            new SwitchAccountRequest("DU7654321"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Set.ShouldBeTrue();
        result.SelectedAccount.ShouldBe("DU7654321");
    }

    [Fact]
    public async Task SetDynAccountAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/dynaccount")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"set":true,"selectedAccount":"DU9999999"}"""));

        var api = CreateRefitClient<IIbkrAccountApi>();

        var result = await api.SetDynAccountAsync(
            new DynAccountRequest("DU9999999"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Set.ShouldBeTrue();
        result.SelectedAccount.ShouldBe("DU9999999");
    }

    [Fact]
    public async Task SearchAccountsAsync_ReturnsResults()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/search/DU123")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "accountId": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "accountType": "INDIVIDUAL"
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrAccountApi>();

        var result = await api.SearchAccountsAsync("DU123", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].AccountId.ShouldBe("DU1234567");
        result[0].AccountTitle.ShouldBe("Paper Trading");
    }

    [Fact]
    public async Task GetAccountInfoAsync_ReturnsAccountInfo()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "accountId": "DU1234567",
                            "accountTitle": "Paper Trading",
                            "accountType": "INDIVIDUAL"
                        }
                        """));

        var api = CreateRefitClient<IIbkrAccountApi>();

        var result = await api.GetAccountInfoAsync("DU1234567", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccountId.ShouldBe("DU1234567");
        result.AccountTitle.ShouldBe("Paper Trading");
        result.AccountType.ShouldBe("INDIVIDUAL");
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private TApi CreateRefitClient<TApi>() where TApi : class
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<TApi>(httpClient);
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
