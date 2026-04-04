using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;

Console.WriteLine("=== IBKR OAuth Diagnostic - ssodh/init 401 Investigation ===\n");

var consumerKey = Require("IBKR_CONSUMER_KEY");
var accessToken = Require("IBKR_ACCESS_TOKEN");
var accessTokenSecret = Require("IBKR_ACCESS_TOKEN_SECRET");
var signatureKeyB64 = Require("IBKR_SIGNATURE_KEY");
var encryptionKeyB64 = Require("IBKR_ENCRYPTION_KEY");
var dhPrimeHex = Require("IBKR_DH_PRIME");

var signatureKey = RSA.Create();
signatureKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(signatureKeyB64)));

var encryptionKey = RSA.Create();
encryptionKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(encryptionKeyB64)));

var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

using var creds = new IbkrOAuthCredentials(
    consumerKey, consumerKey, accessToken, accessTokenSecret,
    signatureKey, encryptionKey, dhPrime);

// Get LST
using var lstHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
};
var lstClient = new LiveSessionTokenClient(lstHttpClient);
var lst = await lstClient.GetLiveSessionTokenAsync(creds, CancellationToken.None);
Console.WriteLine($"LST acquired: {Convert.ToBase64String(lst.Token)}");
Console.WriteLine($"LST expiry: {lst.Expiry}");

// Test 1: GET /portfolio/accounts (known working)
Console.WriteLine("\n--- Test 1: GET /v1/api/portfolio/accounts ---");
await SendSignedRequest(
    HttpMethod.Get,
    "https://api.ibkr.com/v1/api/portfolio/accounts",
    null, lst.Token, consumerKey, accessToken);

// Test 2: POST /iserver/auth/ssodh/init with JSON body (the failing case)
Console.WriteLine("\n--- Test 2: POST /v1/api/iserver/auth/ssodh/init (JSON body) ---");
await SendSignedRequest(
    HttpMethod.Post,
    "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init",
    """{"publish":true,"compete":true}""",
    lst.Token, consumerKey, accessToken);

// Test 3: POST with ibind-style headers (Accept, Host, etc.)
Console.WriteLine("\n--- Test 3: POST ssodh/init with ibind-style headers ---");
await SendSignedRequest(
    HttpMethod.Post,
    "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init",
    """{"publish":true,"compete":true}""",
    lst.Token, consumerKey, accessToken,
    ibindStyle: true);

// Test 4: POST with empty body
Console.WriteLine("\n--- Test 4: POST ssodh/init (empty body) ---");
await SendSignedRequest(
    HttpMethod.Post,
    "https://api.ibkr.com/v1/api/iserver/auth/ssodh/init",
    null, lst.Token, consumerKey, accessToken);

// Test 5: POST tickle (another POST endpoint that may be simpler)
Console.WriteLine("\n--- Test 5: POST /v1/api/tickle ---");
await SendSignedRequest(
    HttpMethod.Post,
    "https://api.ibkr.com/v1/api/tickle",
    null, lst.Token, consumerKey, accessToken);

Console.WriteLine("\nDone.");

static async Task SendSignedRequest(
    HttpMethod method,
    string url,
    string? jsonBody,
    byte[] lstToken,
    string consumerKey,
    string accessToken,
    bool ibindStyle = false)
{
    var signer = new HmacSha256Signer(lstToken);
    var baseStringBuilder = new StandardBaseStringBuilder();
    var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

    var authHeader = headerBuilder.Build(method.Method, url, consumerKey, accessToken);

    using var request = new HttpRequestMessage(method, new Uri(url));
    request.Headers.TryAddWithoutValidation("Authorization", authHeader);

    if (ibindStyle)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        request.Headers.Connection.Add("keep-alive");
        request.Headers.Host = "api.ibkr.com";
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ibind", null));
    }
    else
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("IbkrConduit", "1.0"));
    }

    if (jsonBody != null)
    {
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
    }

    Console.WriteLine($"  {method} {url}");
    Console.WriteLine($"  Authorization: {authHeader[..Math.Min(80, authHeader.Length)]}...");
    foreach (var h in request.Headers)
    {
        if (h.Key != "Authorization")
        {
            Console.WriteLine($"  {h.Key}: {string.Join(", ", h.Value)}");
        }
    }
    if (request.Content != null)
    {
        Console.WriteLine($"  Content-Type: {request.Content.Headers.ContentType}");
        Console.WriteLine($"  Body: {await request.Content.ReadAsStringAsync()}");
    }

    using var client = new HttpClient();
    try
    {
        using var response = await client.SendAsync(request);
        Console.WriteLine($"  => {(int)response.StatusCode} {response.ReasonPhrase}");
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"  => Body: {body[..Math.Min(500, body.Length)]}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  => ERROR: {ex.GetType().Name}: {ex.Message}");
    }
}

static string Require(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
