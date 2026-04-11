using System;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

/// <summary>
/// Tests that <see cref="ServiceCollectionExtensions.AddIbkrClient"/> validates
/// <see cref="IbkrClientOptions"/> fields at registration time.
/// </summary>
public class ServiceCollectionExtensionsValidationTests
{
    [Fact]
    public void AddIbkrClient_DefaultOptionsWithCredentials_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        Should.NotThrow(() => services.AddIbkrClient(opts => opts.Credentials = creds));
    }

    [Fact]
    public void AddIbkrClient_NullCredentials_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<ArgumentNullException>(
            () => services.AddIbkrClient(opts => { }));

        ex.ParamName.ShouldBe("IbkrClientOptions.Credentials");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void AddIbkrClient_InvalidTickleIntervalSeconds_ThrowsArgumentOutOfRangeException(int value)
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.TickleIntervalSeconds = value;
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.TickleIntervalSeconds");
        ex.ActualValue.ShouldBe(value);
    }

    [Fact]
    public void AddIbkrClient_ZeroPreflightCacheDuration_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.PreflightCacheDuration = TimeSpan.Zero;
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.PreflightCacheDuration");
    }

    [Fact]
    public void AddIbkrClient_NegativePreflightCacheDuration_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.PreflightCacheDuration = TimeSpan.FromSeconds(-1);
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.PreflightCacheDuration");
    }

    [Fact]
    public void AddIbkrClient_ZeroProactiveRefreshMargin_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.ProactiveRefreshMargin = TimeSpan.Zero;
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.ProactiveRefreshMargin");
    }

    [Fact]
    public void AddIbkrClient_NegativeProactiveRefreshMargin_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.ProactiveRefreshMargin = TimeSpan.FromMinutes(-5);
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.ProactiveRefreshMargin");
    }

    [Fact]
    public void AddIbkrClient_ZeroFlexPollTimeout_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.FlexPollTimeout = TimeSpan.Zero;
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.FlexPollTimeout");
    }

    [Fact]
    public void AddIbkrClient_NegativeFlexPollTimeout_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.FlexPollTimeout = TimeSpan.FromSeconds(-10);
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.FlexPollTimeout");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("relative/path")]
    [InlineData("://missing-scheme")]
    public void AddIbkrClient_InvalidBaseUrl_ThrowsArgumentException(string badUrl)
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        var ex = Should.Throw<ArgumentException>(
            () => services.AddIbkrClient(opts =>
            {
                opts.Credentials = creds;
                opts.BaseUrl = badUrl;
            }));

        ex.ParamName.ShouldBe("IbkrClientOptions.BaseUrl");
    }

    [Fact]
    public void AddIbkrClient_NullBaseUrl_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        Should.NotThrow(() => services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.BaseUrl = null;
        }));
    }

    [Theory]
    [InlineData("https://api.ibkr.com")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://example.com/api")]
    public void AddIbkrClient_ValidAbsoluteBaseUrl_DoesNotThrow(string validUrl)
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        Should.NotThrow(() => services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.BaseUrl = validUrl;
        }));
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
