using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class ChannelObservableTests
{
    private const string _topic = "sts";

    private static StreamingMetrics Metrics(string tenantId = "channel-obs-test") =>
        new(new TenantContext(tenantId));

    [Fact]
    public async Task Subscribe_ReceivesItems_CallsOnNext()
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);
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
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);
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
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);
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
            NullLogger.Instance, Metrics(), _topic);

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
            NullLogger.Instance, Metrics(), _topic);

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
            logger, Metrics(), _topic);

        var received = new TaskCompletionSource<string>();
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: v => received.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"bad\"").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("\"good\"").RootElement, ct);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public async Task Subscribe_MapperThrows_LogsWireTopicNotDtoTypeName()
    {
        // GAP2-4: the dropped-frame warning must name the wire topic (here "str"), not the DTO
        // type name (here Int32), so a drop is traceable to the topic that lost it.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var logger = new MessageCapturingLogger();
        var observable = new ChannelObservable<int>(channel.Reader,
            e => e.GetInt32() == 0 ? throw new InvalidOperationException("bad") : e.GetInt32(),
            logger, Metrics(), "str");

        var received = new TaskCompletionSource<int>();
        using var sub = observable.Subscribe(new TestObserver<int>(onNext: v => received.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("0").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("7").RootElement, ct);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        var warning = logger.Messages.First(m => m.Level == LogLevel.Warning);
        warning.Message.ShouldContain("str");
        warning.Message.ShouldNotContain("Int32");
    }

    [Fact]
    public async Task Subscribe_MapperThrows_IncrementsDropCounterWithMapperCause()
    {
        // GAP2-4: a mapper drop increments ibkr.conduit.streaming.frames.dropped with cause=mapper.
        var ct = TestContext.Current.CancellationToken;
        const string tenantId = "channel-mapper-drop";
        using var drops = new MeterDropCapture(tenantId);
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader,
            e => e.GetString() == "bad" ? throw new InvalidOperationException("bad map") : e.GetString()!,
            NullLogger.Instance, new StreamingMetrics(new TenantContext(tenantId)), "sts");

        var received = new TaskCompletionSource<string>();
        using var sub = observable.Subscribe(new TestObserver<string>(onNext: v => received.TrySetResult(v)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"bad\"").RootElement, ct);
        await channel.Writer.WriteAsync(JsonDocument.Parse("\"good\"").RootElement, ct);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        drops.Drops.ShouldContain(("sts", "mapper"));
    }

    [Fact]
    public async Task Subscribe_ObserverThrows_CallsOnErrorAndDoesNotCompleteGracefully()
    {
        // FIL-3: a consumer OnNext fault must surface via OnError, never be swallowed nor
        // masqueraded as graceful completion.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);

        var onError = new TaskCompletionSource<Exception>();
        var completedCalled = false;
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: _ => throw new InvalidOperationException("consumer boom"),
            onError: ex => onError.TrySetResult(ex),
            onCompleted: () => completedCalled = true));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"x\"").RootElement, ct);

        var error = await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        error.ShouldBeOfType<InvalidOperationException>();
        completedCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Subscribe_ObserverThrowsOperationCanceled_CallsOnErrorNotOnCompleted()
    {
        // FIL-3 core: an OperationCanceledException thrown by the consumer's OnNext must NOT read as
        // graceful completion — it tears down via OnError.
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);

        var onError = new TaskCompletionSource<Exception>();
        var completedCalled = false;
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: _ => throw new OperationCanceledException("consumer cancelled"),
            onError: ex => onError.TrySetResult(ex),
            onCompleted: () => completedCalled = true));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"x\"").RootElement, ct);

        var error = await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        error.ShouldBeOfType<OperationCanceledException>();
        completedCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Subscribe_ObserverThrows_IncrementsDropCounterWithObserverCause()
    {
        var ct = TestContext.Current.CancellationToken;
        const string tenantId = "channel-observer-drop";
        using var drops = new MeterDropCapture(tenantId);
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, new StreamingMetrics(new TenantContext(tenantId)), "sts");

        var onError = new TaskCompletionSource<Exception>();
        using var sub = observable.Subscribe(new TestObserver<string>(
            onNext: _ => throw new InvalidOperationException("consumer boom"),
            onError: ex => onError.TrySetResult(ex)));

        await channel.Writer.WriteAsync(JsonDocument.Parse("\"x\"").RootElement, ct);
        await onError.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        drops.Drops.ShouldContain(("sts", "observer"));
    }

    [Fact]
    public void Subscribe_SecondConcurrentSubscribe_ThrowsInvalidOperationException()
    {
        // FIL-5: Stream is single-observer — a second concurrent Subscribe throws.
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);

        using var first = observable.Subscribe(new TestObserver<string>());

        Should.Throw<InvalidOperationException>(() => observable.Subscribe(new TestObserver<string>()));
    }

    [Fact]
    public void Subscribe_AfterFirstDisposed_SecondSubscribeSucceeds()
    {
        // FIL-5: disposing the first subscription frees the single-observer slot.
        var channel = Channel.CreateUnbounded<JsonElement>();
        var observable = new ChannelObservable<string>(channel.Reader, e => e.GetString()!, NullLogger.Instance, Metrics(), _topic);

        var first = observable.Subscribe(new TestObserver<string>());
        first.Dispose();

        using var second = observable.Subscribe(new TestObserver<string>());
        second.ShouldNotBeNull();
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }

    private sealed class MessageCapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add((logLevel, formatter(state, exception)));
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
