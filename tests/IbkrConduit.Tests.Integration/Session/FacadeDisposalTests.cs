using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// Integration tests for the single-account (<c>AddIbkrClient</c>) facade disposal-ownership
/// contract (design doc §5.4, PVR-21, finding PRB-4.3), driven end-to-end through the real DI
/// stack against WireMock. <c>IbkrClient.DisposeAsync</c> performs the full-client teardown — the
/// WebSocket client is disconnected/disposed (it was untouched by the pre-PVR-21 facade), then the
/// session is logged out and disposed — idempotently via an atomic guard, so <c>await using
/// client</c> plus provider disposal issues exactly one logout and one active-session gauge
/// decrement.
/// </summary>
public sealed class FacadeDisposalTests
{
    private const string _logoutPath = "/v1/api/logout";
    private const string _tenantId = "facade-dispose-tenant";

    /// <summary>
    /// After the session is established, <c>await using client</c> disposes the WebSocket client and
    /// issues exactly one logout + one gauge decrement; a subsequent provider disposal of the same
    /// container-owned singletons does NOT double-run the teardown.
    /// </summary>
    [Fact]
    public async Task FacadeDisposeThenProviderDispose_TearsDownWebSocketAndLogsOutExactlyOnce()
    {
        using var wireMock = WireMockServer.Start();
        var creds = TestCredentials.Create(TestCredentials.ConsumerKey, TestCredentials.AccessToken, _tenantId);
        StubAuth(wireMock, creds);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(o =>
        {
            o.Credentials = creds;
            o.BaseUrl = wireMock.Url!;
            // Keep the tickle timer quiet for the duration of a short test.
            o.TickleIntervalSeconds = 3600;
            o.WebSocketHeartbeatIntervalSeconds = 3600;
        });
        await using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IIbkrClient>();
        var webSocketClient = provider.GetRequiredService<IIbkrWebSocketClient>();

        // Establish the brokerage session so a logout is warranted and the active-session gauge
        // has been incremented before we start listening for the decrement.
        await client.ValidateConnectionAsync(
            validateFlex: false, cancellationToken: TestContext.Current.CancellationToken);

        // Start the gauge listener AFTER init so it captures only the dispose-time decrement.
        long gaugeNet = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.session.active")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var t in tags)
            {
                if (t.Key == LogFields.TenantId && (string?)t.Value == _tenantId)
                {
                    Interlocked.Add(ref gaugeNet, measurement);
                }
            }
        });
        listener.Start();

        // `await using client`: the facade owns the full-client teardown.
        await client.DisposeAsync();

        LogoutCount(wireMock).ShouldBe(1, "facade dispose must issue exactly one logout");
        Interlocked.Read(ref gaugeNet).ShouldBe(-1, "facade dispose must decrement the active-session gauge once");

        // The facade disposed the WebSocket client — its disposal guard now rejects further use.
        Should.Throw<ObjectDisposedException>(() => webSocketClient.RegisterConnectionEvents());

        // Provider disposal of the same container-owned singletons must NOT double-run teardown.
        await provider.DisposeAsync();

        LogoutCount(wireMock).ShouldBe(1, "provider disposal after facade dispose must not issue a second logout");
        Interlocked.Read(ref gaugeNet).ShouldBe(-1, "provider disposal must not decrement the gauge a second time");
    }

    /// <summary>
    /// The opposite disposal direction: the consumer NEVER calls <c>client.DisposeAsync</c> directly —
    /// only the DI provider is disposed. The container-owned <c>IbkrClient</c> singleton is an
    /// <c>IAsyncDisposable</c>, so provider disposal drives the facade's full-client teardown: the
    /// WebSocket client is disposed and the session is logged out exactly once (one gauge decrement),
    /// with the idempotent guards converging the facade-owned teardown and any direct container
    /// disposal to exactly-once (design doc §5.4, PVR-21).
    /// </summary>
    [Fact]
    public async Task ProviderDisposeWithoutFacadeDispose_RunsFacadeTeardownExactlyOnce()
    {
        using var wireMock = WireMockServer.Start();
        var creds = TestCredentials.Create(TestCredentials.ConsumerKey, TestCredentials.AccessToken, _tenantId);
        StubAuth(wireMock, creds);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(o =>
        {
            o.Credentials = creds;
            o.BaseUrl = wireMock.Url!;
            o.TickleIntervalSeconds = 3600;
            o.WebSocketHeartbeatIntervalSeconds = 3600;
        });
        // Deliberately NOT `await using` — we dispose the provider explicitly and assert afterwards,
        // and the facade's DisposeAsync is never called directly by this test.
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IIbkrClient>();
        var webSocketClient = provider.GetRequiredService<IIbkrWebSocketClient>();

        await client.ValidateConnectionAsync(
            validateFlex: false, cancellationToken: TestContext.Current.CancellationToken);

        long gaugeNet = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                    && instrument.Name == "ibkr.conduit.session.active")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var t in tags)
            {
                if (t.Key == LogFields.TenantId && (string?)t.Value == _tenantId)
                {
                    Interlocked.Add(ref gaugeNet, measurement);
                }
            }
        });
        listener.Start();

        // Provider disposal ALONE drives the teardown — the consumer never disposed the client.
        await provider.DisposeAsync();

        LogoutCount(wireMock).ShouldBe(1, "provider disposal alone must run the facade teardown and log out exactly once");
        Interlocked.Read(ref gaugeNet).ShouldBe(-1, "provider disposal must decrement the active-session gauge exactly once");

        // The facade-owned WebSocket client was disposed as part of that teardown.
        Should.Throw<ObjectDisposedException>(() => webSocketClient.RegisterConnectionEvents());
    }

    private static int LogoutCount(WireMockServer server) =>
        server.FindLogEntries(Request.Create().WithPath(_logoutPath).UsingPost()).Count;

    /// <summary>Registers the LST handshake, ssodh/init, tickle, and a 200 logout.</summary>
    private static void StubAuth(WireMockServer server, IbkrOAuthCredentials creds)
    {
        MockLstServer.Register(server, creds);

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

        server.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}"""));
    }
}
