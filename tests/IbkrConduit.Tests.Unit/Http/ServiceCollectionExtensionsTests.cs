using System;
using System.Numerics;
using System.Security.Cryptography;
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

        services.AddIbkrClient(creds);

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
        var options = new IbkrClientOptions { Compete = false };

        services.AddIbkrClient(creds, options);

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

        services.AddIbkrClient(creds);

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

        services.AddIbkrClient(creds);

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

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = RSA.Create(2048);
        var encKey = RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new BigInteger(23));
    }
}
