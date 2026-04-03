using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TickleTimerTests
{
    [Fact]
    public async Task StartAsync_CallsTickleOnInterval()
    {
        var sessionApi = new FakeSessionApi();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Wait enough time for at least 2 ticks
        await Task.Delay(2500, TestContext.Current.CancellationToken);

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);
        failureCount.ShouldBe(0);
    }

    [Fact]
    public async Task StartAsync_WhenTickleReturnsUnauthenticated_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { Authenticated = false };
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Wait for the failure callback to fire
        var completed = await Task.WhenAny(failureTcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
        completed.ShouldBe(failureTcs.Task, "Failure callback should have been invoked");

        await timer.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTickleThrows_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        var completed = await Task.WhenAny(failureTcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
        completed.ShouldBe(failureTcs.Task, "Failure callback should have been invoked on exception");

        await timer.StopAsync();
    }

    [Fact]
    public async Task StopAsync_StopsTickling()
    {
        var sessionApi = new FakeSessionApi();
        Func<CancellationToken, Task> onFailure = _ => Task.CompletedTask;

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await timer.StartAsync(cts.Token);

        // Let one tick happen
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        await timer.StopAsync();

        var countAfterStop = sessionApi.TickleCallCount;

        // Wait to ensure no more ticks happen
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        sessionApi.TickleCallCount.ShouldBe(countAfterStop);
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
