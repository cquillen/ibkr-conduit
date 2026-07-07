using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;

namespace IbkrConduit.Diagnostics;

/// <summary>
/// Per-provider diagnostics component that owns the rate-limiter queue-depth observable gauge
/// (<c>ibkr.conduit.ratelimiter.global.queue_depth</c>) for one tenant. The gauge is created once
/// per tenant against the tenant's <see cref="RateLimiter"/> singleton and every observation is
/// stamped with the tenant id, so multiple tenants in one process are distinguishable. The gauge
/// lives on a per-tenant <see cref="Meter"/> that shares the library's public
/// <see cref="IbkrConduitDiagnostics.MeterName"/> (so consumers subscribing by name still see it)
/// but is disposed with the provider — so tenant add/remove churn accumulates no stale, untagged
/// gauge instruments pinning retired limiters (VCR-09 / MGR-4). No global or static mutable state
/// is introduced: the Meter is instance-scoped to the tenant provider.
/// </summary>
internal sealed class TenantDiagnostics : IDisposable
{
    private readonly Meter _meter;

    /// <summary>
    /// Creates the per-tenant diagnostics component and registers the queue-depth gauge.
    /// </summary>
    /// <param name="tenant">Per-provider tenant identity used to tag the gauge observations.</param>
    /// <param name="globalRateLimiter">The tenant's global rate limiter singleton observed for queue depth.</param>
    public TenantDiagnostics(TenantContext tenant, RateLimiter globalRateLimiter)
    {
        _meter = new Meter(IbkrConduitDiagnostics.MeterName);
        var tenantTag = new KeyValuePair<string, object?>(LogFields.TenantId, tenant.TenantId);

        _meter.CreateObservableGauge(
            "ibkr.conduit.ratelimiter.global.queue_depth",
            () => new Measurement<long>(
                globalRateLimiter.GetStatistics()?.CurrentQueuedCount ?? 0,
                tenantTag));
    }

    /// <summary>Disposes the per-tenant Meter, removing this tenant's gauge instrument(s).</summary>
    public void Dispose() => _meter.Dispose();
}
