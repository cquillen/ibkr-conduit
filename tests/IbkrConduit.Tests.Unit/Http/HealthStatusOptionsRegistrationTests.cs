using System;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Health;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

/// <summary>
/// Tests that <see cref="ServiceCollectionExtensions.AddIbkrClient"/> exposes the
/// health-status thresholds through the client options (PVR-07): defaults derive from
/// the tenant's <see cref="Session.IbkrClientOptions.TickleIntervalSeconds"/> and a
/// consumer-supplied hook overrides them.
/// </summary>
public class HealthStatusOptionsRegistrationTests
{
    [Theory]
    [InlineData(60, 120)]
    [InlineData(90, 180)]
    [InlineData(30, 60)]
    public void AddIbkrClient_HealthStatusNotConfigured_StalenessDerivesFromTickleInterval(
        int tickleIntervalSeconds, int expectedStalenessSeconds)
    {
        var provider = BuildProvider(opts =>
        {
            opts.Credentials = CreateTestCredentials();
            opts.TickleIntervalSeconds = tickleIntervalSeconds;
        });

        var health = provider.GetRequiredService<HealthStatusOptions>();

        health.StalenessTimeout.ShouldBe(TimeSpan.FromSeconds(expectedStalenessSeconds));
    }

    [Fact]
    public void AddIbkrClient_ConfigureHealthStatus_FlowsToRegisteredOptions()
    {
        var provider = BuildProvider(opts =>
        {
            opts.Credentials = CreateTestCredentials();
            opts.ConfigureHealthStatus = h =>
            {
                h.StalenessTimeout = TimeSpan.FromSeconds(300);
                h.TokenExpiryWarning = TimeSpan.FromMinutes(15);
                h.RateLimiterThresholdPercent = 50;
            };
        });

        var health = provider.GetRequiredService<HealthStatusOptions>();

        health.StalenessTimeout.ShouldBe(TimeSpan.FromSeconds(300));
        health.TokenExpiryWarning.ShouldBe(TimeSpan.FromMinutes(15));
        health.RateLimiterThresholdPercent.ShouldBe(50);
    }

    [Fact]
    public void AddIbkrClient_ConfigureHealthStatus_OverridesTickleDerivedStaleness()
    {
        // The hook runs after defaults are derived from the tickle interval, so an explicit
        // consumer value wins over the derivation.
        var provider = BuildProvider(opts =>
        {
            opts.Credentials = CreateTestCredentials();
            opts.TickleIntervalSeconds = 90; // would derive to 180s
            opts.ConfigureHealthStatus = h => h.StalenessTimeout = TimeSpan.FromSeconds(45);
        });

        var health = provider.GetRequiredService<HealthStatusOptions>();

        health.StalenessTimeout.ShouldBe(TimeSpan.FromSeconds(45));
    }

    private static ServiceProvider BuildProvider(Action<IbkrClientOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIbkrClient(configure);
        return services.BuildServiceProvider();
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = RSA.Create(2048);
        var encKey = RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new BigInteger(23));
    }
}
