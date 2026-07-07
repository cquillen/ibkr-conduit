using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IbkrConduit.Tests.Integration.MultiTenant;

/// <summary>
/// Integration tests for <see cref="IIbkrClientManager"/> teardown (VCR-08 / MGR-1),
/// driven end-to-end through the full <c>AddIbkrClientManager</c> DI stack against WireMock
/// (HTTP) plus an in-process mock WebSocket server. The real <c>TenantBuilder</c> builds
/// each child provider, so these exercise the actual <c>ManagedTenant.DisposeAsync</c> logout
/// pipeline — a cancelled/short-timeout token abandons a hung logout promptly, an internal
/// cap bounds teardown even with no token, and exactly one logout is issued (dedup).
/// </summary>
public sealed class ManagerTeardownTests
{
    private const string _logoutPath = "/v1/api/logout";

    /// <summary>
    /// A cancelled token abandons the hung logout and returns promptly, yet the tenant's
    /// resources are still torn down (de-registered).
    /// </summary>
    [Fact]
    public async Task RemoveAsync_CancelledToken_AbandonsHungLogoutAndReturnsPromptly()
    {
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthWithoutLogout(wireMock, creds);
        StubHungLogout(wireMock, TimeSpan.FromSeconds(30));

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();
        await mgr.AddAsync("acct-a", creds, cancellationToken: TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var sw = Stopwatch.StartNew();
        var removed = await mgr.RemoveAsync("acct-a", cts.Token);
        sw.Stop();

        removed.ShouldBeTrue();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8),
            "a cancelled token must abandon the hung logout instead of blocking on it");
        mgr.ActiveTenants.ShouldBeEmpty();                      // resources still torn down
        mgr.TryGetClient("acct-a", out _).ShouldBeFalse();
    }

    /// <summary>
    /// With no caller token, the internal cap still bounds a hung logout so teardown cannot
    /// block for minutes.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_NoToken_LogoutBoundedByInternalCap()
    {
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthWithoutLogout(wireMock, creds);
        StubHungLogout(wireMock, TimeSpan.FromSeconds(30));

        await using var provider = BuildManagerProvider(wireMock, mockWs,
            logoutCap: TimeSpan.FromMilliseconds(500));
        var mgr = provider.GetRequiredService<IIbkrClientManager>();
        await mgr.AddAsync("acct-a", creds, cancellationToken: TestContext.Current.CancellationToken);

        var sw = Stopwatch.StartNew();
        // No caller cancellation — only the internal cap bounds the hung logout.
        var removed = await mgr.RemoveAsync("acct-a", CancellationToken.None);
        sw.Stop();

        removed.ShouldBeTrue();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5),
            "the internal cap must bound teardown even without a caller token");
        mgr.ActiveTenants.ShouldBeEmpty();
    }

    /// <summary>
    /// A managed tenant issues exactly one logout on removal — the child session manager's
    /// dispose-time logout is deduplicated against ManagedTenant's explicit one.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_IssuesExactlyOneLogout()
    {
        using var wireMock = WireMockServer.Start();
        await using var mockWs = MockWebSocketServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        StubAuthWithoutLogout(wireMock, creds);
        wireMock.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}"""));

        await using var provider = BuildManagerProvider(wireMock, mockWs);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();
        await mgr.AddAsync("acct-a", creds, cancellationToken: TestContext.Current.CancellationToken);

        (await mgr.RemoveAsync("acct-a", TestContext.Current.CancellationToken)).ShouldBeTrue();

        wireMock.FindLogEntries(Request.Create().WithPath(_logoutPath).UsingPost())
            .Count.ShouldBe(1, "the duplicate logout (ManagedTenant + SessionManager) must be deduplicated to one");
    }

    private static ServiceProvider BuildManagerProvider(
        WireMockServer wireMock, MockWebSocketServer mockWs, TimeSpan? logoutCap = null)
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
            if (logoutCap is { } cap)
            {
                o.LogoutTimeout = cap;
            }
        });
        return services.BuildServiceProvider();
    }

    /// <summary>Registers the LST handshake + ssodh/init + tickle, but NOT logout (each test stubs it).</summary>
    private static void StubAuthWithoutLogout(WireMockServer server, IbkrOAuthCredentials creds)
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
    }

    private static void StubHungLogout(WireMockServer server, TimeSpan delay) =>
        server.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}""")
                .WithDelay(delay));
}
