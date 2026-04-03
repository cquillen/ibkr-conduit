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
    /// The Live Session Token bytes derived during the most recent handshake.
    /// Subsequent API request stubs can use this to validate HMAC-SHA256 signatures.
    /// </summary>
    public static byte[]? LastDerivedLst { get; private set; }

    /// <summary>
    /// Registers the LST endpoint handler on the given WireMock server.
    /// The handler performs the server side of the DH exchange using the
    /// same crypto as <see cref="OAuthCrypto"/>.
    /// </summary>
    public static void Register(WireMockServer server, IbkrOAuthCredentials credentials)
    {
        // Decrypt the access token secret (server knows this)
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
                    .WithCallback(request =>
                    {
                        // 1. Validate and extract OAuth parameters from Authorization header
                        var authHeader = request.Headers?["Authorization"]?.FirstOrDefault() ?? "";

                        // Validate required OAuth parameters and extract DH challenge
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

                        var clientDhHex = extracted["diffie_hellman_challenge"];

                        var clientPublicKey = BigInteger.Parse(
                            "0" + clientDhHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        // 2. Generate server's DH key pair (same prime as client)
                        var (serverPrivateKey, serverPublicKey) = OAuthCrypto.GenerateDhKeyPair(credentials.DhPrime);

                        // 3. Compute shared secret (same result as client will compute)
                        var sharedSecretBytes = OAuthCrypto.DeriveDhSharedSecret(
                            clientPublicKey, serverPrivateKey, credentials.DhPrime);

                        // 4. Derive the Live Session Token
                        var lstBytes = OAuthCrypto.DeriveLiveSessionToken(sharedSecretBytes, decryptedSecret);
                        LastDerivedLst = lstBytes;

                        // 5. Compute the LST validation signature
                        // signature = HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))
#pragma warning disable CA5350 // IBKR protocol requires HMAC-SHA1
                        using var hmac = new HMACSHA1(lstBytes);
#pragma warning restore CA5350
                        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(credentials.ConsumerKey));
                        var signatureHex = Convert.ToHexString(sigBytes).ToLowerInvariant();

                        // 6. Build response
                        var serverDhHex = serverPublicKey.ToString("x", CultureInfo.InvariantCulture);
                        var expiration = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();

                        var responseBody = JsonSerializer.Serialize(new
                        {
                            diffie_hellman_response = serverDhHex,
                            live_session_token_signature = signatureHex,
                            live_session_token_expiration = expiration,
                        });

                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 200,
                            BodyData = new WireMock.Util.BodyData
                            {
                                BodyAsString = responseBody,
                                DetectedBodyType = BodyType.String,
                            },
                            Headers = new Dictionary<string, WireMockList<string>>
                            {
                                ["Content-Type"] = new WireMockList<string>("application/json"),
                            },
                        };
                    }));
    }

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
        // OAuth header format: OAuth realm="...", param1="value1", param2="value2"
        var pattern = $@"{paramName}=""([^""]+)""";
        var match = Regex.Match(authHeader, pattern);
        if (match.Success)
        {
            return Uri.UnescapeDataString(match.Groups[1].Value.Replace("+", " "));
        }

        return null;
    }
}
