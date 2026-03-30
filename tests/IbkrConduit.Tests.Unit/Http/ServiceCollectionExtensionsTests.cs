using System;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIbkrClient_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        services.AddIbkrClient<IIbkrPortfolioApi>(creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
        provider.GetService<ILiveSessionTokenClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_WithOptions_RegistersSessionManager()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();
        var options = new IbkrClientOptions { Compete = false };

        services.AddIbkrClient<IIbkrPortfolioApi>(creds, options);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_ResolvesRefitClient()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        services.AddIbkrClient<IIbkrPortfolioApi>(creds);

        var provider = services.BuildServiceProvider();

        var api = provider.GetService<IIbkrPortfolioApi>();
        api.ShouldNotBeNull();
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
