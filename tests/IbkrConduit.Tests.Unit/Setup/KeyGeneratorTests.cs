using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
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
    public void EncodeDhParametersPem_HasCorrectPemArmor()
    {
        var pem = KeyGenerator.EncodeDhParametersPem();

        pem.ShouldStartWith("-----BEGIN DH PARAMETERS-----");
        pem.ShouldEndWith("-----END DH PARAMETERS-----");
    }

    [Fact]
    public void EncodeDhParametersDer_IsValidAsn1Sequence()
    {
        var prime = new BigInteger(23);
        var der = KeyGenerator.EncodeDhParametersDer(prime, 2);

        // Parse the DER back — should be a SEQUENCE with two INTEGERs
        var reader = new AsnReader(der, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        var parsedPrime = sequence.ReadInteger();
        var parsedGenerator = sequence.ReadInteger();
        sequence.ThrowIfNotEmpty();
        reader.ThrowIfNotEmpty();

        parsedPrime.ShouldBe(new BigInteger(23));
        parsedGenerator.ShouldBe(new BigInteger(2));
    }

    [Fact]
    public void EncodeDhParametersDer_Rfc3526Prime_ProducesCorrectLength()
    {
        var prime = BigInteger.Parse(
            "0" + KeyGenerator.Rfc3526Group14PrimeHex,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);

        var der = KeyGenerator.EncodeDhParametersDer(prime);

        // 2048-bit prime = 256 bytes. DER overhead: SEQUENCE(4) + INTEGER tag+len(4) + 257 bytes (with leading 0) + INTEGER(3) for generator 2
        // Total should be around 268 bytes
        der.Length.ShouldBeGreaterThan(260);
        der.Length.ShouldBeLessThan(280);
    }

    [Fact]
    public void Rfc3526Group14PrimeHex_IsCorrectLength()
    {
        // 2048-bit prime = 256 bytes = 512 hex chars
        KeyGenerator.Rfc3526Group14PrimeHex.Length.ShouldBe(512);
    }
}
