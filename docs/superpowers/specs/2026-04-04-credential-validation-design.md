# Credential Validation + Friendly Error Messages — Design Spec

## Goal

Add an explicit `IIbkrClient.ValidateConnectionAsync()` method for fail-fast startup validation, and wrap raw crypto/HTTP exceptions from the session initialization path in descriptive `IbkrConfigurationException` messages that tell consumers which credential is misconfigured.

## Background

Currently, bad credentials are not detected until the first API call triggers lazy session initialization. When credentials are wrong, consumers see raw infrastructure exceptions — `CryptographicException` ("The parameter is incorrect"), `HttpRequestException` (401), `FormatException`, `InvalidOperationException` — with no indication of which credential field is the problem. This applies whether the consumer calls `ValidateConnectionAsync` or not.

## Changes

### 1. New Exception: `IbkrConfigurationException`

Extends `Exception` (not `IbkrApiException` — this is a configuration error, not an API error).

```csharp
public class IbkrConfigurationException : Exception
{
    /// <summary>Suggests which credential or option field to check.</summary>
    public string? CredentialHint { get; }
}
```

Constructors:
- `IbkrConfigurationException(string message, string? credentialHint, Exception innerException)`
- `IbkrConfigurationException(string message, string? credentialHint)`

### 2. Exception Wrapping in SessionManager

Both `EnsureInitializedAsync` and `ReauthenticateAsync` call `_sessionTokenProvider.GetLiveSessionTokenAsync()` / `RefreshAsync()` and `_sessionApi.InitializeBrokerageSessionAsync()`. Currently exceptions from these propagate raw. After the change, these calls are wrapped in try/catch blocks that translate specific exception types into `IbkrConfigurationException`.

**Wrapping logic** (in a private helper method on `SessionManager`):

| Caught Exception | Condition | Message | CredentialHint |
|-----------------|-----------|---------|----------------|
| `CryptographicException` | Message contains "decrypt" or thrown during LST acquisition before HTTP call | "Failed to decrypt access token secret — verify EncryptionPrivateKey matches the key registered in the IBKR portal" | `"EncryptionPrivateKey"` |
| `CryptographicException` | Message contains "sign" or thrown during OAuth header build | "RSA signature failed — verify SignaturePrivateKey matches the key registered in the IBKR portal" | `"SignaturePrivateKey"` |
| `CryptographicException` | Other | "Cryptographic operation failed during session initialization — verify SignaturePrivateKey and EncryptionPrivateKey" | `"SignaturePrivateKey, EncryptionPrivateKey"` |
| `HttpRequestException` | StatusCode is 401 or 403 | "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired" | `"ConsumerKey, AccessToken"` |
| `HttpRequestException` | Network error (no status code) | "Cannot reach IBKR API — check network connectivity and BaseUrl configuration" | `"BaseUrl"` |
| `FormatException` | During LST acquisition | "Diffie-Hellman key exchange produced invalid data — verify DhPrime is the correct RFC 3526 Group 14 prime" | `"DhPrime"` |
| `InvalidOperationException` | During LST acquisition | "Diffie-Hellman key exchange failed — verify DhPrime is the correct RFC 3526 Group 14 prime" | `"DhPrime"` |
| `JsonException` | During LST acquisition | "Unexpected response format from IBKR LST endpoint — the API may be experiencing issues or the endpoint URL may be incorrect" | `"BaseUrl"` |

The original exception is always preserved as `InnerException`.

**Where the wrapping lives:** In `SessionManager`, around the LST + ssodh/init calls in both `EnsureInitializedAsync` and `ReauthenticateAsync`. The wrapping is a private helper to avoid duplication:

```csharp
private static IbkrConfigurationException WrapCredentialException(Exception ex)
{
    // Pattern match on exception type and message to produce friendly error
}
```

**Important:** `TokenRefreshHandler` already catches exceptions from `ReauthenticateAsync` and wraps in `IbkrSessionException`. After this change, the exception it catches will be `IbkrConfigurationException` (from SessionManager's wrapping) instead of a raw `CryptographicException`. The `IbkrSessionException` wrapping in `TokenRefreshHandler` stays as-is — it wraps whatever exception comes out, so the chain becomes `IbkrSessionException` → `IbkrConfigurationException` → original exception. This is correct: the session exception indicates "re-auth failed," the configuration exception indicates "because credentials are bad."

### 3. `IIbkrClient.ValidateConnectionAsync()`

New method on the `IIbkrClient` facade interface:

```csharp
/// <summary>
/// Validates that the configured credentials can establish a session with the IBKR API.
/// Performs LST acquisition, session initialization, and auth status verification.
/// Call at startup for fail-fast credential validation.
/// Throws <see cref="IbkrConfigurationException"/> with a descriptive message if validation fails.
/// </summary>
Task ValidateConnectionAsync(CancellationToken cancellationToken = default);
```

Implementation in `IbkrClient`:

```csharp
public async Task ValidateConnectionAsync(CancellationToken cancellationToken = default)
{
    await _sessionManager.EnsureInitializedAsync(cancellationToken);
}
```

This is intentionally simple — `EnsureInitializedAsync` already does LST + ssodh/init + suppress, and with the new wrapping it throws `IbkrConfigurationException` on failure. No additional logic needed in the facade.

If the session is already initialized (consumer called `ValidateConnectionAsync` after making an API call), `EnsureInitializedAsync` returns immediately — it's idempotent.

## Files

### New Files
| Path | Purpose |
|------|---------|
| `src/IbkrConduit/Errors/IbkrConfigurationException.cs` | New exception type |

### Modified Files
| Path | Change |
|------|--------|
| `src/IbkrClient/IIbkrClient.cs` | Add `ValidateConnectionAsync` to interface |
| `src/IbkrConduit/Client/IbkrClient.cs` | Implement `ValidateConnectionAsync` |
| `src/IbkrConduit/Session/SessionManager.cs` | Wrap credential exceptions in `EnsureInitializedAsync` and `ReauthenticateAsync` |

## Testing

### Unit Tests
- `SessionManager`: mock `ISessionTokenProvider` to throw each exception type → verify `IbkrConfigurationException` with correct message and `CredentialHint`
- `SessionManager`: mock `IIbkrSessionApi.InitializeBrokerageSessionAsync` to throw → verify wrapping
- `IbkrClient.ValidateConnectionAsync`: verify it calls `EnsureInitializedAsync`

### Integration Tests
- `ValidateConnectionAsync` with correctly configured TestHarness → succeeds without exception
- `ValidateConnectionAsync` with broken LST endpoint (WireMock returns 401) → throws `IbkrConfigurationException` with ConsumerKey/AccessToken hint
- `ValidateConnectionAsync` with broken ssodh/init endpoint → throws `IbkrConfigurationException`
- Verify existing 401 recovery tests still pass (TokenRefreshHandler chain: `IbkrSessionException` → `IbkrConfigurationException` → original)

## Scope Boundaries

### In Scope
- `IbkrConfigurationException` with `CredentialHint`
- Exception wrapping in `SessionManager` for both init and re-auth paths
- `ValidateConnectionAsync` on `IIbkrClient`
- Unit and integration tests

### Out of Scope
- Options validation (TickleIntervalSeconds, BaseUrl format, etc.) — separate item #10 from safety report
- SuppressMessageIds validation — separate item #6
- Changing `TokenRefreshHandler` behavior — it wraps whatever comes from SessionManager
