# RSA Key Fixture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Share a single pair of 2048-bit RSA keys across all tests within each slow test class using xUnit's `IClassFixture<T>`, cutting ~8–11 seconds of redundant key generation from the unit test suite.

**Architecture:** A new `RsaKeyFixture` class generates one signature key and one encryption key in its constructor. Each of three test classes declares `IClassFixture<RsaKeyFixture>` and receives the fixture via constructor injection, replacing per-test `RSA.Create(2048)` calls with references to `_fixture.SignatureKey` / `_fixture.EncryptionKey`. One test (`Dispose_ShouldDisposeRsaKeys`) intentionally keeps its own fresh keys because it tests that the credential object disposes the keys it is given — using shared keys there would corrupt the fixture for subsequent tests.

**Tech Stack:** C# / .NET 10, xUnit v3, `System.Security.Cryptography.RSA`

---

## File Map

| Action | Path |
|--------|------|
| Create | `tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs` |
| Modify | `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs` |
| Modify | `tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs` |
| Modify | `tests/IbkrConduit.Tests.Unit/Setup/CredentialFileTests.cs` |

---

## Task 1: Create RsaKeyFixture

**Files:**
- Create: `tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs`

- [ ] **Step 1: Create the fixture file**

```csharp
// tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs
using System.Security.Cryptography;

namespace IbkrConduit.Tests.Unit.Helpers;

public sealed class RsaKeyFixture : IDisposable
{
    public RSA SignatureKey { get; } = RSA.Create(2048);
    public RSA EncryptionKey { get; } = RSA.Create(2048);

    public void Dispose()
    {
        SignatureKey.Dispose();
        EncryptionKey.Dispose();
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build --project tests/IbkrConduit.Tests.Unit --configuration Release`

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs
git commit -m "test: add RsaKeyFixture for shared RSA key generation"
```

---

## Task 2: Update LiveSessionTokenClientTests

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs`

7 of the 8 tests use `using var sigKey = RSA.Create(2048)` and `using var encKey = RSA.Create(2048)`. Replace all of them with references to the fixture. `LiveSessionToken_ShouldExposeProperties` has no RSA keys and needs no change.

- [ ] **Step 1: Add fixture import, field, and constructor; update class declaration**

Replace the class opening:
```csharp
// BEFORE
public class LiveSessionTokenClientTests
{
```

With:
```csharp
// AFTER
using IbkrConduit.Tests.Unit.Helpers;

public class LiveSessionTokenClientTests : IClassFixture<RsaKeyFixture>
{
    private readonly RsaKeyFixture _fixture;

    public LiveSessionTokenClientTests(RsaKeyFixture fixture) => _fixture = fixture;
```

(The `using` goes at the top of the file with the other `using` statements.)

- [ ] **Step 2: Remove RSA key creation from GetLiveSessionTokenAsync_ValidResponse_ReturnsValidatedToken**

Replace:
```csharp
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);

// Known plaintext secret
```

With:
```csharp
// Known plaintext secret
```

Then replace all uses of `sigKey` and `encKey` in that test with `_fixture.SignatureKey` and `_fixture.EncryptionKey`:
```csharp
var creds = new IbkrOAuthCredentials(
    "tenant1", consumerKey, "accesstok", encryptedSecretB64,
    _fixture.SignatureKey, _fixture.EncryptionKey, prime);
```

And:
```csharp
var encryptedSecret = _fixture.EncryptionKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);
```

- [ ] **Step 3: Remove RSA key creation from the remaining 6 tests**

For each of these tests, remove the two `using var` lines and replace `sigKey`/`encKey` references with `_fixture.SignatureKey`/`_fixture.EncryptionKey`. The pattern is identical in all six:

**GetLiveSessionTokenAsync_InvalidSignature_ThrowsCryptographicException:**
```csharp
// Remove:
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);

var plaintextSecret = new byte[] { 0x01, 0x02 };
var encryptedSecret = encKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);

var creds = new IbkrOAuthCredentials(
    "tenant1", "TESTKEY01", "accesstok",
    Convert.ToBase64String(encryptedSecret),
    sigKey, encKey, new BigInteger(9931));

// Becomes:
var plaintextSecret = new byte[] { 0x01, 0x02 };
var encryptedSecret = _fixture.EncryptionKey.Encrypt(plaintextSecret, RSAEncryptionPadding.Pkcs1);

var creds = new IbkrOAuthCredentials(
    "tenant1", "TESTKEY01", "accesstok",
    Convert.ToBase64String(encryptedSecret),
    _fixture.SignatureKey, _fixture.EncryptionKey, new BigInteger(9931));
```

Apply the same transformation to:
- `GetLiveSessionTokenAsync_Non2xxResponse_ThrowsHttpRequestException`
- `GetLiveSessionTokenAsync_MalformedJson_ThrowsJsonException`
- `GetLiveSessionTokenAsync_MissingDhResponse_ThrowsKeyNotFoundException`
- `GetLiveSessionTokenAsync_MissingSignature_ThrowsKeyNotFoundException`
- `GetLiveSessionTokenAsync_InvalidHexDhResponse_ThrowsFormatException`

All six have the same structure: two `using var` RSA lines, one `Encrypt` call using `encKey`, and one `IbkrOAuthCredentials` constructor using both keys.

- [ ] **Step 4: Run tests for this class**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*LiveSessionTokenClientTests*"`

Expected: 8 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs
git commit -m "test: use RsaKeyFixture in LiveSessionTokenClientTests"
```

---

## Task 3: Update OAuthCredentialsFactoryTests

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs`

**Important:** `Dispose_ShouldDisposeRsaKeys` tests that `IbkrOAuthCredentials.Dispose()` disposes the RSA keys passed to it. It must create its own fresh keys — using shared fixture keys here would dispose them and break all subsequent tests in the class. Leave that test unchanged.

Tests to update: `IbkrOAuthCredentials_ShouldExposeAllProperties`, `FromEnvironment_AllVarsSet_ReturnsCredentials`, `FromEnvironment_WithTenantId_UsesTenantId`.

Tests to leave unchanged: `Dispose_ShouldDisposeRsaKeys`, `FromEnvironment_MissingRequired_ThrowsInvalidOperation`.

- [ ] **Step 1: Add fixture import, field, and constructor; update class declaration**

Replace:
```csharp
public class OAuthCredentialsFactoryTests
{
```

With:
```csharp
using IbkrConduit.Tests.Unit.Helpers;

public class OAuthCredentialsFactoryTests : IClassFixture<RsaKeyFixture>
{
    private readonly RsaKeyFixture _fixture;

    public OAuthCredentialsFactoryTests(RsaKeyFixture fixture) => _fixture = fixture;
```

- [ ] **Step 2: Update IbkrOAuthCredentials_ShouldExposeAllProperties**

Replace:
```csharp
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
```

With:
```csharp
var prime = BigInteger.Parse("023", System.Globalization.NumberStyles.HexNumber);

using var creds = new IbkrOAuthCredentials(
    TenantId: "tenant1",
    ConsumerKey: "TESTCONS",
    AccessToken: "token123",
    EncryptedAccessTokenSecret: "c2VjcmV0",
    SignaturePrivateKey: _fixture.SignatureKey,
    EncryptionPrivateKey: _fixture.EncryptionKey,
    DhPrime: prime);

creds.TenantId.ShouldBe("tenant1");
creds.ConsumerKey.ShouldBe("TESTCONS");
creds.AccessToken.ShouldBe("token123");
creds.EncryptedAccessTokenSecret.ShouldBe("c2VjcmV0");
creds.SignaturePrivateKey.ShouldBe(_fixture.SignatureKey);
creds.EncryptionPrivateKey.ShouldBe(_fixture.EncryptionKey);
creds.DhPrime.ShouldBe(prime);
```

Note: `using var creds` is fine — `IbkrOAuthCredentials.Dispose()` disposes the keys it owns, but here it doesn't own the fixture keys (they were constructed externally). Actually, `IbkrOAuthCredentials` does call `Dispose()` on its keys unconditionally. To avoid disposing the fixture keys, do NOT use `using var creds` here — just `var creds`:

```csharp
var creds = new IbkrOAuthCredentials(
    TenantId: "tenant1",
    ConsumerKey: "TESTCONS",
    AccessToken: "token123",
    EncryptedAccessTokenSecret: "c2VjcmV0",
    SignaturePrivateKey: _fixture.SignatureKey,
    EncryptionPrivateKey: _fixture.EncryptionKey,
    DhPrime: prime);
```

- [ ] **Step 3: Update FromEnvironment_AllVarsSet_ReturnsCredentials**

Replace:
```csharp
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);
var sigPem = sigKey.ExportRSAPrivateKeyPem();
var encPem = encKey.ExportRSAPrivateKeyPem();
```

With:
```csharp
var sigPem = _fixture.SignatureKey.ExportRSAPrivateKeyPem();
var encPem = _fixture.EncryptionKey.ExportRSAPrivateKeyPem();
```

- [ ] **Step 4: Update FromEnvironment_WithTenantId_UsesTenantId**

Replace:
```csharp
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);
var sigPem = sigKey.ExportRSAPrivateKeyPem();
var encPem = encKey.ExportRSAPrivateKeyPem();
```

With:
```csharp
var sigPem = _fixture.SignatureKey.ExportRSAPrivateKeyPem();
var encPem = _fixture.EncryptionKey.ExportRSAPrivateKeyPem();
```

- [ ] **Step 5: Run tests for this class**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*OAuthCredentialsFactoryTests*"`

Expected: 5 passed, 0 failed.

- [ ] **Step 6: Commit**

```
git add tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs
git commit -m "test: use RsaKeyFixture in OAuthCredentialsFactoryTests"
```

---

## Task 4: Update CredentialFileTests

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Setup/CredentialFileTests.cs`

`CredentialFileTests` already implements `IDisposable` (to delete a temp directory). xUnit supports both `IClassFixture<T>` and `IDisposable` on the same test class simultaneously — the fixture lifetime is managed separately by xUnit.

Tests to update: `Write_CreatesValidJsonFile`, `Read_RoundTrips`.
Tests to leave unchanged: `Read_MissingFile_Throws`, `Read_MissingField_Throws`.

**Important:** Do NOT pass the fixture keys to `CredentialFile.Write` and then `Dispose` them — the fixture keys must outlive all tests in the class. The tests only call `sigKey.ExportRSAPrivateKeyPem()` and `encKey.ExportRSAPrivateKeyPem()`, which are read-only operations and safe to call on shared keys.

- [ ] **Step 1: Add fixture import, field, and constructor; update class declaration**

Replace:
```csharp
public class CredentialFileTests : IDisposable
{
    private readonly string _tempDir;

    public CredentialFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ibkr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }
```

With:
```csharp
using IbkrConduit.Tests.Unit.Helpers;

public class CredentialFileTests : IClassFixture<RsaKeyFixture>, IDisposable
{
    private readonly string _tempDir;
    private readonly RsaKeyFixture _fixture;

    public CredentialFileTests(RsaKeyFixture fixture)
    {
        _fixture = fixture;
        _tempDir = Path.Combine(Path.GetTempPath(), $"ibkr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }
```

- [ ] **Step 2: Update Write_CreatesValidJsonFile**

Replace:
```csharp
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);
var filePath = Path.Combine(_tempDir, "creds.json");

CredentialFile.Write(
    filePath,
    consumerKey: "XKVMTQWLR",
    accessToken: "mytoken123",
    accessTokenSecret: "c2VjcmV0",
    signaturePrivateKeyPem: sigKey.ExportRSAPrivateKeyPem(),
    encryptionPrivateKeyPem: encKey.ExportRSAPrivateKeyPem(),
    dhPrimeHex: "FF");
```

With:
```csharp
var filePath = Path.Combine(_tempDir, "creds.json");

CredentialFile.Write(
    filePath,
    consumerKey: "XKVMTQWLR",
    accessToken: "mytoken123",
    accessTokenSecret: "c2VjcmV0",
    signaturePrivateKeyPem: _fixture.SignatureKey.ExportRSAPrivateKeyPem(),
    encryptionPrivateKeyPem: _fixture.EncryptionKey.ExportRSAPrivateKeyPem(),
    dhPrimeHex: "FF");
```

- [ ] **Step 3: Update Read_RoundTrips**

Replace:
```csharp
using var sigKey = RSA.Create(2048);
using var encKey = RSA.Create(2048);
var filePath = Path.Combine(_tempDir, "creds.json");
var sigPem = sigKey.ExportRSAPrivateKeyPem();
var encPem = encKey.ExportRSAPrivateKeyPem();
```

With:
```csharp
var filePath = Path.Combine(_tempDir, "creds.json");
var sigPem = _fixture.SignatureKey.ExportRSAPrivateKeyPem();
var encPem = _fixture.EncryptionKey.ExportRSAPrivateKeyPem();
```

- [ ] **Step 4: Run tests for this class**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*CredentialFileTests*"`

Expected: 4 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add tests/IbkrConduit.Tests.Unit/Setup/CredentialFileTests.cs
git commit -m "test: use RsaKeyFixture in CredentialFileTests"
```

---

## Task 5: Verify full suite and measure timing

**Files:** none

- [ ] **Step 1: Run the full unit test suite with timing**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --output Detailed`

Expected: All tests pass. Total runtime should be significantly less than the previous 11m 42s.

- [ ] **Step 2: Confirm no regressions**

The summary line should show the same test count as before (all passing, none skipped that weren't before). If any test fails, investigate before proceeding.
