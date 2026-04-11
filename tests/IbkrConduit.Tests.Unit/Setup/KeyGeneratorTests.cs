using System.Security.Cryptography;
using IbkrConduit.Setup;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Setup;

public class KeyGeneratorTests
{
    [Fact]
    public void GenerateRsaKeyPair_ReturnsValidKeyPair()
    {
        var result = KeyGenerator.GenerateRsaKeyPair();

        result.PrivatePem.ShouldStartWith("-----BEGIN RSA PRIVATE KEY-----");
        result.PublicPem.ShouldStartWith("-----BEGIN PUBLIC KEY-----");
    }

    [Fact]
    public void GenerateRsaKeyPair_PublicKeyMatchesPrivateKey()
    {
        var result = KeyGenerator.GenerateRsaKeyPair();

        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(result.PrivatePem);

        using var publicKey = RSA.Create();
        publicKey.ImportFromPem(result.PublicPem);

        // Sign with private, verify with public
        var data = "test data"u8.ToArray();
        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var isValid = publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public void GenerateRsaKeyPair_KeyIs2048Bits()
    {
        var result = KeyGenerator.GenerateRsaKeyPair();

        using var rsa = RSA.Create();
        rsa.ImportFromPem(result.PrivatePem);

        rsa.KeySize.ShouldBe(2048);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void GenerateDhParameters_PemHasCorrectArmor()
    {
        var result = KeyGenerator.GenerateDhParameters(certainty: 2);

        result.Pem.ShouldStartWith("-----BEGIN DH PARAMETERS-----");
        result.Pem.ShouldEndWith("-----END DH PARAMETERS-----");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void GenerateDhParameters_PrimeHexIsValidHexString()
    {
        var result = KeyGenerator.GenerateDhParameters(certainty: 2);

        result.PrimeHex.ShouldNotBeNullOrEmpty();
        // Verify every character is a valid uppercase hex digit
        result.PrimeHex.ShouldAllBe(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
    }
}
