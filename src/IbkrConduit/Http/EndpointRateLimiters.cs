using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;

namespace IbkrConduit.Http;

/// <summary>
/// Container-owned holder for a tenant's per-endpoint rate limiters. The dictionary itself is not
/// <see cref="IDisposable"/>, so this holder is registered as a singleton (via a factory lambda)
/// and disposes each endpoint limiter — stopping its <c>AutoReplenishment</c> timer — when the
/// tenant provider is disposed. Without it, Microsoft.Extensions.DependencyInjection would never
/// dispose the limiters and every tenant remove would strand live timers (VCR-09 / MGR-5).
/// </summary>
internal sealed class EndpointRateLimiters : IDisposable
{
    /// <summary>Creates a holder over the given endpoint limiters.</summary>
    /// <param name="limiters">The per-endpoint rate limiters, keyed by endpoint path.</param>
    public EndpointRateLimiters(IReadOnlyDictionary<string, RateLimiter> limiters) => Limiters = limiters;

    /// <summary>The per-endpoint rate limiters, keyed by endpoint path.</summary>
    public IReadOnlyDictionary<string, RateLimiter> Limiters { get; }

    /// <summary>Disposes every held endpoint limiter, stopping its replenishment timer.</summary>
    public void Dispose()
    {
        foreach (var limiter in Limiters.Values)
        {
            limiter.Dispose();
        }
    }
}
