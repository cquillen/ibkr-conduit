using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class BuildTenantServicesTests
{
    [Fact]
    public async Task BuildTenantServices_ResolvesIIbkrClient()
    {
        using var creds = CreateTestCredentials();
        var options = new IbkrClientOptions { Credentials = creds, BaseUrl = "https://api.test" };
        var services = new ServiceCollection();
        services.AddLogging();

        ServiceCollectionExtensions.BuildTenantServices(services, creds, options, options.BaseUrl!);

        await using var provider = services.BuildServiceProvider();
        provider.GetService<IIbkrClient>().ShouldNotBeNull();
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
