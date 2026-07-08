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
/// End-to-end coverage for the ADR-0005 subscription-scoped delivery guarantee (PVR-01), driven
/// through the public DI stack (<see cref="IbkrConduit.Client.IIbkrClient"/>) against a local
/// <see cref="MockWebSocketServer"/>. Two concurrent target-qualified subscriptions for different
/// conids/accounts must each observe only their own target's frames (findings PRB-1.1, PRB-1.2,
/// PRB-3.1), and a target-qualified frame with no live subscription must drop observably rather than
/// cross-deliver. All offline.
/// </summary>
public sealed class SubscriptionScopedRoutingTest
{
    private static readonly string[] _fields = ["31"];

    /// <summary>
    /// PRB-1.1 / PRB-1.2: two <c>MarketDataAsync</c> subscriptions for different conids each observe
    /// only their own conid's ticks when the server broadcasts interleaved <c>smd+X</c> / <c>smd+Y</c>
    /// frames — no cross-delivery of one contract's market data to the other's subscriber.
    /// </summary>
    [Fact]
    public async Task MarketDataAsync_TwoConids_EachSubscriptionObservesOnlyItsOwnTicks()
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

        const int conidA = 100;
        const int conidB = 200;

        var aTicks = new List<MarketDataTick>();
        var bTicks = new List<MarketDataTick>();
        var aThird = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var subA = await harness.Client.Streaming.MarketDataAsync(conidA, _fields, ct);
        var subB = await harness.Client.Streaming.MarketDataAsync(conidB, _fields, ct);

        using var obsA = subA.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: t =>
            {
                lock (aTicks)
                {
                    aTicks.Add(t);
                    if (aTicks.Count == 3)
                    {
                        aThird.TrySetResult();
                    }
                }
            }));
        using var obsB = subB.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: t =>
            {
                lock (bTicks)
                {
                    bTicks.Add(t);
                    bFirst.TrySetResult();
                }
            }));

        await WaitForMessageAsync(mockWs, $$"""smd+{{conidA}}+{"fields":["31"]}""", ct);
        await WaitForMessageAsync(mockWs, $$"""smd+{{conidB}}+{"fields":["31"]}""", ct);

        // Interleave: three frames for A's conid, one for B's. On correct routing A sees exactly its
        // three, B sees exactly its one. On prefix (cross-delivering) routing A would also see the B
        // frame and B the A frames.
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{conidA}}","31":"a1"}""", ct);
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{conidB}}","31":"b1"}""", ct);
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{conidA}}","31":"a2"}""", ct);
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{conidA}}","31":"a3"}""", ct);

        await aThird.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        await bFirst.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        MarketDataTick[] aSnapshot;
        MarketDataTick[] bSnapshot;
        lock (aTicks)
        {
            aSnapshot = [.. aTicks];
        }
        lock (bTicks)
        {
            bSnapshot = [.. bTicks];
        }

        aSnapshot.ShouldAllBe(t => t.Conid == conidA,
            "subscription A must observe only conid A's ticks, never conid B's (PRB-1.1/1.2).");
        bSnapshot[0].Conid.ShouldBe(conidB,
            "subscription B's first tick must be conid B's, never conid A's (PRB-1.1/1.2).");
    }

    /// <summary>
    /// PRB-3.1 for <c>ssd</c>: two <c>AccountSummaryAsync</c> subscriptions for different accounts each
    /// observe only their own account's summary rows — account-money data is never cross-delivered.
    /// </summary>
    [Fact]
    public async Task AccountSummaryAsync_TwoAccounts_EachSubscriptionObservesOnlyItsOwnRows()
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

        const string acctA = "DUAAA";
        const string acctB = "DUBBB";

        var aUpdates = new List<AccountSummaryUpdate>();
        var bUpdates = new List<AccountSummaryUpdate>();
        var aThird = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var subA = await harness.Client.Streaming.AccountSummaryAsync(acctA, cancellationToken: ct);
        var subB = await harness.Client.Streaming.AccountSummaryAsync(acctB, cancellationToken: ct);

        using var obsA = subA.Stream.Subscribe(new StreamObserver<AccountSummaryUpdate>(
            onNext: u =>
            {
                lock (aUpdates)
                {
                    aUpdates.Add(u);
                    if (aUpdates.Count == 3)
                    {
                        aThird.TrySetResult();
                    }
                }
            }));
        using var obsB = subB.Stream.Subscribe(new StreamObserver<AccountSummaryUpdate>(
            onNext: u =>
            {
                lock (bUpdates)
                {
                    bUpdates.Add(u);
                    bFirst.TrySetResult();
                }
            }));

        await WaitForMessageAsync(mockWs, $"ssd+{acctA}+{{}}", ct);
        await WaitForMessageAsync(mockWs, $"ssd+{acctB}+{{}}", ct);

        await mockWs.BroadcastTextAsync(SummaryFrame(acctA, "1"), ct);
        await mockWs.BroadcastTextAsync(SummaryFrame(acctB, "1"), ct);
        await mockWs.BroadcastTextAsync(SummaryFrame(acctA, "2"), ct);
        await mockWs.BroadcastTextAsync(SummaryFrame(acctA, "3"), ct);

        await aThird.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        await bFirst.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        AccountSummaryUpdate[] aSnapshot;
        AccountSummaryUpdate[] bSnapshot;
        lock (aUpdates)
        {
            aSnapshot = [.. aUpdates];
        }
        lock (bUpdates)
        {
            bSnapshot = [.. bUpdates];
        }

        aSnapshot.ShouldAllBe(u => u.AccountId == acctA,
            "subscription A must observe only account A's summary rows, never account B's (PRB-3.1).");
        bSnapshot[0].AccountId.ShouldBe(acctB,
            "subscription B's first update must be account B's, never account A's (PRB-3.1).");
    }

    /// <summary>
    /// PRB-3.1 for <c>sld</c>: two <c>AccountLedgerAsync</c> subscriptions for different accounts each
    /// observe only their own account's ledger rows.
    /// </summary>
    [Fact]
    public async Task AccountLedgerAsync_TwoAccounts_EachSubscriptionObservesOnlyItsOwnRows()
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

        const string acctA = "DUAAA";
        const string acctB = "DUBBB";

        var aUpdates = new List<AccountLedgerUpdate>();
        var bUpdates = new List<AccountLedgerUpdate>();
        var aThird = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var subA = await harness.Client.Streaming.AccountLedgerAsync(acctA, cancellationToken: ct);
        var subB = await harness.Client.Streaming.AccountLedgerAsync(acctB, cancellationToken: ct);

        using var obsA = subA.Stream.Subscribe(new StreamObserver<AccountLedgerUpdate>(
            onNext: u =>
            {
                lock (aUpdates)
                {
                    aUpdates.Add(u);
                    if (aUpdates.Count == 3)
                    {
                        aThird.TrySetResult();
                    }
                }
            }));
        using var obsB = subB.Stream.Subscribe(new StreamObserver<AccountLedgerUpdate>(
            onNext: u =>
            {
                lock (bUpdates)
                {
                    bUpdates.Add(u);
                    bFirst.TrySetResult();
                }
            }));

        await WaitForMessageAsync(mockWs, $"sld+{acctA}+{{}}", ct);
        await WaitForMessageAsync(mockWs, $"sld+{acctB}+{{}}", ct);

        await mockWs.BroadcastTextAsync(LedgerFrame(acctA, "1"), ct);
        await mockWs.BroadcastTextAsync(LedgerFrame(acctB, "1"), ct);
        await mockWs.BroadcastTextAsync(LedgerFrame(acctA, "2"), ct);
        await mockWs.BroadcastTextAsync(LedgerFrame(acctA, "3"), ct);

        await aThird.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        await bFirst.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        AccountLedgerUpdate[] aSnapshot;
        AccountLedgerUpdate[] bSnapshot;
        lock (aUpdates)
        {
            aSnapshot = [.. aUpdates];
        }
        lock (bUpdates)
        {
            bSnapshot = [.. bUpdates];
        }

        aSnapshot.ShouldAllBe(u => u.AccountId == acctA,
            "subscription A must observe only account A's ledger rows, never account B's (PRB-3.1).");
        bSnapshot[0].AccountId.ShouldBe(acctB,
            "subscription B's first update must be account B's, never account A's (PRB-3.1).");
    }

    /// <summary>
    /// ADR-0005 §4: a target-qualified frame whose full topic matches no live subscription is dropped
    /// observably — it increments the drop counter under <c>cause=unmatched</c> and reaches no
    /// subscriber — rather than being cross-delivered to a same-prefix subscription.
    /// </summary>
    [Fact]
    public async Task MarketDataAsync_UnmatchedConidFrame_DropsUnmatchedAndReachesNoSubscriber()
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

        const int conid = 100;
        const int unmatchedConid = 999;

        var firstTick = new TaskCompletionSource<MarketDataTick>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sub = await harness.Client.Streaming.MarketDataAsync(conid, _fields, ct);
        using var obs = sub.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: t => firstTick.TrySetResult(t)));

        await WaitForMessageAsync(mockWs, $$"""smd+{{conid}}+{"fields":["31"]}""", ct);

        // The unmatched frame first, then the subscription's own frame. With cross-delivery the
        // subscriber's first tick would be the unmatched conid; with subscription-scoped routing the
        // unmatched frame is dropped and the first tick is the subscribed conid.
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{unmatchedConid}}","31":"x"}""", ct);
        await mockWs.BroadcastTextAsync($$"""{"topic":"smd+{{conid}}","31":"1"}""", ct);

        var tick = await firstTick.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        tick.Conid.ShouldBe(conid, "the unmatched conid frame must not be delivered to the subscription.");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!drops.Drops.Any(d => d.Topic == "smd" && d.Cause == "unmatched") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }

        drops.Drops.ShouldContain(d => d.Topic == "smd" && d.Cause == "unmatched",
            "an unmatched target-qualified frame must increment the drop counter with cause=unmatched (ADR-0005 §4).");
    }

    private static string SummaryFrame(string account, string marker) =>
        $$"""{"topic":"ssd+{{account}}","result":[{"key":"ExcessLiquidity-S","currency":"USD","monetaryValue":{{marker}},"severity":0,"timestamp":1}]}""";

    private static string LedgerFrame(string account, string marker) =>
        $$"""{"topic":"sld+{{account}}","result":[{"key":"LedgerListUSD","cashbalance":{{marker}}.0,"netLiquidationValue":{{marker}}.0,"secondKey":"USD","timestamp":1}]}""";

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

    /// <summary>Captures streaming drop measurements (topic, cause) from the IbkrConduit meter.</summary>
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

    private sealed class StreamObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }
}
