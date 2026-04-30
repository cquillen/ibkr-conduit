using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionLifecycleNotifierTests
{
    [Fact]
    public async Task NotifyAsync_WithSubscribers_CallsAll()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var callCount1 = 0;
        var callCount2 = 0;

        notifier.Subscribe(_ => { callCount1++; return Task.CompletedTask; });
        notifier.Subscribe(_ => { callCount2++; return Task.CompletedTask; });

        await notifier.NotifyAsync(TestContext.Current.CancellationToken);

        callCount1.ShouldBe(1);
        callCount2.ShouldBe(1);
    }

    [Fact]
    public async Task NotifyAsync_SubscriberThrows_DoesNotBlockOthers()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var secondCalled = false;

        notifier.Subscribe(_ => throw new InvalidOperationException("boom"));
        notifier.Subscribe(_ => { secondCalled = true; return Task.CompletedTask; });

        await notifier.NotifyAsync(TestContext.Current.CancellationToken);

        secondCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Subscribe_Dispose_RemovesSubscriber()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var callCount = 0;

        var subscription = notifier.Subscribe(_ => { callCount++; return Task.CompletedTask; });

        await notifier.NotifyAsync(TestContext.Current.CancellationToken);
        callCount.ShouldBe(1);

        subscription.Dispose();

        await notifier.NotifyAsync(TestContext.Current.CancellationToken);
        callCount.ShouldBe(1); // Should not have been called again
    }

    [Fact]
    public async Task NotifyAsync_NoSubscribers_DoesNotThrow()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);

        await notifier.NotifyAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NotifyAsync_PassesCancellationToken()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        CancellationToken receivedToken = default;

        notifier.Subscribe(ct => { receivedToken = ct; return Task.CompletedTask; });

        using var cts = new CancellationTokenSource();
        await notifier.NotifyAsync(cts.Token);

        receivedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task NotifyTickleSucceededAsync_WithSubscribers_CallsAll()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var callCount1 = 0;
        var callCount2 = 0;

        notifier.SubscribeTickleSucceeded(_ => { callCount1++; return Task.CompletedTask; });
        notifier.SubscribeTickleSucceeded(_ => { callCount2++; return Task.CompletedTask; });

        await notifier.NotifyTickleSucceededAsync(TestContext.Current.CancellationToken);

        callCount1.ShouldBe(1);
        callCount2.ShouldBe(1);
    }

    [Fact]
    public async Task NotifyTickleSucceededAsync_SubscriberThrows_DoesNotBlockOthers()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var secondCalled = false;

        notifier.SubscribeTickleSucceeded(_ => throw new InvalidOperationException("boom"));
        notifier.SubscribeTickleSucceeded(_ => { secondCalled = true; return Task.CompletedTask; });

        await notifier.NotifyTickleSucceededAsync(TestContext.Current.CancellationToken);

        secondCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SubscribeTickleSucceeded_DisposedSubscription_NoLongerInvoked()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var callCount = 0;

        var subscription = notifier.SubscribeTickleSucceeded(_ => { callCount++; return Task.CompletedTask; });
        subscription.Dispose();

        await notifier.NotifyTickleSucceededAsync(TestContext.Current.CancellationToken);

        callCount.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyTickleSucceededAsync_DoesNotInvokeSessionRefreshSubscribers()
    {
        var notifier = new SessionLifecycleNotifier(NullLogger<SessionLifecycleNotifier>.Instance);
        var refreshCallCount = 0;
        var tickleCallCount = 0;

        notifier.Subscribe(_ => { refreshCallCount++; return Task.CompletedTask; });
        notifier.SubscribeTickleSucceeded(_ => { tickleCallCount++; return Task.CompletedTask; });

        await notifier.NotifyTickleSucceededAsync(TestContext.Current.CancellationToken);

        refreshCallCount.ShouldBe(0);
        tickleCallCount.ShouldBe(1);
    }
}
