using System;
using System.Threading.RateLimiting;

namespace IbkrConduit.Http;

/// <summary>
/// Container-owned holder for a tenant's two Flex Web Service rate limiters (a per-second burst
/// limiter and a per-minute sustained limiter). Previously these lived only inside the Flex HTTP
/// handler closures, so Microsoft.Extensions.DependencyInjection never disposed them and their
/// <c>AutoReplenishment</c> timers outlived the tenant. Registered as a singleton (via a factory
/// lambda), this holder disposes both limiters when the tenant provider is disposed
/// (VCR-09 / MGR-5).
/// </summary>
internal sealed class FlexRateLimiters : IDisposable
{
    /// <summary>Creates a holder over the Flex burst and sustained limiters.</summary>
    /// <param name="burst">The per-second burst limiter.</param>
    /// <param name="sustained">The per-minute sustained limiter.</param>
    public FlexRateLimiters(RateLimiter burst, RateLimiter sustained)
    {
        Burst = burst;
        Sustained = sustained;
    }

    /// <summary>The per-second Flex burst limiter.</summary>
    public RateLimiter Burst { get; }

    /// <summary>The per-minute Flex sustained limiter.</summary>
    public RateLimiter Sustained { get; }

    /// <summary>Disposes both Flex limiters, stopping their replenishment timers.</summary>
    public void Dispose()
    {
        Burst.Dispose();
        Sustained.Dispose();
    }
}
