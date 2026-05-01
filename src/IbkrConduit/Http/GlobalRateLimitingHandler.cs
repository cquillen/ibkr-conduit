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
/// DelegatingHandler that enforces a global token bucket rate limit per tenant.
/// Requests wait asynchronously for a token; if the queue is full, a
/// <see cref="RateLimitRejectedException"/> is thrown.
/// </summary>
internal sealed partial class GlobalRateLimitingHandler : DelegatingHandler
{
    /// <summary>
    /// Threshold above which an <c>AcquireAsync</c> wait is considered abnormal
    /// and surfaced at <see cref="LogLevel.Warning"/> with limiter state. Tracks
    /// issue #173 (silent rate-limiter hangs were hard to diagnose post-hoc).
    /// </summary>
    private const int _slowAcquireThresholdMs = 5000;

    private static readonly Histogram<double> _waitDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.ratelimiter.global.wait_duration", "ms");

    private static readonly Counter<long> _rejectedCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.ratelimiter.global.rejected.count");

    private readonly RateLimiter _limiter;
    private readonly ILogger<GlobalRateLimitingHandler> _logger;

    /// <summary>
    /// Creates a new global rate limiting handler.
    /// </summary>
    /// <param name="limiter">The shared token bucket rate limiter instance.</param>
    /// <param name="logger">Logger for rate limit events.</param>
    public GlobalRateLimitingHandler(RateLimiter limiter, ILogger<GlobalRateLimitingHandler> logger)
    {
        _limiter = limiter;
        _logger = logger;

        IbkrConduitDiagnostics.Meter.CreateObservableGauge(
            "ibkr.conduit.ratelimiter.global.queue_depth",
            () => _limiter.GetStatistics()?.CurrentQueuedCount ?? 0);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var lease = await _limiter.AcquireAsync(1, cancellationToken);
        sw.Stop();

        _waitDuration.Record(sw.Elapsed.TotalMilliseconds);

        if (sw.ElapsedMilliseconds > 0)
        {
            var requestPath = request.RequestUri?.AbsolutePath ?? "unknown";
            LogGlobalRateLimiterWait(requestPath, sw.ElapsedMilliseconds);
            using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.GlobalRateLimit.Wait");
            activity?.SetTag("wait_ms", sw.ElapsedMilliseconds);

            if (sw.ElapsedMilliseconds >= _slowAcquireThresholdMs)
            {
                var stats = _limiter.GetStatistics();
                LogGlobalRateLimiterSlowAcquire(
                    requestPath,
                    sw.ElapsedMilliseconds,
                    stats?.CurrentQueuedCount ?? -1,
                    stats?.CurrentAvailablePermits ?? -1);
            }
        }

        if (!lease.IsAcquired)
        {
            var requestPath = request.RequestUri?.AbsolutePath ?? "unknown";
            _rejectedCount.Add(1);
            LogGlobalRateLimiterRejected(requestPath);
            throw new RateLimitRejectedException(
                "Global rate limit exceeded — queue is full.");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Global rate limiter rejected request to {RequestPath} — queue full")]
    private partial void LogGlobalRateLimiterRejected(string requestPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Global rate limiter wait for {RequestPath}: {WaitDurationMs}ms")]
    private partial void LogGlobalRateLimiterWait(string requestPath, long waitDurationMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Global rate limiter slow acquire for {RequestPath}: waited {WaitDurationMs}ms (queue depth {QueueDepth}, available permits {AvailablePermits}). See issue #173.")]
    private partial void LogGlobalRateLimiterSlowAcquire(
        string requestPath, long waitDurationMs, long queueDepth, long availablePermits);
}
