# RSA Key Fixture — Design Spec

**Date:** 2026-04-28
**Status:** Approved

## Problem

Three unit test classes generate fresh 2048-bit RSA keys on every test method, making them the primary contributor to the 11-minute unit test suite runtime:

| Class | RSA.Create calls | Estimated cost |
|-------|-----------------|----------------|
| `LiveSessionTokenClientTests` | 2 per test × 8 tests = 16 | ~4–6s |
| `OAuthCredentialsFactoryTests` | 2 per test × 4 tests = 8 | ~2–3s |
| `CredentialFileTests` | 2 per test × 3 tests = 6 | ~1.5–2s |

Total wasted time: ~8–11 seconds of pure key generation that provides no additional test coverage.

## Goal

Share a single pair of 2048-bit RSA keys across all tests within each class using xUnit's `IClassFixture<T>` pattern, without changing production code or reducing key strength.

## Non-Goals

- Reducing key size (would change what is being tested)
- Sharing keys across multiple test classes (adds coupling, marginal benefit since classes run in parallel)
- Mocking crypto operations

## Design

### `RsaKeyFixture`

A new class in `tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs`:

```csharp
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

xUnit constructs this once before the first test in a class runs and disposes it after the last test completes.

### Test Class Changes

Each of the three test classes:

1. Adds `IClassFixture<RsaKeyFixture>` to its class declaration
2. Accepts `RsaKeyFixture fixture` via constructor and stores it in a field
3. Replaces every in-method `RSA.Create(2048)` call with `_fixture.SignatureKey` or `_fixture.EncryptionKey`
4. Removes the `using var` declarations (the fixture owns lifetime)

### Safety

All usages of RSA keys in these tests are read-only (signing, exporting public key bytes, passing as credential parameters). No test calls `ImportParameters`, `ImportRSAPrivateKey`, or any method that mutates key state. Sharing is safe.

## Files Changed

- `tests/IbkrConduit.Tests.Unit/Helpers/RsaKeyFixture.cs` — new file
- `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs` — use fixture
- `tests/IbkrConduit.Tests.Unit/Auth/OAuthCredentialsFactoryTests.cs` — use fixture
- `tests/IbkrConduit.Tests.Unit/Setup/CredentialFileTests.cs` — use fixture

## Expected Outcome

- All tests continue to pass with identical behavior
- Unit test suite runtime drops by ~8–11 seconds
- No production code changes
