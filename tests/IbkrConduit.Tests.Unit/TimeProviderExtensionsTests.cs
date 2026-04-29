using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace IbkrConduit.Tests.Unit;

public class TimeProviderExtensionsTests
{
    [Fact]
    public void Delay_TimeSpan_IsNotCompletedBeforeAdvance()
    {
        var fakeTime = new FakeTimeProvider();

        var delayTask = fakeTime.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        delayTask.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Delay_TimeSpan_CompletesAfterAdvance()
    {
        var fakeTime = new FakeTimeProvider();
        var delayTask = fakeTime.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await delayTask;

        delayTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Delay_Milliseconds_CompletesAfterAdvance()
    {
        var fakeTime = new FakeTimeProvider();
        var delayTask = fakeTime.Delay(1000, TestContext.Current.CancellationToken);

        fakeTime.Advance(TimeSpan.FromMilliseconds(1000));
        await delayTask;

        delayTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void Delay_WithAlreadyCancelledToken_ReturnsImmediatelyCancelledTask()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fakeTime = new FakeTimeProvider();

        var delayTask = fakeTime.Delay(TimeSpan.FromSeconds(1), cts.Token);

        delayTask.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task Delay_WhenTokenCancelledDuringWait_TaskBecomeCancelled()
    {
        using var cts = new CancellationTokenSource();
        var fakeTime = new FakeTimeProvider();
        var delayTask = fakeTime.Delay(TimeSpan.FromSeconds(1), cts.Token);
        delayTask.IsCompleted.ShouldBeFalse();

        cts.Cancel();
        await Task.Yield();

        delayTask.IsCanceled.ShouldBeTrue();
    }
}
