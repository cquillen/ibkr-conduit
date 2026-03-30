using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class EndpointRateLimitingHandlerTests : IDisposable
{
    private readonly TokenBucketRateLimiter _ordersLimiter;
    private readonly Dictionary<string, RateLimiter> _limiters;

    public EndpointRateLimitingHandlerTests()
    {
        _ordersLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 1,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        _limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = _ordersLimiter,
        };
    }

    [Fact]
    public async Task SendAsync_MatchedEndpoint_AcquiresToken()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/v1/api/iserver/account/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_MatchedEndpoint_WhenQueueFull_ThrowsRateLimitRejectedException()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Consume the single token
        await client.GetAsync("http://localhost/v1/api/iserver/account/orders");

        // Next request should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync("http://localhost/v1/api/iserver/account/orders"));
    }

    [Fact]
    public async Task SendAsync_UnmatchedEndpoint_PassesThroughWithoutLimiting()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Unmatched endpoint should always pass through even after token is consumed
        await client.GetAsync("http://localhost/v1/api/iserver/account/orders");

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("http://localhost/v1/api/some/other/endpoint");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task SendAsync_NullRequestUri_PassesThroughWithoutLimiting()
    {
        var handler = new EndpointRateLimitingHandler(_limiters)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, (Uri?)null);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose()
    {
        _ordersLimiter.Dispose();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StubHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
