using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using IbkrConduit.Auth;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthCredentialsFactoryFromFileTests
{
    [Fact]
    public void FromFile_ValidJson_ReturnsCredentials()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);
        var sigPem = sigKey.ExportRSAPrivateKeyPem();
        var encPem = encKey.ExportRSAPrivateKeyPem();
        var dhPrimeHex = "11"; // 0x11 = 17 decimal

        var json = JsonSerializer.Serialize(new
        {
            consumerKey = "TESTCONS9",
            accessToken = "mytoken",
            accessTokenSecret = "mysecret",
            signaturePrivateKey = sigPem,
            encryptionPrivateKey = encPem,
            dhPrime = dhPrimeHex,
        });

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);

            using var creds = OAuthCredentialsFactory.FromFile(path);

            creds.ConsumerKey.ShouldBe("TESTCONS9");
            creds.AccessToken.ShouldBe("mytoken");
            creds.EncryptedAccessTokenSecret.ShouldBe("mysecret");
            creds.TenantId.ShouldBe("TESTCONS9");

            // Verify the loaded signature key can sign and verify
            var data = System.Text.Encoding.UTF8.GetBytes("test-payload");
            var signature = creds.SignaturePrivateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            sigKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).ShouldBeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFile_MissingField_ThrowsConfigurationException()
    {
        var json = JsonSerializer.Serialize(new
        {
            // consumerKey is intentionally missing
            accessToken = "mytoken",
            accessTokenSecret = "mysecret",
            signaturePrivateKey = "placeholder",
            encryptionPrivateKey = "placeholder",
            dhPrime = "11",
        });

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);

            var ex = Should.Throw<IbkrConfigurationException>(() => OAuthCredentialsFactory.FromFile(path));
            ex.Message.ShouldContain("consumerKey");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFile_InvalidPem_ThrowsConfigurationException()
    {
        var json = JsonSerializer.Serialize(new
        {
            consumerKey = "TESTCONS9",
            accessToken = "mytoken",
            accessTokenSecret = "mysecret",
            signaturePrivateKey = "NOT-A-VALID-PEM-KEY",
            encryptionPrivateKey = "NOT-A-VALID-PEM-KEY",
            dhPrime = "11",
        });

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);

            Should.Throw<IbkrConfigurationException>(() => OAuthCredentialsFactory.FromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFile_FileNotFound_Throws()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        Should.Throw<FileNotFoundException>(() => OAuthCredentialsFactory.FromFile(nonExistentPath));
    }
}
