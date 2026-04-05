using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class GlobalRateLimitingHandlerTests
{
    [Fact]
    public async Task SendAsync_WhenTokenAvailable_ForwardsRequest()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(limiter, NullLogger<GlobalRateLimitingHandler>.Instance)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_WhenQueueFull_ThrowsRateLimitRejectedException()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 1,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(limiter, NullLogger<GlobalRateLimitingHandler>.Instance)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        // Consume the single token
        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        // Next request should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_MultipleRequestsWithinLimit_AllSucceed()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 5,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(limiter, NullLogger<GlobalRateLimitingHandler>.Instance)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler);

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
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
