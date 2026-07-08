using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// Thrown when the Live Session Token derived from the OAuth handshake fails validation: the
/// locally computed <c>HMAC-SHA1(key=LST, data=consumerKey)</c> digest does not match the signature
/// IBKR returned. A subtype of <see cref="CryptographicException"/> so existing callers that catch
/// <see cref="CryptographicException"/> continue to work, while the error classifier can recognise
/// the validation failure by type and name the credential fields actually implicated
/// (<c>ConsumerKey</c>, <c>EncryptionPrivateKey</c>/<c>EncryptedAccessTokenSecret</c>, and
/// <c>DhPrime</c>) rather than the signing key, which only signs the LST <em>request</em>.
/// </summary>
internal sealed class LiveSessionTokenValidationException : CryptographicException
{
    /// <summary>
    /// Creates a new <see cref="LiveSessionTokenValidationException"/> with the given message.
    /// </summary>
    /// <param name="message">A description of the validation failure.</param>
    public LiveSessionTokenValidationException(string message)
        : base(message)
    {
    }
}
