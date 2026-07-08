using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IbkrConduit.Errors;

namespace IbkrConduit.Auth;

/// <summary>
/// Creates <see cref="IbkrOAuthCredentials"/> from environment variables, a JSON string, or a JSON credential file.
/// </summary>
public static class OAuthCredentialsFactory
{
    /// <summary>
    /// The default telemetry tenant label used when no explicit tenant id is supplied.
    /// Deliberately the literal <c>"default"</c> — <b>never</b> the consumer key, which would
    /// spread that secret-adjacent identifier into logs, metrics, and spans (AUT-5; design doc §15.2).
    /// </summary>
    public const string DefaultTenantId = "default";

    /// <summary>
    /// Reads OAuth credentials from environment variables and returns a populated
    /// <see cref="IbkrOAuthCredentials"/> instance.
    /// </summary>
    /// <param name="tenantId">
    /// Optional explicit tenant label for telemetry. When <c>null</c>, the <c>IBKR_TENANT_ID</c>
    /// environment variable is used if set; otherwise the label falls back to
    /// <see cref="DefaultTenantId"/> (never the consumer key).
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when a required environment variable is missing.</exception>
    public static IbkrOAuthCredentials FromEnvironment(string? tenantId = null)
    {
        var consumerKey = GetRequired("IBKR_CONSUMER_KEY");
        var accessToken = GetRequired("IBKR_ACCESS_TOKEN");
        var accessTokenSecret = GetRequired("IBKR_ACCESS_TOKEN_SECRET");
        var signatureKeyB64 = GetRequired("IBKR_SIGNATURE_KEY");
        var encryptionKeyB64 = GetRequired("IBKR_ENCRYPTION_KEY");
        var dhPrimeHex = GetRequired("IBKR_DH_PRIME");
        var resolvedTenantId =
            tenantId
            ?? Environment.GetEnvironmentVariable("IBKR_TENANT_ID")
            ?? DefaultTenantId;

        var signatureKey = RSA.Create();
        signatureKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(signatureKeyB64)));

        var encryptionKey = RSA.Create();
        encryptionKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(encryptionKeyB64)));

        var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return new IbkrOAuthCredentials(
            resolvedTenantId, consumerKey, accessToken, accessTokenSecret,
            signatureKey, encryptionKey, dhPrime);
    }

    /// <summary>
    /// Loads OAuth credentials from a JSON string produced by the ibkr-conduit-setup tool.
    /// Use this when retrieving credentials from a secret store (e.g. Azure Key Vault).
    /// </summary>
    /// <param name="json">The JSON credential string.</param>
    /// <param name="tenantId">
    /// Optional explicit tenant label for telemetry. When <c>null</c>, the credential file's
    /// optional <c>tenantId</c> field is used if present; otherwise the label falls back to
    /// <see cref="DefaultTenantId"/> (never the consumer key).
    /// </param>
    /// <returns>A populated <see cref="IbkrOAuthCredentials"/> instance. The caller is responsible for disposing it.</returns>
    /// <exception cref="IbkrConfigurationException">Thrown when a required field is missing or a PEM key cannot be imported.</exception>
    public static IbkrOAuthCredentials FromJson(string json, string? tenantId = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var consumerKey = GetRequiredField(root, "consumerKey");
        var accessToken = GetRequiredField(root, "accessToken");
        var accessTokenSecret = GetRequiredField(root, "accessTokenSecret");
        var signatureKeyPem = GetRequiredField(root, "signaturePrivateKey");
        var encryptionKeyPem = GetRequiredField(root, "encryptionPrivateKey");
        var dhPrimeHex = GetRequiredField(root, "dhPrime");

        var signatureKey = ImportPemKey(signatureKeyPem, "signaturePrivateKey");
        var encryptionKey = ImportPemKey(encryptionKeyPem, "encryptionPrivateKey");

        var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var resolvedTenantId =
            tenantId
            ?? GetOptionalField(root, "tenantId")
            ?? DefaultTenantId;

        return new IbkrOAuthCredentials(
            resolvedTenantId, consumerKey, accessToken, accessTokenSecret,
            signatureKey, encryptionKey, dhPrime);
    }

    /// <summary>
    /// Loads OAuth credentials from a JSON file produced by the ibkr-conduit-setup tool.
    /// </summary>
    /// <param name="path">Absolute or relative path to the JSON credential file.</param>
    /// <param name="tenantId">
    /// Optional explicit tenant label for telemetry. When <c>null</c>, the credential file's
    /// optional <c>tenantId</c> field is used if present; otherwise the label falls back to
    /// <see cref="DefaultTenantId"/> (never the consumer key).
    /// </param>
    /// <returns>A populated <see cref="IbkrOAuthCredentials"/> instance. The caller is responsible for disposing it.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file at <paramref name="path"/> does not exist.</exception>
    /// <exception cref="IbkrConfigurationException">Thrown when a required field is missing or a PEM key cannot be imported.</exception>
    public static IbkrOAuthCredentials FromFile(string path, string? tenantId = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Credential file not found: {path}", path);
        }

        return FromJson(File.ReadAllText(path), tenantId);
    }

    private static string? GetOptionalField(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string GetRequiredField(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            throw new IbkrConfigurationException(
                $"Required field '{fieldName}' is missing from the credential file.",
                fieldName);
        }

        var value = element.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new IbkrConfigurationException(
                $"Required field '{fieldName}' is empty in the credential file.",
                fieldName);
        }

        return value;
    }

    private static RSA ImportPemKey(string pem, string fieldName)
    {
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(pem);
            return key;
        }
        catch (Exception ex) when (ex is not IbkrConfigurationException)
        {
            key.Dispose();
            throw new IbkrConfigurationException(
                $"Failed to import RSA key from field '{fieldName}'. Ensure it contains a valid PEM-encoded RSA private key.",
                fieldName,
                ex);
        }
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
