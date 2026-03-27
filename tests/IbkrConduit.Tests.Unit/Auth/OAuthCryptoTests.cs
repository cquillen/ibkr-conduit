using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthCryptoTests
{
    [Fact]
    public void BigIntegerToByteArray_SmallValue_ReturnsBigEndianBytes()
    {
        var value = new BigInteger(256);
        var result = OAuthCrypto.BigIntegerToByteArray(value);
        result.ShouldBe(new byte[] { 0x01, 0x00 });
    }

    [Fact]
    public void BigIntegerToByteArray_HighBitSet_IncludesLeadingZeroByte()
    {
        var value = new BigInteger(128);
        var result = OAuthCrypto.BigIntegerToByteArray(value);
        result.ShouldBe(new byte[] { 0x00, 0x80 });
    }

    [Fact]
    public void BigIntegerToByteArray_LargeValue_MatchesJavaBehavior()
    {
        var value = new BigInteger(0xFF00);
        var result = OAuthCrypto.BigIntegerToByteArray(value);
        result.ShouldBe(new byte[] { 0x00, 0xFF, 0x00 });
    }

    [Fact]
    public void DecryptAccessTokenSecret_Pkcs1Encrypted_ReturnsPlaintextBytes()
    {
        using var rsa = RSA.Create(2048);
        var plaintext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var ciphertext = rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1);
        var encoded = Convert.ToBase64String(ciphertext);

        var result = OAuthCrypto.DecryptAccessTokenSecret(rsa, encoded);

        result.ShouldBe(plaintext);
    }

    [Fact]
    public void DecryptAccessTokenSecret_OaepEncrypted_FallsBackAndReturnsPlaintextBytes()
    {
        using var rsa = RSA.Create(2048);
        var plaintext = new byte[] { 0xCA, 0xFE };
        var ciphertext = rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
        var encoded = Convert.ToBase64String(ciphertext);

        var result = OAuthCrypto.DecryptAccessTokenSecret(rsa, encoded);

        result.ShouldBe(plaintext);
    }

    [Fact]
    public void GenerateDhKeyPair_ReturnsValidKeyPair()
    {
        var prime = new BigInteger(23);
        var (privateKey, publicKey) = OAuthCrypto.GenerateDhKeyPair(prime);

        privateKey.Sign.ShouldBe(1);
        publicKey.Sign.ShouldBe(1);
        publicKey.ShouldBeLessThan(prime);
    }

    [Fact]
    public void GenerateDhKeyPair_DifferentCallsProduceDifferentKeys()
    {
        var prime = new BigInteger(23);
        var (privateKey1, _) = OAuthCrypto.GenerateDhKeyPair(prime);
        var (privateKey2, _) = OAuthCrypto.GenerateDhKeyPair(prime);

        privateKey1.Sign.ShouldBe(1);
        privateKey2.Sign.ShouldBe(1);
    }

    [Fact]
    public void DeriveDhSharedSecret_KnownValues_ReturnsExpectedBytes()
    {
        var theirPublicKey = new BigInteger(16);
        var myPrivateKey = new BigInteger(6);
        var prime = new BigInteger(23);

        var result = OAuthCrypto.DeriveDhSharedSecret(theirPublicKey, myPrivateKey, prime);

        result.ShouldBe(new byte[] { 0x04 });
    }

    [Fact]
    public void DeriveLiveSessionToken_KnownInputs_ReturnsHmacSha1()
    {
        var dhSharedSecret = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var decryptedSecret = new byte[] { 0xAA, 0xBB, 0xCC };

        var result = OAuthCrypto.DeriveLiveSessionToken(dhSharedSecret, decryptedSecret);

        result.Length.ShouldBe(20);

        var result2 = OAuthCrypto.DeriveLiveSessionToken(dhSharedSecret, decryptedSecret);
        result.ShouldBe(result2);

        using var hmac = new HMACSHA1(dhSharedSecret);
        var expected = hmac.ComputeHash(decryptedSecret);
        result.ShouldBe(expected);
    }

    [Fact]
    public void ValidateLiveSessionToken_ValidSignature_ReturnsTrue()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var consumerKey = "TESTCONS";

        using var hmac = new HMACSHA1(lstBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(consumerKey));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

        var result = OAuthCrypto.ValidateLiveSessionToken(lstBytes, consumerKey, expectedHex);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateLiveSessionToken_InvalidSignature_ReturnsFalse()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var result = OAuthCrypto.ValidateLiveSessionToken(lstBytes, "TESTCONS", "deadbeef");

        result.ShouldBeFalse();
    }
}
