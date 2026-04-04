# Milestone 1 — OAuth Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the OAuth 1.0a pipeline works end-to-end by calling `GET /portfolio/accounts` against a real IBKR paper account.

**Architecture:** Strategy pattern for signing (RSA-SHA256 vs HMAC-SHA256) and base string construction (standard vs prepending). `DelegatingHandler` in the HTTP pipeline injects OAuth Authorization headers. Refit generates typed API clients. All crypto uses `System.Security.Cryptography` exclusively.

**Tech Stack:** .NET 10 / .NET 8 (multi-target), xUnit v3, Shouldly, WireMock.Net, Refit 10.1.6, System.Security.Cryptography

**Spec:** `docs/superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md`
**Protocol Reference:** `docs/ibkr_oauth1.0a.md`

---

## Dependency Graph

```
Task 1.1 (key gen script)     Task 1.2 (credentials + crypto)
         │                              │
         │                              ▼
         │                     Task 1.3 (signing + header builder)
         │                              │
         │                              ▼
         │                     Task 1.4 (LST client)
         │                              │
         │                              ▼
         │                     Task 1.5 (signing handler + pipeline)
         │                              │
         │                              ▼
         │                     Task 1.6 (portfolio endpoint)
         │
    (independent — can run in parallel with any task)
```

**Parallel opportunities:** Task 1.1 is fully independent (bash script, no C# code). It can be built on its own branch in parallel with any other task. Tasks 1.2–1.6 are sequential — each depends on the prior task's PR being merged.

---

## File Structure

### Source Files (`src/IbkrConduit/`)

| File | Responsibility |
|---|---|
| `Auth/IbkrOAuthCredentials.cs` | Credential record holding RSA keys, tokens, DH prime |
| `Auth/OAuthCredentialsFactory.cs` | Loads credentials from environment variables |
| `Auth/OAuthCrypto.cs` | Pure crypto: RSA decrypt, DH key exchange, LST derivation/validation, BigInteger conversion |
| `Auth/IOAuthSigner.cs` | Strategy interface for signature computation |
| `Auth/RsaSha256Signer.cs` | RSA-SHA256 signing (LST requests only) |
| `Auth/HmacSha256Signer.cs` | HMAC-SHA256 signing (regular API requests) |
| `Auth/IBaseStringBuilder.cs` | Strategy interface for OAuth base string construction |
| `Auth/StandardBaseStringBuilder.cs` | Standard `METHOD&URL&PARAMS` base string |
| `Auth/PrependingBaseStringBuilder.cs` | Prepends decrypted secret hex to standard base string |
| `Auth/OAuthEncoding.cs` | QuotePlus encoding, nonce generation, timestamp |
| `Auth/OAuthHeaderBuilder.cs` | Composes signer + base string builder → Authorization header |
| `Auth/LiveSessionToken.cs` | LST record (token bytes + expiry) |
| `Auth/ILiveSessionTokenClient.cs` | Interface for LST acquisition |
| `Auth/LiveSessionTokenClient.cs` | Orchestrates full LST flow: decrypt → DH → sign → HTTP → derive → validate |
| `Auth/ISessionTokenProvider.cs` | Abstracts token acquisition/caching from signing handler |
| `Auth/SessionTokenProvider.cs` | Lazy-acquires and caches LST, thread-safe |
| `Auth/OAuthSigningHandler.cs` | `DelegatingHandler` that signs outgoing requests with HMAC-SHA256 |
| `Http/ServiceCollectionExtensions.cs` | `AddIbkrClient()` DI wiring |
| `Portfolio/IIbkrPortfolioApi.cs` | Refit interface for `/portfolio/accounts` |
| `Portfolio/Account.cs` | Account response model |

### Test Files (`tests/IbkrConduit.Tests.Unit/`)

| File | What it tests |
|---|---|
| `Auth/OAuthCryptoTests.cs` | RSA decryption, DH math, LST derivation, LST validation, BigInteger conversion |
| `Auth/OAuthCredentialsFactoryTests.cs` | Environment variable parsing, missing var errors |
| `Auth/RsaSha256SignerTests.cs` | RSA-SHA256 signature output |
| `Auth/HmacSha256SignerTests.cs` | HMAC-SHA256 signature output |
| `Auth/StandardBaseStringBuilderTests.cs` | Base string format, parameter sorting, QuotePlus encoding |
| `Auth/PrependingBaseStringBuilderTests.cs` | Prepend hex + delegation to standard builder |
| `Auth/OAuthEncodingTests.cs` | QuotePlus encoding, nonce format, timestamp format |
| `Auth/OAuthHeaderBuilderTests.cs` | Full header assembly with realm, sorting, quoting |
| `Auth/LiveSessionTokenClientTests.cs` | LST flow with mocked HTTP, DH math, token derivation |
| `Auth/SessionTokenProviderTests.cs` | Lazy acquisition, caching, thread safety |
| `Auth/OAuthSigningHandlerTests.cs` | Header injection into outgoing requests |
| `Portfolio/PortfolioApiTests.cs` | Refit deserialization of canned response |

### Integration Test Files (`tests/IbkrConduit.Tests.Integration/`)

| File | What it tests |
|---|---|
| `Auth/LiveSessionTokenClientTests.cs` | Full LST flow against WireMock |
| `Portfolio/PortfolioAccountsTests.cs` | End-to-end pipeline against WireMock; real IBKR paper account test (manual) |

---

## Task 1.1: Key Generation Script

**Branch:** `feat/m1-key-generation-script`
**PR scope:** Bash script only — no C# changes.

**Files:**
- Create: `tools/generate-oauth-keys.sh`

- [ ] **Step 1: Create the script file**

```bash
#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-.}"

# Validate OpenSSL is available
if ! command -v openssl &>/dev/null; then
    echo "ERROR: OpenSSL is required but not found on PATH." >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo "Generating IBKR OAuth key material..."

# Signature key pair (RSA 2048)
openssl genrsa -out "$OUTPUT_DIR/private_signature.pem" 2048
openssl rsa -in "$OUTPUT_DIR/private_signature.pem" -pubout -out "$OUTPUT_DIR/public_signature.pem"

# Encryption key pair (RSA 2048)
openssl genrsa -out "$OUTPUT_DIR/private_encryption.pem" 2048
openssl rsa -in "$OUTPUT_DIR/private_encryption.pem" -pubout -out "$OUTPUT_DIR/public_encryption.pem"

# Diffie-Hellman parameters (2048-bit)
openssl dhparam -out "$OUTPUT_DIR/dhparam.pem" 2048

echo ""
echo "=== Key generation complete ==="
echo ""
echo "Upload these 3 files to the IBKR Self-Service Portal:"
echo "  - $OUTPUT_DIR/public_signature.pem"
echo "  - $OUTPUT_DIR/public_encryption.pem"
echo "  - $OUTPUT_DIR/dhparam.pem"
echo ""
echo "Keep these 2 files PRIVATE (never commit them):"
echo "  - $OUTPUT_DIR/private_signature.pem"
echo "  - $OUTPUT_DIR/private_encryption.pem"
```

Run: `chmod +x tools/generate-oauth-keys.sh`

- [ ] **Step 2: Verify the script runs**

Run: `tools/generate-oauth-keys.sh /tmp/ibkr-test-keys`

Expected: 5 PEM files created in `/tmp/ibkr-test-keys/`, summary printed showing which to upload and which to keep private.

- [ ] **Step 3: Verify script fails without OpenSSL**

Run: `PATH="" tools/generate-oauth-keys.sh /tmp/ibkr-test-keys-2`

Expected: Exit code 1, error message about OpenSSL not found.

- [ ] **Step 4: Clean up test output and commit**

Run: `rm -rf /tmp/ibkr-test-keys /tmp/ibkr-test-keys-2`

```bash
git add tools/generate-oauth-keys.sh
git commit -m "feat: add OAuth key generation script

Generates the 5 PEM files needed for IBKR OAuth setup:
signature key pair, encryption key pair, and DH parameters."
```

---

## Task 1.2: OAuth Credentials Model + Crypto Primitives

**Branch:** `feat/m1-credentials-and-crypto`
**PR scope:** `IbkrOAuthCredentials`, `OAuthCredentialsFactory`, `OAuthCrypto` + unit tests.

**Files:**
- Create: `src/IbkrConduit/Auth/IbkrOAuthCredentials.cs`
- Create: `src/IbkrConduit/Auth/OAuthCredentialsFactory.cs`
- Create: `src/IbkrConduit/Auth/OAuthCrypto.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs`

### Sub-task 1.2a: IbkrOAuthCredentials record

- [ ] **Step 1: Write failing test for credentials construction**

File: `tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs`

```csharp
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthCredentialsFactoryTests
{
    [Fact]
    public void IbkrOAuthCredentials_ShouldExposeAllProperties()
    {
        using var signatureKey = RSA.Create(2048);
        using var encryptionKey = RSA.Create(2048);
        var prime = BigInteger.Parse("023", System.Globalization.NumberStyles.HexNumber);

        using var creds = new IbkrOAuthCredentials(
            TenantId: "tenant1",
            ConsumerKey: "TESTCONS",
            AccessToken: "token123",
            EncryptedAccessTokenSecret: "c2VjcmV0",
            SignaturePrivateKey: signatureKey,
            EncryptionPrivateKey: encryptionKey,
            DhPrime: prime);

        creds.TenantId.ShouldBe("tenant1");
        creds.ConsumerKey.ShouldBe("TESTCONS");
        creds.AccessToken.ShouldBe("token123");
        creds.EncryptedAccessTokenSecret.ShouldBe("c2VjcmV0");
        creds.SignaturePrivateKey.ShouldBe(signatureKey);
        creds.EncryptionPrivateKey.ShouldBe(encryptionKey);
        creds.DhPrime.ShouldBe(prime);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCredentialsFactoryTests.IbkrOAuthCredentials_ShouldExposeAllProperties"`

Expected: FAIL — `IbkrOAuthCredentials` type does not exist.

- [ ] **Step 3: Implement IbkrOAuthCredentials**

File: `src/IbkrConduit/Auth/IbkrOAuthCredentials.cs`

```csharp
using System.Numerics;
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// Holds all OAuth credentials needed to authenticate with the IBKR API.
/// Storage-agnostic: accepts pre-loaded cryptographic objects, not file paths.
/// </summary>
/// <param name="TenantId">Unique identifier for the tenant.</param>
/// <param name="ConsumerKey">IBKR consumer key (9 characters).</param>
/// <param name="AccessToken">OAuth access token from IBKR portal.</param>
/// <param name="EncryptedAccessTokenSecret">Base64-encoded RSA-encrypted access token secret.</param>
/// <param name="SignaturePrivateKey">RSA private key for signing LST requests.</param>
/// <param name="EncryptionPrivateKey">RSA private key for decrypting the access token secret.</param>
/// <param name="DhPrime">Diffie-Hellman 2048-bit prime.</param>
public record IbkrOAuthCredentials(
    string TenantId,
    string ConsumerKey,
    string AccessToken,
    string EncryptedAccessTokenSecret,
    RSA SignaturePrivateKey,
    RSA EncryptionPrivateKey,
    BigInteger DhPrime) : IDisposable
{
    /// <summary>
    /// Disposes both RSA key objects.
    /// </summary>
    public void Dispose()
    {
        SignaturePrivateKey.Dispose();
        EncryptionPrivateKey.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCredentialsFactoryTests.IbkrOAuthCredentials_ShouldExposeAllProperties"`

Expected: PASS

- [ ] **Step 5: Write failing test for Dispose**

Add to `OAuthCredentialsFactoryTests.cs`:

```csharp
[Fact]
public void Dispose_ShouldDisposeRsaKeys()
{
    var signatureKey = RSA.Create(2048);
    var encryptionKey = RSA.Create(2048);
    var prime = BigInteger.Parse("023", System.Globalization.NumberStyles.HexNumber);

    var creds = new IbkrOAuthCredentials(
        "tenant1", "TESTCONS", "token123", "c2VjcmV0",
        signatureKey, encryptionKey, prime);

    creds.Dispose();

    Should.Throw<ObjectDisposedException>(() => signatureKey.ExportParameters(false));
    Should.Throw<ObjectDisposedException>(() => encryptionKey.ExportParameters(false));
}
```

- [ ] **Step 6: Run test to verify it passes (Dispose already implemented)**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCredentialsFactoryTests.Dispose_ShouldDisposeRsaKeys"`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/IbkrConduit/Auth/IbkrOAuthCredentials.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs
git commit -m "feat: add IbkrOAuthCredentials record with IDisposable"
```

### Sub-task 1.2b: OAuthCrypto — BigIntegerToByteArray

- [ ] **Step 8: Write failing tests for BigIntegerToByteArray**

File: `tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs`

```csharp
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthCryptoTests
{
    [Fact]
    public void BigIntegerToByteArray_SmallValue_ReturnsBigEndianBytes()
    {
        // 0x0100 = 256
        var value = new BigInteger(256);
        var result = OAuthCrypto.BigIntegerToByteArray(value);

        // Big-endian: [0x01, 0x00]
        result.ShouldBe(new byte[] { 0x01, 0x00 });
    }

    [Fact]
    public void BigIntegerToByteArray_HighBitSet_IncludesLeadingZeroByte()
    {
        // 0x80 = 128 — high bit set, needs leading 0x00 for two's complement
        var value = new BigInteger(128);
        var result = OAuthCrypto.BigIntegerToByteArray(value);

        // Java BigInteger.toByteArray() returns [0x00, 0x80]
        result.ShouldBe(new byte[] { 0x00, 0x80 });
    }

    [Fact]
    public void BigIntegerToByteArray_LargeValue_MatchesJavaBehavior()
    {
        // 0xFF00 = 65280 — high bit set in first content byte
        var value = new BigInteger(0xFF00);
        var result = OAuthCrypto.BigIntegerToByteArray(value);

        // Java: [0x00, 0xFF, 0x00]
        result.ShouldBe(new byte[] { 0x00, 0xFF, 0x00 });
    }
}
```

- [ ] **Step 9: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests"`

Expected: FAIL — `OAuthCrypto` type does not exist.

- [ ] **Step 10: Implement OAuthCrypto with BigIntegerToByteArray**

File: `src/IbkrConduit/Auth/OAuthCrypto.cs`

```csharp
using System.Numerics;
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// Pure cryptographic operations for the IBKR OAuth 1.0a protocol.
/// Uses System.Security.Cryptography exclusively — no external crypto libraries.
/// </summary>
public static class OAuthCrypto
{
    /// <summary>
    /// Converts a BigInteger to a big-endian two's complement byte array,
    /// matching Java's BigInteger.toByteArray() behavior.
    /// </summary>
    public static byte[] BigIntegerToByteArray(BigInteger value)
    {
        var le = value.ToByteArray();
        Array.Reverse(le);
        return le;
    }
}
```

- [ ] **Step 11: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests"`

Expected: PASS

- [ ] **Step 12: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.BigIntegerToByteArray with Java-compatible behavior"
```

### Sub-task 1.2c: OAuthCrypto — DecryptAccessTokenSecret

- [ ] **Step 13: Write failing test for PKCS1 decryption**

Add to `OAuthCryptoTests.cs`:

```csharp
[Fact]
public void DecryptAccessTokenSecret_Pkcs1Encrypted_ReturnsPlaintextBytes()
{
    using var rsa = RSA.Create(2048);
    var plaintext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
    var ciphertext = rsa.Encrypt(plaintext, RSAEncryptionPadding.Pkcs1);
    var encoded = Convert.ToBase64String(ciphertext);

    var result = OAuthCrypto.DecryptAccessTokenSecret(rsa, encoded);

    result.ShouldBe(plaintext);
}

[Fact]
public void DecryptAccessTokenSecret_OaepEncrypted_FallsBackAndReturnsPlaintextBytes()
{
    using var rsa = RSA.Create(2048);
    var plaintext = new byte[] { 0xCA, 0xFE };
    var ciphertext = rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
    var encoded = Convert.ToBase64String(ciphertext);

    var result = OAuthCrypto.DecryptAccessTokenSecret(rsa, encoded);

    result.ShouldBe(plaintext);
}
```

- [ ] **Step 14: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DecryptAccessTokenSecret"`

Expected: FAIL — method does not exist.

- [ ] **Step 15: Implement DecryptAccessTokenSecret**

Add to `OAuthCrypto.cs`:

```csharp
/// <summary>
/// Decrypts the base64-encoded RSA-encrypted access token secret.
/// Tries PKCS#1 v1.5 first (ibind's wire format), falls back to OAEP SHA-256.
/// </summary>
public static byte[] DecryptAccessTokenSecret(RSA encryptionKey, string encryptedSecret)
{
    var ciphertext = Convert.FromBase64String(encryptedSecret);

    try
    {
        return encryptionKey.Decrypt(ciphertext, RSAEncryptionPadding.Pkcs1);
    }
    catch (CryptographicException)
    {
        return encryptionKey.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
    }
}
```

- [ ] **Step 16: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DecryptAccessTokenSecret"`

Expected: PASS

- [ ] **Step 17: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.DecryptAccessTokenSecret with PKCS1/OAEP fallback"
```

### Sub-task 1.2d: OAuthCrypto — DH Key Pair Generation

- [ ] **Step 18: Write failing tests for GenerateDhKeyPair**

Add to `OAuthCryptoTests.cs`:

```csharp
[Fact]
public void GenerateDhKeyPair_ReturnsValidKeyPair()
{
    // Small prime for fast testing: p = 23, g = 2
    var prime = new BigInteger(23);

    var (privateKey, publicKey) = OAuthCrypto.GenerateDhKeyPair(prime);

    // Private key should be positive and less than prime
    privateKey.Sign.ShouldBe(1);

    // Public key should be g^a mod p, which is in range [1, p-1]
    publicKey.Sign.ShouldBe(1);
    publicKey.ShouldBeLessThan(prime);
}

[Fact]
public void GenerateDhKeyPair_DifferentCallsProduceDifferentKeys()
{
    var prime = new BigInteger(23);

    var (privateKey1, _) = OAuthCrypto.GenerateDhKeyPair(prime);
    var (privateKey2, _) = OAuthCrypto.GenerateDhKeyPair(prime);

    // Extremely unlikely to collide with 256-bit randoms, but with small prime
    // the public keys might collide. Just check private keys are generated.
    privateKey1.Sign.ShouldBe(1);
    privateKey2.Sign.ShouldBe(1);
}
```

- [ ] **Step 19: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.GenerateDhKeyPair"`

Expected: FAIL — method does not exist.

- [ ] **Step 20: Implement GenerateDhKeyPair**

Add to `OAuthCrypto.cs`:

```csharp
private const int DhGenerator = 2;

/// <summary>
/// Generates a Diffie-Hellman key pair: random 256-bit private key and computed public key.
/// </summary>
/// <returns>Tuple of (privateKey a, publicKey A) where A = g^a mod p.</returns>
public static (BigInteger PrivateKey, BigInteger PublicKey) GenerateDhKeyPair(BigInteger prime)
{
    var randomBytes = new byte[32]; // 256 bits
    RandomNumberGenerator.Fill(randomBytes);

    // Ensure positive interpretation by appending a zero byte (little-endian sign byte)
    var positiveBytes = new byte[randomBytes.Length + 1];
    randomBytes.CopyTo(positiveBytes, 0);
    positiveBytes[^1] = 0;

    var privateKey = new BigInteger(positiveBytes);
    var publicKey = BigInteger.ModPow(DhGenerator, privateKey, prime);

    return (privateKey, publicKey);
}
```

- [ ] **Step 21: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.GenerateDhKeyPair"`

Expected: PASS

- [ ] **Step 22: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.GenerateDhKeyPair for DH key exchange"
```

### Sub-task 1.2e: OAuthCrypto — DeriveDhSharedSecret

- [ ] **Step 23: Write failing test for DeriveDhSharedSecret**

Add to `OAuthCryptoTests.cs`:

```csharp
[Fact]
public void DeriveDhSharedSecret_KnownValues_ReturnsExpectedBytes()
{
    // p=23, g=2, a=6, b=15
    // A = 2^6 mod 23 = 64 mod 23 = 18
    // B = 2^15 mod 23 = 32768 mod 23 = 32768 - 1424*23 = 32768 - 32752 = 16
    // K = B^a mod p = 16^6 mod 23 = 16777216 mod 23 = 16777216 - 729444*23 = 16777216 - 16777212 = 4
    var theirPublicKey = new BigInteger(16); // B
    var myPrivateKey = new BigInteger(6);    // a
    var prime = new BigInteger(23);          // p

    var result = OAuthCrypto.DeriveDhSharedSecret(theirPublicKey, myPrivateKey, prime);

    // K=4, BigIntegerToByteArray(4) = [0x04]
    result.ShouldBe(new byte[] { 0x04 });
}
```

- [ ] **Step 24: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DeriveDhSharedSecret"`

Expected: FAIL — method does not exist.

- [ ] **Step 25: Implement DeriveDhSharedSecret**

Add to `OAuthCrypto.cs`:

```csharp
/// <summary>
/// Computes the DH shared secret K = theirPublicKey^myPrivateKey mod prime,
/// returned as a big-endian two's complement byte array.
/// </summary>
public static byte[] DeriveDhSharedSecret(
    BigInteger theirPublicKey, BigInteger myPrivateKey, BigInteger prime)
{
    var sharedSecret = BigInteger.ModPow(theirPublicKey, myPrivateKey, prime);
    return BigIntegerToByteArray(sharedSecret);
}
```

- [ ] **Step 26: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DeriveDhSharedSecret"`

Expected: PASS

- [ ] **Step 27: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.DeriveDhSharedSecret"
```

### Sub-task 1.2f: OAuthCrypto — DeriveLiveSessionToken

- [ ] **Step 28: Write failing test for DeriveLiveSessionToken**

Add to `OAuthCryptoTests.cs`:

```csharp
[Fact]
public void DeriveLiveSessionToken_KnownInputs_ReturnsHmacSha1()
{
    var dhSharedSecret = new byte[] { 0x01, 0x02, 0x03, 0x04 };
    var decryptedSecret = new byte[] { 0xAA, 0xBB, 0xCC };

    var result = OAuthCrypto.DeriveLiveSessionToken(dhSharedSecret, decryptedSecret);

    // Verify it's HMAC-SHA1 output (20 bytes)
    result.Length.ShouldBe(20);

    // Verify deterministic: same inputs produce same output
    var result2 = OAuthCrypto.DeriveLiveSessionToken(dhSharedSecret, decryptedSecret);
    result.ShouldBe(result2);

    // Verify against known HMAC-SHA1 computation
    using var hmac = new HMACSHA1(dhSharedSecret);
    var expected = hmac.ComputeHash(decryptedSecret);
    result.ShouldBe(expected);
}
```

- [ ] **Step 29: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DeriveLiveSessionToken"`

Expected: FAIL — method does not exist.

- [ ] **Step 30: Implement DeriveLiveSessionToken**

Add to `OAuthCrypto.cs`:

```csharp
/// <summary>
/// Derives the Live Session Token via HMAC-SHA1(key=dhSharedSecret, data=decryptedAccessTokenSecret).
/// </summary>
public static byte[] DeriveLiveSessionToken(byte[] dhSharedSecret, byte[] decryptedAccessTokenSecret)
{
    using var hmac = new HMACSHA1(dhSharedSecret);
    return hmac.ComputeHash(decryptedAccessTokenSecret);
}
```

- [ ] **Step 31: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.DeriveLiveSessionToken"`

Expected: PASS

- [ ] **Step 32: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.DeriveLiveSessionToken"
```

### Sub-task 1.2g: OAuthCrypto — ValidateLiveSessionToken

- [ ] **Step 33: Write failing tests for ValidateLiveSessionToken**

Add to `OAuthCryptoTests.cs`:

```csharp
[Fact]
public void ValidateLiveSessionToken_ValidSignature_ReturnsTrue()
{
    var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
    var consumerKey = "TESTCONS";

    // Compute expected signature
    using var hmac = new HMACSHA1(lstBytes);
    var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(consumerKey));
    var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

    var result = OAuthCrypto.ValidateLiveSessionToken(lstBytes, consumerKey, expectedHex);

    result.ShouldBeTrue();
}

[Fact]
public void ValidateLiveSessionToken_InvalidSignature_ReturnsFalse()
{
    var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

    var result = OAuthCrypto.ValidateLiveSessionToken(lstBytes, "TESTCONS", "deadbeef");

    result.ShouldBeFalse();
}
```

- [ ] **Step 34: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.ValidateLiveSessionToken"`

Expected: FAIL — method does not exist.

- [ ] **Step 35: Implement ValidateLiveSessionToken**

Add to `OAuthCrypto.cs`:

```csharp
/// <summary>
/// Validates the LST by computing HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))
/// and comparing the lowercase hex digest to the expected signature.
/// </summary>
public static bool ValidateLiveSessionToken(
    byte[] lstBytes, string consumerKey, string expectedSignatureHex)
{
    using var hmac = new HMACSHA1(lstBytes);
    var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(consumerKey));
    var computedHex = Convert.ToHexString(hash).ToLowerInvariant();
    return string.Equals(computedHex, expectedSignatureHex, StringComparison.Ordinal);
}
```

- [ ] **Step 36: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCryptoTests.ValidateLiveSessionToken"`

Expected: PASS

- [ ] **Step 37: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCrypto.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCryptoTests.cs
git commit -m "feat: add OAuthCrypto.ValidateLiveSessionToken"
```

### Sub-task 1.2h: OAuthCredentialsFactory

- [ ] **Step 38: Write failing test for FromEnvironment**

Add to `OAuthCredentialsFactoryTests.cs`:

```csharp
[Fact]
public void FromEnvironment_AllVarsSet_ReturnsCredentials()
{
    // Generate test keys
    using var sigKey = RSA.Create(2048);
    using var encKey = RSA.Create(2048);
    var sigPem = sigKey.ExportRSAPrivateKeyPem();
    var encPem = encKey.ExportRSAPrivateKeyPem();

    // Set environment variables
    var vars = new Dictionary<string, string>
    {
        ["IBKR_CONSUMER_KEY"] = "TESTCONS9",
        ["IBKR_ACCESS_TOKEN"] = "mytoken",
        ["IBKR_ACCESS_TOKEN_SECRET"] = "c2VjcmV0",
        ["IBKR_SIGNATURE_KEY"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sigPem)),
        ["IBKR_ENCRYPTION_KEY"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(encPem)),
        ["IBKR_DH_PRIME"] = "17",
    };

    try
    {
        foreach (var (key, value) in vars)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        using var creds = OAuthCredentialsFactory.FromEnvironment();

        creds.ConsumerKey.ShouldBe("TESTCONS9");
        creds.AccessToken.ShouldBe("mytoken");
        creds.EncryptedAccessTokenSecret.ShouldBe("c2VjcmV0");
        creds.TenantId.ShouldBe("TESTCONS9"); // defaults to consumer key
        creds.DhPrime.ShouldBe(new BigInteger(0x17));
    }
    finally
    {
        foreach (var key in vars.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}

[Fact]
public void FromEnvironment_WithTenantId_UsesTenantId()
{
    using var sigKey = RSA.Create(2048);
    using var encKey = RSA.Create(2048);
    var sigPem = sigKey.ExportRSAPrivateKeyPem();
    var encPem = encKey.ExportRSAPrivateKeyPem();

    var vars = new Dictionary<string, string>
    {
        ["IBKR_CONSUMER_KEY"] = "TESTCONS9",
        ["IBKR_ACCESS_TOKEN"] = "mytoken",
        ["IBKR_ACCESS_TOKEN_SECRET"] = "c2VjcmV0",
        ["IBKR_SIGNATURE_KEY"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sigPem)),
        ["IBKR_ENCRYPTION_KEY"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(encPem)),
        ["IBKR_DH_PRIME"] = "17",
        ["IBKR_TENANT_ID"] = "custom-tenant",
    };

    try
    {
        foreach (var (key, value) in vars)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        using var creds = OAuthCredentialsFactory.FromEnvironment();

        creds.TenantId.ShouldBe("custom-tenant");
    }
    finally
    {
        foreach (var key in vars.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}

[Fact]
public void FromEnvironment_MissingRequired_ThrowsInvalidOperation()
{
    Environment.SetEnvironmentVariable("IBKR_CONSUMER_KEY", null);

    Should.Throw<InvalidOperationException>(() => OAuthCredentialsFactory.FromEnvironment());
}
```

- [ ] **Step 39: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCredentialsFactoryTests.FromEnvironment"`

Expected: FAIL — `OAuthCredentialsFactory` does not exist.

- [ ] **Step 40: Implement OAuthCredentialsFactory**

File: `src/IbkrConduit/Auth/OAuthCredentialsFactory.cs`

```csharp
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// Creates <see cref="IbkrOAuthCredentials"/> from environment variables.
/// </summary>
public static class OAuthCredentialsFactory
{
    /// <summary>
    /// Reads OAuth credentials from environment variables and returns a populated
    /// <see cref="IbkrOAuthCredentials"/> instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required environment variable is missing.</exception>
    public static IbkrOAuthCredentials FromEnvironment()
    {
        var consumerKey = GetRequired("IBKR_CONSUMER_KEY");
        var accessToken = GetRequired("IBKR_ACCESS_TOKEN");
        var accessTokenSecret = GetRequired("IBKR_ACCESS_TOKEN_SECRET");
        var signatureKeyB64 = GetRequired("IBKR_SIGNATURE_KEY");
        var encryptionKeyB64 = GetRequired("IBKR_ENCRYPTION_KEY");
        var dhPrimeHex = GetRequired("IBKR_DH_PRIME");
        var tenantId = Environment.GetEnvironmentVariable("IBKR_TENANT_ID") ?? consumerKey;

        var signatureKey = RSA.Create();
        signatureKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(signatureKeyB64)));

        var encryptionKey = RSA.Create();
        encryptionKey.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(encryptionKeyB64)));

        // Prepend "0" to avoid negative interpretation when leading hex digit >= 8
        var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber);

        return new IbkrOAuthCredentials(
            tenantId, consumerKey, accessToken, accessTokenSecret,
            signatureKey, encryptionKey, dhPrime);
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
```

- [ ] **Step 41: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthCredentialsFactoryTests"`

Expected: PASS

- [ ] **Step 42: Run full build and test suite**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 43: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthCredentialsFactory.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs
git commit -m "feat: add OAuthCredentialsFactory.FromEnvironment"
```

---

## Task 1.3: OAuth Signing Strategies + Header Builder

**Branch:** `feat/m1-signing-and-header-builder`
**Depends on:** Task 1.2 merged
**PR scope:** `IOAuthSigner` implementations, `IBaseStringBuilder` implementations, `OAuthEncoding`, `OAuthHeaderBuilder` + unit tests.

**Files:**
- Create: `src/IbkrConduit/Auth/OAuthEncoding.cs`
- Create: `src/IbkrConduit/Auth/IOAuthSigner.cs`
- Create: `src/IbkrConduit/Auth/RsaSha256Signer.cs`
- Create: `src/IbkrConduit/Auth/HmacSha256Signer.cs`
- Create: `src/IbkrConduit/Auth/IBaseStringBuilder.cs`
- Create: `src/IbkrConduit/Auth/StandardBaseStringBuilder.cs`
- Create: `src/IbkrConduit/Auth/PrependingBaseStringBuilder.cs`
- Create: `src/IbkrConduit/Auth/OAuthHeaderBuilder.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/OAuthEncodingTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/RsaSha256SignerTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/HmacSha256SignerTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/StandardBaseStringBuilderTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/PrependingBaseStringBuilderTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/OAuthHeaderBuilderTests.cs`

### Sub-task 1.3a: OAuthEncoding

- [ ] **Step 1: Write failing tests for OAuthEncoding**

File: `tests/IbkrConduit.Tests.Unit/Auth/OAuthEncodingTests.cs`

```csharp
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthEncodingTests
{
    [Theory]
    [InlineData("hello world", "hello+world")]
    [InlineData("a=b&c=d", "a%3Db%26c%3Dd")]
    [InlineData("simple", "simple")]
    [InlineData("https://api.ibkr.com/v1/api/test", "https%3A%2F%2Fapi.ibkr.com%2Fv1%2Fapi%2Ftest")]
    public void QuotePlus_EncodesCorrectly(string input, string expected)
    {
        var result = OAuthEncoding.QuotePlus(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void GenerateNonce_Returns16AlphanumericChars()
    {
        var nonce = OAuthEncoding.GenerateNonce();

        nonce.Length.ShouldBe(16);
        nonce.ShouldMatch(@"^[A-Za-z0-9]{16}$");
    }

    [Fact]
    public void GenerateNonce_ProducesDifferentValues()
    {
        var nonce1 = OAuthEncoding.GenerateNonce();
        var nonce2 = OAuthEncoding.GenerateNonce();

        nonce1.ShouldNotBe(nonce2);
    }

    [Fact]
    public void GenerateTimestamp_ReturnsUnixSecondsString()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamp = OAuthEncoding.GenerateTimestamp();
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parsed = long.Parse(timestamp);
        parsed.ShouldBeGreaterThanOrEqualTo(before);
        parsed.ShouldBeLessThanOrEqualTo(after);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthEncodingTests"`

Expected: FAIL — `OAuthEncoding` does not exist.

- [ ] **Step 3: Implement OAuthEncoding**

File: `src/IbkrConduit/Auth/OAuthEncoding.cs`

```csharp
using System.Security.Cryptography;

namespace IbkrConduit.Auth;

/// <summary>
/// IBKR-compatible OAuth encoding utilities matching Python's urllib.parse.quote_plus behavior.
/// </summary>
public static class OAuthEncoding
{
    private const string AlphanumericChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Percent-encodes a string using quote_plus semantics: spaces become "+",
    /// all other reserved characters become %XX.
    /// </summary>
    public static string QuotePlus(string value) =>
        Uri.EscapeDataString(value).Replace("%20", "+");

    /// <summary>
    /// Generates a 16-character cryptographically random alphanumeric nonce.
    /// </summary>
    public static string GenerateNonce()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (var i = 0; i < 16; i++)
        {
            chars[i] = AlphanumericChars[bytes[i] % AlphanumericChars.Length];
        }
        return new string(chars);
    }

    /// <summary>
    /// Returns the current UTC time as Unix seconds string.
    /// </summary>
    public static string GenerateTimestamp() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthEncodingTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthEncoding.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthEncodingTests.cs
git commit -m "feat: add OAuthEncoding with QuotePlus, nonce, and timestamp"
```

### Sub-task 1.3b: IOAuthSigner + RsaSha256Signer

- [ ] **Step 6: Write failing test for RsaSha256Signer**

File: `tests/IbkrConduit.Tests.Unit/Auth/RsaSha256SignerTests.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class RsaSha256SignerTests
{
    [Fact]
    public void SignatureMethod_ReturnsRsaSha256()
    {
        using var rsa = RSA.Create(2048);
        var signer = new RsaSha256Signer(rsa);

        signer.SignatureMethod.ShouldBe("RSA-SHA256");
    }

    [Fact]
    public void Sign_ReturnsBase64EncodedSignature()
    {
        using var rsa = RSA.Create(2048);
        var signer = new RsaSha256Signer(rsa);
        var baseString = "test base string";

        var signature = signer.Sign(baseString);

        // Should be valid base64
        var bytes = Convert.FromBase64String(signature);
        bytes.Length.ShouldBeGreaterThan(0);

        // Should be verifiable with the public key
        var data = Encoding.UTF8.GetBytes(baseString);
        rsa.VerifyData(data, bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).ShouldBeTrue();
    }
}
```

- [ ] **Step 7: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "RsaSha256SignerTests"`

Expected: FAIL — types do not exist.

- [ ] **Step 8: Implement IOAuthSigner and RsaSha256Signer**

File: `src/IbkrConduit/Auth/IOAuthSigner.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Strategy interface for OAuth signature computation.
/// Returns base64-encoded signature (no percent-encoding — the caller handles that).
/// </summary>
public interface IOAuthSigner
{
    /// <summary>
    /// The OAuth signature method identifier (e.g., "RSA-SHA256" or "HMAC-SHA256").
    /// </summary>
    string SignatureMethod { get; }

    /// <summary>
    /// Signs the given base string and returns a base64-encoded signature.
    /// </summary>
    string Sign(string baseString);
}
```

File: `src/IbkrConduit/Auth/RsaSha256Signer.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// RSA-SHA256 signer used exclusively for LST requests.
/// Signs via RSASSA-PKCS1-v1_5 with SHA-256.
/// </summary>
public class RsaSha256Signer : IOAuthSigner
{
    private readonly RSA _signaturePrivateKey;

    /// <summary>
    /// Creates a new RSA-SHA256 signer with the given private key.
    /// </summary>
    public RsaSha256Signer(RSA signaturePrivateKey)
    {
        _signaturePrivateKey = signaturePrivateKey;
    }

    /// <inheritdoc />
    public string SignatureMethod => "RSA-SHA256";

    /// <inheritdoc />
    public string Sign(string baseString)
    {
        var data = Encoding.UTF8.GetBytes(baseString);
        var signatureBytes = _signaturePrivateKey.SignData(
            data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }
}
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "RsaSha256SignerTests"`

Expected: PASS

- [ ] **Step 10: Commit**

```bash
git add src/IbkrConduit/Auth/IOAuthSigner.cs src/IbkrConduit/Auth/RsaSha256Signer.cs tests/IbkrConduit.Tests.Unit/Auth/RsaSha256SignerTests.cs
git commit -m "feat: add IOAuthSigner interface and RsaSha256Signer"
```

### Sub-task 1.3c: HmacSha256Signer

- [ ] **Step 11: Write failing test for HmacSha256Signer**

File: `tests/IbkrConduit.Tests.Unit/Auth/HmacSha256SignerTests.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class HmacSha256SignerTests
{
    [Fact]
    public void SignatureMethod_ReturnsHmacSha256()
    {
        var signer = new HmacSha256Signer(new byte[] { 0x01 });

        signer.SignatureMethod.ShouldBe("HMAC-SHA256");
    }

    [Fact]
    public void Sign_ReturnsBase64EncodedHmac()
    {
        var key = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var signer = new HmacSha256Signer(key);
        var baseString = "test base string";

        var signature = signer.Sign(baseString);

        // Verify against direct HMAC computation
        using var hmac = new HMACSHA256(key);
        var expected = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString)));

        signature.ShouldBe(expected);
    }
}
```

- [ ] **Step 12: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "HmacSha256SignerTests"`

Expected: FAIL — `HmacSha256Signer` does not exist.

- [ ] **Step 13: Implement HmacSha256Signer**

File: `src/IbkrConduit/Auth/HmacSha256Signer.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace IbkrConduit.Auth;

/// <summary>
/// HMAC-SHA256 signer used for all regular API requests after LST acquisition.
/// </summary>
public class HmacSha256Signer : IOAuthSigner
{
    private readonly byte[] _liveSessionToken;

    /// <summary>
    /// Creates a new HMAC-SHA256 signer with the given Live Session Token bytes.
    /// </summary>
    public HmacSha256Signer(byte[] liveSessionToken)
    {
        _liveSessionToken = liveSessionToken;
    }

    /// <inheritdoc />
    public string SignatureMethod => "HMAC-SHA256";

    /// <inheritdoc />
    public string Sign(string baseString)
    {
        using var hmac = new HMACSHA256(_liveSessionToken);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return Convert.ToBase64String(hashBytes);
    }
}
```

- [ ] **Step 14: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "HmacSha256SignerTests"`

Expected: PASS

- [ ] **Step 15: Commit**

```bash
git add src/IbkrConduit/Auth/HmacSha256Signer.cs tests/IbkrConduit.Tests.Unit/Auth/HmacSha256SignerTests.cs
git commit -m "feat: add HmacSha256Signer for regular API request signing"
```

### Sub-task 1.3d: IBaseStringBuilder + StandardBaseStringBuilder

- [ ] **Step 16: Write failing tests for StandardBaseStringBuilder**

File: `tests/IbkrConduit.Tests.Unit/Auth/StandardBaseStringBuilderTests.cs`

```csharp
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class StandardBaseStringBuilderTests
{
    [Fact]
    public void Build_SortsParametersLexicographically()
    {
        var builder = new StandardBaseStringBuilder();
        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_token"] = "mytoken",
            ["oauth_consumer_key"] = "MYKEY",
            ["oauth_nonce"] = "abc123",
        };

        var result = builder.Build("GET", "https://api.ibkr.com/v1/api/test", parameters);

        // Parameters should be sorted: oauth_consumer_key, oauth_nonce, oauth_token
        // Joined: oauth_consumer_key=MYKEY&oauth_nonce=abc123&oauth_token=mytoken
        // Then QuotePlus-encoded, URL QuotePlus-encoded, joined with &
        var expectedParams = OAuthEncoding.QuotePlus(
            "oauth_consumer_key=MYKEY&oauth_nonce=abc123&oauth_token=mytoken");
        var expectedUrl = OAuthEncoding.QuotePlus("https://api.ibkr.com/v1/api/test");

        result.ShouldBe($"GET&{expectedUrl}&{expectedParams}");
    }

    [Fact]
    public void Build_UsesQuotePlusEncoding()
    {
        var builder = new StandardBaseStringBuilder();
        var parameters = new SortedDictionary<string, string>
        {
            ["key"] = "value with spaces",
        };

        var result = builder.Build("POST", "https://example.com/path", parameters);

        // Spaces in parameter values get encoded when the whole param string is QuotePlus'd
        result.ShouldContain("POST&");
        result.ShouldContain(OAuthEncoding.QuotePlus("https://example.com/path"));
    }
}
```

- [ ] **Step 17: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "StandardBaseStringBuilderTests"`

Expected: FAIL — types do not exist.

- [ ] **Step 18: Implement IBaseStringBuilder and StandardBaseStringBuilder**

File: `src/IbkrConduit/Auth/IBaseStringBuilder.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Strategy interface for OAuth base string construction.
/// </summary>
public interface IBaseStringBuilder
{
    /// <summary>
    /// Builds the OAuth base string from the HTTP method, URL, and sorted parameters.
    /// </summary>
    string Build(string method, string url, SortedDictionary<string, string> parameters);
}
```

File: `src/IbkrConduit/Auth/StandardBaseStringBuilder.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Builds the standard OAuth base string: METHOD&amp;encoded_url&amp;encoded_params.
/// Used for all regular API requests.
/// </summary>
public class StandardBaseStringBuilder : IBaseStringBuilder
{
    /// <inheritdoc />
    public string Build(string method, string url, SortedDictionary<string, string> parameters)
    {
        var paramString = string.Join("&",
            parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        var encodedUrl = OAuthEncoding.QuotePlus(url);
        var encodedParams = OAuthEncoding.QuotePlus(paramString);

        return $"{method}&{encodedUrl}&{encodedParams}";
    }
}
```

- [ ] **Step 19: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "StandardBaseStringBuilderTests"`

Expected: PASS

- [ ] **Step 20: Commit**

```bash
git add src/IbkrConduit/Auth/IBaseStringBuilder.cs src/IbkrConduit/Auth/StandardBaseStringBuilder.cs tests/IbkrConduit.Tests.Unit/Auth/StandardBaseStringBuilderTests.cs
git commit -m "feat: add IBaseStringBuilder and StandardBaseStringBuilder"
```

### Sub-task 1.3e: PrependingBaseStringBuilder

- [ ] **Step 21: Write failing test for PrependingBaseStringBuilder**

File: `tests/IbkrConduit.Tests.Unit/Auth/PrependingBaseStringBuilderTests.cs`

```csharp
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class PrependingBaseStringBuilderTests
{
    [Fact]
    public void Build_PrependHexBeforeStandardBaseString()
    {
        var decryptedSecret = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var builder = new PrependingBaseStringBuilder(decryptedSecret);
        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = "MYKEY",
        };

        var result = builder.Build(
            "POST", "https://api.ibkr.com/v1/api/oauth/live_session_token", parameters);

        // Should start with lowercase hex of the secret bytes
        result.ShouldStartWith("deadbeef");

        // The rest should be the standard base string
        var standard = new StandardBaseStringBuilder();
        var expectedStandard = standard.Build(
            "POST", "https://api.ibkr.com/v1/api/oauth/live_session_token", parameters);

        result.ShouldBe("deadbeef" + expectedStandard);
    }
}
```

- [ ] **Step 22: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "PrependingBaseStringBuilderTests"`

Expected: FAIL — `PrependingBaseStringBuilder` does not exist.

- [ ] **Step 23: Implement PrependingBaseStringBuilder**

File: `src/IbkrConduit/Auth/PrependingBaseStringBuilder.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Prepends the decrypted access token secret hex to the standard base string.
/// Used exclusively for LST requests.
/// </summary>
public class PrependingBaseStringBuilder : IBaseStringBuilder
{
    private readonly string _prependHex;
    private readonly StandardBaseStringBuilder _inner = new();

    /// <summary>
    /// Creates a prepending builder with the given decrypted access token secret bytes.
    /// </summary>
    public PrependingBaseStringBuilder(byte[] decryptedAccessTokenSecret)
    {
        _prependHex = Convert.ToHexString(decryptedAccessTokenSecret).ToLowerInvariant();
    }

    /// <inheritdoc />
    public string Build(string method, string url, SortedDictionary<string, string> parameters) =>
        _prependHex + _inner.Build(method, url, parameters);
}
```

- [ ] **Step 24: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "PrependingBaseStringBuilderTests"`

Expected: PASS

- [ ] **Step 25: Commit**

```bash
git add src/IbkrConduit/Auth/PrependingBaseStringBuilder.cs tests/IbkrConduit.Tests.Unit/Auth/PrependingBaseStringBuilderTests.cs
git commit -m "feat: add PrependingBaseStringBuilder for LST request signing"
```

### Sub-task 1.3f: OAuthHeaderBuilder

- [ ] **Step 26: Write failing tests for OAuthHeaderBuilder**

File: `tests/IbkrConduit.Tests.Unit/Auth/OAuthHeaderBuilderTests.cs`

```csharp
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthHeaderBuilderTests
{
    [Fact]
    public void Build_IncludesRealmAndSortedParams()
    {
        // Use a deterministic signer for testing
        var signer = new FakeSigner("HMAC-SHA256", "dGVzdHNpZw==");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var header = builder.Build(
            "GET",
            "https://api.ibkr.com/v1/api/portfolio/accounts",
            "MYKEY",
            "mytoken");

        // Should start with realm
        header.ShouldStartWith("OAuth realm=\"limited_poa\", ");

        // Should contain all oauth params in sorted order
        header.ShouldContain("oauth_consumer_key=\"MYKEY\"");
        header.ShouldContain("oauth_token=\"mytoken\"");
        header.ShouldContain("oauth_signature_method=\"HMAC-SHA256\"");
        header.ShouldContain("oauth_signature=");
        header.ShouldContain("oauth_nonce=");
        header.ShouldContain("oauth_timestamp=");
    }

    [Fact]
    public void Build_WithExtraParams_IncludesThemInHeader()
    {
        var signer = new FakeSigner("RSA-SHA256", "c2ln");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var extraParams = new Dictionary<string, string>
        {
            ["diffie_hellman_challenge"] = "abc123",
        };

        var header = builder.Build(
            "POST",
            "https://api.ibkr.com/v1/api/oauth/live_session_token",
            "MYKEY",
            "mytoken",
            extraParams);

        header.ShouldContain("diffie_hellman_challenge=\"abc123\"");

        // diffie_hellman_challenge should come before oauth_ params alphabetically
        var dhIndex = header.IndexOf("diffie_hellman_challenge");
        var oauthIndex = header.IndexOf("oauth_consumer_key");
        dhIndex.ShouldBeLessThan(oauthIndex);
    }

    [Fact]
    public void Build_SignatureIsQuotePlusEncoded()
    {
        // Signature containing chars that need encoding: + and /
        var signer = new FakeSigner("HMAC-SHA256", "a+b/c=");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var header = builder.Build("GET", "https://example.com", "KEY", "TOK");

        // The signature value in the header should be QuotePlus-encoded
        var encodedSig = OAuthEncoding.QuotePlus("a+b/c=");
        header.ShouldContain($"oauth_signature=\"{encodedSig}\"");
    }

    /// <summary>
    /// Test double that returns a fixed signature, bypassing real crypto.
    /// </summary>
    private class FakeSigner : IOAuthSigner
    {
        private readonly string _signature;

        public FakeSigner(string method, string signature)
        {
            SignatureMethod = method;
            _signature = signature;
        }

        public string SignatureMethod { get; }

        public string Sign(string baseString) => _signature;
    }
}
```

- [ ] **Step 27: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthHeaderBuilderTests"`

Expected: FAIL — `OAuthHeaderBuilder` does not exist.

- [ ] **Step 28: Implement OAuthHeaderBuilder**

File: `src/IbkrConduit/Auth/OAuthHeaderBuilder.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Composes an <see cref="IOAuthSigner"/> and <see cref="IBaseStringBuilder"/>
/// to produce the full OAuth Authorization header value.
/// </summary>
public class OAuthHeaderBuilder
{
    private readonly IOAuthSigner _signer;
    private readonly IBaseStringBuilder _baseStringBuilder;

    /// <summary>
    /// Creates a new header builder with the given signing and base string strategies.
    /// </summary>
    public OAuthHeaderBuilder(IOAuthSigner signer, IBaseStringBuilder baseStringBuilder)
    {
        _signer = signer;
        _baseStringBuilder = baseStringBuilder;
    }

    /// <summary>
    /// Builds the OAuth Authorization header value for the given request.
    /// </summary>
    /// <param name="method">HTTP method (GET, POST, etc.).</param>
    /// <param name="url">Full request URL.</param>
    /// <param name="consumerKey">IBKR consumer key.</param>
    /// <param name="accessToken">OAuth access token.</param>
    /// <param name="extraParams">Additional parameters (e.g., diffie_hellman_challenge).</param>
    /// <returns>The complete Authorization header value including "OAuth realm=...".</returns>
    public string Build(
        string method,
        string url,
        string consumerKey,
        string accessToken,
        IDictionary<string, string>? extraParams = null)
    {
        var nonce = OAuthEncoding.GenerateNonce();
        var timestamp = OAuthEncoding.GenerateTimestamp();

        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_token"] = accessToken,
            ["oauth_signature_method"] = _signer.SignatureMethod,
            ["oauth_nonce"] = nonce,
            ["oauth_timestamp"] = timestamp,
        };

        if (extraParams != null)
        {
            foreach (var (key, value) in extraParams)
            {
                parameters[key] = value;
            }
        }

        var baseString = _baseStringBuilder.Build(method, url, parameters);
        var signature = _signer.Sign(baseString);
        var encodedSignature = OAuthEncoding.QuotePlus(signature);

        // Build header: realm first, then all params (including signature) sorted alphabetically
        var headerParams = new SortedDictionary<string, string>(parameters)
        {
            ["oauth_signature"] = encodedSignature,
        };

        var paramPairs = headerParams.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"");
        return $"OAuth realm=\"limited_poa\", {string.Join(", ", paramPairs)}";
    }
}
```

- [ ] **Step 29: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthHeaderBuilderTests"`

Expected: PASS

- [ ] **Step 30: Run full build and test suite**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 31: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthHeaderBuilder.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthHeaderBuilderTests.cs
git commit -m "feat: add OAuthHeaderBuilder composing signer and base string strategies"
```

---

## Task 1.4: Live Session Token Client

**Branch:** `feat/m1-live-session-token-client`
**Depends on:** Task 1.3 merged
**PR scope:** `LiveSessionToken` record, `ILiveSessionTokenClient`, `LiveSessionTokenClient` + unit tests with mocked HTTP.

**Files:**
- Create: `src/IbkrConduit/Auth/LiveSessionToken.cs`
- Create: `src/IbkrConduit/Auth/ILiveSessionTokenClient.cs`
- Create: `src/IbkrConduit/Auth/LiveSessionTokenClient.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs`

### Sub-task 1.4a: LiveSessionToken record

- [ ] **Step 1: Write failing test for LiveSessionToken record**

File: `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs`

```csharp
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
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "LiveSessionTokenClientTests.LiveSessionToken_ShouldExposeProperties"`

Expected: FAIL — `LiveSessionToken` does not exist.

- [ ] **Step 3: Implement LiveSessionToken and ILiveSessionTokenClient**

File: `src/IbkrConduit/Auth/LiveSessionToken.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Represents an acquired Live Session Token with its expiration time.
/// </summary>
/// <param name="Token">The raw LST bytes used as HMAC key for signing API requests.</param>
/// <param name="Expiry">When this token expires.</param>
public record LiveSessionToken(byte[] Token, DateTimeOffset Expiry);
```

File: `src/IbkrConduit/Auth/ILiveSessionTokenClient.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Acquires a Live Session Token from the IBKR OAuth endpoint.
/// </summary>
public interface ILiveSessionTokenClient
{
    /// <summary>
    /// Performs the full LST acquisition flow: decrypt, DH exchange, sign, HTTP request,
    /// derive token, and validate.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(
        IbkrOAuthCredentials credentials, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "LiveSessionTokenClientTests.LiveSessionToken_ShouldExposeProperties"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Auth/LiveSessionToken.cs src/IbkrConduit/Auth/ILiveSessionTokenClient.cs tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs
git commit -m "feat: add LiveSessionToken record and ILiveSessionTokenClient interface"
```

### Sub-task 1.4b: LiveSessionTokenClient

- [ ] **Step 6: Write failing test for LiveSessionTokenClient with mocked HTTP**

This test exercises the full LST flow with a known cryptographic fixture. We need to carefully construct a test where we control both sides of the DH exchange.

Add to `LiveSessionTokenClientTests.cs`:

```csharp
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class LiveSessionTokenClientTests
{
    // ... existing test ...

    [Fact]
    public async Task GetLiveSessionTokenAsync_ValidResponse_ReturnsValidatedToken()
    {
        // === ARRANGE: Build a complete cryptographic fixture ===

        // Generate keys
        using var sigKey = RSA.Create(2048);
        using var encKey = RSA.Create(2048);

        // Known plaintext secret
        var plaintextSecret = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };

        // Encrypt it with the encryption public key (PKCS1) to simulate IBKR's portal output
        var encryptedSecret = encKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);
        var encryptedSecretB64 = Convert.ToBase64String(encryptedSecret);

        // Use a small DH prime for test speed: p = 9931 (a real prime)
        var prime = new BigInteger(9931);
        var consumerKey = "TESTKEY01";

        var creds = new IbkrOAuthCredentials(
            "tenant1", consumerKey, "accesstok", encryptedSecretB64,
            sigKey, encKey, prime);

        // Server-side DH: generate server's private key b and public key B
        var serverPrivate = new BigInteger(42);
        var serverPublic = BigInteger.ModPow(2, serverPrivate, prime);

        // We need to intercept the client's DH challenge to compute the shared secret
        // from the server's perspective. We'll use a custom HttpMessageHandler.
        BigInteger? capturedClientPublic = null;

        var handler = new FakeHttpHandler(request =>
        {
            // Extract diffie_hellman_challenge from the Authorization header
            var authHeader = request.Headers.Authorization!.Parameter!;
            var fullHeader = $"OAuth {authHeader}";

            // Parse DH challenge from header
            var dhMatch = System.Text.RegularExpressions.Regex.Match(
                fullHeader, @"diffie_hellman_challenge=""([^""]+)""");
            dhMatch.Success.ShouldBeTrue("DH challenge should be in Authorization header");
            var dhChallengeHex = dhMatch.Groups[1].Value;
            capturedClientPublic = BigInteger.Parse("0" + dhChallengeHex, NumberStyles.HexNumber);

            // Compute shared secret from server's perspective: K = A^b mod p
            var sharedSecret = BigInteger.ModPow(capturedClientPublic.Value, serverPrivate, prime);
            var sharedSecretBytes = OAuthCrypto.BigIntegerToByteArray(sharedSecret);

            // Derive LST the same way the client will
            var lstBytes = OAuthCrypto.DeriveLiveSessionToken(sharedSecretBytes, plaintextSecret);

            // Compute validation signature: HMAC-SHA1(key=lstBytes, data=UTF8(consumerKey))
            using var hmac = new HMACSHA1(lstBytes);
            var sigHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(consumerKey));
            var signatureHex = Convert.ToHexString(sigHash).ToLowerInvariant();

            // Build response
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
        result.Token.Length.ShouldBe(20); // HMAC-SHA1 output is 20 bytes
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

    private class FakeHttpHandler : HttpMessageHandler
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
```

- [ ] **Step 7: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "LiveSessionTokenClientTests.GetLiveSessionTokenAsync"`

Expected: FAIL — `LiveSessionTokenClient` does not exist.

- [ ] **Step 8: Implement LiveSessionTokenClient**

File: `src/IbkrConduit/Auth/LiveSessionTokenClient.cs`

```csharp
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace IbkrConduit.Auth;

/// <summary>
/// Orchestrates the full Live Session Token acquisition flow.
/// Uses a plain HttpClient (not the Refit pipeline) since the LST endpoint
/// has unique signing requirements.
/// </summary>
public class LiveSessionTokenClient : ILiveSessionTokenClient
{
    private const string LstEndpoint = "oauth/live_session_token";

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
        // 1. Decrypt access token secret
        var decryptedSecret = OAuthCrypto.DecryptAccessTokenSecret(
            credentials.EncryptionPrivateKey, credentials.EncryptedAccessTokenSecret);

        // 2. Generate DH key pair
        var (dhPrivateKey, dhPublicKey) = OAuthCrypto.GenerateDhKeyPair(credentials.DhPrime);
        var dhChallengeHex = dhPublicKey.ToString("x");

        // 3. Build signing components for LST request
        var signer = new RsaSha256Signer(credentials.SignaturePrivateKey);
        var baseStringBuilder = new PrependingBaseStringBuilder(decryptedSecret);
        var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        // 4. Build the full URL
        var url = new Uri(_httpClient.BaseAddress!, LstEndpoint).ToString();

        // 5. Build Authorization header
        var extraParams = new Dictionary<string, string>
        {
            ["diffie_hellman_challenge"] = dhChallengeHex,
        };
        var authHeader = headerBuilder.Build(
            "POST", url, credentials.ConsumerKey, credentials.AccessToken, extraParams);

        // 6. Send HTTP request
        using var request = new HttpRequestMessage(HttpMethod.Post, LstEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
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
        var theirPublicKey = BigInteger.Parse("0" + dhResponseHex, NumberStyles.HexNumber);
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
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "LiveSessionTokenClientTests"`

Expected: PASS

- [ ] **Step 10: Run full build and test suite**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 11: Commit**

```bash
git add src/IbkrConduit/Auth/LiveSessionTokenClient.cs tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs
git commit -m "feat: add LiveSessionTokenClient orchestrating full LST acquisition flow"
```

---

## Task 1.5: OAuthSigningHandler + HTTP Pipeline

**Branch:** `feat/m1-signing-handler-and-pipeline`
**Depends on:** Task 1.4 merged
**PR scope:** `ISessionTokenProvider`, `SessionTokenProvider`, `OAuthSigningHandler`, `ServiceCollectionExtensions`, NuGet dependency additions + unit tests.

**Files:**
- Modify: `Directory.Packages.props` (add Refit, Microsoft.Extensions.Http, Microsoft.Extensions.DependencyInjection.Abstractions)
- Modify: `src/IbkrConduit/IbkrConduit.csproj` (add PackageReferences)
- Create: `src/IbkrConduit/Auth/ISessionTokenProvider.cs`
- Create: `src/IbkrConduit/Auth/SessionTokenProvider.cs`
- Create: `src/IbkrConduit/Auth/OAuthSigningHandler.cs`
- Create: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/SessionTokenProviderTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs`

### Sub-task 1.5a: Add NuGet Dependencies

- [ ] **Step 1: Add package versions to Directory.Packages.props**

Add to the `<ItemGroup>` in `Directory.Packages.props`:

```xml
<!-- HTTP client and DI -->
<PackageVersion Include="Refit" Version="10.1.6" />
<PackageVersion Include="Refit.HttpClientFactory" Version="10.1.6" />
<PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.5" />
<PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
```

- [ ] **Step 2: Add PackageReferences to IbkrConduit.csproj**

Add to `src/IbkrConduit/IbkrConduit.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Refit" />
  <PackageReference Include="Refit.HttpClientFactory" />
  <PackageReference Include="Microsoft.Extensions.Http" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
</ItemGroup>
```

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build --configuration Release`

Expected: BUILD SUCCEEDED with zero warnings.

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props src/IbkrConduit/IbkrConduit.csproj
git commit -m "chore(deps): add Refit, Microsoft.Extensions.Http, DI.Abstractions"
```

### Sub-task 1.5b: ISessionTokenProvider + SessionTokenProvider

- [ ] **Step 5: Write failing tests for SessionTokenProvider**

File: `tests/IbkrConduit.Tests.Unit/Auth/SessionTokenProviderTests.cs`

```csharp
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class SessionTokenProviderTests
{
    [Fact]
    public async Task GetLiveSessionTokenAsync_FirstCall_AcquiresFromClient()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        result.ShouldBe(expectedToken);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_SecondCall_ReturnsCached()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result1 = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        var result2 = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        result1.ShouldBe(result2);
        client.CallCount.ShouldBe(1); // Only called once
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_ConcurrentCalls_OnlyAcquiresOnce()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken, delay: TimeSpan.FromMilliseconds(50));
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        // Launch concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.GetLiveSessionTokenAsync(CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All should get the same token
        foreach (var result in results)
        {
            result.ShouldBe(expectedToken);
        }

        client.CallCount.ShouldBe(1);
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = System.Security.Cryptography.RSA.Create(2048);
        var encKey = System.Security.Cryptography.RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new System.Numerics.BigInteger(23));
    }

    private class FakeLstClient : ILiveSessionTokenClient
    {
        private readonly LiveSessionToken _token;
        private readonly TimeSpan _delay;

        public FakeLstClient(LiveSessionToken token, TimeSpan delay = default)
        {
            _token = token;
            _delay = delay;
        }

        public int CallCount { get; private set; }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(
            IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }
            return _token;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionTokenProviderTests"`

Expected: FAIL — `SessionTokenProvider` does not exist.

- [ ] **Step 7: Implement ISessionTokenProvider and SessionTokenProvider**

File: `src/IbkrConduit/Auth/ISessionTokenProvider.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Abstracts Live Session Token acquisition and caching from the signing handler.
/// </summary>
public interface ISessionTokenProvider
{
    /// <summary>
    /// Gets the current Live Session Token, acquiring it if necessary.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);
}
```

File: `src/IbkrConduit/Auth/SessionTokenProvider.cs`

```csharp
namespace IbkrConduit.Auth;

/// <summary>
/// Lazy-acquires and caches the Live Session Token. Thread-safe via semaphore.
/// No refresh logic in M1 — the 24h validity is sufficient for validation.
/// </summary>
public class SessionTokenProvider : ISessionTokenProvider
{
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ILiveSessionTokenClient _lstClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private LiveSessionToken? _cached;

    /// <summary>
    /// Creates a new provider that will acquire the LST on first use.
    /// </summary>
    public SessionTokenProvider(IbkrOAuthCredentials credentials, ILiveSessionTokenClient lstClient)
    {
        _credentials = credentials;
        _lstClient = lstClient;
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
    {
        if (_cached != null)
        {
            return _cached;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock
            if (_cached != null)
            {
                return _cached;
            }

            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionTokenProviderTests"`

Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add src/IbkrConduit/Auth/ISessionTokenProvider.cs src/IbkrConduit/Auth/SessionTokenProvider.cs tests/IbkrConduit.Tests.Unit/Auth/SessionTokenProviderTests.cs
git commit -m "feat: add SessionTokenProvider with lazy acquisition and caching"
```

### Sub-task 1.5c: OAuthSigningHandler

- [ ] **Step 10: Write failing tests for OAuthSigningHandler**

File: `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs`

```csharp
using System.Net;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthSigningHandlerTests
{
    [Fact]
    public async Task SendAsync_AttachesAuthorizationHeader()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new FakeInnerHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await httpClient.GetAsync("portfolio/accounts");

        capturedRequest.ShouldNotBeNull();
        var authHeader = capturedRequest.Headers.Authorization;
        authHeader.ShouldNotBeNull();
        authHeader.Scheme.ShouldBe("OAuth");
        authHeader.Parameter.ShouldContain("oauth_consumer_key=\"MYKEY\"");
        authHeader.Parameter.ShouldContain("oauth_token=\"mytoken\"");
        authHeader.Parameter.ShouldContain("oauth_signature_method=\"HMAC-SHA256\"");
        authHeader.Parameter.ShouldContain("oauth_signature=");
        authHeader.Parameter.ShouldContain("realm=\"limited_poa\"");
    }

    [Fact]
    public async Task SendAsync_UsesHmacSha256Signing()
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var provider = new FakeTokenProvider(new LiveSessionToken(
            lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var signingHandler = new OAuthSigningHandler(provider, "MYKEY", "mytoken")
        {
            InnerHandler = innerHandler,
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };

        await httpClient.GetAsync("portfolio/accounts");

        // If we got here without exception, signing handler worked
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }

    private class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeInnerHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
```

- [ ] **Step 11: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthSigningHandlerTests"`

Expected: FAIL — `OAuthSigningHandler` does not exist.

- [ ] **Step 12: Implement OAuthSigningHandler**

File: `src/IbkrConduit/Auth/OAuthSigningHandler.cs`

```csharp
using System.Net.Http.Headers;

namespace IbkrConduit.Auth;

/// <summary>
/// DelegatingHandler that signs outgoing HTTP requests with OAuth HMAC-SHA256
/// using the Live Session Token.
/// </summary>
public class OAuthSigningHandler : DelegatingHandler
{
    private readonly ISessionTokenProvider _tokenProvider;
    private readonly string _consumerKey;
    private readonly string _accessToken;

    /// <summary>
    /// Creates a new signing handler.
    /// </summary>
    public OAuthSigningHandler(
        ISessionTokenProvider tokenProvider,
        string consumerKey,
        string accessToken)
    {
        _tokenProvider = tokenProvider;
        _consumerKey = consumerKey;
        _accessToken = accessToken;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var lst = await _tokenProvider.GetLiveSessionTokenAsync(cancellationToken);

        var signer = new HmacSha256Signer(lst.Token);
        var baseStringBuilder = new StandardBaseStringBuilder();
        var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var url = request.RequestUri!.ToString();
        var method = request.Method.Method;

        var authHeaderValue = headerBuilder.Build(method, url, _consumerKey, _accessToken);

        // Parse into scheme + parameter for the typed header
        // Format: "OAuth realm=\"limited_poa\", ..."
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "OAuth",
            authHeaderValue["OAuth ".Length..]);

        return await base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 13: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthSigningHandlerTests"`

Expected: PASS

- [ ] **Step 14: Commit**

```bash
git add src/IbkrConduit/Auth/OAuthSigningHandler.cs tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs
git commit -m "feat: add OAuthSigningHandler DelegatingHandler for HMAC-SHA256 request signing"
```

### Sub-task 1.5d: ServiceCollectionExtensions

- [ ] **Step 15: Implement ServiceCollectionExtensions**

File: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

```csharp
using IbkrConduit.Auth;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string IbkrBaseUrl = "https://api.ibkr.com/v1/api/";

    /// <summary>
    /// Registers the IBKR API client pipeline for the given tenant.
    /// Pipeline: Refit → OAuthSigningHandler → HttpClient → IBKR API.
    /// </summary>
    public static IServiceCollection AddIbkrClient<TApi>(
        this IServiceCollection services,
        IbkrOAuthCredentials credentials) where TApi : class
    {
        var clientName = $"IbkrApi-{credentials.TenantId}";

        // Register LST infrastructure
        services.AddSingleton<ILiveSessionTokenClient>(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(IbkrBaseUrl),
            };
            return new LiveSessionTokenClient(httpClient);
        });

        services.AddSingleton<ISessionTokenProvider>(sp =>
            new SessionTokenProvider(
                credentials,
                sp.GetRequiredService<ILiveSessionTokenClient>()));

        // Register Refit client with signing handler
        services.AddTransient<OAuthSigningHandler>(sp =>
            new OAuthSigningHandler(
                sp.GetRequiredService<ISessionTokenProvider>(),
                credentials.ConsumerKey,
                credentials.AccessToken));

        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(IbkrBaseUrl))
            .AddHttpMessageHandler<OAuthSigningHandler>();

        return services;
    }
}
```

- [ ] **Step 16: Run full build and test suite**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 17: Commit**

```bash
git add src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "feat: add AddIbkrClient DI extension for Refit + OAuth pipeline wiring"
```

---

## Task 1.6: Portfolio Accounts Endpoint + Paper Account Validation

**Branch:** `feat/m1-portfolio-accounts`
**Depends on:** Task 1.5 merged
**PR scope:** Refit interface, Account model, WireMock integration tests, paper account integration test (manual).

**Files:**
- Create: `src/IbkrConduit/Portfolio/IIbkrPortfolioApi.cs`
- Create: `src/IbkrConduit/Portfolio/Account.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Portfolio/PortfolioApiTests.cs`
- Create: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs`

### Sub-task 1.6a: Account Model + Refit Interface

- [ ] **Step 1: Write failing test for Account model deserialization**

File: `tests/IbkrConduit.Tests.Unit/Portfolio/PortfolioApiTests.cs`

```csharp
using System.Text.Json;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

public class PortfolioApiTests
{
    [Fact]
    public void Account_DeserializesFromJson()
    {
        var json = """
            {
                "id": "U1234567",
                "accountTitle": "Paper Trading Account",
                "type": "INDIVIDUAL"
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var account = JsonSerializer.Deserialize<Account>(json, options);

        account.ShouldNotBeNull();
        account.Id.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Paper Trading Account");
        account.Type.ShouldBe("INDIVIDUAL");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "PortfolioApiTests.Account_DeserializesFromJson"`

Expected: FAIL — `Account` type does not exist.

- [ ] **Step 3: Implement Account model and Refit interface**

File: `src/IbkrConduit/Portfolio/Account.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Represents an IBKR account from the /portfolio/accounts endpoint.
/// </summary>
public class Account
{
    /// <summary>
    /// The account identifier (e.g., "U1234567").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The account title/description.
    /// </summary>
    [JsonPropertyName("accountTitle")]
    public string AccountTitle { get; init; } = string.Empty;

    /// <summary>
    /// The account type (e.g., "INDIVIDUAL").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}
```

File: `src/IbkrConduit/Portfolio/IIbkrPortfolioApi.cs`

```csharp
using Refit;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Refit interface for IBKR portfolio endpoints.
/// </summary>
public interface IIbkrPortfolioApi
{
    /// <summary>
    /// Retrieves the list of accounts for the authenticated user.
    /// </summary>
    [Get("/portfolio/accounts")]
    Task<List<Account>> GetAccountsAsync();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "PortfolioApiTests.Account_DeserializesFromJson"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Portfolio/Account.cs src/IbkrConduit/Portfolio/IIbkrPortfolioApi.cs tests/IbkrConduit.Tests.Unit/Portfolio/PortfolioApiTests.cs
git commit -m "feat: add Account model and IIbkrPortfolioApi Refit interface"
```

### Sub-task 1.6b: WireMock Integration Tests

- [ ] **Step 6: Write WireMock integration test for full pipeline**

File: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs`

```csharp
using System.Net;
using IbkrConduit.Auth;
using IbkrConduit.Portfolio;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Portfolio;

public class PortfolioAccountsTests : IDisposable
{
    private readonly WireMockServer _server;

    public PortfolioAccountsTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetAccountsAsync_WithMockedServer_ReturnsAccountList()
    {
        // Arrange: mock the portfolio/accounts endpoint
        _server.Given(
            Request.Create()
                .WithPath("/portfolio/accounts")
                .UsingGet()
                .WithHeader("Authorization", "*"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL"
                            }
                        ]
                        """));

        // Build a minimal pipeline that skips real LST acquisition
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

        // Act
        var accounts = await api.GetAccountsAsync();

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");
        accounts[0].AccountTitle.ShouldBe("Paper Trading");
    }

    [Fact]
    public async Task GetAccountsAsync_Unauthorized_ThrowsApiException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var lstBytes = new byte[20];
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

        await Should.ThrowAsync<Refit.ApiException>(() => api.GetAccountsAsync());
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
```

- [ ] **Step 7: Run integration tests**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --filter "PortfolioAccountsTests"`

Expected: PASS

- [ ] **Step 8: Run full build and test suite**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 9: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs
git commit -m "feat: add WireMock integration tests for portfolio accounts pipeline"
```

### Sub-task 1.6c: Paper Account Integration Test (Manual)

- [ ] **Step 10: Add the real-IBKR integration test (skipped by default)**

Add to `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs`:

```csharp
/// <summary>
/// End-to-end test against a real IBKR paper account.
/// Requires IBKR_* environment variables to be set.
/// Run manually: dotnet test --filter "PortfolioAccounts_WithPaperAccount_ReturnsAccountList"
/// </summary>
[Fact(Skip = "Requires real IBKR paper account credentials in environment variables")]
public async Task PortfolioAccounts_WithPaperAccount_ReturnsAccountList()
{
    using var creds = OAuthCredentialsFactory.FromEnvironment();

    using var lstHttpClient = new HttpClient
    {
        BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
    };
    var lstClient = new LiveSessionTokenClient(lstHttpClient);
    var tokenProvider = new SessionTokenProvider(creds, lstClient);

    var signingHandler = new OAuthSigningHandler(tokenProvider, creds.ConsumerKey, creds.AccessToken)
    {
        InnerHandler = new HttpClientHandler(),
    };

    using var httpClient = new HttpClient(signingHandler)
    {
        BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
    };

    var api = Refit.RestService.For<IIbkrPortfolioApi>(httpClient);

    var accounts = await api.GetAccountsAsync();

    accounts.ShouldNotBeNull();
    accounts.ShouldNotBeEmpty();
    accounts[0].Id.ShouldNotBeNullOrWhiteSpace();
}
```

- [ ] **Step 11: Verify the skip annotation works**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --filter "PortfolioAccountsTests"`

Expected: The paper account test is skipped, WireMock tests pass.

- [ ] **Step 12: Run final full check**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

Expected: All pass with zero warnings.

- [ ] **Step 13: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs
git commit -m "feat: add paper account integration test (skipped, requires IBKR credentials)"
```

- [ ] **Step 14: Update implementation status**

Update `docs/implementation-status.md` to mark Tasks 1.1–1.6 as "Done" with links to their specs.

```bash
git add docs/implementation-status.md
git commit -m "docs: update implementation status for M1 completion"
```

---

## Post-Plan Notes

### Manual Validation

After all tasks are merged, remove the `Skip` annotation from the paper account integration test and run it with real IBKR credentials to prove the full pipeline works end-to-end. This is the M1 success criteria.

### Package Version Summary

| Package | Version |
|---|---|
| Refit | 10.1.6 |
| Refit.HttpClientFactory | 10.1.6 |
| Microsoft.Extensions.Http | 10.0.5 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 |

### Known Design Decisions

1. **Nonce generation on .NET 8**: Uses `RandomNumberGenerator.Fill` + modulo selection instead of `RandomNumberGenerator.GetString` (which requires .NET 9+), ensuring compatibility with the net8.0 target.
2. **LiveSessionTokenClient uses plain HttpClient**: Not the Refit pipeline, since the LST endpoint has unique RSA-SHA256 signing and prepending requirements.
3. **SessionTokenProvider has no refresh logic in M1**: The 24h LST validity is sufficient. M2 adds proactive refresh and reactive 401 retry.
4. **FakeHttpHandler/FakeTokenProvider in tests**: Lightweight test doubles avoid needing a mocking library. If this becomes unwieldy in later milestones, consider adding NSubstitute.
