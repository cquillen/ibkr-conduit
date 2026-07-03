using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class ChannelObservableTests
{
    [Fact]
    public async Task Subscribe_ReceivesItems_CallsOnNext()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance);
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
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance);
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
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance);
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
    public async Task Subscribe_MapperThrows_DoesNotCallOnErrorAndStreamStaysAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader,
            _ => throw new InvalidOperationException("bad map"),
            NullLogger.Instance);

        var errorCalled = false;
        var completed = new TaskCompletionSource();
        using var sub = observable.Subscribe(new TestObserver<string>(
            onError: _ => errorCalled = true,
            onCompleted: () => completed.TrySetResult()));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"x\"").RootElement, ct);

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
    public async Task Subscribe_MapperThrowsOnOneItem_SubsequentGoodItemIsDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader,
            e => e.GetString() == "bad" ? throw new InvalidOperationException("bad map") : e.GetString()!,
            NullLogger.Instance);

        var received = new TaskCompletionSource<string>();
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: v => received.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"bad\"").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("\"good\"").RootElement, ct);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        result.ShouldBe("good");
    }

    [Fact]
    public async Task Subscribe_MapperThrows_LogsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var logger = new CapturingLogger();
        var observable = new ChannelObservable<string>(channel.Reader,
            e => e.GetString() == "bad" ? throw new InvalidOperationException("bad map") : e.GetString()!,
            logger);

        var received = new TaskCompletionSource<string>();
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: v => received.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"bad\"").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("\"good\"").RootElement, ct);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        logger.Levels.ShouldContain(LogLevel.Warning);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
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
