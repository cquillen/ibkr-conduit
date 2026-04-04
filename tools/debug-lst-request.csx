#!/usr/bin/env dotnet-script
// Diagnostic script: dumps all intermediate values in the LST request
// Run: dotnet script tools/debug-lst-request.csx

#r "../src/IbkrConduit/bin/Debug/net10.0/IbkrConduit.dll"

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;

// Load credentials
var creds = OAuthCredentialsFactory.FromEnvironment();
Console.WriteLine("=== CREDENTIALS ===");
Console.WriteLine($"ConsumerKey: {creds.ConsumerKey}");
Console.WriteLine($"AccessToken: {creds.AccessToken}");
Console.WriteLine($"DhPrime (first 20 hex): {creds.DhPrime.ToString("x").Substring(0, 20)}...");
Console.WriteLine($"EncryptedSecret (first 20): {creds.EncryptedAccessTokenSecret.Substring(0, 20)}...");

// Step 1: Decrypt
var decryptedSecret = OAuthCrypto.DecryptAccessTokenSecret(
    creds.EncryptionPrivateKey, creds.EncryptedAccessTokenSecret);
var prependHex = Convert.ToHexString(decryptedSecret).ToLowerInvariant();
Console.WriteLine($"\n=== STEP 1: DECRYPT ===");
Console.WriteLine($"Decrypted secret length: {decryptedSecret.Length} bytes");
Console.WriteLine($"Prepend hex: {prependHex}");

// Step 2: DH key pair
var (dhPrivateKey, dhPublicKey) = OAuthCrypto.GenerateDhKeyPair(creds.DhPrime);
var dhChallengeHex = dhPublicKey.ToString("x", CultureInfo.InvariantCulture);
Console.WriteLine($"\n=== STEP 2: DH KEY PAIR ===");
Console.WriteLine($"DH challenge hex length: {dhChallengeHex.Length}");
Console.WriteLine($"DH challenge hex (first 40): {dhChallengeHex.Substring(0, Math.Min(40, dhChallengeHex.Length))}...");

// Step 3: Build base string manually to inspect
var url = "https://api.ibkr.com/v1/api/oauth/live_session_token";
var nonce = OAuthEncoding.GenerateNonce();
var timestamp = OAuthEncoding.GenerateTimestamp();

var parameters = new SortedDictionary<string, string>
{
    ["oauth_consumer_key"] = creds.ConsumerKey,
    ["oauth_token"] = creds.AccessToken,
    ["oauth_signature_method"] = "RSA-SHA256",
    ["oauth_nonce"] = nonce,
    ["oauth_timestamp"] = timestamp,
    ["diffie_hellman_challenge"] = dhChallengeHex,
};

Console.WriteLine($"\n=== STEP 3: PARAMETERS (sorted) ===");
foreach (var kvp in parameters)
{
    var display = kvp.Value.Length > 60 ? kvp.Value.Substring(0, 60) + "..." : kvp.Value;
    Console.WriteLine($"  {kvp.Key} = {display}");
}

// Build param string
var paramString = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
Console.WriteLine($"\n=== STEP 4: PARAM STRING (first 200) ===");
Console.WriteLine(paramString.Substring(0, Math.Min(200, paramString.Length)) + "...");

var encodedUrl = OAuthEncoding.QuotePlus(url);
var encodedParams = OAuthEncoding.QuotePlus(paramString);
Console.WriteLine($"\n=== STEP 5: ENCODED URL ===");
Console.WriteLine(encodedUrl);

Console.WriteLine($"\n=== STEP 6: ENCODED PARAMS (first 200) ===");
Console.WriteLine(encodedParams.Substring(0, Math.Min(200, encodedParams.Length)) + "...");

var standardBaseString = $"POST&{encodedUrl}&{encodedParams}";
var fullBaseString = prependHex + standardBaseString;
Console.WriteLine($"\n=== STEP 7: FULL BASE STRING (first 300) ===");
Console.WriteLine(fullBaseString.Substring(0, Math.Min(300, fullBaseString.Length)) + "...");
Console.WriteLine($"Full base string length: {fullBaseString.Length}");

// Step 4: Sign
var signer = new RsaSha256Signer(creds.SignaturePrivateKey);
var signature = signer.Sign(fullBaseString);
Console.WriteLine($"\n=== STEP 8: SIGNATURE ===");
Console.WriteLine($"Raw base64 sig (first 60): {signature.Substring(0, Math.Min(60, signature.Length))}...");
var encodedSig = OAuthEncoding.QuotePlus(signature);
Console.WriteLine($"QuotePlus sig (first 80): {encodedSig.Substring(0, Math.Min(80, encodedSig.Length))}...");

// Step 5: Build full header
var headerParams = new SortedDictionary<string, string>(parameters)
{
    ["oauth_signature"] = encodedSig,
};
var paramPairs = headerParams.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"");
var authHeader = $"OAuth realm=\"limited_poa\", {string.Join(", ", paramPairs)}";
Console.WriteLine($"\n=== STEP 9: AUTHORIZATION HEADER ===");
Console.WriteLine(authHeader.Substring(0, Math.Min(400, authHeader.Length)) + "...");
Console.WriteLine($"Header length: {authHeader.Length}");

// Step 6: Actually send the request and get the response
Console.WriteLine($"\n=== STEP 10: SENDING REQUEST ===");
using var handler = new HttpClientHandler();
using var httpClient = new HttpClient(handler)
{
    BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
};

using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/live_session_token");
request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
request.Headers.TryAddWithoutValidation("Authorization", authHeader);
request.Headers.Connection.Add("keep-alive");
request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("IbkrConduit", "1.0"));

Console.WriteLine("Request headers:");
foreach (var h in request.Headers)
{
    var val = string.Join(", ", h.Value);
    if (val.Length > 200) val = val.Substring(0, 200) + "...";
    Console.WriteLine($"  {h.Key}: {val}");
}

try
{
    var response = await httpClient.SendAsync(request);
    Console.WriteLine($"\nResponse status: {(int)response.StatusCode} {response.ReasonPhrase}");
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Response body: {body}");

    foreach (var h in response.Headers)
    {
        Console.WriteLine($"  {h.Key}: {string.Join(", ", h.Value)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
