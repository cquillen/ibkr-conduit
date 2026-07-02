using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
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
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

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
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

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
        var observable = new FanOutChannelObservable<int>(channel.Reader, MapArgs);

        var completed = new TaskCompletionSource();
        using var sub = observable.Subscribe(
            new CollectingObserver<int>(_ => { }, onCompleted: () => completed.TrySetResult()));

        channel.Writer.Complete();

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        completed.Task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private sealed class CollectingObserver<T>(Action<T> onNext, Action? onCompleted = null) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() => onCompleted?.Invoke();
    }
}
