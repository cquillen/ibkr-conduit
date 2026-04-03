using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IbkrConduit.Auth;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Configures WireMock to handle the IBKR OAuth Live Session Token handshake.
/// Implements the server side of the DH key exchange so the client's real crypto
/// pipeline works end-to-end against the mock.
/// </summary>
public static class MockLstServer
{
    /// <summary>
    /// Registers the LST endpoint handler on the given WireMock server.
    /// The handler performs the server side of the DH exchange using the
    /// same crypto as <see cref="OAuthCrypto"/>.
    /// </summary>
    public static void Register(WireMockServer server, IbkrOAuthCredentials credentials)
    {
        var decryptedSecret = OAuthCrypto.DecryptAccessTokenSecret(
            credentials.EncryptionPrivateKey, credentials.EncryptedAccessTokenSecret);

        server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost()
                .WithHeader("Authorization", "*RSA-SHA256*")
                .WithHeader("Authorization", $"*oauth_consumer_key=\"{credentials.ConsumerKey}\"*")
                .WithHeader("Authorization", $"*oauth_token=\"{credentials.AccessToken}\"*"))
            .RespondWith(
                Response.Create()
                    .WithCallback(request => HandleLstRequest(request, credentials, decryptedSecret)));
    }

    private static WireMock.ResponseMessage HandleLstRequest(
        WireMock.IRequestMessage request,
        IbkrOAuthCredentials credentials,
        byte[] decryptedSecret)
    {
        var authHeader = request.Headers?["Authorization"]?.FirstOrDefault() ?? "";

        var validationError = ValidateOAuthParams(authHeader);
        if (validationError is not null)
        {
            return validationError;
        }

        var clientDhHex = ExtractOAuthParam(authHeader, "diffie_hellman_challenge")!;
        var clientPublicKey = BigInteger.Parse(
            "0" + clientDhHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        // Generate server DH key pair — must use same pair for both derivation and response
        var (serverPrivateKey, serverPublicKey) = OAuthCrypto.GenerateDhKeyPair(credentials.DhPrime);

        var lstBytes = DeriveLiveSessionToken(clientPublicKey, serverPrivateKey, credentials.DhPrime, decryptedSecret);

        var signatureHex = ComputeLstSignature(lstBytes, credentials.ConsumerKey);
        var responseBody = BuildResponseBody(serverPublicKey, signatureHex);

        return CreateJsonResponse(200, responseBody);
    }

    private static WireMock.ResponseMessage? ValidateOAuthParams(string authHeader)
    {
        var requiredParams = new[]
        {
            "oauth_consumer_key", "oauth_token", "oauth_signature_method",
            "oauth_nonce", "oauth_timestamp", "oauth_signature",
            "diffie_hellman_challenge",
        };

        var extracted = requiredParams.ToDictionary(
            p => p, p => ExtractOAuthParam(authHeader, p));

        var missing = extracted
            .Where(kv => string.IsNullOrEmpty(kv.Value))
            .Select(kv => kv.Key)
            .ToList();

        if (extracted["oauth_signature_method"] != "RSA-SHA256")
        {
            missing.Add("oauth_signature_method (expected RSA-SHA256)");
        }

        if (missing.Count > 0)
        {
            return CreateErrorResponse(400,
                "missing or invalid OAuth parameters: " + string.Join(", ", missing));
        }

        return null;
    }

    private static byte[] DeriveLiveSessionToken(
        BigInteger clientPublicKey, BigInteger serverPrivateKey,
        BigInteger dhPrime, byte[] decryptedSecret)
    {
        var sharedSecretBytes = OAuthCrypto.DeriveDhSharedSecret(clientPublicKey, serverPrivateKey, dhPrime);
        return OAuthCrypto.DeriveLiveSessionToken(sharedSecretBytes, decryptedSecret);
    }

#pragma warning disable CA5350 // IBKR protocol requires HMAC-SHA1
    private static string ComputeLstSignature(byte[] lstBytes, string consumerKey)
    {
        using var hmac = new HMACSHA1(lstBytes);
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(consumerKey));
        return Convert.ToHexString(sigBytes).ToLowerInvariant();
    }
#pragma warning restore CA5350

    private static string BuildResponseBody(BigInteger serverPublicKey, string signatureHex)
    {
        var serverDhHex = serverPublicKey.ToString("x", CultureInfo.InvariantCulture);
        var expiration = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();

        return JsonSerializer.Serialize(new
        {
            diffie_hellman_response = serverDhHex,
            live_session_token_signature = signatureHex,
            live_session_token_expiration = expiration,
        });
    }

    private static WireMock.ResponseMessage CreateJsonResponse(int statusCode, string body) =>
        new()
        {
            StatusCode = statusCode,
            BodyData = new WireMock.Util.BodyData
            {
                BodyAsString = body,
                DetectedBodyType = BodyType.String,
            },
            Headers = new Dictionary<string, WireMockList<string>>
            {
                ["Content-Type"] = new WireMockList<string>("application/json"),
            },
        };

    private static WireMock.ResponseMessage CreateErrorResponse(int statusCode, string error) =>
        new()
        {
            StatusCode = statusCode,
            BodyData = new WireMock.Util.BodyData
            {
                BodyAsString = JsonSerializer.Serialize(new { error }),
                DetectedBodyType = BodyType.String,
            },
        };

    private static string? ExtractOAuthParam(string authHeader, string paramName)
    {
        var pattern = $@"{paramName}=""([^""]+)""";
        var match = Regex.Match(authHeader, pattern);
        if (match.Success)
        {
            return Uri.UnescapeDataString(match.Groups[1].Value.Replace("+", " "));
        }

        return null;
    }
}
