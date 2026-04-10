using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthSigningHandlerTests
{
    [Fact]
    public async Task SendAsync_AttachesAuthorizationHeader()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new FakeInnerHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        // Verify Authorization header is present and contains expected OAuth params
        var authValues = capturedRequest.Headers.GetValues("Authorization").ToList();
        authValues.ShouldNotBeEmpty();
        var authHeader = authValues[0];
        authHeader.ShouldContain("oauth_consumer_key=\"MYKEY\"");
        authHeader.ShouldContain("oauth_token=\"mytoken\"");
        authHeader.ShouldContain("oauth_signature_method=\"HMAC-SHA256\"");
        authHeader.ShouldContain("oauth_signature=");
        authHeader.ShouldContain("realm=\"limited_poa\"");
    }

    [Fact]
    public async Task SendAsync_UsesHmacSha256Signing()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_CallsEnsureInitializedAsync_BeforeSigning()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var sessionManager = new FakeSessionManager();

        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var signingHandler = new OAuthSigningHandler(
            provider, "MYKEY", "mytoken", sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken);

        sessionManager.EnsureInitCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_EnsureInitializedThrows_ExceptionPropagates()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var sessionManager = new ThrowingSessionManager();

        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var signingHandler = new OAuthSigningHandler(
            provider, "MYKEY", "mytoken", sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_ExistingUserAgent_NotReplaced()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new FakeInnerHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, "portfolio/accounts");
        request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("CustomAgent", "2.0"));

        await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        var userAgents = capturedRequest.Headers.UserAgent.ToList();
        userAgents.Count.ShouldBe(1);
        userAgents[0].Product!.Name.ShouldBe("CustomAgent");
        userAgents[0].Product!.Version.ShouldBe("2.0");
    }

    [Fact]
    public async Task SendAsync_InnerHandlerThrows_ExceptionPropagates()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var innerHandler = new FakeInnerHandler(_ =>
            throw new HttpRequestException("Simulated network failure"));

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await Should.ThrowAsync<HttpRequestException>(
            () => httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken));
    }

    private class ThrowingSessionManager : ISessionManager
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Session initialization failed");

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class FakeSessionManager : ISessionManager
    {
        public bool EnsureInitCalled { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            EnsureInitCalled = true;
            return Task.CompletedTask;
        }

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public DateTimeOffset? CurrentTokenExpiry => _token.Expiry;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }

    private class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeInnerHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
