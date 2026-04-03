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
        _harness = await TestHarness.CreateAsync(new IbkrClientOptions
        {
            TickleIntervalSeconds = 5,
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
