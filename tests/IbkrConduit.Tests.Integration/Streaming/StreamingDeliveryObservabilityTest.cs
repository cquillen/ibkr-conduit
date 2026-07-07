using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// End-to-end coverage for the ADR-0002 streaming delivery-observability guarantee, driven through
/// the public DI stack (<see cref="IbkrConduit.Client.IIbkrClient"/>) against a local
/// <see cref="MockWebSocketServer"/>: an overflow eviction is counted (finding FIL-1), a reconnect
/// emits consumer-visible lifecycle events with the replayed topics (FIL-4), and a consumer
/// <c>OnNext</c> fault surfaces via <c>OnError</c> rather than a false completion (FIL-3).
/// </summary>
public sealed class StreamingDeliveryObservabilityTest
{
    private const int _conid = 265598;
    private static readonly string[] _fields = ["31"];

    /// <summary>
    /// FIL-1: with a subscription whose stream is never drained, broadcasting more frames than the
    /// bounded buffer holds evicts the oldest — and every eviction increments
    /// <c>ibkr.conduit.streaming.frames.dropped</c> with <c>cause=overflow</c> and the wire topic,
    /// through the DI-composed client. Regression for <c>SubscribeTopicAsync_BufferOverflow_EmitsDropSignal</c>.
    /// </summary>
    [Fact]
    public async Task MarketData_BufferOverflow_IncrementsDropCounterThroughDiStack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mockWs = MockWebSocketServer.Start();
        await using var harness = await TestHarness.CreateAsync(opts =>
        {
            opts.WebSocketBaseUrl = mockWs.Url;
            opts.StreamingBufferSize = 4;
            opts.TickleIntervalSeconds = 3600;
            opts.WebSocketHeartbeatIntervalSeconds = 3600;
        });

        using var drops = new OverflowDropCapture();

        await harness.Client.Streaming.ConnectAsync(ct);

        // Get the subscription but never subscribe an observer to its Stream, so nothing drains the
        // channel — arriving frames pile up and overflow.
        var subscription = await harness.Client.Streaming.MarketDataAsync(_conid, _fields, ct);
        subscription.ShouldNotBeNull();

        await WaitForMessageAsync(mockWs, $$"""smd+{{_conid}}+{"fields":["31"]}""", ct);

        for (var i = 1; i <= 12; i++)
        {
            await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{_conid}}","seq":{{i}},"31":"{{i}}.00"}""", ct);
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!drops.Drops.Any(d => d.Topic == "smd" && d.Cause == "overflow") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }

        drops.Drops.ShouldContain(d => d.Topic == "smd" && d.Cause == "overflow",
            "An overflow eviction must increment ibkr.conduit.streaming.frames.dropped with cause=overflow.");
    }

    /// <summary>
    /// FIL-4: a server-initiated close makes the client reconnect, and the consumer observes a
    /// <see cref="ConnectionDisconnected"/> / <see cref="ConnectionReconnected"/> pair bracketing the
    /// gap, with the reconnect listing the replayed topics — through the DI-composed client.
    /// Regression for <c>Reconnect_EmitsGapEventToConsumer</c>.
    /// </summary>
    [Fact]
    public async Task Reconnect_EmitsDisconnectedThenReconnectedThroughDiStack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mockWs = MockWebSocketServer.Start();
        await using var harness = await TestHarness.CreateAsync(opts =>
        {
            opts.WebSocketBaseUrl = mockWs.Url;
            opts.TickleIntervalSeconds = 3600;
            opts.WebSocketHeartbeatIntervalSeconds = 3600;
        });

        var events = new List<ConnectionEvent>();
        var pairSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionSub = harness.Client.Streaming.SubscribeConnectionEvents();
        using var observer = connectionSub.Stream.Subscribe(new StreamObserver<ConnectionEvent>(
            onNext: e =>
            {
                lock (events)
                {
                    events.Add(e);
                    if (events.Count == 2)
                    {
                        pairSeen.TrySetResult();
                    }
                }
            },
            onError: ex => pairSeen.TrySetException(ex),
            onCompleted: () => { }));

        await harness.Client.Streaming.ConnectAsync(ct);

        // A solicited subscription so the reconnect has a topic to replay.
        await harness.Client.Streaming.MarketDataAsync(_conid, _fields, ct);
        await WaitForMessageAsync(mockWs, $$"""smd+{{_conid}}+{"fields":["31"]}""", ct);

        await mockWs.CloseAllConnectionsAsync(ct);

        await pairSeen.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);

        List<ConnectionEvent> snapshot;
        lock (events)
        {
            snapshot = [.. events];
        }

        snapshot[0].ShouldBeOfType<ConnectionDisconnected>();
        var reconnected = snapshot[1].ShouldBeOfType<ConnectionReconnected>();
        reconnected.ReplayedTopics.ShouldContain("smd");
    }

    /// <summary>
    /// FIL-3: a consumer <c>OnNext</c> that throws is surfaced via <c>OnError</c> — never swallowed
    /// as a malformed frame nor masqueraded as graceful completion — through the DI-composed client.
    /// </summary>
    [Fact]
    public async Task MarketData_ObserverThrows_SurfacesOnErrorNotOnCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mockWs = MockWebSocketServer.Start();
        await using var harness = await TestHarness.CreateAsync(opts =>
        {
            opts.WebSocketBaseUrl = mockWs.Url;
            opts.TickleIntervalSeconds = 3600;
            opts.WebSocketHeartbeatIntervalSeconds = 3600;
        });

        await harness.Client.Streaming.ConnectAsync(ct);

        var subscription = await harness.Client.Streaming.MarketDataAsync(_conid, _fields, ct);

        var errored = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completedCalled = false;
        using var observer = subscription.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: _ => throw new InvalidOperationException("consumer boom"),
            onError: ex => errored.TrySetResult(ex),
            onCompleted: () => completedCalled = true));

        await WaitForMessageAsync(mockWs, $$"""smd+{{_conid}}+{"fields":["31"]}""", ct);
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{_conid}}","31":"647.09"}""", ct);

        var error = await errored.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        error.ShouldBeOfType<InvalidOperationException>();
        completedCalled.ShouldBeFalse();
    }

    private static async Task WaitForMessageAsync(MockWebSocketServer mockWs, string expected, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (mockWs.ReceivedTextMessages.Contains(expected))
            {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    /// <summary>Captures overflow drop measurements from the IbkrConduit meter for assertions.</summary>
    private sealed class OverflowDropCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<(string Topic, string Cause)> _drops = [];
        private readonly object _lock = new();

        public OverflowDropCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == IbkrConduitDiagnostics.MeterName
                        && instrument.Name == "ibkr.conduit.streaming.frames.dropped")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string? topic = null;
                string? cause = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == LogFields.Topic)
                    {
                        topic = tag.Value as string;
                    }
                    else if (tag.Key == LogFields.Cause)
                    {
                        cause = tag.Value as string;
                    }
                }

                if (topic is not null && cause is not null)
                {
                    lock (_lock)
                    {
                        _drops.Add((topic, cause));
                    }
                }
            });
            _listener.Start();
        }

        public IReadOnlyList<(string Topic, string Cause)> Drops
        {
            get
            {
                lock (_lock)
                {
                    return _drops.ToArray();
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed class StreamObserver<T>(Action<T> onNext, Action<Exception> onError, Action onCompleted) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);

        public void OnError(Exception error) => onError(error);

        public void OnCompleted() => onCompleted();
    }
}
