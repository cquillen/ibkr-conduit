using System.Numerics;
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// Pure cryptographic operations for the IBKR OAuth 1.0a protocol.
/// Uses System.Security.Cryptography exclusively — no external crypto libraries.
/// </summary>
public static class OAuthCrypto
{
    private const int _dhGenerator = 2;

    /// <summary>
    /// Converts a BigInteger to a big-endian two's complement byte array,
    /// matching Java's BigInteger.toByteArray() behavior.
    /// </summary>
    public static byte[] BigIntegerToByteArray(BigInteger value)
    {
        var le = value.ToByteArray();
        Array.Reverse(le);
        return le;
    }

    /// <summary>
    /// Decrypts the base64-encoded RSA-encrypted access token secret.
    /// Tries PKCS#1 v1.5 first (ibind's wire format), falls back to OAEP SHA-256.
    /// </summary>
    public static byte[] DecryptAccessTokenSecret(RSA encryptionKey, string encryptedSecret)
    {
        var ciphertext = Convert.FromBase64String(encryptedSecret);

        try
        {
            return encryptionKey.Decrypt(ciphertext, RSAEncryptionPadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return encryptionKey.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
        }
    }

    /// <summary>
    /// Generates a Diffie-Hellman key pair: random 256-bit private key and computed public key.
    /// </summary>
    public static (BigInteger PrivateKey, BigInteger PublicKey) GenerateDhKeyPair(BigInteger prime)
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);

        var positiveBytes = new byte[randomBytes.Length + 1];
        randomBytes.CopyTo(positiveBytes, 0);
        positiveBytes[^1] = 0;

        var privateKey = new BigInteger(positiveBytes);
        var publicKey = BigInteger.ModPow(_dhGenerator, privateKey, prime);

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Computes the DH shared secret K = theirPublicKey^myPrivateKey mod prime,
    /// returned as a big-endian two's complement byte array.
    /// </summary>
    public static byte[] DeriveDhSharedSecret(
        BigInteger theirPublicKey, BigInteger myPrivateKey, BigInteger prime)
    {
        var sharedSecret = BigInteger.ModPow(theirPublicKey, myPrivateKey, prime);
        return BigIntegerToByteArray(sharedSecret);
    }

    /// <summary>
    /// Derives the Live Session Token via HMAC-SHA1(key=dhSharedSecret, data=decryptedAccessTokenSecret).
    /// </summary>
#pragma warning disable CA5350 // IBKR OAuth 1.0a protocol mandates HMAC-SHA1 — algorithm is not a choice here
    public static byte[] DeriveLiveSessionToken(byte[] dhSharedSecret, byte[] decryptedAccessTokenSecret)
    {
        using var hmac = new HMACSHA1(dhSharedSecret);
        return hmac.ComputeHash(decryptedAccessTokenSecret);
    }
#pragma warning restore CA5350

    /// <summary>
    /// Validates the LST by computing HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))
    /// and comparing the lowercase hex digest to the expected signature.
    /// </summary>
#pragma warning disable CA5350 // IBKR OAuth 1.0a protocol mandates HMAC-SHA1 — algorithm is not a choice here
    public static bool ValidateLiveSessionToken(
        byte[] lstBytes, string consumerKey, string expectedSignatureHex)
    {
        using var hmac = new HMACSHA1(lstBytes);
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(consumerKey));
        var computedHex = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(computedHex, expectedSignatureHex, StringComparison.Ordinal);
    }
#pragma warning restore CA5350
}
