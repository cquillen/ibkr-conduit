using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class LiveSessionTokenClientTests
{
    [Fact]
    public void LiveSessionToken_ShouldExposeProperties()
    {
        var token = new byte[] { 0x01, 0x02, 0x03 };
        var expiry = DateTimeOffset.UtcNow.AddHours(24);

        var lst = new LiveSessionToken(token, expiry);

        lst.Token.ShouldBe(token);
        lst.Expiry.ShouldBe(expiry);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_ValidResponse_ReturnsValidatedToken()
    {
        // === ARRANGE: Build a complete cryptographic fixture ===
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);

        // Known plaintext secret
        var plaintextSecret = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };

        // Encrypt with PKCS1 to simulate IBKR portal output
        var encryptedSecret = encKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);
        var encryptedSecretB64 = Convert.ToBase64String(encryptedSecret);

        // Small DH prime for test speed
        var prime = new BigInteger(9931);
        var consumerKey = "TESTKEY01";

        var creds = new IbkrOAuthCredentials(
            "tenant1", consumerKey, "accesstok", encryptedSecretB64,
            sigKey, encKey, prime);

        // Server-side DH
        var serverPrivate = new BigInteger(42);
        var serverPublic = BigInteger.ModPow(2, serverPrivate, prime);

        var handler = new FakeHttpHandler(request =>
        {
            // Extract DH challenge from Authorization header
            var authValues = request.Headers.GetValues("Authorization");
            var authHeader = string.Join(" ", authValues);
            var fullHeader = authHeader.StartsWith("OAuth", StringComparison.Ordinal)
                ? authHeader : $"OAuth {authHeader}";

            var dhMatch = Regex.Match(fullHeader, @"diffie_hellman_challenge=""([^""]+)""");
            dhMatch.Success.ShouldBeTrue("DH challenge should be in Authorization header");
            var dhChallengeHex = dhMatch.Groups[1].Value;
            var capturedClientPublic = BigInteger.Parse("0" + dhChallengeHex, NumberStyles.HexNumber);

            // Compute shared secret from server's perspective
            var sharedSecret = BigInteger.ModPow(capturedClientPublic, serverPrivate, prime);
            var sharedSecretBytes = OAuthCrypto.BigIntegerToByteArray(sharedSecret);

            // Derive LST the same way client will
            var lstBytes = OAuthCrypto.DeriveLiveSessionToken(sharedSecretBytes, plaintextSecret);

            // Compute validation signature
#pragma warning disable CA5350 // IBKR OAuth 1.0a protocol mandates HMAC-SHA1
            using var hmac = new HMACSHA1(lstBytes);
#pragma warning restore CA5350
            var sigHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(consumerKey));
            var signatureHex = Convert.ToHexString(sigHash).ToLowerInvariant();

            var expirationMs = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();
            var responseBody = JsonSerializer.Serialize(new
            {
                diffie_hellman_response = serverPublic.ToString("x"),
                live_session_token_signature = signatureHex,
                live_session_token_expiration = expirationMs,
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        var client = new LiveSessionTokenClient(httpClient);

        // === ACT ===
        var result = await client.GetLiveSessionTokenAsync(creds, CancellationToken.None);

        // === ASSERT ===
        result.ShouldNotBeNull();
        result.Token.Length.ShouldBe(20); // HMAC-SHA1 output
        result.Expiry.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_InvalidSignature_ThrowsCryptographicException()
    {
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);

        var plaintextSecret = new byte[] { 0x01, 0x02 };
        var encryptedSecret = encKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);

        var creds = new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "accesstok",
            Convert.ToBase64String(encryptedSecret),
            sigKey, encKey, new BigInteger(9931));

        var handler = new FakeHttpHandler(_ =>
        {
            var responseBody = JsonSerializer.Serialize(new
            {
                diffie_hellman_response = BigInteger.ModPow(2, 42, 9931).ToString("x"),
                live_session_token_signature = "badhexsignature00000000000000000000000000",
                live_session_token_expiration = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds(),
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        var client = new LiveSessionTokenClient(httpClient);

        await Should.ThrowAsync<CryptographicException>(
            () => client.GetLiveSessionTokenAsync(creds, CancellationToken.None));
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
