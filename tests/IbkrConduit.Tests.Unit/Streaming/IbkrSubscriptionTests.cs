using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class IbkrSubscriptionTests
{
    private static IObservable<int> EmptyStream() => new NoopObservable();

    [Fact]
    public async Task UnsubscribeAsync_InvokesUnderlyingDelegateOnce()
    {
        var count = 0;
        var sub = new IbkrSubscription<int>(EmptyStream(), _ => { count++; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);
        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_InvokesUnsubscribeOnce_EvenAfterExplicitUnsubscribe()
    {
        var count = 0;
        var sub = new IbkrSubscription<int>(EmptyStream(), _ => { count++; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(TestContext.Current.CancellationToken);
        await sub.DisposeAsync();

        count.ShouldBe(1);
    }

    [Fact]
    public void Stream_ReturnsTheProvidedObservable()
    {
        var stream = EmptyStream();
        var sub = new IbkrSubscription<int>(stream, _ => ValueTask.CompletedTask);

        sub.Stream.ShouldBeSameAs(stream);
    }

    [Fact]
    public async Task UnsubscribeAsync_PassesCancellationTokenToDelegate()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        var sub = new IbkrSubscription<int>(EmptyStream(), ct => { seen = ct; return ValueTask.CompletedTask; });

        await sub.UnsubscribeAsync(cts.Token);

        seen.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task UnsubscribeAsync_UnderConcurrency_InvokesDelegateExactlyOnce()
    {
        var count = 0;
        var sub = new IbkrSubscription<int>(EmptyStream(), _ => { Interlocked.Increment(ref count); return ValueTask.CompletedTask; });

        var ct = TestContext.Current.CancellationToken;
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(async () =>
            {
                if (i % 2 == 0)
                {
                    await sub.UnsubscribeAsync(ct);
                }
                else
                {
                    await sub.DisposeAsync();
                }
            }, ct));
        await Task.WhenAll(tasks);

        count.ShouldBe(1);
    }

    private sealed class NoopObservable : IObservable<int>
    {
        public IDisposable Subscribe(IObserver<int> observer) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
