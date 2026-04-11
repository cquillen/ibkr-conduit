using System.Text.Json;
using System.Text.RegularExpressions;

namespace IbkrConduit.Setup;

/// <summary>
/// Reads and writes the ibkr-credentials.json file.
/// </summary>
internal static partial class CredentialFile
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Result of reading a credential file.
    /// </summary>
    internal record CredentialData(
        string ConsumerKey,
        string AccessToken,
        string AccessTokenSecret,
        string SignaturePrivateKeyPem,
        string EncryptionPrivateKeyPem,
        string DhPrimeHex);

    /// <summary>
    /// Writes the credential file to disk as formatted JSON.
    /// </summary>
    internal static void Write(
        string path,
        string consumerKey,
        string accessToken,
        string accessTokenSecret,
        string signaturePrivateKeyPem,
        string encryptionPrivateKeyPem,
        string dhPrimeHex)
    {
        var obj = new
        {
            consumerKey,
            accessToken,
            accessTokenSecret,
            signaturePrivateKey = signaturePrivateKeyPem,
            encryptionPrivateKey = encryptionPrivateKeyPem,
            dhPrime = dhPrimeHex,
        };

        var json = JsonSerializer.Serialize(obj, _jsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Reads and validates a credential file from disk.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required fields are missing or invalid.</exception>
    internal static CredentialData Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Credential file not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var consumerKey = GetRequiredString(root, "consumerKey");
        var accessToken = GetRequiredString(root, "accessToken");
        var accessTokenSecret = GetRequiredString(root, "accessTokenSecret");
        var signaturePrivateKeyPem = GetRequiredString(root, "signaturePrivateKey");
        var encryptionPrivateKeyPem = GetRequiredString(root, "encryptionPrivateKey");
        var dhPrimeHex = GetRequiredString(root, "dhPrime");

        return new CredentialData(
            consumerKey, accessToken, accessTokenSecret,
            signaturePrivateKeyPem, encryptionPrivateKeyPem, dhPrimeHex);
    }

    /// <summary>
    /// Validates a consumer key (exactly 9 uppercase letters).
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    internal static string? ValidateConsumerKey(string input)
    {
        if (string.IsNullOrEmpty(input) || !ConsumerKeyRegex().IsMatch(input))
        {
            return $"Consumer key must be exactly 9 uppercase letters (got \"{input}\"). Example: XKVMTQWLR";
        }

        return null;
    }

    /// <summary>
    /// Validates an access token (non-empty).
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    internal static string? ValidateAccessToken(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "Access token cannot be empty. Copy the full token from the IBKR portal.";
        }

        return null;
    }

    /// <summary>
    /// Validates an access token secret (non-empty, valid base64).
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    internal static string? ValidateAccessTokenSecret(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "Access token secret cannot be empty.";
        }

        try
        {
            Convert.FromBase64String(input);
            return null;
        }
        catch (FormatException)
        {
            return "Access token secret is not valid base64. Make sure you copied the entire value from the portal without extra whitespace.";
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.GetString() is not { } value)
        {
            throw new InvalidOperationException(
                $"Credential file is missing required field '{propertyName}'.");
        }

        return value;
    }

    [GeneratedRegex("^[A-Z]{9}$")]
    private static partial Regex ConsumerKeyRegex();
}
