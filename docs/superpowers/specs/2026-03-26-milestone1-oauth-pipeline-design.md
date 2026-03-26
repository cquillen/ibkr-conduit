# Milestone 1 — First Authenticated API Call to Paper Account

**Date:** 2026-03-26
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
| `DecryptAccessTokenSecret` | `(RSA encryptionKey, string encryptedSecret) → byte[]` | RSA-OAEP decrypt the access token secret |
| `GenerateDhKeyPair` | `(BigInteger prime) → (BigInteger privateKey, BigInteger publicKey)` | Random 256-bit `a`, compute `A = g^a mod p` (g=2) |
| `DeriveDhSecret` | `(BigInteger theirPublicKey, BigInteger myPrivateKey, BigInteger prime) → byte[]` | Compute `K = B^a mod p` |
| `DeriveLiveSessionToken` | `(byte[] dhSecret, byte[] decryptedAccessTokenSecret) → byte[]` | `HMAC-SHA1(K, secret)` |
| `ComputeSignature` | `(byte[] signingKey, string baseString) → string` | HMAC-SHA256, base64-encoded |
| `BuildBaseString` | `(string method, string url, SortedDictionary<string, string> parameters) → string` | OAuth base string per RFC 5849 |

---

## Task 1.3 — OAuth Signature and Header Builder

### OAuthHeaderBuilder

Builds the `Authorization: OAuth ...` header for any request.

**Inputs:**

- HTTP method, full URL
- Consumer key, access token
- Signing key (`byte[]`) — either decrypted access token secret (for LST request) or live session token (for normal requests)
- Optional extra OAuth params (e.g., `diffie_hellman_challenge` for LST request)

**Behavior:**

1. Generate `oauth_nonce` (random hex string) and `oauth_timestamp` (Unix seconds)
2. Assemble OAuth parameter set including `oauth_consumer_key`, `oauth_token`, `oauth_signature_method` (`HMAC-SHA256`), `oauth_nonce`, `oauth_timestamp`, plus any extras
3. Call `OAuthCrypto.BuildBaseString()` — uppercase method, lowercase scheme/host, query params merged with OAuth params, sorted
4. Call `OAuthCrypto.ComputeSignature()` to sign the base string
5. Format header: `OAuth realm="limited_poa", oauth_consumer_key="...", ...`

**Key details:**

- Parameters are percent-encoded per RFC 5849
- `realm` is included in the header but excluded from the base string (per OAuth spec)
- Realm value: `"limited_poa"` for first-party OAuth
- Same builder for both LST requests and normal API requests — only the signing key and extra params differ

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

1. Decrypt access token secret via `OAuthCrypto.DecryptAccessTokenSecret()`
2. Generate DH key pair via `OAuthCrypto.GenerateDhKeyPair()`
3. Build OAuth-signed `POST /oauth/live_session_token` — signed with the *decrypted access token secret*, with `diffie_hellman_challenge` (hex-encoded DH public value `A`) as an extra OAuth param
4. Parse response: `diffie_hellman_response` (IBKR's DH public value `B`) and `live_session_token_signature`
5. Derive shared DH secret via `OAuthCrypto.DeriveDhSecret()`
6. Derive live session token via `OAuthCrypto.DeriveLiveSessionToken()`
7. Validate `live_session_token_signature` from response — compute `HMAC-SHA1(live_session_token, consumer_key_bytes)` and compare to the signature returned by IBKR (integrity check)
8. Return `LiveSessionToken` with 24h expiry

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

- Constructor takes `ISessionTokenProvider` and `IbkrOAuthCredentials`
- In `SendAsync`: gets LST from provider, calls `OAuthHeaderBuilder` to build Authorization header, attaches to request, calls `base.SendAsync()`

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
