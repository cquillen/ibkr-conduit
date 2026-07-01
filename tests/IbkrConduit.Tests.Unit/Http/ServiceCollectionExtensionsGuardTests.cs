using System;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class ServiceCollectionExtensionsGuardTests
{
    [Fact]
    public void AddIbkrClient_CalledTwice_Throws()
    {
        using var creds = CreateTestCredentials();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(o => o.Credentials = creds);

        var ex = Should.Throw<InvalidOperationException>(
            () => services.AddIbkrClient(o => o.Credentials = creds));

        ex.Message.ShouldContain("IIbkrClientManager");
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
