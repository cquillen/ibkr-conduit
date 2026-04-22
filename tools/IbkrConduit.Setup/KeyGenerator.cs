using System.Formats.Asn1;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace IbkrConduit.Setup;

/// <summary>
/// Generates RSA key pairs, consumer keys, and DH parameters for IBKR OAuth 1.0a setup.
/// All cryptography uses BouncyCastle — matching the key formats produced by OpenSSL.
/// </summary>
internal static class KeyGenerator
{
    private const string _consumerKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int _consumerKeyLength = 9;

    /// <summary>
    /// Generates a random 9-character uppercase letter consumer key.
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
    /// Result of an RSA key pair generation.
    /// </summary>
    /// <param name="PrivatePem">PEM private key.</param>
    /// <param name="PublicPem">PEM public key (the format IBKR expects for upload).</param>
    internal record RsaKeyPairResult(string PrivatePem, string PublicPem);

    /// <summary>
    /// Generates a new RSA 2048-bit key pair and exports both keys as PEM strings.
    /// </summary>
    internal static RsaKeyPairResult GenerateRsaKeyPair()
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var keyPair = generator.GenerateKeyPair();

        return new RsaKeyPairResult(
            ToPem(keyPair.Private),
            ToPem(keyPair.Public));
    }

    /// <summary>
    /// Result of DH parameter generation.
    /// </summary>
    /// <param name="Pem">PEM-encoded DH parameters (BEGIN DH PARAMETERS) for portal upload.</param>
    /// <param name="PrimeHex">The prime as an uppercase hex string for the JSON credential file.</param>
    internal record DhParametersResult(string Pem, string PrimeHex);

    /// <summary>
    /// Generates random 2048-bit DH parameters with G=2 (matching both IBKR's server and
    /// the runtime code in <c>OAuthCrypto</c>). This is equivalent to <c>openssl dhparam 2048</c>.
    /// </summary>
    /// <param name="certainty">Miller-Rabin certainty parameter (higher = more confidence, slower).
    /// Default 128 matches OpenSSL's default.</param>
    internal static DhParametersResult GenerateDhParameters(int certainty = 128)
    {
        // Generate a safe prime p where (p-1)/2 is also prime.
        // Use G=2 — this matches openssl's default, IBKR's server implementation,
        // and OAuthCrypto._dhGenerator in the runtime code.
        var generator = new DHParametersGenerator();
        generator.Init(2048, certainty, new SecureRandom());
        var dhParams = generator.GenerateParameters();

        // Override the generator with 2 regardless of what BouncyCastle chose
        var dhWithG2 = new DHParameters(dhParams.P, Org.BouncyCastle.Math.BigInteger.Two);

        var primeHex = dhWithG2.P.ToString(16).ToUpperInvariant();
        var pem = EncodeDhParametersPem(dhWithG2);

        return new DhParametersResult(pem, primeHex);
    }

    /// <summary>
    /// Encodes DH parameters (P and G) as a DER ASN.1 SEQUENCE wrapped in PEM armor.
    /// BouncyCastle's PemWriter does not support DHParameters directly, so we encode manually.
    /// </summary>
    private static string EncodeDhParametersPem(DHParameters dhParams)
    {
        // Encode as DER SEQUENCE { INTEGER p, INTEGER g }.
        // Use BouncyCastle's ToByteArray() (two's-complement signed big-endian) rather
        // than ToByteArrayUnsigned() — AsnWriter.WriteInteger treats its input as a
        // signed integer, so passing the unsigned magnitude of a value whose high bit
        // is set (always true for a 2048-bit safe prime) encodes it as a negative DER
        // INTEGER. That produces a malformed DH PEM and causes the DH handshake to
        // derive an LST that doesn't match the server — every authenticated call
        // then returns 401.
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(dhParams.P.ToByteArray());
            writer.WriteInteger(dhParams.G.ToByteArray());
        }

        var der = writer.Encode();
        var base64 = Convert.ToBase64String(der);

        // Wrap in PEM armor with 64-char line breaks
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-----BEGIN DH PARAMETERS-----");
        for (var i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }

        sb.Append("-----END DH PARAMETERS-----");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Exports a BouncyCastle object (key or parameters) to PEM format.
    /// </summary>
    private static string ToPem(object obj)
    {
        using var writer = new StringWriter();
        var pemWriter = new PemWriter(writer);
        pemWriter.WriteObject(obj);
        pemWriter.Writer.Flush();
        return writer.ToString().TrimEnd();
    }
}
