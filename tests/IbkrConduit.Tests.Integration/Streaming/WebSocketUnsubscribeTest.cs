using System;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Client;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// Integration coverage for WebSocket unsubscribe driven entirely through the public DI
/// stack (<see cref="IIbkrClient"/>), proving that <see cref="IIbkrSubscription{T}.UnsubscribeAsync"/>
/// actually puts the IBKR cancel wire message ("umd+{conid}+{}") on the socket.
/// </summary>
/// <remarks>
/// Unlike <see cref="WebSocketReconnectViaTickleWatchdogTest"/> — which never establishes a
/// real WebSocket connection because it leaves <c>IbkrClientOptions.WebSocketBaseUrl</c>
/// pointed at the unreachable production default — this test overrides
/// <c>WebSocketBaseUrl</c> to a local <see cref="MockWebSocketServer"/> (the same hermetic
/// stand-in used by <c>ClientManagerTests</c>) so <c>IStreamingOperations.ConnectAsync</c>
/// opens a genuine loopback <see cref="System.Net.WebSockets.ClientWebSocket"/> connection.
/// The mock server records every inbound text frame it receives, so the subscribe and
/// cancel messages the client actually sends are directly observable — no fallback to
/// stream-completion inference is needed here.
/// </remarks>
public sealed class WebSocketUnsubscribeTest : IAsyncLifetime, IDisposable
{
    private const int _conid = 265598;
    private static readonly string[] _fields = ["31"];

    private MockWebSocketServer _mockWs = null!;
    private TestHarness _harness = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _mockWs = MockWebSocketServer.Start();
        _harness = await TestHarness.CreateAsync(opts =>
        {
            opts.WebSocketBaseUrl = _mockWs.Url;
            // Keep background timers quiet for the duration of a short test.
            opts.TickleIntervalSeconds = 3600;
            opts.WebSocketHeartbeatIntervalSeconds = 3600;
        });
    }

    /// <summary>
    /// Verifies that unsubscribing a market-data subscription sends the IBKR "umd" cancel
    /// message on the wire — the observable outcome of Task 1-4's
    /// <see cref="IIbkrSubscription{T}.UnsubscribeAsync"/> implementation, exercised through
    /// the real DI-composed <see cref="IIbkrClient"/> rather than the internal WebSocket
    /// client directly.
    /// </summary>
    [Fact]
    public async Task UnsubscribeAsync_MarketDataSubscription_SendsCancelWireMessageOverSocket()
    {
        var ct = TestContext.Current.CancellationToken;

        await _harness.Client.Streaming.ConnectAsync(ct);

        var subscription = await _harness.Client.Streaming.MarketDataAsync(_conid, _fields, ct);

        // The subscribe message is sent immediately because the socket is already open —
        // wait for it to confirm the connection is live before exercising unsubscribe.
        var subscribeMessage = $"smd+{_conid}+{{\"fields\":[\"31\"]}}";
        await WaitForMessageAsync(subscribeMessage, ct);
        _mockWs.ReceivedTextMessages.ShouldContain(subscribeMessage);

        await subscription.UnsubscribeAsync(ct);

        var cancelMessage = $"umd+{_conid}+{{}}";
        await WaitForMessageAsync(cancelMessage, ct);
        _mockWs.ReceivedTextMessages.ShouldContain(cancelMessage,
            "UnsubscribeAsync should send the IBKR 'umd' cancel message over the live WebSocket.");
    }

    /// <summary>
    /// Polls <see cref="MockWebSocketServer.ReceivedTextMessages"/> until the expected
    /// frame arrives or the timeout elapses, since the client sends the wire message
    /// asynchronously off the calling thread.
    /// </summary>
    private async Task WaitForMessageAsync(string expected, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (_mockWs.ReceivedTextMessages.Contains(expected))
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
        await _mockWs.DisposeAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
