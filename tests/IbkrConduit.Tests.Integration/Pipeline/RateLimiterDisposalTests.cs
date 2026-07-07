using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// VCR-09 / MGR-5: every per-tenant rate limiter (the global limiter, the 8 endpoint limiters,
/// and the 2 Flex limiters) must be owned by the DI container — registered via factory lambdas
/// (or a container-owned disposable holder) — so <c>provider.DisposeAsync()</c> disposes them and
/// stops their <c>AutoReplenishment</c> timers. Pre-fix they were registered as pre-built
/// instances that Microsoft.Extensions.DependencyInjection never disposes, so tenant churn
/// stranded live replenishment timers unboundedly.
/// </summary>
public sealed class RateLimiterDisposalTests
{
    [Fact]
    public async Task ProviderDisposal_DisposesGlobalEndpointAndFlexRateLimiters()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(o =>
        {
            o.Credentials = TestCredentials.Create("VCR09-DISPOSE-KEY", "dispose-token", "vcr09-dispose");
            o.BaseUrl = "http://127.0.0.1:9";      // never contacted — the graph is built but not driven
            o.FlexToken = "synthetic-flex-token";  // registers the Flex limiter pair
        });
        var provider = services.BuildServiceProvider();

        var globalLimiter = provider.GetRequiredService<RateLimiter>();
        var endpointLimiters = provider.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>();
        var flexLimiters = provider.GetRequiredService<FlexRateLimiters>();

        // Live before disposal.
        globalLimiter.AttemptAcquire(1).IsAcquired.ShouldBeTrue();

        await provider.DisposeAsync();

        Should.Throw<ObjectDisposedException>(() => globalLimiter.AttemptAcquire(1));
        foreach (var limiter in endpointLimiters.Values)
        {
            Should.Throw<ObjectDisposedException>(() => limiter.AttemptAcquire(1));
        }
        Should.Throw<ObjectDisposedException>(() => flexLimiters.Burst.AttemptAcquire(1));
        Should.Throw<ObjectDisposedException>(() => flexLimiters.Sustained.AttemptAcquire(1));
    }
}
