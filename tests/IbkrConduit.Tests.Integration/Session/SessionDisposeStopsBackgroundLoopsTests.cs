using System;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// CON-1 regression guard, driven end-to-end through the full <c>AddIbkrClient</c> DI stack against
/// WireMock: disposing the client provider while the session manager's background tickle loop is
/// running must stop the loop and leave nothing firing. A leaked tickle (or proactive-refresh) loop
/// surviving dispose was the root cause of the intermittent
/// <c>WebSocketReconnect_FirstAttemptFails_RecoversViaTickleWatchdog</c> testhost hang — a real loop
/// still POSTing after teardown. The observable contract here is the REST footprint: once teardown
/// settles, no further <c>/tickle</c> request may arrive.
/// </summary>
public sealed class SessionDisposeStopsBackgroundLoopsTests
{
    private static readonly IRequestBuilder _tickle = Request.Create().WithPath("/v1/api/tickle").UsingPost();

    [Fact]
    public async Task DisposeProvider_WhileTickleLoopRunning_StopsLoop_NoPostDisposeHttpActivity()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = WireMockServer.Start();
        using var creds = TestCredentials.Create();
        StubAuth(server, creds);
        server.Given(Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]"""));

        // Build the provider ourselves so we can dispose the whole client (session manager tickle
        // loop AND the WebSocket reconnect watchdog, which also issues tickles) while the WireMock
        // server stays up to inspect its request log.
        var provider = BuildProvider(server, creds);
        try
        {
            var client = provider.GetRequiredService<IIbkrClient>();

            // Trigger session init so the tickle timer starts running.
            await client.Portfolio.GetAccountsAsync(ct);

            // Let the tickle loop fire several times so we know it is genuinely alive.
            await Task.Delay(TimeSpan.FromSeconds(2.5), ct);
            server.FindLogEntries(_tickle).Count.ShouldBeGreaterThanOrEqualTo(2,
                "the tickle loop should be actively running before dispose");
        }
        finally
        {
            // Dispose the whole client. SessionManager.DisposeAsync cancels the dispose token first
            // and AWAITS the tickle + proactive-refresh loops to completion, so nothing is left
            // running once disposal returns.
            await provider.DisposeAsync();
        }

        // Let any straggler in-flight request settle, then snapshot the count.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        var countAfterSettle = server.FindLogEntries(_tickle).Count;

        // Wait well beyond several tickle intervals. A leaked loop would keep POSTing /tickle every
        // second; a stopped-and-awaited loop leaves the count flat.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        server.FindLogEntries(_tickle).Count.ShouldBe(countAfterSettle,
            "no tickle request may arrive after teardown settles — the background loops must be stopped and awaited, not leaked");
    }

    private static ServiceProvider BuildProvider(WireMockServer server, IbkrOAuthCredentials creds)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.BaseUrl = server.Url!;
            opts.TickleIntervalSeconds = 1;
            opts.TickleFailureIntervalSeconds = 1;
        });
        return services.BuildServiceProvider();
    }

    private static void StubAuth(WireMockServer server, IbkrOAuthCredentials creds)
    {
        MockLstServer.Register(server, creds);

        server.Given(Request.Create().WithPath("/v1/api/iserver/auth/ssodh/init").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"authenticated":true,"competing":false,"connected":true,"passed":true,"established":true}"""));

        server.Given(_tickle)
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
}
