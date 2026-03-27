using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// Creates <see cref="IbkrOAuthCredentials"/> from environment variables.
/// </summary>
public static class OAuthCredentialsFactory
{
    /// <summary>
    /// Reads OAuth credentials from environment variables and returns a populated
    /// <see cref="IbkrOAuthCredentials"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required environment variable is missing.</exception>
    public static IbkrOAuthCredentials FromEnvironment()
    {
        var consumerKey = GetRequired("IBKR_CONSUMER_KEY");
        var accessToken = GetRequired("IBKR_ACCESS_TOKEN");
        var accessTokenSecret = GetRequired("IBKR_ACCESS_TOKEN_SECRET");
        var signatureKeyB64 = GetRequired("IBKR_SIGNATURE_KEY");
        var encryptionKeyB64 = GetRequired("IBKR_ENCRYPTION_KEY");
        var dhPrimeHex = GetRequired("IBKR_DH_PRIME");
        var tenantId = Environment.GetEnvironmentVariable("IBKR_TENANT_ID") ?? consumerKey;

        var signatureKey = RSA.Create();
        signatureKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(signatureKeyB64)));

        var encryptionKey = RSA.Create();
        encryptionKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(encryptionKeyB64)));

        var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return new IbkrOAuthCredentials(
            tenantId, consumerKey, accessToken, accessTokenSecret,
            signatureKey, encryptionKey, dhPrime);
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
