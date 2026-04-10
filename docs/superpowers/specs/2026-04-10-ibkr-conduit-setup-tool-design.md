# ibkr-conduit-setup — OAuth Credential Setup Tool Design Spec

## Goal

Build a `dotnet tool` that guides users through the full IBKR OAuth 1.0a credential setup flow: generate cryptographic keys, walk through the IBKR portal configuration, collect portal outputs, write a single JSON credentials file, and validate the credentials against the live API. No OpenSSL dependency — all crypto via `System.Security.Cryptography`.

## Background

The IBKR OAuth 1.0a setup is the biggest friction point for new users. It requires generating RSA key pairs and DH parameters, uploading public keys to the IBKR Self-Service Portal, and correctly copying the consumer key, access token, and encrypted access token secret. The existing bash script (`tools/generate-oauth-keys.sh`) only handles key generation and requires OpenSSL. There's no validation step, so users don't discover configuration errors until their first API call fails with cryptic `CryptographicException` or `HttpRequestException` messages.

## Installation & Usage

```bash
# Install globally
dotnet tool install -g IbkrConduit.Setup

# Full wizard (default — first-time users)
ibkr-conduit-setup

# Individual steps (for key rotation, re-configuration, or CI)
ibkr-conduit-setup generate-keys [--output <dir>]
ibkr-conduit-setup configure [--credentials <dir>] [--consumer-key <key>] [--access-token <token>] [--access-token-secret <secret>]
ibkr-conduit-setup validate --credentials <path-to-json>
```

## Project Structure

```
tools/IbkrConduit.Setup/
├── IbkrConduit.Setup.csproj      # dotnet tool, packable as NuGet tool package
├── Program.cs                     # entry point, command routing
├── Commands/
│   ├── WizardCommand.cs           # default full flow (Steps 1-4)
│   ├── GenerateKeysCommand.cs     # standalone key generation
│   ├── ConfigureCommand.cs        # portal walkthrough + credential file creation
│   └── ValidateCommand.cs         # test credentials against IBKR API
├── KeyGenerator.cs                # RSA key pair + DH prime generation
└── CredentialFile.cs              # JSON read/write for ibkr-credentials.json
```

The tool project references `IbkrConduit.csproj` for the `OAuthCredentialsFactory.FromFile()` method and for running the validation step via the library's full DI pipeline.

## Wizard Flow

The default (no-args) command runs all 4 steps in sequence:

### Step 1: Generate Cryptographic Keys

- Prompts for output directory (default: `./ibkr-credentials/`)
- Generates two RSA 2048-bit key pairs using `RSA.Create(2048)`
- Exports private keys as PEM via `rsa.ExportRSAPrivateKeyPem()`
- Exports public keys as PEM via `rsa.ExportRSAPublicKeyPem()`
- Writes the RFC 3526 Group 14 DH prime (2048-bit constant, same as `OAuthCrypto.Rfc3526Group14Prime`) to `dhparam.pem` in the format IBKR expects
- Outputs 3 files for portal upload: `public_signature.pem`, `public_encryption.pem`, `dhparam.pem`
- Keeps 2 private key files in memory (written to the JSON file in Step 3, not to separate PEM files)

### Step 2: Portal Configuration Instructions

Interactive guidance:
1. Log into the IBKR portal
2. Navigate to Settings → User Settings → API → OAuth
3. Upload the 3 public files
4. Click "Create" to generate credentials
5. Copy the Consumer Key, Access Token, and Encrypted Access Token Secret

Pauses for user to complete the portal steps.

### Step 3: Collect Portal Outputs

Prompts for:
- **Consumer Key** (validated: exactly 9 alphanumeric characters)
- **Access Token** (validated: non-empty)
- **Encrypted Access Token Secret** (validated: valid base64)

Writes `ibkr-credentials.json` to the output directory with all credentials.

### Step 4: Validate Connection

Loads credentials from the JSON file using `OAuthCredentialsFactory.FromFile()`, builds a full `IIbkrClient` via `AddIbkrClient`, calls `ValidateConnectionAsync()`. Reports:
- Success: "Your credentials are valid."
- Failure: Specific guidance based on the `IbkrConfigurationException.CredentialHint` (e.g., "verify SignaturePrivateKey matches the key registered in the IBKR portal")

Prints usage example and security warnings.

## Subcommands

### `generate-keys`

```
ibkr-conduit-setup generate-keys [--output <dir>]
```

Runs Step 1 only. Default output: `./ibkr-credentials/`. Produces the 3 public files for portal upload. Stores private keys as PEM files in the same directory (`private_signature.pem`, `private_encryption.pem`) since the JSON file doesn't exist yet.

If the output directory already exists and contains key files, prompts for overwrite confirmation (or `--force` to skip).

### `configure`

```
ibkr-conduit-setup configure [--credentials <dir>] [--consumer-key <key>] [--access-token <token>] [--access-token-secret <secret>]
```

Runs Steps 2-3. Reads existing private key PEM files from the credentials directory. Prompts for portal outputs interactively, or accepts them via flags for CI/automation.

Writes `ibkr-credentials.json` combining the private keys from the PEM files + the portal credentials.

### `validate`

```
ibkr-conduit-setup validate --credentials <path-to-json>
```

Runs Step 4 only. Loads the JSON credentials file, attempts LST acquisition + session initialization + auth status check via the library's full pipeline. Reports success or specific failure with guidance.

## JSON Credential File Format

```json
{
  "consumerKey": "TESTKEY01",
  "accessToken": "abc123def456",
  "accessTokenSecret": "base64-encrypted-secret...",
  "signaturePrivateKey": "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAK...\n-----END RSA PRIVATE KEY-----",
  "encryptionPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAK...\n-----END RSA PRIVATE KEY-----",
  "dhPrime": "FFFFFFFFFFFFFFFFC90FDAA2...long hex string..."
}
```

- Private keys stored as PEM strings (human-readable, standard format, `RSA.ImportFromPem()` reads directly)
- DH prime stored as hex string (parsed via `BigInteger.Parse("0" + hex, HexNumber)`)
- Access token secret stored as the base64-encoded encrypted value from the portal (decrypted at runtime by the library using the encryption private key)
- File should be treated as a secret — never committed to source control

## Library Changes

### New: `OAuthCredentialsFactory.FromFile(string path)`

Added to `src/IbkrConduit/Auth/OAuthCredentialsFactory.cs`:

```csharp
/// <summary>
/// Loads OAuth credentials from a JSON file produced by the ibkr-conduit-setup tool.
/// </summary>
/// <param name="path">Path to the ibkr-credentials.json file.</param>
/// <returns>A new <see cref="IbkrOAuthCredentials"/> instance. The caller owns disposal.</returns>
public static IbkrOAuthCredentials FromFile(string path)
{
    var json = File.ReadAllText(path);
    var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    var consumerKey = root.GetProperty("consumerKey").GetString()!;
    var accessToken = root.GetProperty("accessToken").GetString()!;
    var accessTokenSecret = root.GetProperty("accessTokenSecret").GetString()!;
    var signatureKeyPem = root.GetProperty("signaturePrivateKey").GetString()!;
    var encryptionKeyPem = root.GetProperty("encryptionPrivateKey").GetString()!;
    var dhPrimeHex = root.GetProperty("dhPrime").GetString()!;

    var signatureKey = RSA.Create();
    signatureKey.ImportFromPem(signatureKeyPem);

    var encryptionKey = RSA.Create();
    encryptionKey.ImportFromPem(encryptionKeyPem);

    var dhPrime = BigInteger.Parse("0" + dhPrimeHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    return new IbkrOAuthCredentials(
        consumerKey, consumerKey, accessToken, accessTokenSecret,
        signatureKey, encryptionKey, dhPrime);
}
```

This is the only change to the main library package. Everything else is in the tool.

## Key Generation (No OpenSSL)

All cryptography uses `System.Security.Cryptography`. No OpenSSL, no external dependencies. Works on Windows, macOS, and Linux.

### RSA Key Pairs

```csharp
var rsa = RSA.Create(2048);
var privatePem = rsa.ExportRSAPrivateKeyPem();   // PKCS#1 PEM
var publicPem = rsa.ExportRSAPublicKeyPem();     // PKCS#1 PEM
```

Both `ExportRSAPrivateKeyPem()` and `ExportRSAPublicKeyPem()` are available in .NET 7+. They produce standard PKCS#1 PEM with `-----BEGIN RSA PRIVATE KEY-----` / `-----BEGIN RSA PUBLIC KEY-----` armor.

Two key pairs are generated: one for signing (OAuth signature), one for encryption (access token secret decryption).

### DH Parameters PEM

The RFC 3526 Group 14 2048-bit prime is a constant already used by the library (`OAuthCrypto.Rfc3526Group14Prime`). No random DH parameter generation is needed — the prime is fixed by the RFC.

IBKR expects a PEM file in the standard OpenSSL `dhparam` format:

```
-----BEGIN DH PARAMETERS-----
<base64-encoded ASN.1 DER>
-----END DH PARAMETERS-----
```

The DER content encodes an ASN.1 `DHParameter` structure per RFC 3279:

```asn1
DHParameter ::= SEQUENCE {
    prime     INTEGER,   -- the 2048-bit prime (256 bytes)
    generator INTEGER    -- always 2 for Group 14
}
```

.NET does not have a built-in "export DH parameters to PEM" method. The tool constructs the DER bytes manually using `System.Formats.Asn1.AsnWriter`:

```csharp
using System.Formats.Asn1;
using System.Numerics;

static byte[] EncodeDhParametersDer(BigInteger prime, int generator = 2)
{
    var writer = new AsnWriter(AsnEncodingRules.DER);
    writer.PushSequence();
    writer.WriteInteger(prime);
    writer.WriteInteger(generator);
    writer.PopSequence();
    return writer.Encode();
}

static string EncodeDhParametersPem(BigInteger prime)
{
    var der = EncodeDhParametersDer(prime);
    var base64 = Convert.ToBase64String(der);

    var sb = new StringBuilder();
    sb.AppendLine("-----BEGIN DH PARAMETERS-----");
    for (var i = 0; i < base64.Length; i += 64)
    {
        sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
    }
    sb.AppendLine("-----END DH PARAMETERS-----");
    return sb.ToString();
}
```

The output should be byte-identical to `openssl dhparam` for the same prime. This can be verified in tests by comparing against a known-good PEM produced by OpenSSL for the RFC 3526 Group 14 prime.

### DH Prime Hex for JSON

The JSON credentials file stores the prime as a hex string (matching the `IBKR_DH_PRIME` env var format). The tool extracts it from the same constant:

```csharp
var dhPrimeHex = OAuthCrypto.Rfc3526Group14Prime.ToString("X");
// or derive from the BigInteger directly
```

This is written to the `dhPrime` field in `ibkr-credentials.json` and to `dhparam.pem` (DER-encoded) for portal upload. Both represent the same prime in different formats.

## Input Validation

| Input | Validation | Error Message |
|-------|-----------|---------------|
| Consumer key | Exactly 9 alphanumeric chars | "Consumer key must be exactly 9 alphanumeric characters (got {n}). Check the value in the IBKR portal." |
| Access token | Non-empty | "Access token cannot be empty. Copy the full token from the IBKR portal." |
| Access token secret | Valid base64 | "Access token secret is not valid base64. Make sure you copied the entire value from the portal without extra whitespace." |
| Output directory | Writable | "Cannot write to {path}: {reason}" |
| Existing files | Prompt overwrite | "Files already exist in {dir}. Overwrite? [y/N]" (or `--force`) |
| Credential file | Valid JSON, all fields present, PEM parseable, hex valid | Specific message per field |

## Testing

### Unit Tests

- **KeyGenerator**: produces valid RSA 2048 keys, public key derivable from private, DH PEM is valid ASN.1
- **CredentialFile**: round-trip write/read produces identical values, rejects missing fields with clear error, rejects invalid PEM, rejects invalid base64 secret
- **OAuthCredentialsFactory.FromFile**: loads valid JSON, produces working `IbkrOAuthCredentials`, rejects invalid/missing fields with `IbkrConfigurationException`
- **Input validation**: consumer key format (length, alphanumeric), base64 validation, path validation
- **DH PEM generation**: output matches expected ASN.1 structure, parseable by OpenSSL (verified by comparing against `openssl dhparam -inform PEM -text`)

### Integration Test

- `validate` command against WireMock: mock LST + ssodh/init endpoints, run validation, verify success output
- `validate` command with bad credentials: verify specific error guidance

## Scope Boundaries

### In Scope
- `ibkr-conduit-setup` dotnet tool with wizard + 3 subcommands
- `OAuthCredentialsFactory.FromFile()` on the main library
- JSON credential file format
- Cross-platform key generation (no OpenSSL)
- Interactive wizard with input validation
- Non-interactive mode via command-line flags
- Credential validation against live API
- End-user documentation in the tool's console output

### Out of Scope
- Secret manager integration (Azure Key Vault, AWS Secrets Manager) — consumers load the JSON from wherever they store it and pass to `FromFile()` or parse it themselves
- Browser automation for the IBKR portal
- Key rotation automation beyond re-running the tool
- Modifying the existing `FromEnvironment()` method
- GUI or web interface — console only
- Token refresh (IBKR OAuth 1.0a access tokens don't expire)
