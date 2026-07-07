using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IbkrConduit.Tests.Integration.MultiTenant;

/// <summary>
/// VCR-09 Done-when: driven end-to-end through the real <c>AddIbkrClientManager</c> DI stack
/// (WireMock HTTP + in-process mock WebSocket), a tenant add → remove → add churn must accumulate
/// NO stale observable gauges. A <see cref="MeterListener"/> watches across the whole churn: after
/// the first tenant is removed, its per-tenant Meters are disposed with its provider, so its
/// <c>queue_depth</c> and <c>connection_state</c> gauges stop reporting; only the live tenant's
/// tenant-tagged gauges remain (MGR-4 / FIL-7).
/// </summary>
public sealed class MetricsChurnTests
{
    private const string _queueDepthGauge = "ibkr.conduit.ratelimiter.global.queue_depth";
    private const string _connectionStateGauge = "ibkr.conduit.websocket.connection_state";

    [Fact]
    public async Task TenantChurn_AddRemoveAdd_AccumulatesNoStaleGauges()
    {
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        // Unique tenant ids so concurrently-running tests' gauges never pollute the assertions.
        var idA = $"vcr09-churn-a-{Guid.NewGuid():N}";
        var idB = $"vcr09-churn-b-{Guid.NewGuid():N}";
        var credsA = TestCredentials.Create("VCR09-A-KEY", "a-token", idA);
        var credsB = TestCredentials.Create("VCR09-B-KEY", "b-token", idB);
        MockLstServer.Register(wireMock, credsA);
        MockLstServer.Register(wireMock, credsB);
        StubSessionPaths(wireMock);

        var queueDepth = new List<(string Tenant, long Value)>();
        var connectionState = new List<(string Tenant, int Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name is _queueDepthGauge or _connectionStateGauge)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == _queueDepthGauge && TenantOf(tags) is { } tenant)
            {
                queueDepth.Add((tenant, measurement));
            }
        });
        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == _connectionStateGauge && TenantOf(tags) is { } tenant)
            {
                connectionState.Add((tenant, measurement));
            }
        });
        listener.Start();

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var manager = provider.GetRequiredService<IIbkrClientManager>();

        await manager.AddAsync(idA, credsA, cancellationToken: TestContext.Current.CancellationToken);
        (await manager.RemoveAsync(idA, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await manager.AddAsync(idB, credsB, cancellationToken: TestContext.Current.CancellationToken);

        listener.RecordObservableInstruments();

        queueDepth.Count(x => x.Tenant == idA).ShouldBe(0,
            "the removed tenant's queue_depth gauge must not linger after its provider is disposed.");
        connectionState.Count(x => x.Tenant == idA).ShouldBe(0,
            "the removed tenant's connection_state gauge must not linger after disposal.");
        queueDepth.Count(x => x.Tenant == idB).ShouldBe(1,
            "the live tenant reports exactly one tenant-tagged queue_depth gauge.");
        connectionState.Count(x => x.Tenant == idB).ShouldBe(1,
            "the live tenant reports exactly one tenant-tagged connection_state gauge.");
    }

    private static ServiceProvider BuildManagerProvider(WireMockServer wireMock, MockWebSocketServer mockWs)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClientManager(o =>
        {
            o.BaseUrl = wireMock.Url!;
            o.WebSocketBaseUrl = mockWs.Url;
            o.TickleIntervalSeconds = 3600;
            o.WebSocketHeartbeatIntervalSeconds = 3600;
        });
        return services.BuildServiceProvider();
    }

    private static void StubSessionPaths(WireMockServer server)
    {
        server.Given(Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

        server.Given(Request.Create().WithPath("/v1/api/tickle").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"session":"abc123","iserver":{"authStatus":{"authenticated":true,"competing":false,"connected":true}}}"""));

        server.Given(Request.Create().WithPath("/v1/api/logout").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}"""));
    }

    private static string? TenantOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
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
