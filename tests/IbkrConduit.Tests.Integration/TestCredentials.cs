using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Generates synthetic OAuth credentials for integration testing.
/// All keys are ephemeral — generated fresh per test run. No real secrets.
/// </summary>
public static class TestCredentials
{
    /// <summary>The test consumer key.</summary>
    public const string ConsumerKey = "TESTKEY01";

    /// <summary>The test access token.</summary>
    public const string AccessToken = "test_access_token";

    /// <summary>
    /// The raw access token secret bytes (before encryption).
    /// This is the shared secret that both client and server know.
    /// </summary>
    public static readonly byte[] RawAccessTokenSecret = Encoding.UTF8.GetBytes("test_secret_1234");

    /// <summary>
    /// Creates a complete set of synthetic credentials using freshly generated RSA keys
    /// and a small DH prime suitable for testing.
    /// </summary>
    public static IbkrOAuthCredentials Create()
    {
        // Generate RSA keys for signing and encryption
        var signatureKey = RSA.Create(2048);
        var encryptionKey = RSA.Create(2048);

        // Encrypt the access token secret with the encryption public key
        // (client will decrypt with the private key during LST acquisition)
        var encryptedSecret = encryptionKey.Encrypt(RawAccessTokenSecret, RSAEncryptionPadding.Pkcs1);
        var encryptedSecretB64 = Convert.ToBase64String(encryptedSecret);

        // Use a well-known small-ish DH prime for testing (RFC 3526 Group 14, 2048-bit)
        // In production this comes from the IBKR portal
        var dhPrime = BigInteger.Parse(
            "0" +
            "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD1" +
            "29024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
            "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
            "E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
            "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3D" +
            "C2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F" +
            "83655D23DCA3AD961C62F356208552BB9ED529077096966D" +
            "670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
            "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9" +
            "DE2BCBF6955817183995497CEA956AE515D2261898FA0510" +
            "15728E5A8AACAA68FFFFFFFFFFFFFFFF",
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);

        return new IbkrOAuthCredentials(
            TenantId: "test-tenant",
            ConsumerKey: ConsumerKey,
            AccessToken: AccessToken,
            EncryptedAccessTokenSecret: encryptedSecretB64,
            SignaturePrivateKey: signatureKey,
            EncryptionPrivateKey: encryptionKey,
            DhPrime: dhPrime);
    }
}
