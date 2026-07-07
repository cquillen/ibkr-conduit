using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class FanOutChannelObservableTests
{
    private const string _topic = "str";

    private static StreamingMetrics Metrics(string tenantId = "fanout-obs-test") =>
        new(new TenantContext(tenantId));

    // Mapper: return one int per element of the frame's "args" array.
    private static IEnumerable<int> MapArgs(JsonElement frame) =>
        frame.GetProperty("args").EnumerateArray().Select(e => e.GetInt32());

    [Fact]
    public async Task Subscribe_FrameWithThreeElements_EmitsOnePerElement()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var received = new List<int>();
        var done = new TaskCompletionSource();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v =>
        {
            received.Add(v);
            if (received.Count == 3)
            {
                done.TrySetResult();
            }
        }));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[10,20,30]}""").RootElement, ct);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        received.ShouldBe(new[] { 10, 20, 30 });
    }

    [Fact]
    public async Task Subscribe_EmptyArgs_EmitsNothingButStaysAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var received = new List<int>();
        var got = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v =>
        {
            received.Add(v);
            got.TrySetResult(v);
        }));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[]}""").RootElement, ct);
        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[99]}""").RootElement, ct);

        var value = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        value.ShouldBe(99);
        received.ShouldBe(new[] { 99 });
    }

    [Fact]
    public async Task Subscribe_ChannelCompletes_CallsOnCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var completed = new TaskCompletionSource();
        using var sub = observable.Subscribe(
            new CollectingObserver<int>(_ => { }, onCompleted: () => completed.TrySetResult()));

        channel.Writer.Complete();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        completed.Task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Subscribe_MapperThrows_DoesNotCallOnErrorAndStreamStaysAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(
            channel.Reader,
            _ => throw new InvalidOperationException("boom"),
            NullLogger.Instance, Metrics(), _topic);

        var errorCalled = false;
        var completed = new TaskCompletionSource();
        using var sub = observable.Subscribe(new CollectingObserver<int>(
            _ => { },
            onCompleted: () => completed.TrySetResult(),
            onError: _ => errorCalled = true));

        await channel.Writer.WriteAsync(
            JsonDocument.Parse("""{"args":[1]}""").RootElement, ct);

        // Give the pump a moment to process (and drop) the bad frame.
        await Task.Delay(200, ct);
        errorCalled.ShouldBeFalse();

        // The pump loop must still be running (not torn down by OnError): completing the
        // channel now should still flow through to OnCompleted.
        channel.Writer.Complete();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        completed.Task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Subscribe_MapperThrowsOnOneFrame_SubsequentGoodFrameIsDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(
            channel.Reader,
            frame => frame.TryGetProperty("bad", out _) ? throw new InvalidOperationException("boom") : MapArgs(frame),
            NullLogger.Instance, Metrics(), _topic);

        var got = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v => got.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"bad":true}""").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[42]}""").RootElement, ct);

        var value = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        value.ShouldBe(42);
    }

    [Fact]
    public async Task Subscribe_MapperThrows_IncrementsDropCounterWithMapperCause()
    {
        var ct = TestContext.Current.CancellationToken;
        const string tenantId = "fanout-mapper-drop";
        using var drops = new MeterDropCapture(tenantId);
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(
            channel.Reader,
            frame => frame.TryGetProperty("bad", out _) ? throw new InvalidOperationException("boom") : MapArgs(frame),
            NullLogger.Instance, new StreamingMetrics(new TenantContext(tenantId)), "str");

        var got = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v => got.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"bad":true}""").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[42]}""").RootElement, ct);
        await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        drops.Drops.ShouldContain(("str", "mapper"));
    }

    [Fact]
    public async Task Subscribe_ObserverThrows_CallsOnErrorAndDoesNotCompleteGracefully()
    {
        // FIL-3: a consumer OnNext fault surfaces via OnError, not swallowed as a malformed frame.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var onError = new TaskCompletionSource<Exception>();
        var completedCalled = false;
        using var sub = observable.Subscribe(new CollectingObserver<int>(
            _ => throw new InvalidOperationException("consumer boom"),
            onCompleted: () => completedCalled = true,
            onError: ex => onError.TrySetResult(ex)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[1]}""").RootElement, ct);

        var error = await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        error.ShouldBeOfType<InvalidOperationException>();
        completedCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Subscribe_ObserverThrowsOperationCanceled_CallsOnErrorNotOnCompleted()
    {
        // FIL-3 core: OperationCanceledException from OnNext must not read as graceful completion.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var onError = new TaskCompletionSource<Exception>();
        var completedCalled = false;
        using var sub = observable.Subscribe(new CollectingObserver<int>(
            _ => throw new OperationCanceledException("consumer cancelled"),
            onCompleted: () => completedCalled = true,
            onError: ex => onError.TrySetResult(ex)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[1]}""").RootElement, ct);

        var error = await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        error.ShouldBeOfType<OperationCanceledException>();
        completedCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Subscribe_ObserverThrows_IncrementsDropCounterWithObserverCause()
    {
        var ct = TestContext.Current.CancellationToken;
        const string tenantId = "fanout-observer-drop";
        using var drops = new MeterDropCapture(tenantId);
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, new StreamingMetrics(new TenantContext(tenantId)), "str");

        var onError = new TaskCompletionSource<Exception>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(
            _ => throw new InvalidOperationException("consumer boom"),
            onError: ex => onError.TrySetResult(ex)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[1]}""").RootElement, ct);
        await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        drops.Drops.ShouldContain(("str", "observer"));
    }

    [Fact]
    public void Subscribe_SecondConcurrentSubscribe_ThrowsInvalidOperationException()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        using var first = observable.Subscribe(new CollectingObserver<int>(_ => { }));

        Should.Throw<InvalidOperationException>(() => observable.Subscribe(new CollectingObserver<int>(_ => { })));
    }

    [Fact]
    public void Subscribe_AfterFirstDisposed_SecondSubscribeSucceeds()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance, Metrics(), _topic);

        var first = observable.Subscribe(new CollectingObserver<int>(_ => { }));
        first.Dispose();

        using var second = observable.Subscribe(new CollectingObserver<int>(_ => { }));
        second.ShouldNotBeNull();
    }

    private sealed class CollectingObserver<T>(Action<T> onNext, Action? onCompleted = null, Action<Exception>? onError = null) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);
        public void OnError(Exception error) => onError?.Invoke(error);
        public void OnCompleted() => onCompleted?.Invoke();
    }
}
