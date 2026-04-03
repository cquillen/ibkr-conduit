# IBKR OAuth Authentication Flow

## Overview

The library implements a **two-stage OAuth 1.0a authentication system**:

1. **Stage 1: Live Session Token Acquisition** — RSA-SHA256 signing + Diffie-Hellman key exchange to derive an HMAC key from the server via `POST /v1/api/oauth/live_session_token`
2. **Stage 2: API Request Signing** — HMAC-SHA256 with the Live Session Token as the key for all subsequent API requests

"Live Session Token" (abbreviated LST in code) is IBKR's term for the ephemeral HMAC key derived from the DH exchange. It's valid for ~24 hours.

## HTTP Pipeline

### Consumer Pipeline (for business logic APIs)
```
Refit Client
  → TokenRefreshHandler (401 detection & re-auth)
  → ErrorNormalizationHandler
  → ResilienceHandler (Polly retry: 5xx, 408, 429)
  → GlobalRateLimitingHandler (10 req/sec)
  → EndpointRateLimitingHandler (per-endpoint limits)
  → OAuthSigningHandler (signs with LST via HMAC-SHA256)
  → HttpClient
```

### Session Pipeline (for session management)
Same as above but **no TokenRefreshHandler** — tickle failures trigger re-authentication instead of retry.

### Live Session Token Client
Plain `HttpClient` via `IHttpClientFactory` — no middleware pipeline. Used only for `POST /v1/api/oauth/live_session_token`.

---

## Stage 1: Live Session Token Acquisition

**Endpoint:** `POST /v1/api/oauth/live_session_token`
**Signed with:** RSA-SHA256 using the signature private key
**Called by:** `LiveSessionTokenClient.GetLiveSessionTokenAsync()`

### Flow

```
1. Decrypt access token secret
   RSA.Decrypt(encryptedSecret, encryptionPrivateKey)
   → decryptedSecret (raw bytes)

2. Generate ephemeral DH key pair
   privateKey = random 256-bit BigInteger
   publicKey = 2^privateKey mod dhPrime
   → dhChallengeHex (hex-encoded public key)

3. Build OAuth Authorization header
   Signer: RSA-SHA256 (signature private key)
   Base string: hex(decryptedSecret) + standard OAuth base string
   Extra params: { diffie_hellman_challenge: dhChallengeHex }

4. Send POST request
   Authorization: OAuth realm="limited_poa",
     oauth_consumer_key="...",
     oauth_token="...",
     oauth_signature_method="RSA-SHA256",
     oauth_nonce="...",
     oauth_timestamp="...",
     diffie_hellman_challenge="...",
     oauth_signature="..."
   Body: (empty)

5. Parse response
   {
     "diffie_hellman_response": "hex_server_public_key",
     "live_session_token_signature": "hex_hmac_sha1_sig",
     "live_session_token_expiration": 1234567890000
   }

6. Compute DH shared secret
   sharedSecret = serverPublicKey^myPrivateKey mod dhPrime
   → sharedSecretBytes (~256 bytes)

7. Derive LST
   lstBytes = HMAC-SHA1(key=sharedSecretBytes, data=decryptedSecret)
   → 20 bytes

8. Validate LST
   computed = HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))
   assert computed == live_session_token_signature from response

9. Cache Live Session Token with expiry
   LiveSessionToken { Token: lstBytes, Expiry: fromUnixMs(expiration) }
```

### Key Detail: Base String for the Live Session Token Request

The token request uses `PrependingBaseStringBuilder` which **prepends** `hex(decryptedAccessTokenSecret)` to the standard OAuth base string:

```
baseString = hex(decryptedSecret) + "POST&" + urlEncode(url) + "&" + urlEncode(sortedParams)
```

This is different from all subsequent API requests which use `StandardBaseStringBuilder` (no prepending).

---

## Stage 2: API Request Signing

**Every request** through the consumer and session pipelines is signed by `OAuthSigningHandler`:

```
1. SessionManager.EnsureInitializedAsync()
   (First call triggers LST acquisition + ssodh/init)

2. Get cached LST
   lst = SessionTokenProvider.GetLiveSessionTokenAsync()

3. Sign request
   Signer: HMAC-SHA256 (key = lst.Token, 20 bytes)
   Base string: METHOD + "&" + urlEncode(URL) + "&" + urlEncode(sortedParams)

4. Add Authorization header
   OAuth realm="limited_poa",
     oauth_consumer_key="...",
     oauth_token="...",
     oauth_signature_method="HMAC-SHA256",
     oauth_nonce="...",
     oauth_timestamp="...",
     oauth_signature="..."

5. Add User-Agent: IbkrConduit/1.0
```

---

## Session Initialization

On first API request, `SessionManager.EnsureInitializedAsync()` runs:

```
1. Acquire Live Session Token (if not cached) via POST /v1/api/oauth/live_session_token
2. POST /v1/api/iserver/auth/ssodh/init { "publish": true, "compete": true }
3. POST /v1/api/iserver/questions/suppress { "messageIds": [...] }  (if configured)
4. Start tickle timer (POST /v1/api/tickle every 5 minutes)
5. Schedule proactive token refresh (expiry - 1 hour)
```

---

## 401 Recovery (TokenRefreshHandler)

When any API request returns 401:

```
1. Skip if request was to POST /v1/api/tickle (dead tickle = dead session, don't retry)
2. SessionManager.ReauthenticateAsync()
   a. Stop tickle timer
   b. Acquire fresh Live Session Token via POST /v1/api/oauth/live_session_token
   c. POST /v1/api/iserver/auth/ssodh/init (re-initialize session)
   d. POST /v1/api/iserver/questions/suppress (re-suppress)
   e. Restart tickle timer (POST /v1/api/tickle every 5 minutes)
   f. Reschedule proactive refresh
3. Clone original request (without Authorization header)
4. Retry — OAuthSigningHandler will sign with the new Live Session Token
```

---

## Crypto Summary

| Operation | Algorithm | Key | Data | Output | Used For |
|---|---|---|---|---|---|
| Decrypt access token secret | RSA (PKCS#1 or OAEP-SHA256) | Encryption private key | Encrypted secret | Raw bytes | LST derivation input |
| Sign LST request | RSA-SHA256 (PKCS#1 v1.5) | Signature private key | Prepended base string | Base64 signature | LST request Authorization header |
| DH key exchange | DH (2048-bit prime, generator=2) | Random 256-bit private key | Server's public key | Shared secret bytes | LST derivation input |
| Derive LST | HMAC-SHA1 | DH shared secret | Decrypted access token secret | 20 bytes (LST) | API request signing key |
| Validate LST | HMAC-SHA1 | LST bytes | Consumer key (UTF-8) | Hex signature | Verify server's response |
| Sign API requests | HMAC-SHA256 | LST bytes (20 bytes) | Standard base string | Base64 signature | Authorization header |

---

## Implications for WireMock Integration Testing

The Live Session Token acquisition (`POST /v1/api/oauth/live_session_token`) involves **bidirectional crypto** — the client generates a random DH key pair, sends the public key to the server, and expects a DH response that derives into a valid token. WireMock cannot participate in this exchange because:

1. The client's DH private key is random each time
2. The server's DH response must produce a shared secret that, when HMAC'd, creates a valid token
3. The token must produce a valid HMAC-SHA1 signature matching what the server returns

This means WireMock cannot return a pre-canned response to `POST /v1/api/oauth/live_session_token` — the crypto wouldn't validate.

### Possible Approaches

1. **Fixed DH keys** — Use deterministic "random" bytes in test mode so the DH exchange is reproducible
2. **Skip token validation** — Add an option to bypass step 8 (signature validation)
3. **Pre-computed token** — Inject a known Live Session Token directly into `SessionTokenProvider`, bypassing the acquisition flow entirely
4. **Intercept at a higher level** — Mock `ISessionTokenProvider` to return a known token, then WireMock only needs to validate the HMAC-SHA256 signatures on API requests (which are deterministic given a known token + known nonce/timestamp)
