namespace IbkrConduit.Auth;

/// <summary>
/// Strategy interface for OAuth signature computation.
/// Returns base64-encoded signature (no percent-encoding — the caller handles that).
/// </summary>
public interface IOAuthSigner
{
    /// <summary>
    /// The OAuth signature method identifier (e.g., "RSA-SHA256" or "HMAC-SHA256").
    /// </summary>
    string SignatureMethod { get; }

    /// <summary>
    /// Signs the given base string and returns a base64-encoded signature.
    /// </summary>
    string Sign(string baseString);
}
