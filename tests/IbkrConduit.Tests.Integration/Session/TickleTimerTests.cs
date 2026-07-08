using System;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Shouldly;
using WireMock.RequestBuilders;

namespace IbkrConduit.Tests.Integration.Session;

/// <summary>
/// Validates that the tickle timer fires repeatedly to keep the session alive.
/// Tagged as "Slow" — excluded from CI via --filter-not-trait "Category=Slow".
/// Run explicitly: dotnet test -- --filter-trait "Category=Slow"
/// </summary>
public class TickleTimerTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.TickleIntervalSeconds = 5;
        });
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task TickleTimer_FiresRepeatedly_KeepsSessionAlive()
    {
        // Make a request to trigger session init (which starts the tickle timer)
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            """[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]""");

        await _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);

        // Count tickles so far (session init may have triggered one)
        var initialTickles = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/tickle").UsingPost()).Count;

        // Wait for 3.5 intervals (17.5 seconds at 5s interval) to observe at least 3 new tickles
        await Task.Delay(TimeSpan.FromMilliseconds(17500), TestContext.Current.CancellationToken);

        var finalTickles = _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/tickle").UsingPost()).Count;

        var newTickles = finalTickles - initialTickles;
        newTickles.ShouldBeGreaterThanOrEqualTo(3,
            $"Expected at least 3 tickles in 17.5s (5s interval), but got {newTickles}. " +
            $"Initial: {initialTickles}, Final: {finalTickles}");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task TickleLoop_InitializingCallerTokenCancelledAfterInit_KeepsTickling()
    {
        // SES-2 (PVR-12), end-to-end through the DI stack: ValidateConnectionAsync warms up the session
        // by calling EnsureInitializedAsync with the caller's raw token (a common startup pattern — a
        // consumer eagerly inits with a bounded startup CTS). That is the token the keepalive loop's
        // lifetime CTS was (pre-fix) linked to. Cancelling it after init must NOT stop keepalive: the
        // loop's lifetime is the SessionManager's, not the caller's, so /tickle POSTs must keep arriving.
        await using var harness = await TestHarness.CreateAsync(opts =>
        {
            opts.TickleIntervalSeconds = 1;
            opts.TickleFailureIntervalSeconds = 1;
        });

        using var callerCts = new CancellationTokenSource();

        // Eagerly initialize the session; EnsureInitializedAsync (and hence StartAsync) runs on the
        // caller's raw token, passed straight through by ValidateConnectionAsync.
        await harness.Client.ValidateConnectionAsync(validateFlex: false, cancellationToken: callerCts.Token);

        // Cancel the initializing caller's token now that init has completed.
        await callerCts.CancelAsync();

        var tickle = Request.Create().WithPath("/v1/api/tickle").UsingPost();
        var ticklesAtCancel = harness.Server.FindLogEntries(tickle).Count;

        // Wait several tickle intervals. Pre-fix: the loop is linked to the cancelled caller token and
        // is now dead, so the count stays flat. Post-fix: keepalive is independent and keeps POSTing.
        await Task.Delay(TimeSpan.FromMilliseconds(4000), TestContext.Current.CancellationToken);

        var ticklesAfterWait = harness.Server.FindLogEntries(tickle).Count;

        (ticklesAfterWait - ticklesAtCancel).ShouldBeGreaterThanOrEqualTo(2,
            $"After the initializing caller's token is cancelled, keepalive must keep ticking — its "
            + $"lifetime is the SessionManager's, not the caller's. Tickles at cancel: {ticklesAtCancel}, "
            + $"after wait: {ticklesAfterWait}.");
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
