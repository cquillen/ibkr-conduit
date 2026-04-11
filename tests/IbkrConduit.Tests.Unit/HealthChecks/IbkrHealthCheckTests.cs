using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Health;
using IbkrConduit.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.HealthChecks;

public class IbkrHealthCheckTests
{
    private readonly IIbkrClient _client = Substitute.For<IIbkrClient>();
    private readonly IbkrHealthCheckOptions _options = new();

    [Fact]
    public async Task CheckHealthAsync_Healthy_ReturnsHealthy()
    {
        var status = CreateStatus(HealthState.Healthy);
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldContain("healthy");
        result.Data.ShouldContainKey("overallStatus");
        result.Data["session.authenticated"].ShouldBe(true);
    }

    [Fact]
    public async Task CheckHealthAsync_Degraded_ReturnsDegraded()
    {
        var status = CreateStatus(HealthState.Degraded);
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldContain("degraded");
    }

    [Fact]
    public async Task CheckHealthAsync_Unhealthy_ReturnsUnhealthy()
    {
        var status = CreateStatus(HealthState.Unhealthy);
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldContain("unhealthy");
    }

    [Fact]
    public async Task CheckHealthAsync_ActiveProbeOption_PassedToClient()
    {
        _options.ActiveProbe = true;
        var status = CreateStatus(HealthState.Healthy);
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        await _client.Received(1).GetHealthStatusAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_Exception_ReturnsUnhealthy()
    {
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldContain("exception");
        result.Exception.ShouldNotBeNull();
        result.Exception.Message.ShouldBe("Connection failed");
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesStreamingData_WhenPresent()
    {
        var status = new IbkrHealthStatus
        {
            OverallStatus = HealthState.Healthy,
            Session = new BrokerageSessionHealth(true, true, false, true, null),
            Streaming = new StreamingHealth(true, 3, DateTimeOffset.UtcNow.AddSeconds(-5)),
            Token = new OAuthTokenHealth(false, TimeSpan.FromHours(1)),
            RateLimiter = new RateLimiterHealth(10, 10, 0, 0),
            LastSuccessfulCall = DateTimeOffset.UtcNow,
            CheckedAt = DateTimeOffset.UtcNow,
        };
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Data.ShouldContainKey("streaming.isConnected");
        result.Data["streaming.isConnected"].ShouldBe(true);
        result.Data["streaming.activeSubscriptions"].ShouldBe(3);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesTokenExpiry_WhenPresent()
    {
        var status = CreateStatus(HealthState.Healthy);
        _client.GetHealthStatusAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(status);

        var check = CreateHealthCheck();
        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Data.ShouldContainKey("token.timeUntilExpiry");
    }

    private IbkrHealthCheck CreateHealthCheck() =>
        new(_client, Options.Create(_options));

    private static IbkrHealthStatus CreateStatus(HealthState state) =>
        new()
        {
            OverallStatus = state,
            Session = new BrokerageSessionHealth(true, true, false, true, null),
            Streaming = null,
            Token = new OAuthTokenHealth(false, TimeSpan.FromHours(1)),
            RateLimiter = new RateLimiterHealth(10, 10, 0, 0),
            LastSuccessfulCall = DateTimeOffset.UtcNow,
            CheckedAt = DateTimeOffset.UtcNow,
        };
}
