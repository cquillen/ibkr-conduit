using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that enforces a global token bucket rate limit per tenant.
/// Requests wait asynchronously for a token; if the queue is full, a
/// <see cref="RateLimitRejectedException"/> is thrown.
/// </summary>
internal sealed class GlobalRateLimitingHandler : DelegatingHandler
{
    private readonly RateLimiter _limiter;

    /// <summary>
    /// Creates a new global rate limiting handler.
    /// </summary>
    /// <param name="limiter">The shared token bucket rate limiter instance.</param>
    public GlobalRateLimitingHandler(RateLimiter limiter)
    {
        _limiter = limiter;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var lease = await _limiter.AcquireAsync(1, cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > 0)
        {
            using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.GlobalRateLimit.Wait");
            activity?.SetTag("wait_ms", sw.ElapsedMilliseconds);
        }

        if (!lease.IsAcquired)
        {
            throw new RateLimitRejectedException(
                "Global rate limit exceeded — queue is full.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
