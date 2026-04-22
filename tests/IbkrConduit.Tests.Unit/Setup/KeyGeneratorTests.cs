using System;
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

    [Fact]
    [Trait("Category", "Slow")]
    public void GenerateDhParameters_PemPrimeMatchesPrimeHex_WhenParsedAsSignedDerInteger()
    {
        // Regression test: DH parameter P must be DER-encoded as a POSITIVE signed integer.
        // For a 2048-bit prime the high bit of byte 0 is always set, so a compliant DER
        // encoding must prepend a 0x00 sign byte. If it doesn't, strict DER parsers
        // (including IBKR's) read P as a negative (or otherwise different) value, causing
        // the DH handshake to produce an LST that doesn't match the server — every
        // authenticated call then returns 401.
        var result = KeyGenerator.GenerateDhParameters(certainty: 2);

        // Extract the raw PEM body and decode
        var pemBody = result.Pem
            .Replace("-----BEGIN DH PARAMETERS-----", string.Empty)
            .Replace("-----END DH PARAMETERS-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
        var der = Convert.FromBase64String(pemBody);

        // Parse the DER SEQUENCE { INTEGER p, INTEGER g } and read p as a signed BigInteger
        var reader = new AsnReader(der, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        var derPrime = sequence.ReadInteger();

        // Expected value comes from the hex that gets written into the credential JSON
        // and used at runtime — leading "0" forces .NET BigInteger.Parse to treat it
        // as positive (the same trick used in OAuthCredentialsFactory).
        var expected = BigInteger.Parse("0" + result.PrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        derPrime.Sign.ShouldBe(1, "DER INTEGER for prime P must be positive");
        derPrime.ShouldBe(expected, "PEM-encoded prime must equal the hex stored in the credential JSON");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void GenerateDhParameters_UsesGeneratorTwo()
    {
        // The runtime code (OAuthCrypto._dhGenerator) hardcodes G=2.
        // IBKR's server also uses G=2. The generated DH PEM must match.
        var result = KeyGenerator.GenerateDhParameters(certainty: 2);

        var pemBody = result.Pem
            .Replace("-----BEGIN DH PARAMETERS-----", string.Empty)
            .Replace("-----END DH PARAMETERS-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
        var der = Convert.FromBase64String(pemBody);

        var reader = new AsnReader(der, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        _ = sequence.ReadInteger(); // skip P
        var g = sequence.ReadInteger();

        g.ShouldBe(new BigInteger(2), "DH generator G must be 2 to match runtime OAuthCrypto and IBKR server");
    }
}
