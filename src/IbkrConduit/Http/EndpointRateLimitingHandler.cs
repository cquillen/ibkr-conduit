using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that enforces per-endpoint token bucket rate limits.
/// Matches request URLs against known path patterns and applies the
/// corresponding limiter. Unmatched URLs pass through without limiting.
/// </summary>
internal sealed partial class EndpointRateLimitingHandler : DelegatingHandler
{
    /// <summary>
    /// Threshold above which an <c>AcquireAsync</c> wait is considered abnormal
    /// and surfaced at <see cref="LogLevel.Warning"/> with limiter state. Tracks
    /// issue #173 (silent rate-limiter hangs were hard to diagnose post-hoc).
    /// </summary>
    private const int _slowAcquireThresholdMs = 5000;

    private static readonly Histogram<double> _waitDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.ratelimiter.endpoint.wait_duration", "ms");

    private static readonly Counter<long> _rejectedCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.ratelimiter.endpoint.rejected.count");

    private readonly IReadOnlyDictionary<string, RateLimiter> _endpointLimiters;
    private readonly ILogger<EndpointRateLimitingHandler> _logger;

    /// <summary>
    /// Creates a new endpoint rate limiting handler.
    /// </summary>
    /// <param name="endpointLimiters">
    /// A dictionary mapping URL path patterns to their rate limiters.
    /// A request matches if its path contains the pattern (case-insensitive).
    /// </param>
    /// <param name="logger">Logger for rate limit events.</param>
    public EndpointRateLimitingHandler(
        IReadOnlyDictionary<string, RateLimiter> endpointLimiters,
        ILogger<EndpointRateLimitingHandler> logger)
    {
        _endpointLimiters = endpointLimiters;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (limiter, pattern) = FindLimiter(request);

        if (limiter != null)
        {
            var endpoint = request.RequestUri?.PathAndQuery ?? "unknown";
            var sw = Stopwatch.StartNew();
            using var lease = await limiter.AcquireAsync(1, cancellationToken);
            sw.Stop();

            _waitDuration.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(LogFields.Endpoint, endpoint));

            if (sw.ElapsedMilliseconds > 0)
            {
                LogEndpointRateLimiterWait(pattern!, endpoint, sw.ElapsedMilliseconds);
                using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.EndpointRateLimit.Wait");
                activity?.SetTag(LogFields.Endpoint, endpoint);
                activity?.SetTag("wait_ms", sw.ElapsedMilliseconds);

                if (sw.ElapsedMilliseconds >= _slowAcquireThresholdMs)
                {
                    var stats = limiter.GetStatistics();
                    LogEndpointRateLimiterSlowAcquire(
                        pattern!,
                        endpoint,
                        sw.ElapsedMilliseconds,
                        stats?.CurrentQueuedCount ?? -1,
                        stats?.CurrentAvailablePermits ?? -1);
                }
            }

            if (!lease.IsAcquired)
            {
                _rejectedCount.Add(1,
                    new KeyValuePair<string, object?>(LogFields.Endpoint, endpoint));
                LogEndpointRateLimiterRejected(pattern!, endpoint);
                throw new RateLimitRejectedException(
                    $"Endpoint rate limit exceeded for {request.RequestUri?.PathAndQuery} — queue is full.");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Endpoint rate limiter rejected request to {RequestPath} (pattern: {EndpointPattern}) — queue full")]
    private partial void LogEndpointRateLimiterRejected(string endpointPattern, string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Endpoint rate limiter wait for {RequestPath} (pattern: {EndpointPattern}): {WaitDurationMs}ms")]
    private partial void LogEndpointRateLimiterWait(string endpointPattern, string requestPath, long waitDurationMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Endpoint rate limiter slow acquire for {RequestPath} (pattern: {EndpointPattern}): waited {WaitDurationMs}ms (queue depth {QueueDepth}, available permits {AvailablePermits}). See issue #173.")]
    private partial void LogEndpointRateLimiterSlowAcquire(
        string endpointPattern, string requestPath, long waitDurationMs, long queueDepth, long availablePermits);

    private (RateLimiter? Limiter, string? Pattern) FindLimiter(HttpRequestMessage request)
    {
        var path = request.RequestUri?.PathAndQuery;
        if (path == null)
        {
            return (null, null);
        }

        foreach (var kvp in _endpointLimiters)
        {
            if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return (kvp.Value, kvp.Key);
            }
        }

        return (null, null);
    }
}
