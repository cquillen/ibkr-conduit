using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.RateLimiting;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Diagnostics;

/// <summary>
/// Regression tests for VCR-09 / MGR-4: the rate-limiter queue-depth gauge must be registered
/// once per tenant (against the limiter singleton) with a tenant tag, on a per-tenant Meter that
/// is disposed with the provider — so tenant churn accumulates no stale, untagged gauges. These
/// tests share one class so no other queue-depth-emitting component (only <see cref="TenantDiagnostics"/>
/// creates that instrument) runs concurrently while <see cref="Handler_Construction_RegistersNoUntaggedQueueDepthGauge"/>
/// counts published instruments.
/// </summary>
public sealed class TenantDiagnosticsTests
{
    private const string _queueDepthGauge = "ibkr.conduit.ratelimiter.global.queue_depth";

    [Fact]
    public void QueueDepthGauge_Registered_CarriesTenantTag()
    {
        using var limiter = NewLimiter();
        var captured = new List<KeyValuePair<string, object?>[]>();
        using var listener = NewQueueDepthListener((_, tags) => captured.Add(tags));
        listener.Start();

        using var diagnostics = new TenantDiagnostics(new TenantContext("tid-A"), limiter);
        listener.RecordObservableInstruments();

        captured.ShouldContain(
            tags => tags.Any(t => t.Key == LogFields.TenantId && (string?)t.Value == "tid-A"),
            customMessage: "Expected a queue_depth measurement tagged with the tenant id.");
    }

    [Fact]
    public void QueueDepthGauge_AfterDispose_StopsReporting()
    {
        using var limiter = NewLimiter();
        var values = new List<long>();
        using var listener = NewQueueDepthListener((value, tags) =>
        {
            if (TenantOf(tags) == "tid-B")
            {
                values.Add(value);
            }
        });
        listener.Start();

        var diagnostics = new TenantDiagnostics(new TenantContext("tid-B"), limiter);
        listener.RecordObservableInstruments();
        values.Count.ShouldBe(1, "the live tenant's queue_depth gauge reports exactly once");

        diagnostics.Dispose();
        values.Clear();
        listener.RecordObservableInstruments();

        values.ShouldBeEmpty("a disposed TenantDiagnostics leaves no gauge reporting (its Meter is disposed).");
    }

    [Fact]
    public void MultipleTenants_DisposeOne_LeavesOnlyTheLiveGaugeWithNoAccumulation()
    {
        using var limiterA = NewLimiter();
        using var limiterB = NewLimiter();
        var tenants = new List<string>();
        using var listener = NewQueueDepthListener((_, tags) =>
        {
            var tenant = TenantOf(tags);
            if (tenant is "tenant-A" or "tenant-B")
            {
                tenants.Add(tenant);
            }
        });
        listener.Start();

        var diagnosticsA = new TenantDiagnostics(new TenantContext("tenant-A"), limiterA);
        using var diagnosticsB = new TenantDiagnostics(new TenantContext("tenant-B"), limiterB);

        listener.RecordObservableInstruments();
        tenants.ShouldBe(["tenant-A", "tenant-B"], ignoreOrder: true);

        diagnosticsA.Dispose();
        tenants.Clear();
        listener.RecordObservableInstruments();

        tenants.ShouldBe(["tenant-B"], "the removed tenant's gauge must not linger after disposal.");
    }

    [Fact]
    public void Handler_Construction_RegistersNoUntaggedQueueDepthGauge()
    {
        // Post-fix, the ONLY queue_depth gauges are TenantDiagnostics' — every one tenant-tagged.
        // A handler that (as before) minted its own gauge per instance produced UNTAGGED
        // measurements. Asserting no untagged measurement therefore pins that the handler no longer
        // registers the gauge, and is immune to other tests' (tenant-tagged) gauges running in
        // parallel (MGR-4).
        using var limiter = NewLimiter();
        var untaggedSeen = false;
        using var listener = NewQueueDepthListener((_, tags) =>
        {
            if (TenantOf(tags) is null or "")
            {
                untaggedSeen = true;
            }
        });
        listener.Start();

        for (var i = 0; i < 5; i++)
        {
            using var handler = new GlobalRateLimitingHandler(
                new NoOpSharedRateGovernor(),
                limiter,
                NullLogger<GlobalRateLimitingHandler>.Instance,
                new TenantContext("handler-tenant"));
        }

        listener.RecordObservableInstruments();

        untaggedSeen.ShouldBeFalse(
            "the handler must not register an untagged queue_depth gauge; the gauge lives once per tenant in TenantDiagnostics (MGR-4).");
    }

    private static TokenBucketRateLimiter NewLimiter() =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 500,
        });

    private static MeterListener NewQueueDepthListener(Action<long, KeyValuePair<string, object?>[]> onMeasurement)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == _queueDepthGauge)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            onMeasurement(measurement, tags.ToArray()));
        return listener;
    }

    private static string? TenantOf(KeyValuePair<string, object?>[] tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == LogFields.TenantId)
            {
                return tag.Value as string;
            }
        }

        return null;
    }
}
