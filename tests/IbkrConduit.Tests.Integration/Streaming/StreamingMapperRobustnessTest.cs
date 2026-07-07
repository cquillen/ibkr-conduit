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
/// End-to-end coverage for the VCR-03 streaming mapper-robustness fixes, driven through the public
/// DI stack (<see cref="IbkrConduit.Client.IIbkrClient"/>) against a local
/// <see cref="MockWebSocketServer"/>: a <c>str</c> frame with one malformed execution still delivers
/// every good execution while counting the bad one (FIL-2), and an <c>sts</c> frame whose
/// <c>authenticated</c> is string-encoded still surfaces a session-status event (GAP2-1).
/// </summary>
public sealed class StreamingMapperRobustnessTest
{
    /// <summary>
    /// FIL-2: a <c>str</c> snapshot frame carries up to a whole day's fills in one <c>args</c> array.
    /// One malformed execution mid-array must not discard the tail — every good execution is
    /// delivered, and the malformed one is counted on
    /// <c>ibkr.conduit.streaming.frames.dropped</c> with <c>cause=mapper</c> and the <c>str</c> topic.
    /// </summary>
    [Fact]
    public async Task TradeExecutions_FrameWithOneMalformedElement_DeliversRemainingAndCountsDropThroughDiStack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mockWs = MockWebSocketServer.Start();
        await using var harness = await TestHarness.CreateAsync(opts =>
        {
            opts.WebSocketBaseUrl = mockWs.Url;
            opts.TickleIntervalSeconds = 3600;
            opts.WebSocketHeartbeatIntervalSeconds = 3600;
        });

        using var drops = new DropCapture();

        await harness.Client.Streaming.ConnectAsync(ct);

        var subscription = await harness.Client.Streaming.TradeExecutionsAsync(cancellationToken: ct);

        var received = new List<TradeExecution>();
        var bothSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = subscription.Stream.Subscribe(new StreamObserver<TradeExecution>(
            onNext: e =>
            {
                lock (received)
                {
                    received.Add(e);
                    if (received.Count == 2)
                    {
                        bothSeen.TrySetResult();
                    }
                }
            },
            onError: ex => bothSeen.TrySetException(ex),
            onCompleted: () => { }));

        await WaitForMessageAsync(mockWs, "str+{}", ct);

        await mockWs.BroadcastTextAsync(
            """
            {"topic":"str","args":[
              {"execution_id":"good-1","symbol":"AAPL","conid":265598},
              {"execution_id":"bad","conid":"garbage-object"},
              {"execution_id":"good-2","symbol":"MSFT","conid":272093}
            ]}
            """, ct);

        await bothSeen.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        List<TradeExecution> snapshot;
        lock (received)
        {
            snapshot = [.. received];
        }

        snapshot.Select(e => e.ExecutionId).ShouldBe(["good-1", "good-2"]);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!drops.Drops.Any(d => d is { Topic: "str", Cause: "mapper" }) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }

        drops.Drops.ShouldContain(d => d.Topic == "str" && d.Cause == "mapper",
            "The malformed execution must increment ibkr.conduit.streaming.frames.dropped with cause=mapper.");
    }

    /// <summary>
    /// GAP2-1: IBKR demonstrably type-drifts boolean-ish flags to strings. An <c>sts</c> frame whose
    /// <c>authenticated</c> arrives as the string <c>"false"</c> (a session-death push) must still
    /// surface a session-status event reporting <c>Authenticated == false</c> through the
    /// DI-composed client, rather than being dropped as malformed.
    /// </summary>
    [Fact]
    public async Task SessionStatus_StringEncodedAuthenticatedFalse_SurfacesFalseThroughDiStack()
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

        var subscription = harness.Client.Streaming.SubscribeSessionStatus();

        var firstEvent = new TaskCompletionSource<SessionStatusEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = subscription.Stream.Subscribe(new StreamObserver<SessionStatusEvent>(
            onNext: e => firstEvent.TrySetResult(e),
            onError: ex => firstEvent.TrySetException(ex),
            onCompleted: () => { }));

        // sts is an unsolicited topic (no subscribe message), so re-broadcast until the first event
        // arrives, tolerating the small window before the loopback socket is registered server-side.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!firstEvent.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            await mockWs.BroadcastTextAsync("""{"topic":"sts","args":{"authenticated":"false"}}""", ct);
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        var evt = await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        evt.Authenticated.ShouldBe(false);
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

    /// <summary>Captures dropped-frame measurements from the IbkrConduit meter for assertions.</summary>
    private sealed class DropCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<(string Topic, string Cause)> _drops = [];
        private readonly object _lock = new();

        public DropCapture()
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
