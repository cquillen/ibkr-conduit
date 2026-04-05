using System;
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// RSA-SHA256 signer used exclusively for LST requests.
/// Signs via RSASSA-PKCS1-v1_5 with SHA-256.
/// </summary>
internal class RsaSha256Signer : IOAuthSigner
{
    private readonly RSA _signaturePrivateKey;

    /// <summary>
    /// Creates a new RSA-SHA256 signer with the given private key.
    /// </summary>
    public RsaSha256Signer(RSA signaturePrivateKey)
    {
        _signaturePrivateKey = signaturePrivateKey;
    }

    /// <inheritdoc />
    public string SignatureMethod => "RSA-SHA256";

    /// <inheritdoc />
    public string Sign(string baseString)
    {
        var data = Encoding.UTF8.GetBytes(baseString);
        var signatureBytes = _signaturePrivateKey.SignData(
            data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }
}
