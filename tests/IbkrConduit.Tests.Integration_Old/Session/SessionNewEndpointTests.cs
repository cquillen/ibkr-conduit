using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Session;

public class SessionNewEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public SessionNewEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task ResetSuppressedQuestionsAsync_ReturnsStatus()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress/reset")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"status":"submitted"}"""));

        var api = CreateRefitClient<IIbkrSessionApi>();

        var result = await api.ResetSuppressedQuestionsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("submitted");
    }

    [Fact]
    public async Task GetAuthStatusAsync_ReturnsAuthStatus()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/status")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "authenticated": true,
                            "competing": false,
                            "connected": true,
                            "fail": null,
                            "message": null,
                            "prompts": null
                        }
                        """));

        var api = CreateRefitClient<IIbkrSessionApi>();

        var result = await api.GetAuthStatusAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Authenticated.ShouldBeTrue();
        result.Competing.ShouldBeFalse();
        result.Connected.ShouldBeTrue();
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
