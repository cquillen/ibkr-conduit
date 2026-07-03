using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// Integration coverage for the inbound streaming frame path, driven end-to-end through the
/// public DI stack (<see cref="IbkrConduit.Client.IIbkrClient"/>) against a local
/// <see cref="MockWebSocketServer"/>. Proves that a frame the server originates flows all the
/// way through the real <see cref="System.Net.WebSockets.ClientWebSocket"/>, the message pump,
/// topic routing, the mapper, and the observable to a consumer — coverage the mapper unit
/// tests (which call the mapper directly) cannot provide.
/// </summary>
public sealed class MarketDataFrameFlowTest : IAsyncLifetime, IDisposable
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
    /// Subscribes to market data over a real loopback WebSocket, has the mock server originate
    /// a realistic <c>smd</c> frame, and asserts it surfaces to the consumer as a mapped
    /// <see cref="MarketDataTick"/> — exercising receive → pump → topic routing → mapper →
    /// observable through the DI-composed client.
    /// </summary>
    [Fact]
    public async Task MarketDataAsync_ServerOriginatedSmdFrame_SurfacesAsTickThroughDiStack()
    {
        var ct = TestContext.Current.CancellationToken;

        await _harness.Client.Streaming.ConnectAsync(ct);

        var subscription = await _harness.Client.Streaming.MarketDataAsync(_conid, _fields, ct);

        var firstTick = new TaskCompletionSource<MarketDataTick>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = subscription.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: tick => firstTick.TrySetResult(tick),
            onError: ex => firstTick.TrySetException(ex),
            onCompleted: () => { }));

        // Wait for the subscribe message so the client socket is registered server-side before
        // the server pushes a frame back.
        await WaitForSubscribeAsync(ct);

        await _mockWs.BroadcastTextAsync(
            $$"""{"topic":"smd+{{_conid}}","_updated":1717171717000,"31":"647.09"}""", ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var tick = await firstTick.Task.WaitAsync(timeout.Token);

        tick.Conid.ShouldBe(_conid);
        tick.Fields.ShouldNotBeNull();
        tick.Fields!["31"].ShouldBe("647.09");
    }

    private async Task WaitForSubscribeAsync(CancellationToken cancellationToken)
    {
        var expected = $$"""smd+{{_conid}}+{"fields":["31"]}""";
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

    /// <summary>Minimal <see cref="IObserver{T}"/> — the project doesn't reference System.Reactive.</summary>
    private sealed class StreamObserver<T>(Action<T> onNext, Action<Exception> onError, Action onCompleted) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);

        public void OnError(Exception error) => onError(error);

        public void OnCompleted() => onCompleted();
    }
}
