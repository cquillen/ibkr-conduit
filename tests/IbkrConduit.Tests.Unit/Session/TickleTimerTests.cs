using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Health;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TickleTimerTests
{
    [Fact]
    public async Task StartAsync_CallsTickleOnInterval()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Pump: advance 1s at a time until 2 ticks are observed.
        // If RunAsync hasn't re-registered its next timer yet when we advance,
        // the loop advances again on the next iteration — reliable without real delays.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sessionApi.TickleCallCount < 2 && DateTime.UtcNow < deadline)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);
        failureCount.ShouldBe(0);
    }

    private static async Task WaitForTickleCount(FakeSessionApi sessionApi, int expectedCount, CancellationToken cancellationToken)
    {
        while (sessionApi.TickleCallCount < expectedCount)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [Fact]
    public async Task StartAsync_WhenTickleReturnsUnauthenticated_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { Authenticated = false };
        var fakeTime = new FakeTimeProvider();
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await failureTcs.Task.WaitAsync(TestContext.Current.CancellationToken);

        failureTcs.Task.IsCompletedSuccessfully.ShouldBeTrue("Failure callback should have been invoked");
        await timer.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTickleThrows_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var fakeTime = new FakeTimeProvider();
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await failureTcs.Task.WaitAsync(TestContext.Current.CancellationToken);

        failureTcs.Task.IsCompletedSuccessfully.ShouldBeTrue("Failure callback should have been invoked on exception");
        await timer.StopAsync();
    }

    [Fact]
    public async Task StopAsync_StopsTickling()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        Func<CancellationToken, Task> onFailure = _ => Task.CompletedTask;

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Let one tick happen before stopping
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await WaitForTickleCount(sessionApi, 1, TestContext.Current.CancellationToken);

        var countAfterStop = sessionApi.TickleCallCount;
        await timer.StopAsync();

        // Advance clock further — no more ticks should happen since the timer is stopped
        fakeTime.Advance(TimeSpan.FromSeconds(2));
        await Task.Yield();

        sessionApi.TickleCallCount.ShouldBe(countAfterStop);
    }

    [Fact]
    public async Task StopAsync_CalledTwice_DoesNotThrow()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        await timer.StopAsync();
        await timer.StopAsync(); // Should not throw
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var sessionApi = new FakeSessionApi();
        var fakeTime = new FakeTimeProvider();
        var timer = new TickleTimer(
            sessionApi,
            _ => Task.CompletedTask,
            new SessionHealthState(),
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1,
            fakeTime);

        await timer.StopAsync(); // Should not throw
    }

    private class FakeSessionApi : IIbkrSessionApi
    {
        public int TickleCallCount { get; private set; }
        public bool Authenticated { get; set; } = true;
        public bool ShouldThrow { get; set; }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default)
        {
            TickleCallCount++;

            if (ShouldThrow)
            {
                throw new HttpRequestException("Simulated tickle failure");
            }

            return Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Hmds: null,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(Authenticated: Authenticated, Competing: false, Connected: true, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null))));
        }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null));

        public Task<SuppressResponse> SuppressQuestionsAsync(SuppressRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResponse(Status: "submitted"));

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LogoutResponse(Confirmed: true));

        public Task<SuppressResetResponse> ResetSuppressedQuestionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResetResponse(Status: "submitted"));

        public Task<AuthStatusResponse> GetAuthStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthStatusResponse(true, false, true, true, null, null, null, null, null, null));
    }
}
