using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Setup;

/// <summary>
/// Generates RSA key pairs, consumer keys, and DH parameters PEM for IBKR OAuth 1.0a setup.
/// All cryptography uses System.Security.Cryptography — no external dependencies.
/// </summary>
internal static class KeyGenerator
{
    private const string _consumerKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int _consumerKeyLength = 9;

    /// <summary>
    /// Generates a random 9-character uppercase alphanumeric consumer key.
    /// </summary>
    public static string GenerateConsumerKey()
    {
        var chars = new char[_consumerKeyLength];
        for (var i = 0; i < _consumerKeyLength; i++)
        {
            chars[i] = _consumerKeyChars[RandomNumberGenerator.GetInt32(_consumerKeyChars.Length)];
        }

        return new string(chars);
    }

    /// <summary>
    /// RFC 3526 Group 14 2048-bit MODP prime, as a hex string (no leading 0 sentinel).
    /// This is the same constant used by the IBKR OAuth protocol.
    /// </summary>
    internal const string Rfc3526Group14PrimeHex =
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
        "15728E5A8AACAA68FFFFFFFFFFFFFFFF";

    /// <summary>
    /// Result of an RSA key pair generation.
    /// </summary>
    /// <param name="PrivatePem">PKCS#1 PEM private key (BEGIN RSA PRIVATE KEY).</param>
    /// <param name="PublicPem">SPKI PEM public key (BEGIN PUBLIC KEY) — the format IBKR expects.</param>
    internal record RsaKeyPairResult(string PrivatePem, string PublicPem);

    /// <summary>
    /// Generates a new RSA 2048-bit key pair and exports both keys as PEM strings.
    /// The public key uses SPKI format (BEGIN PUBLIC KEY) matching OpenSSL's <c>rsa -pubout</c>.
    /// </summary>
    internal static RsaKeyPairResult GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportRSAPrivateKeyPem();
        var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        return new RsaKeyPairResult(privatePem, publicPem);
    }

    /// <summary>
    /// Encodes the RFC 3526 Group 14 DH prime as an ASN.1 DER structure
    /// wrapped in PEM armor (BEGIN DH PARAMETERS), matching OpenSSL's dhparam output.
    /// </summary>
    internal static string EncodeDhParametersPem()
    {
        var prime = BigInteger.Parse("0" + Rfc3526Group14PrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var der = EncodeDhParametersDer(prime);
        var base64 = Convert.ToBase64String(der);

        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN DH PARAMETERS-----");
        for (var i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64[i..Math.Min(i + 64, base64.Length)]);
        }
        sb.Append("-----END DH PARAMETERS-----");
        return sb.ToString();
    }

    /// <summary>
    /// Encodes a DHParameter ASN.1 structure: SEQUENCE { prime INTEGER, generator INTEGER }.
    /// </summary>
    internal static byte[] EncodeDhParametersDer(BigInteger prime, int generator = 2)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteInteger(prime);
        writer.WriteInteger(generator);
        writer.PopSequence();
        return writer.Encode();
    }
}
