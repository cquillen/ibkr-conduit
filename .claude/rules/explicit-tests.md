# Explicit Tests

Some tests are marked `[Fact(Explicit = true)]` and are skipped in normal test runs because they are intentionally slow or resource-intensive. They must be run manually when the relevant production code changes.

## KeyGenerator tests

**When to run:** Any time you modify files under `tools/IbkrConduit.Setup/` — specifically `KeyGenerator.cs`, `CredentialFile.cs`, or any command that invokes key/DH generation.

**How to run:**

```bash
dotnet test --project tests/IbkrConduit.Tests.Unit --explicit only --filter-class "*KeyGeneratorTests*"
```

These tests generate actual cryptographic DH prime numbers and take ~60 seconds. They verify that:
- RSA key pairs are 2048-bit and the public/private keys match
- DH parameters are DER-encoded correctly (positive signed integer with leading 0x00 byte)
- The PEM prime matches the hex stored in the credential JSON
- The DH generator G is 2 (matching IBKR's server and the runtime `OAuthCrypto` code)
