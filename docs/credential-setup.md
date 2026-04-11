# Credential Setup Guide

This guide walks through generating OAuth 1.0a credentials for IbkrConduit using the `ibkr-conduit-setup` tool. The tool generates all required cryptographic keys, guides you through the IBKR portal configuration, and produces a JSON credential file ready for use.

## Prerequisites

- .NET 10 SDK or later
- An Interactive Brokers account with Client Portal API access
- Access to the [IBKR OAuth Self-Service Portal](https://ndcdyn.interactivebrokers.com/sso/Login?action=OAUTH)

## Installation

The setup tool is a .NET tool included in the repository:

```bash
# Run directly from the project
dotnet run --project tools/IbkrConduit.Setup
```

Or install it globally:

```bash
dotnet pack tools/IbkrConduit.Setup
dotnet tool install --global --add-source tools/IbkrConduit.Setup/bin/Release IbkrConduit.Setup
```

## Quick Start — Interactive Wizard

The fastest way to get set up is the interactive wizard. It walks through all four steps in sequence:

```bash
dotnet run --project tools/IbkrConduit.Setup
```

The wizard will:

1. **Generate cryptographic keys** — RSA 2048-bit signature and encryption key pairs, plus 2048-bit DH parameters (safe prime generation takes 30–120 seconds)
2. **Guide portal configuration** — step-by-step instructions for the IBKR OAuth page
3. **Collect credentials** — prompts for the access token and secret from the portal
4. **Validate the connection** — attempts an API call to verify everything works

All output is saved to `.ibkr-credentials/` by default (a hidden directory in the current working directory).

## Step-by-Step Manual Setup

If you prefer to run each step individually, use the subcommands below.

### Step 1: Generate Keys

```bash
dotnet run --project tools/IbkrConduit.Setup -- generate-keys
```

Options:

| Flag | Description |
|------|-------------|
| `--output <dir>` | Output directory (default: `./.ibkr-credentials`) |
| `--force` | Overwrite existing files without prompting |

This generates six files:

| File | Purpose |
|------|---------|
| `public_signature.pem` | Upload to IBKR portal (Public Signing Key) |
| `public_encryption.pem` | Upload to IBKR portal (Public Encryption Key) |
| `dhparam.pem` | Upload to IBKR portal (Diffie-Hellman Parameters) |
| `private_signature.pem` | Private — used for OAuth request signing |
| `private_encryption.pem` | Private — used for session token decryption |
| `.dhprime.hex` | Internal — DH prime in hex format for the credential file |

> **Note:** DH parameter generation involves finding a 2048-bit safe prime and typically takes 30–120 seconds. This is normal and matches the behavior of `openssl dhparam 2048`.

### Step 2: Configure OAuth in the IBKR Portal

1. Log in at <https://ndcdyn.interactivebrokers.com/sso/Login?action=OAUTH>
2. If this is your first time, accept the OAuth agreement (check the box, type your name, submit)
3. Check the **Enabled** checkbox if not already enabled
4. Enter a **Consumer Key** — the wizard generates one for you (9 uppercase letters, e.g. `XKVMTQWLR`). If running manually, you can generate any 9-letter uppercase string.
5. Upload the three public files using the upload controls on the page:
   - **Public Signing Key** → `public_signature.pem`
   - **Public Encryption Key** → `public_encryption.pem`
   - **Diffie-Hellman Parameters** → `dhparam.pem`
6. Click **Generate Token** at the bottom of the page
7. Copy the **Access Token** and **Access Token Secret** before leaving the page

> **Ignore the OpenSSL commands** shown on the portal page — the setup tool already generated all required keys in the correct format.

> **Activation delay:** Newly configured OAuth credentials can take up to a few days to become active on IBKR's API servers. The portal will generate tokens immediately, but API calls may return 401 until activation completes.

### Step 3: Create the Credential File

```bash
dotnet run --project tools/IbkrConduit.Setup -- configure
```

Options:

| Flag | Description |
|------|-------------|
| `--credentials <dir>` | Directory containing PEM files (default: `./.ibkr-credentials`) |
| `--consumer-key <key>` | Consumer key (9 uppercase letters) |
| `--access-token <token>` | Access token from the portal |
| `--access-token-secret <secret>` | Access token secret from the portal |

If flags are omitted, the tool prompts interactively with input validation.

This produces `ibkr-credentials.json` in the credentials directory.

### Step 4: Validate

```bash
dotnet run --project tools/IbkrConduit.Setup -- validate --credentials .ibkr-credentials/ibkr-credentials.json
```

If validation fails with a 401 error shortly after portal configuration, wait a day or two and try again — this is the normal activation delay.

## Credential File Format

The `ibkr-credentials.json` file contains everything needed to authenticate:

```json
{
  "consumerKey": "XKVMTQWLR",
  "accessToken": "...",
  "accessTokenSecret": "...(base64)...",
  "signaturePrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
  "encryptionPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
  "dhPrime": "A1B2C3...(hex)..."
}
```

The file is typically around 4.5 KB. Key sizes are fixed (RSA 2048-bit, DH 2048-bit), so file size does not vary significantly.

## Loading Credentials in Code

### From a file

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;

using var creds = OAuthCredentialsFactory.FromFile(".ibkr-credentials/ibkr-credentials.json");

var services = new ServiceCollection();
services.AddLogging();
services.AddIbkrClient(opts => opts.Credentials = creds);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();
```

### From a JSON string (e.g. Azure Key Vault)

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var kvClient = new SecretClient(
    new Uri("https://your-vault.vault.azure.net"),
    new DefaultAzureCredential());
var secret = await kvClient.GetSecretAsync("ibkr-credentials");

using var creds = OAuthCredentialsFactory.FromJson(secret.Value.Value);

var services = new ServiceCollection();
services.AddLogging();
services.AddIbkrClient(opts => opts.Credentials = creds);
```

### From environment variables

```csharp
using var creds = OAuthCredentialsFactory.FromEnvironment();
```

Required environment variables:

| Variable | Description |
|----------|-------------|
| `IBKR_CONSUMER_KEY` | Consumer key (9 uppercase letters) |
| `IBKR_ACCESS_TOKEN` | Access token |
| `IBKR_ACCESS_TOKEN_SECRET` | Access token secret |
| `IBKR_SIGNATURE_KEY` | Base64-encoded PEM private signing key |
| `IBKR_ENCRYPTION_KEY` | Base64-encoded PEM private encryption key |
| `IBKR_DH_PRIME` | Hex-encoded DH prime |
| `IBKR_TENANT_ID` | (Optional) Tenant ID, defaults to consumer key |

## Security

- **Never commit** `ibkr-credentials.json`, `*.pem`, or private key files to source control
- The `.gitignore` in this repository already excludes `*.pem`, `.ibkr-credentials/`, and `.creds/`
- For production deployments, store the JSON content in a secret manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) and load via `FromJson()`
- The credential file is approximately 4.5 KB — well within the 25 KB limit for Azure Key Vault secrets

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| 401 on LST acquisition | OAuth not yet activated | Wait 1–3 days after portal configuration |
| `FileNotFoundException` on validate | Wrong path to JSON file | Check `--credentials` path |
| `IbkrConfigurationException` | Malformed PEM or missing field | Re-run `generate-keys` and `configure` |
| DH generation hangs | Normal — finding safe prime | Wait 30–120 seconds |
| Portal rejects key upload | Wrong file uploaded | Ensure you upload the `public_*.pem` files, not `private_*.pem` |

## Command Reference

```
ibkr-conduit-setup                          Run the interactive wizard (default)
ibkr-conduit-setup generate-keys [options]  Generate RSA keys and DH parameters
ibkr-conduit-setup configure [options]      Create the JSON credential file
ibkr-conduit-setup validate [options]       Test credentials against the IBKR API
ibkr-conduit-setup --help                   Show help
```
