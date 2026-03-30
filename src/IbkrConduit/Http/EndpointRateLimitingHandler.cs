using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that enforces per-endpoint token bucket rate limits.
/// Matches request URLs against known path patterns and applies the
/// corresponding limiter. Unmatched URLs pass through without limiting.
/// </summary>
internal sealed class EndpointRateLimitingHandler : DelegatingHandler
{
    private readonly IReadOnlyDictionary<string, RateLimiter> _endpointLimiters;

    /// <summary>
    /// Creates a new endpoint rate limiting handler.
    /// </summary>
    /// <param name="endpointLimiters">
    /// A dictionary mapping URL path patterns to their rate limiters.
    /// A request matches if its path contains the pattern (case-insensitive).
    /// </param>
    public EndpointRateLimitingHandler(IReadOnlyDictionary<string, RateLimiter> endpointLimiters)
    {
        _endpointLimiters = endpointLimiters;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var limiter = FindLimiter(request);

        if (limiter != null)
        {
            using var lease = await limiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new RateLimitRejectedException(
                    $"Endpoint rate limit exceeded for {request.RequestUri?.PathAndQuery} — queue is full.");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private RateLimiter? FindLimiter(HttpRequestMessage request)
    {
        var path = request.RequestUri?.PathAndQuery;
        if (path == null)
        {
            return null;
        }

        foreach (var kvp in _endpointLimiters)
        {
            if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
    }
}
