using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class FanOutChannelObservableTests
{
    // Mapper: return one int per element of the frame's "args" array.
    private static IEnumerable<int> MapArgs(JsonElement frame) =>
        frame.GetProperty("args").EnumerateArray().Select(e => e.GetInt32());

    [Fact]
    public async Task Subscribe_FrameWithThreeElements_EmitsOnePerElement()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance);

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
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance);

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
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs, NullLogger.Instance);

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
            NullLogger.Instance);

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
            NullLogger.Instance);

        var got = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new CollectingObserver<int>(v => got.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"bad":true}""").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("""{"args":[42]}""").RootElement, ct);

        var value = await got.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        value.ShouldBe(42);
    }

    private sealed class CollectingObserver<T>(Action<T> onNext, Action? onCompleted = null, Action<Exception>? onError = null) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);
        public void OnError(Exception error) => onError?.Invoke(error);
        public void OnCompleted() => onCompleted?.Invoke();
    }
}
