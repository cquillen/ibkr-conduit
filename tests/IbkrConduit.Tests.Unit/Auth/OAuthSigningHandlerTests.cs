using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
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

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

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
