using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Auth;

/// <summary>
/// Orchestrates the full Live Session Token acquisition flow.
/// Uses a plain HttpClient (not the Refit pipeline) since the LST endpoint
/// has unique signing requirements.
/// </summary>
public class LiveSessionTokenClient : ILiveSessionTokenClient
{
    private const string _lstEndpoint = "oauth/live_session_token";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new LST client using the provided HttpClient.
    /// The HttpClient should have BaseAddress set to the IBKR API base URL.
    /// </summary>
    public LiveSessionTokenClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> GetLiveSessionTokenAsync(
        IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.OAuth.AcquireLst");

        // 1. Decrypt access token secret
        var decryptedSecret = OAuthCrypto.DecryptAccessTokenSecret(
            credentials.EncryptionPrivateKey, credentials.EncryptedAccessTokenSecret);

        // 2. Generate DH key pair
        var (dhPrivateKey, dhPublicKey) = OAuthCrypto.GenerateDhKeyPair(credentials.DhPrime);
        var dhChallengeHex = dhPublicKey.ToString("x", CultureInfo.InvariantCulture);

        // 3. Build signing components for LST request
        var signer = new RsaSha256Signer(credentials.SignaturePrivateKey);
        var baseStringBuilder = new PrependingBaseStringBuilder(decryptedSecret);
        var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        // 4. Build the full URL
        var url = new Uri(_httpClient.BaseAddress!, _lstEndpoint).ToString();

        // 5. Build Authorization header
        var extraParams = new Dictionary<string, string>
        {
            ["diffie_hellman_challenge"] = dhChallengeHex,
        };
        var authHeader = headerBuilder.Build(
            "POST", url, credentials.ConsumerKey, credentials.AccessToken, extraParams);

        // 6. Send HTTP request
        // Note: Accept-Encoding is handled by HttpClientHandler.AutomaticDecompression
        using var request = new HttpRequestMessage(HttpMethod.Post, _lstEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        request.Headers.Connection.Add("keep-alive");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("IbkrConduit", "1.0"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // 7. Parse response
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dhResponseHex = root.GetProperty("diffie_hellman_response").GetString()!;
        var signatureHex = root.GetProperty("live_session_token_signature").GetString()!;
        var expirationMs = root.GetProperty("live_session_token_expiration").GetInt64();

        // 8. Derive shared secret
        var theirPublicKey = BigInteger.Parse("0" + dhResponseHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var sharedSecretBytes = OAuthCrypto.DeriveDhSharedSecret(
            theirPublicKey, dhPrivateKey, credentials.DhPrime);

        // 9. Derive LST
        var lstBytes = OAuthCrypto.DeriveLiveSessionToken(sharedSecretBytes, decryptedSecret);

        // 10. Validate LST
        if (!OAuthCrypto.ValidateLiveSessionToken(lstBytes, credentials.ConsumerKey, signatureHex))
        {
            throw new CryptographicException(
                "Live Session Token validation failed: computed signature does not match server's signature.");
        }

        // 11. Convert expiration and return
        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(expirationMs);
        return new LiveSessionToken(lstBytes, expiry);
    }
}
