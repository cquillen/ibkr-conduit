using System.Numerics;
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// Holds all OAuth credentials needed to authenticate with the IBKR API.
/// Storage-agnostic: accepts pre-loaded cryptographic objects, not file paths.
/// </summary>
/// <param name="TenantId">Unique identifier for the tenant.</param>
/// <param name="ConsumerKey">IBKR consumer key (9 characters).</param>
/// <param name="AccessToken">OAuth access token from IBKR portal.</param>
/// <param name="EncryptedAccessTokenSecret">Base64-encoded RSA-encrypted access token secret.</param>
/// <param name="SignaturePrivateKey">RSA private key for signing LST requests.</param>
/// <param name="EncryptionPrivateKey">RSA private key for decrypting the access token secret.</param>
/// <param name="DhPrime">Diffie-Hellman 2048-bit prime.</param>
public record IbkrOAuthCredentials(
    string TenantId,
    string ConsumerKey,
    string AccessToken,
    string EncryptedAccessTokenSecret,
    RSA SignaturePrivateKey,
    RSA EncryptionPrivateKey,
    BigInteger DhPrime) : IDisposable
{
    /// <summary>
    /// Disposes both RSA key objects.
    /// </summary>
    public void Dispose()
    {
        SignaturePrivateKey.Dispose();
        EncryptionPrivateKey.Dispose();
        GC.SuppressFinalize(this);
    }
}
