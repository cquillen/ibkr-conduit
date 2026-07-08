using System;
using System.Diagnostics;
using System.Security.Cryptography;
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
/// Integration tests for TEN-1 (PVR-22): a tenant whose eager init (ssodh/init) SUCCEEDS
/// but whose overall <c>TenantBuilder.BuildAsync</c> then FAILS (e.g. the eager WebSocket
/// connect that follows) must not leak the server-side brokerage session — it issues the
/// same bounded best-effort logout <see cref="ManagedTenant"/> would have performed on a
/// clean teardown. A tenant whose init never succeeds has nothing to tear down and must
/// not attempt one. Driven end-to-end through the real <c>AddIbkrClientManager</c> DI
/// stack against WireMock, so the real <c>TenantBuilder</c> exercises the actual failure
/// path (no fakes).
/// </summary>
public sealed class TenantBuilderPostInitFailureLogoutTests
{
    private const string _logoutPath = "/v1/api/logout";
    private const string _ssodhInitPath = "/v1/api/iserver/auth/ssodh/init";
    private const string _ticklePath = "/v1/api/tickle";

    /// <summary>
    /// Eager init (ssodh/init) succeeds — the server-side session is live — but the eager
    /// WebSocket connect that follows fails (its tickle probe errors), so the overall build
    /// fails. The resulting teardown must still issue a best-effort IBKR logout, tearing
    /// down the server-side session that would otherwise be orphaned (TEN-1).
    /// </summary>
    [Fact]
    public async Task AddAsync_PostInitBuildFailure_IssuesBestEffortLogout()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        MockLstServer.Register(wireMock, creds);
        StubSsodhInitOk(wireMock);
        StubTickleFailure(wireMock);
        wireMock.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}"""));

        await using var provider = BuildManagerProvider(wireMock);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        await Should.ThrowAsync<Exception>(
            () => mgr.AddAsync("acct-a", creds, cancellationToken: ct));

        wireMock.FindLogEntries(Request.Create().WithPath(_logoutPath).UsingPost())
            .Count.ShouldBe(1,
                "eager init already brought the server-side session up, so the build-failure path must tear it down");
        mgr.ActiveTenants.ShouldBeEmpty();
    }

    /// <summary>
    /// Same post-init build-failure scenario, but the IBKR logout endpoint hangs. The
    /// teardown must be bounded by the same internal cap (<see cref="IbkrClientOptions.LogoutTimeout"/>)
    /// that <see cref="ManagedTenant"/>'s own teardown uses — a hung logout must never block
    /// the build-failure path for minutes.
    /// </summary>
    [Fact]
    public async Task AddAsync_PostInitBuildFailure_LogoutBoundedByInternalCap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        MockLstServer.Register(wireMock, creds);
        StubSsodhInitOk(wireMock);
        StubTickleFailure(wireMock);
        StubHungLogout(wireMock, TimeSpan.FromSeconds(30));

        await using var provider = BuildManagerProvider(wireMock, logoutCap: TimeSpan.FromMilliseconds(500));
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        var sw = Stopwatch.StartNew();
        await Should.ThrowAsync<Exception>(
            () => mgr.AddAsync("acct-a", creds, cancellationToken: ct));
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5),
            "the internal cap must bound the post-init-failure logout, same as ManagedTenant teardown");
        mgr.ActiveTenants.ShouldBeEmpty();
    }

    /// <summary>
    /// Eager init itself fails (ssodh/init errors) — the session never came up, so there is
    /// nothing to log out. The failure path must still dispose the child provider and the
    /// credentials (no regression), just without attempting a pointless logout call.
    /// </summary>
    [Fact]
    public async Task AddAsync_SessionInitFails_NoLogoutAttempted_StillDisposesCredentialsAndProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        using var wireMock = WireMockServer.Start();

        var creds = TestCredentials.Create("ACCT-A-KEY", "acct-a-token", "acct-a");
        MockLstServer.Register(wireMock, creds);
        StubSsodhInitFailure(wireMock);
        wireMock.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}"""));

        await using var provider = BuildManagerProvider(wireMock);
        var mgr = provider.GetRequiredService<IIbkrClientManager>();

        await Should.ThrowAsync<Exception>(
            () => mgr.AddAsync("acct-a", creds, cancellationToken: ct));

        wireMock.FindLogEntries(Request.Create().WithPath(_logoutPath).UsingPost())
            .Count.ShouldBe(0, "the session was never established — there is nothing to tear down");
        mgr.ActiveTenants.ShouldBeEmpty();

        // Credentials must still be disposed on this failure path (no regression from TEN-1's fix).
        Should.Throw<ObjectDisposedException>(() => creds.SignaturePrivateKey.ExportParameters(true));
    }

    private static ServiceProvider BuildManagerProvider(WireMockServer wireMock, TimeSpan? logoutCap = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClientManager(o =>
        {
            o.BaseUrl = wireMock.Url!;
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

    private static void StubSsodhInitOk(WireMockServer server) =>
        server.Given(Request.Create().WithPath(_ssodhInitPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

    private static void StubSsodhInitFailure(WireMockServer server) =>
        server.Given(Request.Create().WithPath(_ssodhInitPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("ssodh/init failure"));

    private static void StubTickleFailure(WireMockServer server) =>
        server.Given(Request.Create().WithPath(_ticklePath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("tickle failure"));

    private static void StubHungLogout(WireMockServer server, TimeSpan delay) =>
        server.Given(Request.Create().WithPath(_logoutPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"confirmed":true}""")
                .WithDelay(delay));
}
