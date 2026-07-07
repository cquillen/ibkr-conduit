using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IbkrConduit.Http;

/// <summary>
/// Registers global and per-endpoint rate limiters.
/// </summary>
internal static class RateLimitingAndResilienceRegistration
{
    /// <summary>
    /// Creates and registers rate limiting singletons shared across all HTTP pipelines.
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<ISharedRateGovernor, NoOpSharedRateGovernor>();

        // Register via factory lambdas so the DI container OWNS these limiters and disposes them
        // (stopping their AutoReplenishment timers) when the tenant provider is disposed. A
        // pre-built instance passed to AddSingleton would never be disposed by MS.DI, leaking a
        // live replenishment timer per tenant remove/re-add cycle (VCR-09 / MGR-5).
        services.AddSingleton<RateLimiter>(_ => CreateGlobalRateLimiter());

        // The endpoint dictionary itself is not IDisposable, so a container-owned holder disposes
        // each endpoint limiter within it.
        services.AddSingleton(_ => new EndpointRateLimiters(CreateEndpointRateLimiters()));
        services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(
            sp => sp.GetRequiredService<EndpointRateLimiters>().Limiters);
    }

    /// <summary>
    /// Creates the global token bucket rate limiter (10 req/s, queue 500).
    /// </summary>
    private static TokenBucketRateLimiter CreateGlobalRateLimiter() =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 500,
        });

    /// <summary>
    /// Creates the per-endpoint rate limiters for known IBKR endpoints.
    /// </summary>
    private static Dictionary<string, RateLimiter> CreateEndpointRateLimiters()
    {
        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/trades"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/pnl/partitioned"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/marketdata/snapshot"] = CreateEndpointLimiter(10, TimeSpan.FromSeconds(1), 50),
            ["/iserver/scanner/params"] = CreateEndpointLimiter(1, TimeSpan.FromMinutes(15), 50),
            ["/iserver/scanner/run"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(1), 50),
            ["/portfolio/accounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/portfolio/subaccounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
        };

        return limiters;
    }

    private static TokenBucketRateLimiter CreateEndpointLimiter(
        int tokenLimit, TimeSpan replenishmentPeriod, int queueLimit) =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            ReplenishmentPeriod = replenishmentPeriod,
            TokensPerPeriod = tokenLimit,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit,
        });
}
