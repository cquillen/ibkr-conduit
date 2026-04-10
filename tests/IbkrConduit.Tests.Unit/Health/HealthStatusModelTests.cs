using System;
using IbkrConduit.Health;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Health;

public class HealthStatusModelTests
{
    [Fact]
    public void IbkrHealthStatus_CanBeConstructed()
    {
        var now = DateTimeOffset.UtcNow;

        var status = new IbkrHealthStatus
        {
            OverallStatus = HealthState.Healthy,
            Session = new BrokerageSessionHealth(true, true, false, true, null),
            Streaming = new StreamingHealth(true, 3, now),
            Token = new OAuthTokenHealth(false, TimeSpan.FromMinutes(10)),
            RateLimiter = new RateLimiterHealth(5, 8, 50.0, 20.0),
            LastSuccessfulCall = now,
            CheckedAt = now,
        };

        status.OverallStatus.ShouldBe(HealthState.Healthy);
        status.Session.Authenticated.ShouldBeTrue();
        status.Session.Connected.ShouldBeTrue();
        status.Session.Competing.ShouldBeFalse();
        status.Session.Established.ShouldBeTrue();
        status.Session.FailReason.ShouldBeNull();
        status.Streaming.ShouldNotBeNull();
        status.Streaming!.IsConnected.ShouldBeTrue();
        status.Streaming.ActiveSubscriptions.ShouldBe(3);
        status.Streaming.LastMessageAt.ShouldBe(now);
        status.Token.IsExpired.ShouldBeFalse();
        status.Token.TimeUntilExpiry.ShouldBe(TimeSpan.FromMinutes(10));
        status.RateLimiter.BurstRemaining.ShouldBe(5);
        status.RateLimiter.SustainedRemaining.ShouldBe(8);
        status.RateLimiter.BurstUtilizationPercent.ShouldBe(50.0);
        status.RateLimiter.SustainedUtilizationPercent.ShouldBe(20.0);
        status.LastSuccessfulCall.ShouldBe(now);
        status.CheckedAt.ShouldBe(now);
    }

    [Fact]
    public void IbkrHealthStatus_StreamingCanBeNull()
    {
        var now = DateTimeOffset.UtcNow;

        var status = new IbkrHealthStatus
        {
            OverallStatus = HealthState.Healthy,
            Session = new BrokerageSessionHealth(true, true, false, true, null),
            Streaming = null,
            Token = new OAuthTokenHealth(false, TimeSpan.FromMinutes(10)),
            RateLimiter = new RateLimiterHealth(10, 10, 0, 0),
            LastSuccessfulCall = null,
            CheckedAt = now,
        };

        status.Streaming.ShouldBeNull();
        status.LastSuccessfulCall.ShouldBeNull();
    }

    [Fact]
    public void HealthStatusOptions_HasSensibleDefaults()
    {
        var options = new HealthStatusOptions();

        options.TokenExpiryWarning.ShouldBe(TimeSpan.FromMinutes(5));
        options.RateLimiterThresholdPercent.ShouldBe(80);
        options.StalenessTimeout.ShouldBe(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void HealthState_HasExpectedValues()
    {
        HealthState.Healthy.ShouldBe((HealthState)0);
        HealthState.Degraded.ShouldBe((HealthState)1);
        HealthState.Unhealthy.ShouldBe((HealthState)2);
    }

    [Fact]
    public void BrokerageSessionHealth_WithFailReason()
    {
        var health = new BrokerageSessionHealth(false, false, false, false, "Auth failed");

        health.Authenticated.ShouldBeFalse();
        health.FailReason.ShouldBe("Auth failed");
    }

    [Fact]
    public void OAuthTokenHealth_WhenExpired()
    {
        var health = new OAuthTokenHealth(true, null);

        health.IsExpired.ShouldBeTrue();
        health.TimeUntilExpiry.ShouldBeNull();
    }
}
