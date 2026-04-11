using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using IbkrConduit.Setup;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Setup;

public class CredentialFileTests : IDisposable
{
    private readonly string _tempDir;

    public CredentialFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ibkr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void Write_CreatesValidJsonFile()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);
        var filePath = Path.Combine(_tempDir, "creds.json");

        CredentialFile.Write(
            filePath,
            consumerKey: "XKVMTQWLR",
            accessToken: "mytoken123",
            accessTokenSecret: "c2VjcmV0",
            signaturePrivateKeyPem: sigKey.ExportRSAPrivateKeyPem(),
            encryptionPrivateKeyPem: encKey.ExportRSAPrivateKeyPem(),
            dhPrimeHex: KeyGenerator.Rfc3526Group14PrimeHex);

        File.Exists(filePath).ShouldBeTrue();

        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("consumerKey").GetString().ShouldBe("XKVMTQWLR");
        root.GetProperty("accessToken").GetString().ShouldBe("mytoken123");
        root.GetProperty("accessTokenSecret").GetString().ShouldBe("c2VjcmV0");
        root.GetProperty("signaturePrivateKey").GetString().ShouldStartWith("-----BEGIN RSA PRIVATE KEY-----");
        root.GetProperty("encryptionPrivateKey").GetString().ShouldStartWith("-----BEGIN RSA PRIVATE KEY-----");
        root.GetProperty("dhPrime").GetString().ShouldBe(KeyGenerator.Rfc3526Group14PrimeHex);
    }

    [Fact]
    public void Read_RoundTrips()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);
        var filePath = Path.Combine(_tempDir, "creds.json");
        var sigPem = sigKey.ExportRSAPrivateKeyPem();
        var encPem = encKey.ExportRSAPrivateKeyPem();

        CredentialFile.Write(filePath, "XKVMTQWLR", "mytoken123", "c2VjcmV0", sigPem, encPem, "17");

        var result = CredentialFile.Read(filePath);

        result.ConsumerKey.ShouldBe("XKVMTQWLR");
        result.AccessToken.ShouldBe("mytoken123");
        result.AccessTokenSecret.ShouldBe("c2VjcmV0");
        result.SignaturePrivateKeyPem.ShouldBe(sigPem);
        result.EncryptionPrivateKeyPem.ShouldBe(encPem);
        result.DhPrimeHex.ShouldBe("17");
    }

    [Fact]
    public void Read_MissingFile_Throws()
    {
        var filePath = Path.Combine(_tempDir, "nonexistent.json");

        Should.Throw<FileNotFoundException>(() => CredentialFile.Read(filePath));
    }

    [Fact]
    public void Read_MissingField_Throws()
    {
        var filePath = Path.Combine(_tempDir, "partial.json");
        File.WriteAllText(filePath, """{"consumerKey": "XKVMTQWLR"}""");

        var ex = Should.Throw<InvalidOperationException>(() => CredentialFile.Read(filePath));
        ex.Message.ShouldContain("accessToken");
    }
}
