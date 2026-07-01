using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
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
/// Integration tests for <see cref="IIbkrClientManager"/> driven end-to-end through the
/// full DI stack (<c>AddIbkrClientManager</c>) against WireMock (HTTP) plus an in-process
/// mock WebSocket server (<see cref="MockWebSocketServer"/>). No library internals are
/// faked: the real <c>TenantBuilder</c> builds each child provider, acquires a live
/// session token, calls <c>ssodh/init</c>, and eagerly connects a real WebSocket to the
/// local mock. Every assertion exercises the public manager API.
/// </summary>
public sealed class ClientManagerTests
{
    private const string _accountsPath = "/v1/api/iserver/accounts";
    private const string _accountsBody = """{"accounts":["U1234567"],"selectedAccount":"U1234567"}""";

    /// <summary>
    /// Scenario 1 — eager add full flow: LST → ssodh/init → WebSocket connect → a real
    /// data call succeeds, and the tenant is registered as active.
    /// </summary>
    [Fact]
    public async Task AddAsync_EagerFullFlow_ConnectsAndServesData()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthEndpoints(wireMock, creds);
        StubAccountsOk(wireMock);

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        var client = await mgr.AddAsync("acct-a", creds, cancellationToken: ct);

        var result = await client.Accounts.GetAccountsAsync(ct);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Accounts.ShouldNotBeEmpty();
        result.Value.Accounts[0].ShouldBe("U1234567");

        mgr.ActiveTenants.ShouldBe(new[] { "acct-a" });
        mockWs.ConnectionCount.ShouldBeGreaterThanOrEqualTo(1);

        // The full auth handshake must have run inside the manager-built child provider.
        LstCount(wireMock).ShouldBeGreaterThanOrEqualTo(1);
        SsodhInitCount(wireMock).ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Scenario 2 (headline) — two-tenant isolation: two tenants added concurrently with
    /// distinct ids and distinct consumer keys are independent — distinct client
    /// instances, each request signed with its OWN consumer key, and both calls succeed.
    /// </summary>
    [Fact]
    public async Task AddAsync_TwoTenants_AreIsolatedAndSignedWithOwnConsumerKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        const string keyA = "TENANT-A-KEY";
        const string keyB = "TENANT-B-KEY";
        var credsA = TestCredentials.Create(keyA, "tenant-a-token", "acct-a");
        var credsB = TestCredentials.Create(keyB, "tenant-b-token", "acct-b");
        StubAuthEndpoints(wireMock, credsA, credsB);
        StubAccountsOk(wireMock);

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        // Add both tenants concurrently — each eagerly connects to the shared mock WS.
        var added = await Task.WhenAll(
            mgr.AddAsync("acct-a", credsA, cancellationToken: ct),
            mgr.AddAsync("acct-b", credsB, cancellationToken: ct));

        mgr.ActiveTenants.OrderBy(x => x).ShouldBe(new[] { "acct-a", "acct-b" });

        var clientA = mgr.GetClient("acct-a");
        var clientB = mgr.GetClient("acct-b");
        clientA.ShouldNotBeSameAs(clientB);
        clientA.ShouldBeSameAs(added[0]);
        clientB.ShouldBeSameAs(added[1]);

        // Drive a data call on each — both must succeed independently.
        (await clientA.Accounts.GetAccountsAsync(ct)).IsSuccess.ShouldBeTrue();
        (await clientB.Accounts.GetAccountsAsync(ct)).IsSuccess.ShouldBeTrue();

        // Each tenant's data request must carry ITS OWN consumer key in the OAuth header.
        var authHeaders = wireMock
            .FindLogEntries(Request.Create().WithPath(_accountsPath).UsingGet())
            .Select(e => e.RequestMessage.Headers!["Authorization"][0])
            .ToList();

        authHeaders.ShouldContain(h => h.Contains($"oauth_consumer_key=\"{keyA}\"", StringComparison.Ordinal));
        authHeaders.ShouldContain(h => h.Contains($"oauth_consumer_key=\"{keyB}\"", StringComparison.Ordinal));
        // Isolation: no request signed with A's key should also carry B's key.
        authHeaders.ShouldNotContain(h =>
            h.Contains($"oauth_consumer_key=\"{keyA}\"", StringComparison.Ordinal)
            && h.Contains($"oauth_consumer_key=\"{keyB}\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// Scenario 3 — remove tears the tenant down: it issues <c>POST /v1/api/logout</c>,
    /// returns <c>true</c>, and de-registers the tenant so lookups fail afterwards.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_LogsOutAndDeregistersTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthEndpoints(wireMock, creds);
        StubAccountsOk(wireMock);

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        await mgr.AddAsync("acct-a", creds, cancellationToken: ct);

        var removed = await mgr.RemoveAsync("acct-a", ct);

        removed.ShouldBeTrue();
        wireMock.FindLogEntries(Request.Create().WithPath("/v1/api/logout").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(1, "RemoveAsync should issue a best-effort IBKR logout");

        mgr.ActiveTenants.ShouldBeEmpty();
        mgr.TryGetClient("acct-a", out _).ShouldBeFalse();
        Should.Throw<KeyNotFoundException>(() => mgr.GetClient("acct-a"));

        // Removing a tenant that is not active returns false.
        (await mgr.RemoveAsync("acct-a", ct)).ShouldBeFalse();
    }

    /// <summary>
    /// Scenario 4 (mandatory) — 401 recovery inside a manager-built child provider:
    /// a data endpoint returns 401 then 200, and the pipeline's <c>TokenRefreshHandler</c>
    /// re-authenticates (fresh LST + ssodh/init) and retries the original request.
    /// </summary>
    [Fact]
    public async Task ManagedTenant_401_TriggersReauthAndRetrySucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthEndpoints(wireMock, creds);

        // First call → 401 (expired token); after re-auth, the retried call → 200.
        wireMock.Given(Request.Create().WithPath(_accountsPath).UsingGet())
            .InScenario("accounts-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("Unauthorized"));
        wireMock.Given(Request.Create().WithPath(_accountsPath).UsingGet())
            .InScenario("accounts-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(_accountsBody));

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        var client = await mgr.AddAsync("acct-a", creds, cancellationToken: ct);

        // Baseline: the eager add already performed exactly one handshake.
        var lstBefore = LstCount(wireMock);
        var initBefore = SsodhInitCount(wireMock);

        var result = await client.Accounts.GetAccountsAsync(ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Accounts[0].ShouldBe("U1234567");

        // The 401 must have driven a fresh re-authentication (new LST + ssodh/init).
        LstCount(wireMock).ShouldBeGreaterThan(lstBefore,
            "a 401 should trigger a fresh Live Session Token handshake");
        SsodhInitCount(wireMock).ShouldBeGreaterThan(initBefore,
            "a 401 should trigger a fresh ssodh/init");
    }

    /// <summary>
    /// Scenario 5 — telemetry attribution: HTTP request metrics emitted while driving
    /// calls on two tenants carry the <see cref="LogFields.TenantId"/> tag equal to the
    /// respective tenant id, proving per-tenant observability.
    /// </summary>
    [Fact]
    public async Task Telemetry_HttpRequestMetrics_AreTaggedWithTenantId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        // Tenant ids are unique to this test (not the shared acct-a/acct-b) because the
        // MeterListener below reads the process-global meter; see the note at its setup.
        var credsA = TestCredentials.Create("TELE-A-KEY", "tele-a-token", "tele-a");
        var credsB = TestCredentials.Create("TELE-B-KEY", "tele-b-token", "tele-b");
        StubAuthEndpoints(wireMock, credsA, credsB);
        StubAccountsOk(wireMock);

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        var clientA = await mgr.AddAsync("tele-a", credsA, cancellationToken: ct);
        var clientB = await mgr.AddAsync("tele-b", credsB, cancellationToken: ct);

        // This captures from the process-global static IbkrConduit meter, so the tenant
        // ids asserted here must stay unique across the whole suite to avoid cross-test
        // contamination from measurements emitted by other tests running in parallel.
        var tenantTags = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                && instrument.Name == "ibkr.conduit.http.request.count")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == LogFields.TenantId && tag.Value is string tenantId)
                {
                    tenantTags.Add(tenantId);
                }
            }
        });
        listener.Start();

        // Drive one call on each tenant with the listener active.
        (await clientA.Accounts.GetAccountsAsync(ct)).IsSuccess.ShouldBeTrue();
        (await clientB.Accounts.GetAccountsAsync(ct)).IsSuccess.ShouldBeTrue();

        listener.Dispose(); // stop capturing new measurements

        tenantTags.ShouldContain("tele-a");
        tenantTags.ShouldContain("tele-b");
    }

    private static ServiceProvider BuildManagerProvider(WireMockServer wireMock, MockWebSocketServer mockWs)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClientManager(o =>
        {
            o.BaseUrl = wireMock.Url!;
            o.WebSocketBaseUrl = mockWs.Url;
            // Keep background timers quiet for the duration of a short test.
            o.TickleIntervalSeconds = 3600;
            o.WebSocketHeartbeatIntervalSeconds = 3600;
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Registers the shared IBKR auth/session endpoints (LST handshake per tenant, plus
    /// path-only ssodh/init, tickle, and logout) on the WireMock server.
    /// </summary>
    private static void StubAuthEndpoints(WireMockServer server, params IbkrOAuthCredentials[] tenants)
    {
        foreach (var creds in tenants)
        {
            // Real DH handshake keyed by each tenant's consumer key + access token.
            MockLstServer.Register(server, creds);
        }

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

    private static void StubAccountsOk(WireMockServer server) =>
        server.Given(Request.Create().WithPath(_accountsPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(_accountsBody));

    private static int LstCount(WireMockServer server) =>
        server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost()).Count;

    private static int SsodhInitCount(WireMockServer server) =>
        server.FindLogEntries(
            Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost()).Count;
}
