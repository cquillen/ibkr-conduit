# Milestone 1 — First Authenticated API Call to Paper Account

**Date:** 2026-03-26
**Revised:** 2026-03-27 — updated crypto details, signing strategies, and wire format based on [ibind](https://github.com/Voyz/ibind) reverse-engineered reference (`docs/ibkr_oauth1.0a.md`)
**Status:** Approved
**Goal:** Prove the OAuth 1.0a pipeline works end-to-end by calling `GET /portfolio/accounts` against a real IBKR paper account.

---

## Scope

M1 is laser-focused on proving OAuth works. No session lifecycle (M2), no rate limiting or resilience handlers (M3). The minimum path:

1. Generate OAuth key material
2. Load credentials from environment
3. Decrypt access token secret, perform DH exchange, derive live session token
4. Sign requests with HMAC-SHA256
5. Call `GET /portfolio/accounts` and get a response

Per design doc §10.3, `/portfolio` endpoints do not require brokerage session init — they work with just the OAuth-signed request.

### Deferred to Later Milestones

- **M2:** Brokerage session init, question suppression, tickle timer, token refresh
- **M3:** `RateLimitingHandler`, `ResilienceHandler` (Polly), 429 adaptive response — the HTTP pipeline in M1 is `Refit → OAuthSigningHandler → HttpClient` only

---

## IBKR Protocol Deviations from Standard OAuth 1.0a

IBKR's OAuth 1.0a is not a standard three-legged flow. Key deviations that affect this implementation:

| Deviation | Standard OAuth 1.0a | IBKR OAuth 1.0a |
|---|---|---|
| **Dual signature methods** | Single method (typically HMAC-SHA1) | RSA-SHA256 for LST request; HMAC-SHA256 for all subsequent API calls |
| **Prepend value** | Base string = `METHOD&URL&PARAMS` | LST request prepends hex-encoded decrypted access token secret before the base string |
| **Diffie-Hellman key exchange** | Not part of OAuth | Client sends DH challenge in LST request; server responds with DH response; shared secret derives the session token |
| **Pre-encrypted access token secret** | Plain secret shared during token exchange | Access token secret is RSA-encrypted (PKCS#1 v1.5); client must decrypt with private encryption key |
| **Live Session Token (LST)** | Not part of OAuth | A derived session key (via DH + HMAC-SHA1) used as the HMAC key for all subsequent requests |
| **LST validation** | Not part of OAuth | Server returns an HMAC-SHA1 signature of the LST that the client validates locally |
| **Two separate RSA key pairs** | Typically one key pair | One "encryption key" (for decrypting the access token secret) and one "signature key" (for RSA-signing the LST request) |
| **Percent-encoding** | `%20` for spaces | Uses `quote_plus` semantics (`+` for spaces) |

Where IBKR's official documentation and the ibind reference implementation disagree, we follow ibind's wire-tested behavior first and fall back to the official docs only if integration testing fails.

---

## Task 1.1 — Key Generation Script

**File:** `tools/generate-oauth-keys.sh`

A bash script that generates the 5 PEM files needed for IBKR OAuth setup:

- `private_signature.pem` + `public_signature.pem` (RSA 2048)
- `private_encryption.pem` + `public_encryption.pem` (RSA 2048)
- `dhparam.pem` (DH 2048)

Behavior:

- Accepts optional output directory argument (default: current directory)
- Validates OpenSSL is on PATH before proceeding
- Generates all 5 files
- Prints summary: which 3 files to upload to the IBKR Self-Service Portal (`public_signature.pem`, `public_encryption.pem`, `dhparam.pem`) and which 2 to keep private
- Exits non-zero on any failure

No `.ps1` equivalent — OpenSSL is required regardless, and bash is available on Windows via Git Bash / WSL.

---

## Task 1.2 — OAuth Credentials Model + Crypto Primitives

### IbkrOAuthCredentials

Record per design doc §4.5:

```csharp
public record IbkrOAuthCredentials(
    string TenantId,
    string ConsumerKey,
    string AccessToken,
    string EncryptedAccessTokenSecret,
    RSA SignaturePrivateKey,
    RSA EncryptionPrivateKey,
    BigInteger DhPrime
) : IDisposable;
```

- `IDisposable` — disposes both RSA objects
- Storage-agnostic — accepts pre-loaded objects, not file paths

### OAuthCredentialsFactory

Static helper with `FromEnvironment()` that reads:

| Environment Variable | Description |
|---|---|
| `IBKR_CONSUMER_KEY` | 9-character consumer key |
| `IBKR_ACCESS_TOKEN` | OAuth access token |
| `IBKR_ACCESS_TOKEN_SECRET` | Encrypted access token secret (base64) |
| `IBKR_SIGNATURE_KEY` | Private signature key (base64-encoded PEM) |
| `IBKR_ENCRYPTION_KEY` | Private encryption key (base64-encoded PEM) |
| `IBKR_DH_PRIME` | DH prime (hex string) |
| `IBKR_TENANT_ID` | Optional, defaults to consumer key value |

Parses PEM → RSA, hex → BigInteger, returns populated `IbkrOAuthCredentials`.

### OAuthCrypto

Static class with pure crypto methods using `System.Security.Cryptography` exclusively:

| Method | Signature | Purpose |
|---|---|---|
| `DecryptAccessTokenSecret` | `(RSA encryptionKey, string encryptedSecret) → byte[]` | RSA PKCS#1 v1.5 decrypt (primary), OAEP fallback. Input is base64-encoded ciphertext. Returns raw plaintext bytes |
| `GenerateDhKeyPair` | `(BigInteger prime) → (BigInteger privateKey, BigInteger publicKey)` | Random 256-bit `a`, compute `A = g^a mod p` (g=2) |
| `DeriveDhSharedSecret` | `(BigInteger theirPublicKey, BigInteger myPrivateKey, BigInteger prime) → byte[]` | Compute `K = B^a mod p`, convert to big-endian two's complement byte array via `BigIntegerToByteArray` |
| `BigIntegerToByteArray` | `(BigInteger value) → byte[]` | Big-endian two's complement conversion matching Java's `BigInteger.toByteArray()` — uses `BigInteger.ToByteArray()` (little-endian with sign byte) then reverses to big-endian |
| `DeriveLiveSessionToken` | `(byte[] dhSharedSecret, byte[] decryptedAccessTokenSecret) → byte[]` | `HMAC-SHA1(key=dhSharedSecret, data=decryptedAccessTokenSecret)` — returns raw LST bytes |
| `ValidateLiveSessionToken` | `(byte[] lstBytes, string consumerKey, string expectedSignatureHex) → bool` | Computes `HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))`, converts to lowercase hex digest, compares to `expectedSignatureHex` |

#### Decryption Strategy

The access token secret from IBKR's portal is base64-encoded ciphertext encrypted with the encryption public key. Despite IBKR documentation referencing OAEP, the ibind reference confirms the actual wire protocol uses PKCS#1 v1.5. Our implementation:

1. Try `RSAEncryptionPadding.Pkcs1` first
2. If that throws `CryptographicException`, fall back to `RSAEncryptionPadding.OaepSHA256`
3. If both fail, propagate the exception

#### BigInteger Byte Array Conversion

This conversion is critical for correct LST derivation. It must match Java's `BigInteger.toByteArray()` (two's complement, big-endian, leading zero byte when high bit is set):

```csharp
static byte[] BigIntegerToByteArray(BigInteger value)
{
    byte[] le = value.ToByteArray();  // little-endian, includes sign byte if needed
    Array.Reverse(le);               // now big-endian
    return le;
}
```

C#'s `BigInteger.ToByteArray()` already includes the leading zero byte for positive numbers with a high bit set, matching the ibind/Java behavior. Reversing to big-endian is the only transformation needed.

Note: When parsing hex strings to `BigInteger`, prepend `"0"` to avoid negative interpretation when the leading hex digit is >= 8:

```csharp
var value = BigInteger.Parse("0" + hexString, NumberStyles.HexNumber);
```

---

## Task 1.3 — OAuth Signing Strategies + Header Builder

### IOAuthSigner

Strategy interface for signature computation:

```csharp
public interface IOAuthSigner
{
    string SignatureMethod { get; }
    string Sign(string baseString);
}
```

Returns base64-encoded signature (no percent-encoding — the caller handles that).

#### RsaSha256Signer

- Takes `RSA signaturePrivateKey`
- `SignatureMethod` → `"RSA-SHA256"`
- Signs via `RSA.SignData(UTF8(baseString), SHA256, Pkcs1)`
- Returns `Convert.ToBase64String(signatureBytes)`
- Used only for LST requests

#### HmacSha256Signer

- Takes `byte[] liveSessionToken`
- `SignatureMethod` → `"HMAC-SHA256"`
- Signs via `HMACSHA256(key=liveSessionToken).ComputeHash(UTF8(baseString))`
- Returns `Convert.ToBase64String(hashBytes)`
- Used for all regular API requests

### IBaseStringBuilder

Strategy interface for OAuth base string construction:

```csharp
public interface IBaseStringBuilder
{
    string Build(string method, string url, SortedDictionary<string, string> parameters);
}
```

#### StandardBaseStringBuilder

Builds the standard OAuth base string:

1. Sort parameters lexicographically by key
2. Join as `key1=value1&key2=value2&...` (values NOT individually percent-encoded)
3. `QuotePlus`-encode the full parameter string
4. `QuotePlus`-encode the full URL
5. Return `METHOD&encoded_url&encoded_params`

Used for all regular API requests.

#### PrependingBaseStringBuilder

Wraps `StandardBaseStringBuilder` and prepends the decrypted access token secret hex:

- Takes `byte[] decryptedAccessTokenSecret`
- Converts to lowercase hex string (`prepend`)
- Delegates to `StandardBaseStringBuilder` to build the standard portion
- Returns `prepend + standardBaseString` (direct concatenation, no separator)

Used only for LST requests.

### OAuthEncoding

Static utility for IBKR-compatible encoding:

| Method | Purpose |
|---|---|
| `QuotePlus(string value) → string` | `Uri.EscapeDataString(value).Replace("%20", "+")` — matches Python `urllib.parse.quote_plus` |
| `GenerateNonce() → string` | 16 chars from `[A-Za-z0-9]` via `RandomNumberGenerator.GetString` (.NET 9+) |
| `GenerateTimestamp() → string` | `DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()` |

### OAuthHeaderBuilder

Composes `IOAuthSigner` and `IBaseStringBuilder` to produce the full Authorization header:

**Constructor:** `(IOAuthSigner signer, IBaseStringBuilder baseStringBuilder)`

**Method:** `Build(string method, string url, string consumerKey, string accessToken, IDictionary<string, string>? extraParams = null) → string`

**Behavior:**

1. Generate nonce via `OAuthEncoding.GenerateNonce()` and timestamp via `OAuthEncoding.GenerateTimestamp()`
2. Assemble parameter set: `oauth_consumer_key`, `oauth_token`, `oauth_signature_method` (from `signer.SignatureMethod`), `oauth_nonce`, `oauth_timestamp`, plus any `extraParams` (e.g., `diffie_hellman_challenge`)
3. Delegate to `IBaseStringBuilder.Build()` for the base string
4. Delegate to `IOAuthSigner.Sign()` for the signature
5. `QuotePlus`-encode the signature
6. Format header: `OAuth realm="limited_poa", ` followed by all parameters (including `oauth_signature`) sorted alphabetically, each formatted as `key="value"`, joined with `", "`

**Key details:**

- `realm` is included in the header but excluded from the base string (per OAuth spec)
- Realm value: `"limited_poa"` for first-party OAuth
- All header values are double-quoted; `oauth_signature` is percent-encoded before quoting (step 5), other values are raw
- Parameters in the header are sorted alphabetically; `realm` always comes first, outside the sorted list

---

## Task 1.4 — Live Session Token Client

### LiveSessionToken

```csharp
public record LiveSessionToken(byte[] Token, DateTimeOffset Expiry);
```

### ILiveSessionTokenClient

```csharp
public interface ILiveSessionTokenClient
{
    Task<LiveSessionToken> GetLiveSessionTokenAsync(
        IbkrOAuthCredentials credentials, CancellationToken cancellationToken);
}
```

### LiveSessionTokenClient

Orchestrates the full LST acquisition flow:

1. Decrypt access token secret via `OAuthCrypto.DecryptAccessTokenSecret()` (PKCS#1 v1.5 primary, OAEP fallback)
2. Convert decrypted bytes to lowercase hex string (`prepend`)
3. Generate DH key pair via `OAuthCrypto.GenerateDhKeyPair()`
4. Compose `OAuthHeaderBuilder` with `RsaSha256Signer(credentials.SignaturePrivateKey)` + `PrependingBaseStringBuilder(decryptedSecretBytes)`
5. Build Authorization header for `POST https://api.ibkr.com/v1/api/oauth/live_session_token` with `diffie_hellman_challenge` (hex-encoded DH public value `A`) as an extra param
6. Send HTTP request with standard headers:
   - `Accept: */*`
   - `Accept-Encoding: gzip,deflate`
   - `Authorization: <constructed above>`
   - `Connection: keep-alive`
   - `Host: api.ibkr.com`
   - `User-Agent: IbkrConduit`
   - Body: empty
7. Parse JSON response:
   - `diffie_hellman_response` — server's DH public value `B` (hex string, no `0x` prefix)
   - `live_session_token_signature` — HMAC-SHA1 hex digest for validation
   - `live_session_token_expiration` — expiration as milliseconds since epoch
8. Derive shared secret via `OAuthCrypto.DeriveDhSharedSecret(B, a, prime)` — includes `BigIntegerToByteArray` conversion
9. Derive LST via `OAuthCrypto.DeriveLiveSessionToken(dhSharedSecretBytes, decryptedSecretBytes)`
10. Validate via `OAuthCrypto.ValidateLiveSessionToken(lstBytes, consumerKey, signatureHex)` — throws `CryptographicException` if mismatch
11. Convert `live_session_token_expiration` from milliseconds to `DateTimeOffset` and return `LiveSessionToken(lstBytes, expiry)`

Uses a plain `HttpClient` (not the Refit pipeline) since `/oauth/live_session_token` has unique signing requirements — it's a one-shot bootstrap call.

Unit tests mock the HTTP response to verify DH exchange math and token derivation independently.

---

## Task 1.5 — OAuthSigningHandler + HTTP Pipeline

### ISessionTokenProvider

```csharp
public interface ISessionTokenProvider
{
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);
}
```

Abstracts token acquisition/caching from the signing handler. M2 adds proactive refresh and 401 retry.

### SessionTokenProvider (M1 implementation)

- Holds `IbkrOAuthCredentials` and `ILiveSessionTokenClient`
- Lazy-acquires the LST on first call, caches it
- Thread-safe via `SemaphoreSlim(1,1)` — concurrent requests wait rather than all hitting the LST endpoint
- No refresh logic in M1 (24h validity is sufficient for validation)

### OAuthSigningHandler : DelegatingHandler

In `SendAsync`:

1. Get LST from `ISessionTokenProvider`
2. Compose `HmacSha256Signer` with the LST bytes
3. Compose `StandardBaseStringBuilder` (no prepend)
4. Create `OAuthHeaderBuilder` with those strategies
5. Build Authorization header for the current request
6. Attach header to request, call `base.SendAsync()`

### HTTP Pipeline Wiring

Extension method on `IServiceCollection`:

```csharp
services.AddIbkrClient(tenantId, credentials)
```

Registers:

- `ILiveSessionTokenClient`
- `ISessionTokenProvider` (per-tenant)
- Refit client for `IIbkrPortfolioApi` with `OAuthSigningHandler` in the pipeline

Pipeline: `Refit → OAuthSigningHandler → HttpClient → IBKR API`

**No rate limiting or resilience handlers in M1 — deferred to M3.**

---

## Task 1.6 — Portfolio Accounts Endpoint + Paper Account Validation

### IIbkrPortfolioApi (M1 scope)

```csharp
public interface IIbkrPortfolioApi
{
    [Get("/portfolio/accounts")]
    Task<List<Account>> GetAccounts();
}
```

### Account Model

Minimal model for the `/portfolio/accounts` response:

- `AccountId` (string)
- `AccountTitle` (string)
- `AccountType` (string)

Additional fields mapped as discovered from the actual response.

### Integration Test

**`PortfolioAccounts_WithPaperAccount_ReturnsAccountList`:**

- Loads credentials via `OAuthCredentialsFactory.FromEnvironment()`
- Wires up the full HTTP pipeline
- Calls `GET /portfolio/accounts`
- Asserts: response is not null, contains at least one account, account ID matches expected paper account
- This is the M1 proof — a real HTTP call to IBKR's paper account

### Unit Tests (WireMock)

- Verify Refit client deserializes a canned `/portfolio/accounts` response correctly
- Verify full pipeline (signing handler → request → response) works against mock server
- Verify auth failure (401) surfaces as expected exception

### Success Criteria

The integration test passes against the paper account, proving the entire OAuth pipeline works end to end: key loading → token decryption → DH exchange → LST derivation → request signing → authenticated API call → valid response.

---

## Project Structure (New Files)

```
tools/
  generate-oauth-keys.sh

src/IbkrConduit/
  Auth/
    IbkrOAuthCredentials.cs
    OAuthCredentialsFactory.cs
    OAuthCrypto.cs
    IOAuthSigner.cs
    RsaSha256Signer.cs
    HmacSha256Signer.cs
    IBaseStringBuilder.cs
    StandardBaseStringBuilder.cs
    PrependingBaseStringBuilder.cs
    OAuthEncoding.cs
    OAuthHeaderBuilder.cs
    LiveSessionToken.cs
    ILiveSessionTokenClient.cs
    LiveSessionTokenClient.cs
    ISessionTokenProvider.cs
    SessionTokenProvider.cs
    OAuthSigningHandler.cs
  Http/
    ServiceCollectionExtensions.cs
  Portfolio/
    IIbkrPortfolioApi.cs
    Account.cs

tests/IbkrConduit.Tests.Unit/
  Auth/
    OAuthCryptoTests.cs
    RsaSha256SignerTests.cs
    HmacSha256SignerTests.cs
    StandardBaseStringBuilderTests.cs
    PrependingBaseStringBuilderTests.cs
    OAuthEncodingTests.cs
    OAuthHeaderBuilderTests.cs
    LiveSessionTokenClientTests.cs
    SessionTokenProviderTests.cs
    OAuthSigningHandlerTests.cs
  Portfolio/
    PortfolioApiTests.cs

tests/IbkrConduit.Tests.Integration/
  Auth/
    LiveSessionTokenClientTests.cs
  Portfolio/
    PortfolioAccountsTests.cs
```

---

## Dependencies to Add

| Package | Purpose |
|---|---|
| `Refit` | HTTP client generation from interfaces |
| `Refit.HttpClientFactory` | IHttpClientFactory integration for Refit |
| `Microsoft.Extensions.Http` | `IHttpClientFactory`, `DelegatingHandler` pipeline |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `IServiceCollection` extension methods |

All added to `Directory.Packages.props` with versions. No external crypto libraries.

---

## Cryptographic Algorithm Summary

| Phase | Signature Method | Signing Key | Base String | Extra Header Params |
|---|---|---|---|---|
| LST Request | RSA-SHA256 (`RsaSha256Signer`) | Private signature RSA key | Hex prepend + standard (`PrependingBaseStringBuilder`) | `diffie_hellman_challenge` |
| LST Derivation | HMAC-SHA1 | DH shared secret bytes (big-endian two's complement) | N/A (prepend bytes are the HMAC data) | N/A |
| LST Validation | HMAC-SHA1 | LST bytes | N/A (consumer key is the HMAC data) | N/A |
| API Requests | HMAC-SHA256 (`HmacSha256Signer`) | LST bytes (base64-decoded) | Standard only (`StandardBaseStringBuilder`) | None |
