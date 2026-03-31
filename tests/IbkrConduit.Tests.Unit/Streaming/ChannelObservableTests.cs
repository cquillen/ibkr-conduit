using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class ChannelObservableTests
{
    [Fact]
    public async Task Subscribe_ReceivesItems_CallsOnNext()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!);
        var received = new TaskCompletionSource<string>();

        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: v => received.TrySetResult(v)));

        var json = JsonDocument.Parse("\"hello\"").RootElement;
        await channel.Writer.WriteAsync(json, TestContext.Current.CancellationToken);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.ShouldBe("hello");
    }

    [Fact]
    public async Task Subscribe_ChannelCompleted_CallsOnCompleted()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!);
        var completed = new TaskCompletionSource<bool>();

        using var sub = observable.Subscribe(new TestObserver<string>(
            onCompleted: () => completed.TrySetResult(true)));

        channel.Writer.Complete();

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task Subscribe_Dispose_StopsReceiving()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!);
        var itemCount = 0;
        var completed = new TaskCompletionSource<bool>();

        var sub = observable.Subscribe(new TestObserver<string>(
            onNext: _ => Interlocked.Increment(ref itemCount),
            onCompleted: () => completed.TrySetResult(true)));

        var json = JsonDocument.Parse("\"first\"").RootElement;
        await channel.Writer.WriteAsync(json, TestContext.Current.CancellationToken);

        // Give the pump a moment to process
        await Task.Delay(100, TestContext.Current.CancellationToken);

        sub.Dispose();

        // Wait for OnCompleted to be called
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.ShouldBeTrue();

        // Writing more items after dispose should not increase count
        var beforeCount = Volatile.Read(ref itemCount);
        await channel.Writer.WriteAsync(JsonDocument.Parse("\"second\"").RootElement, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Volatile.Read(ref itemCount).ShouldBe(beforeCount);
    }

    [Fact]
    public async Task Subscribe_MapperThrows_CallsOnError()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader,
            _ => throw new InvalidOperationException("bad map"));
        var error = new TaskCompletionSource<Exception>();

        using var sub = observable.Subscribe(new TestObserver<string>(
            onError: ex => error.TrySetResult(ex)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"x\"").RootElement, TestContext.Current.CancellationToken);

        var result = await error.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InvalidOperationException>();
        result.Message.ShouldBe("bad map");
    }

    private sealed class TestObserver<T> : IObserver<T>
    {
        private readonly Action<T>? _onNext;
        private readonly Action<Exception>? _onError;
        private readonly Action? _onCompleted;

        public TestObserver(
            Action<T>? onNext = null,
            Action<Exception>? onError = null,
            Action? onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(T value) => _onNext?.Invoke(value);
        public void OnError(Exception error) => _onError?.Invoke(error);
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
