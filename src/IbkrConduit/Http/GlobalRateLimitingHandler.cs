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

    private readonly ISharedRateGovernor _governor;
    private readonly RateLimiter _limiter;
    private readonly ILogger<GlobalRateLimitingHandler> _logger;
    private readonly TenantContext _tenant;
    private readonly Dictionary<string, object> _logScope;

    /// <summary>
    /// Creates a new global rate limiting handler.
    /// </summary>
    /// <param name="governor">Process-wide shared rate governor, acquired before the tenant limiter.</param>
    /// <param name="limiter">The shared token bucket rate limiter instance.</param>
    /// <param name="logger">Logger for rate limit events.</param>
    /// <param name="tenant">Per-provider tenant identity used to tag telemetry.</param>
    public GlobalRateLimitingHandler(ISharedRateGovernor governor, RateLimiter limiter, ILogger<GlobalRateLimitingHandler> logger, TenantContext tenant)
    {
        _governor = governor;
        _limiter = limiter;
        _logger = logger;
        _tenant = tenant;
        _logScope = new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId };

        // The queue-depth gauge is NOT registered here: a handler is constructed once per HTTP
        // pipeline (up to a dozen per tenant), so registering per instance minted duplicate,
        // untagged gauges that pinned retired handler chains and limiters. It now lives once per
        // tenant on TenantDiagnostics' per-tenant Meter, tenant-tagged and disposed with the
        // provider (VCR-09 / MGR-4).
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope(_logScope);

        await _governor.AcquireAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        using var lease = await _limiter.AcquireAsync(1, cancellationToken);
        sw.Stop();

        _waitDuration.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));

        if (sw.ElapsedMilliseconds > 0)
        {
            var requestPath = request.RequestUri?.AbsolutePath ?? "unknown";
            LogGlobalRateLimiterWait(requestPath, sw.ElapsedMilliseconds);
            using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.GlobalRateLimit.Wait");
            activity?.SetTag(LogFields.TenantId, _tenant.TenantId);
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
            _rejectedCount.Add(1,
                new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
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
