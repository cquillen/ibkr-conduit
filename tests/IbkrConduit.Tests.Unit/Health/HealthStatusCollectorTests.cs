using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Health;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Health;

public class HealthStatusCollectorTests
{
    private readonly IIbkrSessionApi _sessionApi = Substitute.For<IIbkrSessionApi>();
    private readonly ISessionTokenProvider _tokenProvider = Substitute.For<ISessionTokenProvider>();
    private readonly IIbkrWebSocketClient _wsClient = Substitute.For<IIbkrWebSocketClient>();
    private readonly LastSuccessfulCallTracker _lastCallTracker = new();
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly HealthStatusOptions _options = new();
    private readonly SessionHealthState _sessionHealthState = CreateHealthyState();

    private static SessionHealthState CreateHealthyState()
    {
        var state = new SessionHealthState();
        state.Update(authenticated: true, connected: true, competing: false, established: true);
        return state;
    }

    public HealthStatusCollectorTests()
    {
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = false,
            QueueLimit = 0,
        });
    }

    [Fact]
    public async Task GetHealthStatusAsync_Passive_AllHealthy_ReturnsHealthy()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Healthy);
        result.Session.Authenticated.ShouldBeTrue();
        result.Session.Connected.ShouldBeTrue();
        result.Session.Competing.ShouldBeFalse();
        result.Streaming.ShouldBeNull();
    }

    [Fact]
    public async Task GetHealthStatusAsync_TokenExpired_ReturnsUnhealthy()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddMinutes(-1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Unhealthy);
        result.Token.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public async Task GetHealthStatusAsync_TokenNearExpiry_ReturnsDegraded()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddMinutes(2));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Degraded);
        result.Token.IsExpired.ShouldBeFalse();
        result.Token.TimeUntilExpiry!.Value.TotalMinutes.ShouldBeLessThan(5);
    }

    [Fact]
    public async Task GetHealthStatusAsync_ActiveProbe_CallsAuthStatus()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        _sessionApi.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new AuthStatusResponse(true, false, true, true, null, null, null, null, null, null));

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: true, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Healthy);
        result.Session.Authenticated.ShouldBeTrue();
        await _sessionApi.Received(1).GetAuthStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHealthStatusAsync_SessionNotAuthenticated_ReturnsUnhealthy()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        _sessionApi.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new AuthStatusResponse(false, false, false, false, "Not authenticated", null, null, null, null, null));

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: true, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Unhealthy);
        result.Session.Authenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetHealthStatusAsync_SessionCompeting_ReturnsDegraded()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        _sessionApi.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new AuthStatusResponse(true, true, true, true, null, null, null, null, null, null));

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: true, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Degraded);
        result.Session.Competing.ShouldBeTrue();
    }

    [Fact]
    public async Task GetHealthStatusAsync_WebSocketConnected_IncludesStreamingHealth()
    {
        var lastMsg = DateTimeOffset.UtcNow.AddSeconds(-5);
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(true);
        _wsClient.ActiveSubscriptionCount.Returns(3);
        _wsClient.LastMessageReceivedAt.Returns(lastMsg);

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Healthy);
        result.Streaming.ShouldNotBeNull();
        result.Streaming!.IsConnected.ShouldBeTrue();
        result.Streaming.ActiveSubscriptions.ShouldBe(3);
        result.Streaming.LastMessageAt.ShouldBe(lastMsg);
    }

    [Fact]
    public async Task GetHealthStatusAsync_StaleLastCall_ReturnsUnhealthy()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        // Record a successful call, then use a very short staleness timeout
        var staleTracker = new LastSuccessfulCallTracker();
        staleTracker.RecordSuccess();

        var options = new HealthStatusOptions { StalenessTimeout = TimeSpan.FromMilliseconds(1) };
        await Task.Delay(10, TestContext.Current.CancellationToken);

        var collector = new HealthStatusCollector(
            _sessionApi, _tokenProvider, _wsClient, staleTracker, _rateLimiter, options, _sessionHealthState);
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Unhealthy);
    }

    [Fact]
    public async Task GetHealthStatusAsync_NoTokenYet_ReturnsHealthyWithNullExpiry()
    {
        _tokenProvider.CurrentTokenExpiry.Returns((DateTimeOffset?)null);
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Healthy);
        result.Token.IsExpired.ShouldBeFalse();
        result.Token.TimeUntilExpiry.ShouldBeNull();
    }

    [Fact]
    public async Task GetHealthStatusAsync_StreamingDisconnectedWithActiveSubs_ReturnsDegraded()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(2);

        SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Degraded);
        result.Streaming.ShouldNotBeNull();
        result.Streaming!.IsConnected.ShouldBeFalse();
        result.Streaming.ActiveSubscriptions.ShouldBe(2);
    }

    [Fact]
    public async Task GetHealthStatusAsync_Passive_SessionNotAuthenticated_ReturnsUnhealthy()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        SetLastCallToNow();

        var unhealthyState = new SessionHealthState();
        unhealthyState.Update(authenticated: false, connected: false, competing: false, established: false, failReason: "Session lost");

        var collector = new HealthStatusCollector(
            _sessionApi, _tokenProvider, _wsClient, _lastCallTracker, _rateLimiter, _options, unhealthyState);
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Unhealthy);
        result.Session.Authenticated.ShouldBeFalse();
        result.Session.FailReason.ShouldBe("Session lost");
    }

    [Fact]
    public async Task GetHealthStatusAsync_Passive_SessionCompeting_ReturnsDegraded()
    {
        _tokenProvider.CurrentTokenExpiry.Returns(DateTimeOffset.UtcNow.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);

        SetLastCallToNow();

        var competingState = new SessionHealthState();
        competingState.Update(authenticated: true, connected: true, competing: true, established: true);

        var collector = new HealthStatusCollector(
            _sessionApi, _tokenProvider, _wsClient, _lastCallTracker, _rateLimiter, _options, competingState);
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Degraded);
        result.Session.Competing.ShouldBeTrue();
    }

    [Fact]
    public async Task GetHealthStatusAsync_OnFakeClock_StampsCheckedAtFromTimeProvider()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(start);

        _tokenProvider.CurrentTokenExpiry.Returns(start.AddHours(1));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);
        _lastCallTracker.RecordSuccess();

        var collector = CreateCollector(fakeTime);
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.CheckedAt.ShouldBe(start);

        fakeTime.Advance(TimeSpan.FromHours(2));
        var laterResult = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        laterResult.CheckedAt.ShouldBe(start.AddHours(2));
    }

    [Fact]
    public async Task GetHealthStatusAsync_TokenExpiry_OnFakeClock_ComputesRemainingFromTimeProvider()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(start);

        _tokenProvider.CurrentTokenExpiry.Returns(start.AddMinutes(30));
        _wsClient.IsConnected.Returns(false);
        _wsClient.ActiveSubscriptionCount.Returns(0);
        _lastCallTracker.RecordSuccess();

        var collector = CreateCollector(fakeTime);
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.Token.IsExpired.ShouldBeFalse();
        result.Token.TimeUntilExpiry.ShouldBe(TimeSpan.FromMinutes(30));

        fakeTime.Advance(TimeSpan.FromMinutes(45));
        var afterExpiry = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        afterExpiry.Token.IsExpired.ShouldBeTrue();
        afterExpiry.Token.TimeUntilExpiry!.Value.ShouldBe(TimeSpan.FromMinutes(-15));
        afterExpiry.OverallStatus.ShouldBe(HealthState.Unhealthy);
    }

    private HealthStatusCollector CreateCollector(TimeProvider? timeProvider = null) =>
        new(_sessionApi, _tokenProvider, _wsClient, _lastCallTracker, _rateLimiter, _options, _sessionHealthState, timeProvider);

    private void SetLastCallToNow() => _lastCallTracker.RecordSuccess();
}
