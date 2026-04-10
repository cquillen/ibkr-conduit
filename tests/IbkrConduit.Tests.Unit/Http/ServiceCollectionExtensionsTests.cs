using System;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Http;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIbkrClient_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
        provider.GetService<ILiveSessionTokenClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_WithOptions_RegistersSessionManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.Compete = false;
        });

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_ResolvesRefitClients()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<IIbkrPortfolioApi>().ShouldNotBeNull();
        provider.GetService<IIbkrContractApi>().ShouldNotBeNull();
        provider.GetService<IIbkrOrderApi>().ShouldNotBeNull();
        provider.GetService<IIbkrMarketDataApi>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_ResolvesFacade()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        var client = provider.GetService<IIbkrClient>();
        client.ShouldNotBeNull();
        client.Portfolio.ShouldNotBeNull();
        client.Contracts.ShouldNotBeNull();
        client.Orders.ShouldNotBeNull();
        client.MarketData.ShouldNotBeNull();
        client.Streaming.ShouldNotBeNull();
        client.Flex.ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_WithFlexToken_ResolvesFlexOperations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.FlexToken = "TEST_TOKEN";
        });

        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IIbkrClient>();
        client.Flex.ShouldNotBeNull();

        // Verify the named HttpClient resolves (pipeline wired correctly)
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var flexClient = factory.CreateClient($"IbkrConduit-Flex-{creds.TenantId}");
        flexClient.ShouldNotBeNull();
    }

    [Fact]
    public async Task FlexHttpClient_HasRateLimitingHandlers_SecondRequestDelayed()
    {
        // Verifies the Flex HttpClient pipeline includes rate limiting
        // by measuring that two rapid requests are spaced apart by the burst gate (1 req/sec).
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.FlexToken = "TEST_TOKEN";
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var flexClient = factory.CreateClient($"IbkrConduit-Flex-{creds.TenantId}");

        // First request — consumes the single burst token (HTTP failure is expected)
        var ct = TestContext.Current.CancellationToken;
        var sw = Stopwatch.StartNew();
        try
        {
            await flexClient.GetAsync("http://localhost:1/test1", ct);
        }
        catch (HttpRequestException)
        {
            // Expected — no server listening
        }

        var firstDone = sw.ElapsedMilliseconds;

        // Second request — burst limiter should enforce ~1 second delay
        try
        {
            await flexClient.GetAsync("http://localhost:1/test2", ct);
        }
        catch (HttpRequestException)
        {
            // Expected — no server listening
        }

        var secondDone = sw.ElapsedMilliseconds;

        // The burst limiter gates at 1 req/sec, so the second request should wait ~1 second
        (secondDone - firstDone).ShouldBeGreaterThan(900);
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
