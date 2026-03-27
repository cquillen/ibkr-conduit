using System.Globalization;
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// IBKR-compatible OAuth encoding utilities matching Python's urllib.parse.quote_plus behavior.
/// </summary>
public static class OAuthEncoding
{
    private const string _alphanumericChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Percent-encodes a string using quote_plus semantics: spaces become "+",
    /// all other reserved characters become %XX.
    /// </summary>
    public static string QuotePlus(string value) =>
        Uri.EscapeDataString(value).Replace("%20", "+");

    /// <summary>
    /// Generates a 16-character cryptographically random alphanumeric nonce.
    /// </summary>
    public static string GenerateNonce()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (var i = 0; i < 16; i++)
        {
            chars[i] = _alphanumericChars[bytes[i] % _alphanumericChars.Length];
        }
        return new string(chars);
    }

    /// <summary>
    /// Returns the current UTC time as Unix seconds string.
    /// </summary>
    public static string GenerateTimestamp() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
