using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging;
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

    [Fact]
    [Trait("Category", "Slow")]
    public async Task SendAsync_AcquireExceedsSlowThreshold_LogsWarning()
    {
        // Repro: configure the bucket so the second AcquireAsync queues and waits
        // ~6 seconds (one replenishment period) for its token. The handler's
        // 5000ms threshold is crossed; expect a Warning-level log carrying the
        // limiter's queue depth and available-permit counts. This is the
        // observability hook tracked in issue #173 — when a future CI flake
        // or production hang surfaces, this log gives us actionable evidence.
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(6),
            TokensPerPeriod = 1,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5,
        });

        var logger = new CapturingLogger();
        var handler = new GlobalRateLimitingHandler(limiter, logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK),
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        // First request: bucket has the only token, acquires immediately.
        (await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second request: queues, waits ~6s for replenishment, then succeeds.
        (await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        logger.Messages.ShouldContain(
            m => m.Level == LogLevel.Warning && m.Formatted.Contains("slow acquire", StringComparison.Ordinal),
            customMessage: $"Expected a Warning 'slow acquire' log. Captured: [{string.Join(", ", logger.Messages)}]");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StubHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }

    private sealed class CapturingLogger : ILogger<GlobalRateLimitingHandler>
    {
        public List<(LogLevel Level, string Formatted)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add((logLevel, formatter(state, exception)));
        }
    }
}
