using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class TokenRefreshHandlerTests
{
    [Fact]
    public async Task SendAsync_401WithBody_ClonesBodyForRetry()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var content = new StringContent("""{"side":"BUY"}""", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/v1/api/iserver/account/1234/orders")
        {
            Content = content,
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessionManager.ReauthCallCount.ShouldBe(1);
        innerHandler.CallCount.ShouldBe(2);
        // The retry request should have had content (body was cloned)
        innerHandler.LastRequestHadContent.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_TicklePath_SkipsRetryOn401()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(HttpStatusCode.Unauthorized);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/tickle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sessionManager.ReauthCallCount.ShouldBe(0);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_RetryAlso401_ReturnsSecondResponse()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Unauthorized);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/iserver/account/orders",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sessionManager.ReauthCallCount.ShouldBe(1);
        innerHandler.CallCount.ShouldBe(2); // original + one retry only
    }

    [Fact]
    public async Task SendAsync_Non401_PassesThrough()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(HttpStatusCode.OK);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessionManager.ReauthCallCount.ShouldBe(0);
        innerHandler.CallCount.ShouldBe(1);
    }

    private sealed class FakeSessionManager : ISessionManager
    {
        public int ReauthCallCount { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken)
        {
            ReauthCallCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public bool LastRequestHadContent { get; private set; }

        public SequenceHandler(params HttpStatusCode[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            LastRequestHadContent = request.Content != null;
            var statusCode = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
