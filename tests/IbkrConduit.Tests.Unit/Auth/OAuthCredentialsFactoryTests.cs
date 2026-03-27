using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthCredentialsFactoryTests
{
    [Fact]
    public void IbkrOAuthCredentials_ShouldExposeAllProperties()
    {
        using var signatureKey = RSA.Create(2048);
        using var encryptionKey = RSA.Create(2048);
        var prime = BigInteger.Parse("023", System.Globalization.NumberStyles.HexNumber);

        using var creds = new IbkrOAuthCredentials(
            TenantId: "tenant1",
            ConsumerKey: "TESTCONS",
            AccessToken: "token123",
            EncryptedAccessTokenSecret: "c2VjcmV0",
            SignaturePrivateKey: signatureKey,
            EncryptionPrivateKey: encryptionKey,
            DhPrime: prime);

        creds.TenantId.ShouldBe("tenant1");
        creds.ConsumerKey.ShouldBe("TESTCONS");
        creds.AccessToken.ShouldBe("token123");
        creds.EncryptedAccessTokenSecret.ShouldBe("c2VjcmV0");
        creds.SignaturePrivateKey.ShouldBe(signatureKey);
        creds.EncryptionPrivateKey.ShouldBe(encryptionKey);
        creds.DhPrime.ShouldBe(prime);
    }

    [Fact]
    public void Dispose_ShouldDisposeRsaKeys()
    {
        var signatureKey = RSA.Create(2048);
        var encryptionKey = RSA.Create(2048);
        var prime = BigInteger.Parse("023", System.Globalization.NumberStyles.HexNumber);

        var creds = new IbkrOAuthCredentials(
            "tenant1", "TESTCONS", "token123", "c2VjcmV0",
            signatureKey, encryptionKey, prime);

        creds.Dispose();

        Should.Throw<ObjectDisposedException>(() => signatureKey.ExportParameters(false));
        Should.Throw<ObjectDisposedException>(() => encryptionKey.ExportParameters(false));
    }

    [Fact]
    public void FromEnvironment_AllVarsSet_ReturnsCredentials()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);
        var sigPem = sigKey.ExportRSAPrivateKeyPem();
        var encPem = encKey.ExportRSAPrivateKeyPem();

        var vars = new Dictionary<string, string>
        {
            ["IBKR_CONSUMER_KEY"] = "TESTCONS9",
            ["IBKR_ACCESS_TOKEN"] = "mytoken",
            ["IBKR_ACCESS_TOKEN_SECRET"] = "c2VjcmV0",
            ["IBKR_SIGNATURE_KEY"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(sigPem)),
            ["IBKR_ENCRYPTION_KEY"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(encPem)),
            ["IBKR_DH_PRIME"] = "17",
        };

        try
        {
            foreach (var (key, value) in vars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            using var creds = OAuthCredentialsFactory.FromEnvironment();

            creds.ConsumerKey.ShouldBe("TESTCONS9");
            creds.AccessToken.ShouldBe("mytoken");
            creds.EncryptedAccessTokenSecret.ShouldBe("c2VjcmV0");
            creds.TenantId.ShouldBe("TESTCONS9");
            creds.DhPrime.ShouldBe(new BigInteger(0x17));
        }
        finally
        {
            foreach (var key in vars.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }

    [Fact]
    public void FromEnvironment_WithTenantId_UsesTenantId()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);
        var sigPem = sigKey.ExportRSAPrivateKeyPem();
        var encPem = encKey.ExportRSAPrivateKeyPem();

        var vars = new Dictionary<string, string>
        {
            ["IBKR_CONSUMER_KEY"] = "TESTCONS9",
            ["IBKR_ACCESS_TOKEN"] = "mytoken",
            ["IBKR_ACCESS_TOKEN_SECRET"] = "c2VjcmV0",
            ["IBKR_SIGNATURE_KEY"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(sigPem)),
            ["IBKR_ENCRYPTION_KEY"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(encPem)),
            ["IBKR_DH_PRIME"] = "17",
            ["IBKR_TENANT_ID"] = "custom-tenant",
        };

        try
        {
            foreach (var (key, value) in vars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            using var creds = OAuthCredentialsFactory.FromEnvironment();

            creds.TenantId.ShouldBe("custom-tenant");
        }
        finally
        {
            foreach (var key in vars.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }

    [Fact]
    public void FromEnvironment_MissingRequired_ThrowsInvalidOperation()
    {
        Environment.SetEnvironmentVariable("IBKR_CONSUMER_KEY", null);

        Should.Throw<InvalidOperationException>(() => OAuthCredentialsFactory.FromEnvironment());
    }
}
