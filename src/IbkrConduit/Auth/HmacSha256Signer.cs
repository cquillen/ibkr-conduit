using System;
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// HMAC-SHA256 signer used for all regular API requests after LST acquisition.
/// </summary>
public class HmacSha256Signer : IOAuthSigner
{
    private readonly byte[] _liveSessionToken;

    /// <summary>
    /// Creates a new HMAC-SHA256 signer with the given Live Session Token bytes.
    /// </summary>
    public HmacSha256Signer(byte[] liveSessionToken)
    {
        _liveSessionToken = liveSessionToken;
    }

    /// <inheritdoc />
    public string SignatureMethod => "HMAC-SHA256";

    /// <inheritdoc />
    public string Sign(string baseString)
    {
        using var hmac = new HMACSHA256(_liveSessionToken);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return Convert.ToBase64String(hashBytes);
    }
}
