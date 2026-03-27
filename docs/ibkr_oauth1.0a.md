# IBKR OAuth 1.0a Protocol Documentation

> Reverse-engineered from the [ibind](https://github.com/Voyz/ibind) Python reference implementation.
> This document describes Interactive Brokers' non-standard OAuth 1.0a protocol as actually implemented, intended to inform a custom C# / .NET 10 implementation.

---

## Table of Contents

1. [Overview & Deviations from Standard OAuth 1.0a](#1-overview--deviations-from-standard-oauth-10a)
2. [Prerequisites & Configuration](#2-prerequisites--configuration)
3. [Key Material](#3-key-material)
4. [Complete Authentication Flow](#4-complete-authentication-flow)
5. [Step-by-Step: Live Session Token Request](#5-step-by-step-live-session-token-request)
6. [Step-by-Step: Signing Regular API Requests](#6-step-by-step-signing-regular-api-requests)
7. [Session Maintenance](#7-session-maintenance)
8. [Cryptographic Recipes](#8-cryptographic-recipes)
9. [Wire Format Reference](#9-wire-format-reference)
10. [Error Handling & Re-authentication](#10-error-handling--re-authentication)
11. [C# Implementation Notes](#11-c-implementation-notes)

---

## 1. Overview & Deviations from Standard OAuth 1.0a

IBKR's OAuth 1.0a is **not** a standard three-legged OAuth flow. It builds on top of OAuth 1.0a's signing mechanism but introduces several proprietary extensions:

### What is standard
- Authorization header format (`OAuth realm="...", oauth_consumer_key="...", ...`)
- Nonce and timestamp generation
- Lexicographic parameter sorting in the base string
- Percent-encoding of the base string components

### What is non-standard

| Deviation | Standard OAuth 1.0a | IBKR OAuth 1.0a |
|---|---|---|
| **Prepend value** | Base string = `METHOD&URL&PARAMS` | LST request prepends hex-encoded decrypted access token secret before the base string |
| **Dual signature methods** | Single method (typically HMAC-SHA1) | RSA-SHA256 for LST request; HMAC-SHA256 for all subsequent API calls |
| **Diffie-Hellman key exchange** | Not part of OAuth | Client sends DH challenge in LST request; server responds with DH response; shared secret derives the session token |
| **Pre-encrypted access token secret** | Plain secret shared during token exchange | Access token secret is RSA-encrypted (PKCS#1 v1.5); client must decrypt it with a private encryption key |
| **Live Session Token (LST)** | Not part of OAuth | A derived session key (via DH + HMAC-SHA1) used as the HMAC key for all subsequent requests |
| **LST validation** | Not part of OAuth | Server returns an HMAC-SHA1 signature of the LST that the client validates locally |
| **Two separate RSA key pairs** | Typically one key pair | One "encryption key" (for decrypting the access token secret) and one "signature key" (for RSA-signing the LST request) |
| **Brokerage session initialization** | Not part of OAuth | After LST is obtained, must call `/iserver/auth/ssodh/init` to enable trading/market data endpoints |
| **Tickle keepalive** | Not part of OAuth | Must POST to `/tickle` every ~60 seconds to prevent session expiry |

### Flow comparison

```
Standard OAuth 1.0a:
  1. Request Token  →  2. User Authorization  →  3. Access Token  →  4. Sign API requests with HMAC

IBKR OAuth 1.0a:
  1. Pre-provisioned Access Token + Encrypted Secret (from IBKR portal)
  2. Live Session Token Request (RSA-SHA256 signed, with DH key exchange)
  3. Derive LST client-side (HMAC-SHA1 with DH shared secret)
  4. Validate LST (HMAC-SHA1 check)
  5. Initialize Brokerage Session
  6. Sign API requests with HMAC-SHA256 (using LST as key)
  7. Maintain session with periodic /tickle calls
```

---

## 2. Prerequisites & Configuration

### Required parameters (obtained from IBKR self-service portal)

| Parameter | Description | Example |
|---|---|---|
| `access_token` | OAuth access token from self-service portal | `"a1b2c3d4e5"` |
| `access_token_secret` | Base64-encoded, RSA-encrypted secret from portal | Base64 string |
| `consumer_key` | Identifies your application in IBKR ecosystem | 9-character string |
| `dh_prime` | Hex-encoded 2048-bit Diffie-Hellman prime | Hex string (no `0x` prefix) |
| `encryption_key` | Private RSA key for decrypting access token secret | PEM file or string |
| `signature_key` | Private RSA key for signing the LST request | PEM file or string |

### Optional parameters

| Parameter | Default | Description |
|---|---|---|
| `dh_generator` | `2` | Diffie-Hellman generator value |
| `realm` | `"limited_poa"` | OAuth realm; use `"test_realm"` for TESTCONS |
| `base_url` | `https://api.ibkr.com/v1/api/` | API base URL |
| `lst_endpoint` | `oauth/live_session_token` | LST endpoint path |

---

## 3. Key Material

IBKR uses **two separate RSA key pairs** with distinct purposes:

### Encryption Key (Private)
- **Purpose**: Decrypt the access token secret that was encrypted by IBKR's portal using the corresponding public key
- **Algorithm used**: RSA PKCS#1 v1.5 decryption (note: not OAEP despite ibind's comments)
- **When used**: Once, at the start of each LST request, to recover the raw access token secret bytes

### Signature Key (Private)
- **Purpose**: RSA-sign the base string when requesting the Live Session Token
- **Algorithm used**: RSA PKCS#1 v1.5 signature with SHA-256
- **When used**: Once per LST request only

### Key loading
Both keys are standard PEM-encoded RSA private keys. They can be loaded from file paths or provided as string content. If both are provided, the string content takes precedence.

```
-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA...
-----END RSA PRIVATE KEY-----
```

---

## 4. Complete Authentication Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CLIENT-SIDE PREPARATION                         │
│                                                                     │
│  1. Load encryption private key (PEM)                              │
│  2. Load signature private key (PEM)                               │
│  3. Decrypt access_token_secret using encryption key               │
│     → produces raw secret bytes → convert to hex string ("prepend")│
│  4. Generate 256-bit random value 'a' (hex)                        │
│  5. Compute DH challenge: A = g^a mod p (hex)                     │
│  6. Build base string with prepend + METHOD & URL & PARAMS         │
│  7. RSA-SHA256 sign the base string using signature key            │
│  8. Construct Authorization header                                  │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    LST REQUEST (POST)                               │
│                                                                     │
│  POST https://api.ibkr.com/v1/api/oauth/live_session_token         │
│  Headers:                                                           │
│    Authorization: OAuth realm="limited_poa",                        │
│      diffie_hellman_challenge="<hex A>",                            │
│      oauth_consumer_key="<key>",                                    │
│      oauth_nonce="<16 chars>",                                      │
│      oauth_signature="<RSA-SHA256 sig>",                            │
│      oauth_signature_method="RSA-SHA256",                           │
│      oauth_timestamp="<unix seconds>",                              │
│      oauth_token="<access_token>"                                   │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    SERVER RESPONSE (JSON)                           │
│                                                                     │
│  {                                                                  │
│    "diffie_hellman_response": "<hex B>",                           │
│    "live_session_token_signature": "<hex HMAC>",                   │
│    "live_session_token_expiration": <ms timestamp>                 │
│  }                                                                  │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    CLIENT-SIDE LST DERIVATION                      │
│                                                                     │
│  1. Compute shared secret: K = B^a mod p                           │
│  2. Convert K to byte array (with leading zero pad if needed)      │
│  3. Convert prepend (hex) to byte array                            │
│  4. LST = Base64( HMAC-SHA1(key=K_bytes, data=prepend_bytes) )    │
│  5. Validate: HMAC-SHA1(key=LST_bytes, data=consumer_key_utf8)    │
│     must equal live_session_token_signature from server             │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    POST-AUTHENTICATION                              │
│                                                                     │
│  1. Initialize brokerage session:                                   │
│     POST /iserver/auth/ssodh/init  {"publish": true, "compete": true}│
│  2. Start tickler: POST /tickle every 60 seconds                   │
│  3. All subsequent API requests signed with HMAC-SHA256 using LST  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Step-by-Step: Live Session Token Request

### 5.1 Decrypt the access token secret

The access token secret from the portal is base64-encoded ciphertext that was encrypted with your encryption public key.

```
Input:  access_token_secret (base64 string from config)
Key:    private encryption RSA key

1. ciphertext_bytes = Base64Decode(access_token_secret)
2. plaintext_bytes = RSA_PKCS1v15_Decrypt(encryption_key, ciphertext_bytes)
3. prepend = BytesToHex(plaintext_bytes)   // lowercase hex, no "0x" prefix
```

The `prepend` is both:
- Prepended to the base string for the LST request signature
- Used later as the HMAC data input when deriving the LST

### 5.2 Generate the Diffie-Hellman challenge

```
Input:  dh_prime (hex string), dh_generator (integer, typically 2)

1. a = SecureRandom(256 bits)             // stored as hex string, no "0x" prefix
2. A = ModPow(dh_generator, a, dh_prime)  // all values as big integers
3. dh_challenge = BigIntToHex(A)          // hex string, no "0x" prefix
```

### 5.3 Build the OAuth parameter set

Construct the following dictionary of OAuth parameters:

```
{
    "oauth_consumer_key":    "<consumer_key>",
    "oauth_nonce":           "<16 random alphanumeric chars>",
    "oauth_signature_method": "RSA-SHA256",
    "oauth_timestamp":       "<unix timestamp in seconds as string>",
    "oauth_token":           "<access_token>",
    "diffie_hellman_challenge": "<hex A from step 5.2>"
}
```

**Nonce generation**: 16 characters from `[A-Za-z0-9]`, using a cryptographically secure random source.

**Timestamp**: Current UTC time as integer seconds since epoch, converted to string.

### 5.4 Build the base string

This is where the critical non-standard behavior occurs:

```
1. Collect ALL parameters into a single dict:
   - OAuth header params (oauth_consumer_key, oauth_nonce, etc.)
   - The diffie_hellman_challenge
   - Any query parameters (none for LST request)
   - Any form/body data (none for LST request)

2. Sort lexicographically by key name

3. Join as: key1=value1&key2=value2&...
   NOTE: Values are NOT individually percent-encoded at this stage.
   The raw key=value pairs are joined with "&".

4. Percent-encode the entire parameter string:
   encoded_params = PercentEncode(param_string)
   (using RFC 3986 percent-encoding, where spaces become "+" via quote_plus)

5. Percent-encode the request URL:
   encoded_url = PercentEncode("https://api.ibkr.com/v1/api/oauth/live_session_token")

6. Construct the standard base string:
   standard_base = "POST" & encoded_url & encoded_params
   (joined with literal "&" characters)

7. *** NON-STANDARD: Prepend the decrypted access token secret hex ***
   final_base_string = prepend + standard_base
   (simple string concatenation, no separator)
```

**Important**: The prepend is concatenated directly in front of the base string with no delimiter. For example:
```
"a1b2c3d4e5f6POST&https%3A%2F%2Fapi.ibkr.com%2Fv1%2Fapi%2Foauth%2Flive_session_token&diffie_hellman_challenge%3D..."
```

### 5.5 Generate the RSA-SHA256 signature

```
Input:  base_string (UTF-8 encoded), private signature RSA key

1. hash = SHA256(base_string_bytes)
2. signature_bytes = RSA_PKCS1v15_Sign(signature_key, hash)
3. signature_b64 = Base64Encode(signature_bytes)
4. Remove any newline characters from signature_b64
5. signature = PercentEncode(signature_b64)
```

Note: Python's `base64.encodebytes()` inserts newlines every 76 characters; these must be stripped. In C#, `Convert.ToBase64String()` does not insert newlines by default.

### 5.6 Build the Authorization header

```
1. Add oauth_signature to the parameter dict

2. Sort ALL parameters alphabetically by key

3. Format each as: key="value"

4. Join with ", " (comma-space)

5. Prepend: OAuth realm="limited_poa",

Result:
  OAuth realm="limited_poa", diffie_hellman_challenge="<hex>",
  oauth_consumer_key="<key>", oauth_nonce="<nonce>",
  oauth_signature="<sig>", oauth_signature_method="RSA-SHA256",
  oauth_timestamp="<ts>", oauth_token="<token>"
```

**Important**: ALL parameters (including `diffie_hellman_challenge`) appear in the Authorization header, quoted with double quotes. They are sorted alphabetically. The realm is always first and outside the sorted list.

### 5.7 Send the HTTP request

```
POST https://api.ibkr.com/v1/api/oauth/live_session_token
Headers:
  Accept: */*
  Accept-Encoding: gzip,deflate
  Authorization: <constructed above>
  Connection: keep-alive
  Host: api.ibkr.com
  User-Agent: <your app name>
Body: empty
```

SSL verification must be enabled (`verify=true`).

### 5.8 Process the response

The server returns JSON:
```json
{
    "diffie_hellman_response": "<hex string B>",
    "live_session_token_signature": "<hex string>",
    "live_session_token_expiration": 1234567890000
}
```

- `diffie_hellman_response`: Server's DH public value B (hex, no "0x" prefix)
- `live_session_token_signature`: HMAC-SHA1 hex digest for validation
- `live_session_token_expiration`: Expiration as milliseconds since epoch

### 5.9 Derive the Live Session Token

```
Input:
  B     = diffie_hellman_response (hex string)
  a     = client DH random (hex string, from step 5.2)
  p     = dh_prime (hex string)
  prepend = decrypted access token secret (hex string, from step 5.1)

1. Compute shared secret:
   K = ModPow(B_int, a_int, p_int)

2. Convert K to a byte array with special handling:
   a. hex_string = BigIntToHex(K)           // no "0x" prefix
   b. If hex_string has odd length, left-pad with "0"
   c. If the binary representation bit-length is divisible by 8
      (i.e., len(bin(K)) - 2) % 8 == 0), prepend a 0x00 byte
   d. Parse remaining hex pairs into bytes

   This ensures the byte array matches the Java BigInteger.toByteArray()
   behavior which uses two's-complement representation with a leading
   zero byte when the high bit is set.

3. Convert prepend hex to byte array:
   prepend_bytes = HexToBytes(prepend)

4. Compute LST:
   lst_bytes = HMAC-SHA1(key=K_byte_array, data=prepend_bytes)
   live_session_token = Base64Encode(lst_bytes)
```

### 5.10 Validate the Live Session Token

```
Input:
  live_session_token (base64 string)
  live_session_token_signature (hex string from server response)
  consumer_key (string)

1. lst_bytes = Base64Decode(live_session_token)
2. computed = HMAC-SHA1(key=lst_bytes, data=UTF8Encode(consumer_key))
3. Assert: computed.HexDigest() == live_session_token_signature
```

If validation fails, the LST derivation or decryption went wrong.

---

## 6. Step-by-Step: Signing Regular API Requests

After obtaining the LST, all subsequent API requests use **HMAC-SHA256** signing with the LST as the key. This applies to every endpoint except the LST request itself.

### 6.1 Build the OAuth parameter set

```
{
    "oauth_consumer_key":    "<consumer_key>",
    "oauth_nonce":           "<16 random alphanumeric chars>",
    "oauth_signature_method": "HMAC-SHA256",
    "oauth_timestamp":       "<unix timestamp in seconds>",
    "oauth_token":           "<access_token>"
}
```

No `diffie_hellman_challenge` for regular requests.

### 6.2 Build the base string

```
1. Collect parameters:
   - OAuth header params
   - Query string parameters (if any)
   - Form data / body data (if any — note: JSON body is NOT included)

2. Sort lexicographically by key

3. Join as: key1=value1&key2=value2&...

4. Percent-encode the parameter string

5. Percent-encode the full request URL

6. base_string = METHOD & encoded_url & encoded_params

*** NO PREPEND for regular requests ***
```

### 6.3 Generate the HMAC-SHA256 signature

```
Input:  base_string (UTF-8), live_session_token (base64 string)

1. key_bytes = Base64Decode(live_session_token)
2. hmac = HMAC-SHA256(key=key_bytes, data=UTF8Encode(base_string))
3. signature = PercentEncode(Base64Encode(hmac.DigestBytes()))
```

Note the difference from standard OAuth: the signature is `Base64(HMAC-SHA256-bytes)` then percent-encoded, NOT `Base64(hex-encoded-digest)`.

### 6.4 Build the Authorization header

Same format as the LST request:
```
OAuth realm="limited_poa", oauth_consumer_key="<key>",
oauth_nonce="<nonce>", oauth_signature="<sig>",
oauth_signature_method="HMAC-SHA256", oauth_timestamp="<ts>",
oauth_token="<token>"
```

### 6.5 Send the request

```
<METHOD> https://api.ibkr.com/v1/api/<endpoint>
Headers:
  Accept: */*
  Accept-Encoding: gzip,deflate
  Authorization: <constructed above>
  Connection: keep-alive
  Host: api.ibkr.com
  User-Agent: <your app name>
Body: <JSON if POST, otherwise empty>
```

The OAuth headers completely replace any session/cookie-based auth. The Authorization header is set as a top-level HTTP header. The six static headers (`Accept`, `Accept-Encoding`, `Authorization`, `Connection`, `Host`, `User-Agent`) are sent on every request.

---

## 7. Session Maintenance

### Tickle endpoint
- **URL**: `POST /tickle`
- **Frequency**: Every ~60 seconds
- **Purpose**: Prevents session timeout
- **Signed**: Yes, with HMAC-SHA256 like all other endpoints
- **Response**: JSON with session status including `iserver.authStatus`

### Brokerage session initialization
- **URL**: `POST /iserver/auth/ssodh/init`
- **Body**: `{"publish": true, "compete": true}`
- **When**: Immediately after LST validation, before any trading/market data calls
- **Purpose**: Required to enable all `/iserver` endpoints

### LST expiration
- The LST expires after ~24 hours (value in `live_session_token_expiration` field, in milliseconds)
- On expiry or 401 errors, generate a new LST by repeating the full flow
- Before re-authenticating, stop any existing tickle timer

### Logout
- **URL**: `POST /logout`
- Call on shutdown to cleanly terminate the session

---

## 8. Cryptographic Recipes

### 8.1 RSA PKCS#1 v1.5 Decrypt (access token secret)

```
Algorithm:  RSA with PKCS#1 v1.5 padding
Input:      Base64-decoded ciphertext (access_token_secret)
Key:        Private encryption RSA key
Output:     Raw plaintext bytes

C# equivalent:
  using var rsa = RSA.Create();
  rsa.ImportFromPem(encryptionKeyPem);
  byte[] plaintext = rsa.Decrypt(ciphertext, RSAEncryptionPadding.Pkcs1);
```

**Warning**: PKCS#1 v1.5 is used here (not OAEP). Despite some comments in the ibind source mentioning OAEP, the actual implementation uses `PKCS1_v1_5` from PyCryptodome, which is PKCS#1 v1.5.

### 8.2 RSA PKCS#1 v1.5 Sign with SHA-256 (LST request)

```
Algorithm:  RSASSA-PKCS1-v1_5 with SHA-256
Input:      UTF-8 encoded base string (with prepend)
Key:        Private signature RSA key
Output:     Base64-encoded signature (newlines stripped, then percent-encoded)

C# equivalent:
  using var rsa = RSA.Create();
  rsa.ImportFromPem(signatureKeyPem);
  byte[] sig = rsa.SignData(
      Encoding.UTF8.GetBytes(baseString),
      HashAlgorithmName.SHA256,
      RSASignaturePadding.Pkcs1
  );
  string encoded = Uri.EscapeDataString(Convert.ToBase64String(sig));
```

### 8.3 HMAC-SHA256 (regular request signing)

```
Algorithm:  HMAC with SHA-256
Key:        Base64-decoded live session token bytes
Data:       UTF-8 encoded base string
Output:     Base64-encoded HMAC digest bytes, then percent-encoded

C# equivalent:
  byte[] keyBytes = Convert.FromBase64String(liveSessionToken);
  using var hmac = new HMACSHA256(keyBytes);
  byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
  string encoded = Uri.EscapeDataString(Convert.ToBase64String(hash));
```

### 8.4 HMAC-SHA1 (LST derivation)

```
Algorithm:  HMAC with SHA-1
Key:        DH shared secret K as byte array (see section 5.9 step 2)
Data:       Decrypted access token secret as byte array (hex-decoded prepend)
Output:     Base64-encoded HMAC digest = live session token

C# equivalent:
  byte[] kBytes = BigIntegerToByteArray(sharedSecret);  // custom, see below
  byte[] secretBytes = Convert.FromHexString(prepend);
  using var hmac = new HMACSHA1(kBytes);
  byte[] lstBytes = hmac.ComputeHash(secretBytes);
  string lst = Convert.ToBase64String(lstBytes);
```

### 8.5 HMAC-SHA1 (LST validation)

```
Algorithm:  HMAC with SHA-1
Key:        Base64-decoded live session token bytes
Data:       UTF-8 encoded consumer key
Output:     Hex digest string, compared to server's live_session_token_signature

C# equivalent:
  byte[] lstBytes = Convert.FromBase64String(liveSessionToken);
  using var hmac = new HMACSHA1(lstBytes);
  byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(consumerKey));
  string hexDigest = Convert.ToHexString(hash).ToLower();
  // Compare hexDigest == serverSignature
```

### 8.6 Diffie-Hellman computation

```
All values are arbitrary-precision integers (BigInteger in C#).

Parameters:
  p = BigInteger.Parse(dh_prime, NumberStyles.HexNumber)  // 2048-bit prime
  g = dh_generator (typically 2)
  a = random 256-bit value (hex string)

Client public value:
  A = BigInteger.ModPow(g, a, p)
  Challenge = A.ToString("x")    // hex, no "0x" prefix

After receiving server response B:
  K = BigInteger.ModPow(B, a, p)
```

### 8.7 BigInteger to byte array conversion (critical for LST derivation)

This conversion must match Java's `BigInteger.toByteArray()` behavior (two's complement, big-endian, with a leading zero byte when the high bit is set):

```python
# ibind's implementation:
def to_byte_array(x: int) -> list[int]:
    hex_string = hex(x)[2:]
    if len(hex_string) % 2 > 0:
        hex_string = '0' + hex_string    # pad to even length
    byte_array = []
    if len(bin(x)[2:]) % 8 == 0:        # if bit-length is multiple of 8
        byte_array.append(0)             # prepend 0x00 (two's complement sign)
    for i in range(0, len(hex_string), 2):
        byte_array.append(int(hex_string[i:i+2], 16))
    return byte_array
```

**C# equivalent**:
```csharp
static byte[] BigIntegerToByteArray(BigInteger value)
{
    // BigInteger.ToByteArray() returns little-endian, two's complement.
    // We need big-endian with the same two's complement leading-zero behavior.
    byte[] le = value.ToByteArray();  // little-endian, includes sign byte if needed
    Array.Reverse(le);               // now big-endian
    return le;
}
```

Note: C#'s `BigInteger.ToByteArray()` already handles the leading zero byte for positive numbers with a high bit set, matching the ibind behavior. You just need to reverse it to big-endian.

---

## 9. Wire Format Reference

### 9.1 Percent-encoding

ibind uses Python's `urllib.parse.quote_plus()` which:
- Encodes spaces as `+`
- Encodes all other reserved characters as `%XX`

In C#, use `Uri.EscapeDataString()` but be aware it encodes spaces as `%20` not `+`. You may need to use `System.Web.HttpUtility.UrlEncode()` or a custom implementation matching `quote_plus` behavior for the base string encoding, and `Uri.EscapeDataString()` for the final signature encoding.

**Specifically**:
- The base string construction uses `quote_plus` for both the URL and the parameter string
- The final signature value is also passed through `quote_plus`
- The Authorization header values are quoted with `"` but NOT percent-encoded (they are the raw values)

### 9.2 Base string parameter joining

Parameters are joined as `key=value` (no quoting of values) then sorted lexicographically and joined with `&`. The entire resulting string is then percent-encoded as a single unit.

```
Example sorted params:
  diffie_hellman_challenge=abc123&oauth_consumer_key=MYKEY&oauth_nonce=Rn4x...&...

Then percent-encoded:
  diffie_hellman_challenge%3Dabc123%26oauth_consumer_key%3DMYKEY%26oauth_nonce%3DRn4x...
```

### 9.3 Authorization header format

```
OAuth realm="<realm>", <sorted key="value" pairs joined with ", ">
```

All values are wrapped in double quotes. Keys are sorted alphabetically. The `realm` always comes first, before the sorted parameters.

### 9.4 Full HTTP headers for all OAuth requests

```
Accept: */*
Accept-Encoding: gzip,deflate
Authorization: OAuth realm="...", ...
Connection: keep-alive
Host: api.ibkr.com
User-Agent: <your app name>
```

These six headers are sent on every OAuth-authenticated request. The `Host` header is hardcoded to `api.ibkr.com` in ibind.

---

## 10. Error Handling & Re-authentication

### HTTP 401 Unauthorized
- Indicates the LST has expired or is invalid
- Stop the tickle timer
- Generate a new LST (full flow from section 5)
- Restart the tickle timer

### HTTP 410 Gone
- Server suggests trying a different endpoint
- Try alternate servers: `1.api.ibkr.com`, `2.api.ibkr.com`, etc.

### LST validation failure
- If `HMAC-SHA1(LST_bytes, consumer_key) != server_signature`, something went wrong
- Check that:
  - The encryption key correctly decrypts the access token secret
  - The DH computation used the correct prime, generator, and random value
  - The byte array conversion of the shared secret matches the expected format

### Connection errors
- Retry up to 3 times with backoff
- On persistent failure, check connectivity to `api.ibkr.com`

### Re-authentication flow
```
1. Stop tickler
2. Generate new Live Session Token (full flow)
3. Validate new LST
4. Restart tickler
5. Re-initialize brokerage session
```

---

## 11. C# Implementation Notes

### Key .NET APIs to use

| Operation | .NET API |
|---|---|
| RSA key import | `RSA.Create()` + `ImportFromPem()` |
| RSA decrypt (PKCS1 v1.5) | `rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1)` |
| RSA sign (PKCS1 v1.5 + SHA256) | `rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)` |
| HMAC-SHA256 | `System.Security.Cryptography.HMACSHA256` |
| HMAC-SHA1 | `System.Security.Cryptography.HMACSHA1` |
| BigInteger | `System.Numerics.BigInteger` |
| ModPow | `BigInteger.ModPow()` |
| Hex parsing | `BigInteger.Parse(hex, NumberStyles.HexNumber)` |
| Base64 | `Convert.ToBase64String()` / `Convert.FromBase64String()` |
| Hex conversion | `Convert.ToHexString()` / `Convert.FromHexString()` |
| Percent-encoding | `Uri.EscapeDataString()` or custom `quote_plus` equivalent |
| HTTP client | `HttpClient` with `HttpRequestMessage` |
| Secure random | `RandomNumberGenerator` |
| Nonce generation | `RandomNumberGenerator.GetString()` (.NET 9+) or custom |

### BigInteger sign handling

C#'s `BigInteger.Parse` with `NumberStyles.HexNumber` may interpret the value as negative if the leading hex digit is >= 8. To ensure positive interpretation, prepend "0" to the hex string:

```csharp
var value = BigInteger.Parse("0" + hexString, NumberStyles.HexNumber);
```

### Percent-encoding compatibility

Python's `quote_plus` encodes spaces as `+` and uses `%XX` for other reserved chars. To replicate this in C#:

```csharp
static string QuotePlus(string value)
{
    return Uri.EscapeDataString(value).Replace("%20", "+");
}
```

### Nonce generation

```csharp
const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
string nonce = RandomNumberGenerator.GetString(chars, 16); // .NET 9+
```

### Timer for tickle

Use `System.Threading.PeriodicTimer` (.NET 6+) or `System.Threading.Timer` for the 60-second tickle interval.

### Summary of algorithm usage by phase

| Phase | Signature Method | Signing Key | Prepend? | Extra Headers |
|---|---|---|---|---|
| LST Request | RSA-SHA256 | Private signature key | Yes (decrypted secret hex) | `diffie_hellman_challenge` |
| LST Derivation | HMAC-SHA1 | DH shared secret bytes | N/A (prepend is the HMAC data) | N/A |
| LST Validation | HMAC-SHA1 | LST bytes | N/A | N/A |
| API Requests | HMAC-SHA256 | LST bytes (base64-decoded) | No | None |
