using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Health;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Health;

public class HealthStatusCollectorTests
{
    private readonly IIbkrSessionApi _sessionApi = Substitute.For<IIbkrSessionApi>();
    private readonly ISessionTokenProvider _tokenProvider = Substitute.For<ISessionTokenProvider>();
    private readonly IIbkrWebSocketClient _wsClient = Substitute.For<IIbkrWebSocketClient>();
    private readonly LastSuccessfulCallHandler _lastCallHandler;
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly HealthStatusOptions _options = new();

    public HealthStatusCollectorTests()
    {
        _lastCallHandler = new LastSuccessfulCallHandler
        {
            InnerHandler = new FakeOkHandler(),
        };

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        await SetLastCallToNow();

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

        // Set last call to a stale time by sending a request then waiting conceptually
        // Instead, we create a handler with a timestamp in the past
        var staleHandler = new LastSuccessfulCallHandler
        {
            InnerHandler = new FakeOkHandler(),
        };
        using var staleClient = new HttpClient(staleHandler);
        await staleClient.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        // Now override staleness timeout to be very short
        var options = new HealthStatusOptions { StalenessTimeout = TimeSpan.FromMilliseconds(1) };
        await Task.Delay(10, TestContext.Current.CancellationToken);

        var collector = new HealthStatusCollector(
            _sessionApi, _tokenProvider, _wsClient, staleHandler, _rateLimiter, options);
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

        await SetLastCallToNow();

        var collector = CreateCollector();
        var result = await collector.GetHealthStatusAsync(
            activeProbe: false, cancellationToken: TestContext.Current.CancellationToken);

        result.OverallStatus.ShouldBe(HealthState.Degraded);
        result.Streaming.ShouldNotBeNull();
        result.Streaming!.IsConnected.ShouldBeFalse();
        result.Streaming.ActiveSubscriptions.ShouldBe(2);
    }

    private HealthStatusCollector CreateCollector() =>
        new(_sessionApi, _tokenProvider, _wsClient, _lastCallHandler, _rateLimiter, _options);

    private async Task SetLastCallToNow()
    {
        using var client = new HttpClient(_lastCallHandler, disposeHandler: false);
        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);
    }

    private sealed class FakeOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
