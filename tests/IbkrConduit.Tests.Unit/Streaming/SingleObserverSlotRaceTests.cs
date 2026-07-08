using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// STR-2 pins: <see cref="SingleObserverChannelObservable{T}"/> must free its single-observer slot
/// only after the pump task has exited, and the pump must observe cancellation per item so a
/// disposed subscription stops draining buffered frames to a dead observer — so dispose-then-
/// resubscribe never yields two concurrent pumps splitting delivery on one reader.
/// </summary>
public class SingleObserverSlotRaceTests
{
    private const string _topic = "sor";

    private static StreamingMetrics Metrics(string tenantId = "slot-race-test") =>
        new(new TenantContext(tenantId));

    [Fact]
    public async Task Dispose_WhilePumpDrainingBufferedFrames_BlocksUntilPumpExitsAndStopsDelivery()
    {
        // A hot stream with buffered frames: the observer parks inside OnNext on the first frame,
        // holding the pump. Disposing the slot must (a) not return until the pump has exited, and
        // (b) leave the remaining buffered frames undelivered to the disposed observer.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<int>(
            channel.Reader, e => e.GetInt32(), NullLogger.Instance, Metrics(), _topic);

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new SemaphoreSlim(0, 1);
        var firstReceived = new List<int>();

        var first = observable.Subscribe(new SlotObserver<int>(onNext: v =>
        {
            lock (firstReceived)
            {
                firstReceived.Add(v);
            }
            if (v == 1)
            {
                firstEntered.TrySetResult();
                releaseFirst.Wait(ct);
            }
        }));

        // Prefill four frames while the observer will park on the first.
        for (var i = 1; i <= 4; i++)
        {
            await channel.Writer.WriteAsync(JsonDocument.Parse(i.ToString()).RootElement, ct);
        }

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // Dispose on a background thread: it cancels then must block on the pump join.
        var disposeReturned = Task.Run(() => first.Dispose(), ct);

        // The pump is parked in OnNext, so Dispose must NOT have returned yet.
        await Task.Delay(150, ct);
        disposeReturned.IsCompleted.ShouldBeFalse("Dispose must block until the pump exits");

        // Let the parked OnNext return; the pump then observes cancellation per item and exits
        // WITHOUT draining the remaining buffered frames to the disposed observer.
        releaseFirst.Release();
        await disposeReturned.WaitAsync(TimeSpan.FromSeconds(5), ct);

        int deliveredToFirst;
        lock (firstReceived)
        {
            deliveredToFirst = firstReceived.Count;
        }
        deliveredToFirst.ShouldBe(1, "only the in-flight frame was delivered; buffered frames were not drained to the disposed observer");

        // The slot is free (pump exited): resubscribe succeeds and drains the remaining frames.
        var secondReceived = new List<int>();
        var secondGotAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var second = observable.Subscribe(new SlotObserver<int>(onNext: v =>
        {
            secondReceived.Add(v);
            if (secondReceived.Count == 3)
            {
                secondGotAll.TrySetResult();
            }
        }));

        await secondGotAll.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        secondReceived.ShouldBe(new[] { 2, 3, 4 });
    }

    [Fact]
    public async Task Dispose_WhilePumpParkedMidMultiItemFrame_StopsDeliveringRemainingItemsOfThatFrame()
    {
        // FanOutChannelObservable materializes N>1 items per frame and delivers them via an inner
        // loop. The per-frame cancellation check only guards reading the NEXT frame; without a
        // per-item gate, a dispose that fires while the observer is parked on item 1 of a multi-item
        // frame would still drain items 2..N of that same already-fetched frame to the disposed
        // observer (the STR-2 failure mode, bounded to one frame). This pins that per-item gate: on
        // sor/spl fan-out topics the remaining items of the in-flight frame must NOT be delivered
        // after dispose, and a fresh resubscribe starts clean.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(
            channel.Reader,
            frame => frame.GetProperty("args").EnumerateArray().Select(e => e.GetInt32()),
            NullLogger.Instance, Metrics(), _topic);

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new SemaphoreSlim(0, 1);
        var firstReceived = new List<int>();

        var first = observable.Subscribe(new SlotObserver<int>(onNext: v =>
        {
            lock (firstReceived)
            {
                firstReceived.Add(v);
            }
            if (v == 10)
            {
                firstEntered.TrySetResult();
                releaseFirst.Wait(ct);
            }
        }));

        // A single frame mapping to FOUR items; the observer parks on the first (10).
        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[10,20,30,40]}""").RootElement, ct);

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // Dispose on a background thread: it cancels then must block on the pump join.
        var disposeReturned = Task.Run(() => first.Dispose(), ct);

        // The pump is parked in OnNext on item 1, so Dispose must NOT have returned yet.
        await Task.Delay(150, ct);
        disposeReturned.IsCompleted.ShouldBeFalse("Dispose must block until the pump exits");

        // Let the parked OnNext return; the pump then observes cancellation per item and exits
        // WITHOUT draining items 2..N of the same frame to the disposed observer.
        releaseFirst.Release();
        await disposeReturned.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // Give any (erroneous) further deliveries a chance to land before asserting.
        await Task.Delay(100, ct);

        int deliveredToFirst;
        lock (firstReceived)
        {
            deliveredToFirst = firstReceived.Count;
        }
        deliveredToFirst.ShouldBe(1, "only the in-flight item was delivered; the remaining items of the same frame were not drained to the disposed observer");

        // The slot is free (pump exited): resubscribe succeeds and receives a fresh frame cleanly.
        var secondReceived = new List<int>();
        var secondGotAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var second = observable.Subscribe(new SlotObserver<int>(onNext: v =>
        {
            secondReceived.Add(v);
            if (secondReceived.Count == 2)
            {
                secondGotAll.TrySetResult();
            }
        }));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[50,60]}""").RootElement, ct);

        await secondGotAll.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        secondReceived.ShouldBe(new[] { 50, 60 });
    }

    [Fact]
    public async Task Dispose_FromWithinObserverCallback_DoesNotDeadlock()
    {
        // Disposing the slot from inside the observer's OnNext runs on the pump itself; a
        // block-join there would deadlock. The re-entrant dispose must return without deadlocking.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<int>(
            channel.Reader, e => e.GetInt32(), NullLogger.Instance, Metrics(), _topic);

        IDisposable? sub = null;
        var disposedFromCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        sub = observable.Subscribe(new SlotObserver<int>(onNext: _ =>
        {
            sub!.Dispose();
            disposedFromCallback.TrySetResult();
        }));

        await channel.Writer.WriteAsync(JsonDocument.Parse("1").RootElement, ct);

        // Completes (rather than hanging) => no deadlock.
        await disposedFromCallback.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
    }

    private sealed class SlotObserver<T>(Action<T>? onNext = null, Action<Exception>? onError = null, Action? onCompleted = null) : IObserver<T>
    {
        public void OnNext(T value) => onNext?.Invoke(value);
        public void OnError(Exception error) => onError?.Invoke(error);
        public void OnCompleted() => onCompleted?.Invoke();
    }
}
