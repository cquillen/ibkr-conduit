using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Diagnostics;

public class TenantTaggingTests
{
    [Fact]
    public void TenantContext_ExposesTenantId()
    {
        var ctx = new TenantContext("tenant-42");
        ctx.TenantId.ShouldBe("tenant-42");
    }

    [Fact]
    public async Task GlobalRateLimitingHandler_EmitsMeasurement_TaggedWithTenantId()
    {
        // Capture the wait_duration histogram measurements emitted by IbkrConduit's meter.
        // A MeterListener records the raw tag set for each measurement so we can assert
        // the tenant dimension actually reaches the emitted metric — not merely that a
        // TenantContext exists.
        var captured = new List<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.ratelimiter.global.wait_duration")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => captured.Add(tags.ToArray()));
        listener.Start();

        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var handler = new GlobalRateLimitingHandler(
            new NoOpSharedRateGovernor(),
            limiter,
            NullLogger<GlobalRateLimitingHandler>.Instance,
            new TenantContext("tid"))
        {
            InnerHandler = new StubHandler(),
        };

        using var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        // Flush any pending measurements to the callback.
        listener.Dispose();

        captured.ShouldNotBeEmpty();
        captured.ShouldContain(
            tags => tags.Any(t => t.Key == LogFields.TenantId && (string?)t.Value == "tid"),
            customMessage: "Expected a wait_duration measurement tagged with the tenant id.");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
