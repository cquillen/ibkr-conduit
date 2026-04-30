using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// Regression test for the 2026-04-30 incident where a single failed reconnect attempt
/// after a network outage left the WebSocket dead indefinitely. Verifies the new
/// tickle-watchdog behavior: a successful tickle after a failed reconnect re-triggers
/// the reconnect path.
/// </summary>
/// <remarks>
/// <para>
/// WireMock cannot speak the WebSocket protocol, and the WebSocket base URL inside
/// <c>IbkrWebSocketClient</c> is hardcoded (<c>wss://api.ibkr.com/v1/api/ws</c>) — so
/// no <see cref="IIbkrClient"/> WebSocket call in this test ever produces a real
/// connection. That is fine: the watchdog flow we are exercising is observable
/// entirely at the REST level. Specifically:
/// </para>
/// <list type="number">
///   <item>The tickle timer runs <c>POST /v1/api/tickle</c> on a configurable cadence.</item>
///   <item>A scenario stub returns 200 → 503 → 200 → 200… across consecutive tickles.</item>
///   <item>On each failure, the timer switches to the fast cadence
///     (<see cref="IbkrClientOptions.TickleFailureIntervalSeconds"/>).</item>
///   <item>On the next success, <c>NotifyTickleSucceededAsync</c> fires;
///     <c>IbkrWebSocketClient.OnTickleSucceededAsync</c> sees the WS is not open and calls
///     <c>ReconnectAsync</c>, which itself issues another tickle as part of <c>ConnectCoreAsync</c>.</item>
/// </list>
/// <para>
/// We assert the observable REST footprint of that flow: tickle was hit through all
/// scenario states (proving 503 occurred and a recovery tickle followed), and the total
/// tickle count exceeds the bare initial-plus-failure count (proving the watchdog re-fired
/// reconnect, which itself called tickle again). We do not assert
/// <see cref="IStreamingOperations.IsConnected"/> because that requires real WebSocket
/// scaffolding the integration harness does not provide.
/// </para>
/// </remarks>
public class WebSocketReconnectViaTickleWatchdogTest : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;
    private CapturingLoggerProvider _loggerProvider = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Use very short tickle intervals so the test finishes in seconds, not minutes.
        // TickleIntervalSeconds drives the first tickle (the timer's healthy cadence);
        // it must be small or the test would wait a full minute before the first call.
        _loggerProvider = new CapturingLoggerProvider();
        _harness = await TestHarness.CreateAsync(
            opts =>
            {
                opts.TickleIntervalSeconds = 1;
                opts.TickleFailureIntervalSeconds = 1;
            },
            configureServices: services =>
            {
                // Plug a capturing provider into the existing logging pipeline so we can
                // assert the watchdog actually fired. The harness sets a global Warning
                // floor; we lower it for the WebSocket client category so the
                // Information-level "Tickle watchdog detected dead WebSocket" message
                // reaches our provider.
                services.AddSingleton<ILoggerProvider>(_loggerProvider);
                services.AddLogging(b => b.AddFilter(
                    "IbkrConduit.Streaming.IbkrWebSocketClient",
                    LogLevel.Information));
            });
    }

    /// <summary>
    /// Verifies that a 503 tickle followed by a successful tickle drives the
    /// watchdog through its reconnect path — observable as additional tickle calls
    /// beyond the initial heartbeat plus the simulated failure.
    /// </summary>
    [Fact]
    [Trait("Category", "Slow")]
    public async Task WebSocketReconnect_FirstAttemptFails_RecoversViaTickleWatchdog()
    {
        var ct = TestContext.Current.CancellationToken;

        // Scenario stub for tickle: 200 (initial) -> 503 (simulated outage) -> 200 (recovery).
        // After the recovery state, all subsequent tickles fall through to the default
        // tickle stub registered by TestHarness.
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .InScenario("watchdog-recovery")
            .WhenStateIs("Started")
            .WillSetStateTo("first-tickle-done")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"session":"s","iserver":{"authStatus":{"authenticated":true,"competing":false,"connected":true}}}"""));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .InScenario("watchdog-recovery")
            .WhenStateIs("first-tickle-done")
            .WillSetStateTo("first-tickle-failed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .InScenario("watchdog-recovery")
            .WhenStateIs("first-tickle-failed")
            .WillSetStateTo("recovery-done")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"session":"s","iserver":{"authStatus":{"authenticated":true,"competing":false,"connected":true}}}"""));

        // Trigger session init so the tickle timer starts running.
        _harness.StubAuthenticatedGet(
            "/v1/api/portfolio/accounts",
            """[{"id":"U1234567","accountTitle":"Test","type":"DEMO"}]""");
        await _harness.Client.Portfolio.GetAccountsAsync(ct);

        // Queue a market-data subscription so the WebSocket client has something to
        // replay on reconnect (exercises the same code path as a real subscriber).
        _ = await _harness.Client.Streaming.MarketDataAsync(
            265598, new[] { "31" }, ct);

        // Wait long enough for the tickle loop to exhaust the scenario states:
        //   t≈0      initial tickle from session init -> "first-tickle-done"
        //   t≈1s     503 (failure cadence) -> "first-tickle-failed"
        //   t≈2s     200 -> "recovery-done"; NotifyTickleSucceededAsync fires;
        //            watchdog triggers ReconnectAsync, which calls tickle again
        //            (default fall-through stub returns 200).
        //   t≈3-15s  steady-state tickles at the fast cadence (still "succeeded == false"
        //            from the watchdog reconnect's failure to actually open the WS).
        await Task.Delay(TimeSpan.FromSeconds(15), ct);

        // The scenario must have advanced through every state — proving that the
        // 503 was returned and a follow-up successful tickle occurred.
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/tickle").UsingPost())
            .Count.ShouldBeGreaterThanOrEqualTo(3,
                "tickle should have been called at least three times: initial 200, 503, recovery 200.");

        // The load-bearing assertion: the watchdog logs "Tickle watchdog detected dead
        // WebSocket — triggering reconnect" at Information level every time
        // OnTickleSucceededAsync decides to reconnect. With the watchdog wired,
        // every successful tickle while the WS is not Open should produce this log
        // entry. Without the watchdog wiring, this message will never appear and the
        // test fails — giving real regression protection for the Task 6 subscription.
        _loggerProvider.Messages
            .ShouldContain(
                m => m.Level == LogLevel.Information
                    && m.Message.Contains("Tickle watchdog detected dead WebSocket", StringComparison.Ordinal),
                "watchdog should have logged a triggering-reconnect message when a successful tickle observed a dead WebSocket.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        _loggerProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A captured log record — level plus formatted message text.
    /// </summary>
    private sealed record CapturedLog(LogLevel Level, string Category, string Message);

    /// <summary>
    /// Test logger provider that captures every formatted log message into a thread-safe
    /// list so tests can assert on log output. Modeled on the CapturingLogger pattern in
    /// AuditLogHandlerTests.cs.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<CapturedLog> _messages = [];

        public IReadOnlyList<CapturedLog> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _messages);

        public void Dispose()
        {
            // Nothing to dispose — the bag is just an in-memory container.
        }

        private sealed class CapturingLogger(string category, ConcurrentBag<CapturedLog> sink) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                sink.Add(new CapturedLog(logLevel, category, formatter(state, exception)));
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();
            public void Dispose()
            {
                // No-op.
            }
        }
    }
}
