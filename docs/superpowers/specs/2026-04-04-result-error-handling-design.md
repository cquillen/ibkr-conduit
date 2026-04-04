# Result-Based Error Handling Design

## Goal

Replace the exception-driven error handling pipeline with a `Result<T>` pattern at the facade layer. Consumers inspect success/failure without catching exceptions. Exceptions remain only for genuinely exceptional conditions (rate limiter queue full, schema validation in strict mode, unrecoverable auth failure).

## Motivation

The current pipeline has several problems:

1. **IBKR misuses HTTP status codes** — returns 401 for invalid account IDs, 500/503 for bad input, 200 for rejected orders. Global remapping in `ErrorNormalizationHandler` is fragile and borders on magic.
2. **Exceptions for expected outcomes** — API errors are normal in IBKR interactions (invalid parameters, permission denials, paper account limitations). Forcing consumers to catch exceptions for routine error handling is noisy.
3. **Misleading error messages** — the `TokenRefreshHandler` throws `IbkrSessionException("credentials may be invalidated")` when IBKR returns 401 for a bad account ID, triggering a needless re-auth cycle first.
4. **No non-exception error path** — consumers who prefer result inspection over try/catch have no option.

## Design

### IbkrError Hierarchy

Immutable records for error details. Pattern matching friendly. `RawBody` always populated (empty string for empty bodies).

```
IbkrError (abstract base record)
  HttpStatusCode? StatusCode
  string? Message
  string? RawBody
  string? RequestPath

  IbkrApiError             — generic API error (catch-all for non-2xx)
  IbkrSessionError         — auth/session failure
    bool IsCompeting
  IbkrRateLimitError       — 429 from IBKR server
    TimeSpan? RetryAfter
  IbkrOrderRejectedError   — 200 OK with error body on order endpoints
    string RejectionMessage
  IbkrHiddenError          — 200 OK with {"error":"..."} or {"success":false} on non-order endpoints
```

### Result\<T\>

Readonly struct for zero-allocation on the success path.

```csharp
public readonly struct Result<T>
{
    bool IsSuccess { get; }
    T Value { get; }             // throws InvalidOperationException if !IsSuccess
    IbkrError Error { get; }     // throws InvalidOperationException if IsSuccess

    static Result<T> Success(T value);
    static Result<T> Failure(IbkrError error);

    Result<T> EnsureSuccess();   // returns self if success, throws IbkrApiException if failure
    Result<TOut> Map<TOut>(Func<T, TOut> selector);
    void Switch(Action<T> onSuccess, Action<IbkrError> onError);
    TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IbkrError, TOut> onError);
}
```

### Simplified IbkrApiException

Single exception class wrapping `IbkrError`. Replaces the current hierarchy (`IbkrSessionException`, `IbkrRateLimitException`, `IbkrOrderRejectedException`).

```csharp
public class IbkrApiException : Exception
{
    public IbkrError Error { get; }
    public IbkrApiException(IbkrError error) : base(error.Message) => Error = error;
}
```

Consumers who catch exceptions pattern match on `ex.Error`:

```csharp
catch (IbkrApiException ex) when (ex.Error is IbkrRateLimitError rle)
{
    await Task.Delay(rle.RetryAfter ?? TimeSpan.FromSeconds(5));
}
```

These exception types are unchanged and remain separate:
- `IbkrConfigurationException` — not an API error, thrown at startup
- `RateLimitRejectedException` — client-side rate limiter queue full
- `IbkrSchemaViolationException` — opt-in development diagnostic

### Refit Interface Changes

All Refit interfaces change from `Task<T>` to `Task<IApiResponse<T>>`. This prevents Refit from throwing on non-2xx and gives the facade access to status code, headers, and raw body.

```csharp
// Before
Task<IserverAccountsResponse> GetAccountsAsync(CancellationToken cancellationToken = default);

// After
Task<IApiResponse<IserverAccountsResponse>> GetAccountsAsync(CancellationToken cancellationToken = default);
```

Order endpoints that need polymorphic deserialization use `IApiResponse<string>` — raw body that the facade parses via a custom delegate.

### ResultFactory

Shared helper that converts `IApiResponse<T>` into `Result<T>`. Two overloads:

```csharp
internal static class ResultFactory
{
    // Standard — Refit already deserialized T
    static Result<T> FromResponse<T>(IApiResponse<T> response, string? requestPath = null);

    // Custom parsing — caller provides deserialization from raw body
    static Result<T> FromResponse<T>(IApiResponse<string> response, Func<string, T> parser, string? requestPath = null);
}
```

**Standard overload logic:**

1. 2xx with non-null `.Content` — check for hidden error (`{"error":"..."}` or `{"success":false}` in raw body). If clean, `Result.Success(content)`.
2. 2xx with null `.Content` — `Result.Failure(IbkrApiError)` with raw body.
3. Non-2xx with JSON error body — parse `{"error":"...", "statusCode":...}`, build appropriate `IbkrError` subtype (429 with Retry-After header → `IbkrRateLimitError`).
4. Non-2xx with empty body — `IbkrApiError` with status code, empty raw body.
5. Non-2xx with text/HTML body — `IbkrApiError` with raw body as message.

**Custom parsing overload** does the same error checks, but on success passes the raw body to the `Func<string, T>` delegate.

### Handler Chain Changes

**Remove:**
- `ErrorNormalizationHandler` — logic moves to facade via ResultFactory
- `ResilienceHandler` — IBKR's 5xx misuse makes retries counterproductive (retries 500s that are really 400s)

**Modify:**
- `TokenRefreshHandler` — on repeated 401 after re-auth, return the response instead of throwing. Re-auth failure (bad credentials) still throws `IbkrApiException` wrapping `IbkrSessionError`.

**Unchanged:**
- `OAuthSigningHandler`
- `GlobalRateLimitingHandler` (throws `RateLimitRejectedException` on queue full)
- `EndpointRateLimitingHandler` (throws `RateLimitRejectedException` on queue full)
- `ResponseSchemaValidationHandler` (throws `IbkrSchemaViolationException` in strict mode)

**Resulting handler chain (request flows down):**

```
TokenRefreshHandler              (401 → re-auth + retry; repeated 401 → pass through)
ResponseSchemaValidationHandler  (opt-in strict mode)
GlobalRateLimitingHandler        (10 req/s global)
EndpointRateLimitingHandler      (per-endpoint limits)
OAuthSigningHandler              (HMAC-SHA256 signing)
HttpClientHandler
```

5 handlers, down from 7.

### ThrowOnApiError Option

`IbkrClientOptions.ThrowOnApiError` (default `false`) provides a global opt-in for exception-based error handling. When enabled, the facade calls `EnsureSuccess()` internally before returning, so consumers get the same exception behavior as before without changing their call sites.

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.ThrowOnApiError = true; // all facade methods throw on failure
});
```

Operations implementations receive the option via constructor injection. The return type is still `Task<Result<T>>` — the Result is constructed, then `EnsureSuccess()` is called if the option is enabled. This keeps the API surface uniform regardless of the setting.

### Facade Method Pattern

Every facade method follows the same shape:

```csharp
public async Task<Result<IserverAccountsResponse>> GetAccountsAsync(
    CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Accounts.GetAccounts");
    var response = await _api.GetAccountsAsync(cancellationToken);
    var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
    return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
}
```

`EnsureSuccess()` returns the same `Result<T>` on success (pass-through) or throws `IbkrApiException` on failure. The return type is always `Result<T>` — the option only controls whether failures reach the caller as a Result or as an exception.

Order endpoints with custom parsing:

```csharp
public async Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(
    string accountId, PlaceOrderRequest request, CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Orders.PlaceOrder");
    activity?.SetTag(LogFields.AccountId, accountId);
    var response = await _api.PlaceOrderAsync(accountId, request, cancellationToken);
    var result = ResultFactory.FromResponse(response, ParseOrderResponse, response.RequestMessage?.RequestUri?.AbsolutePath);
    return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
}
```

No try/catch in facade methods.

### Consumer Usage

**Result inspection (recommended):**

```csharp
var result = await client.Accounts.GetAccountsAsync();
if (result.IsSuccess)
{
    var accounts = result.Value;
}
else
{
    switch (result.Error)
    {
        case IbkrRateLimitError { RetryAfter: var delay }:
            await Task.Delay(delay);
            break;
        case IbkrSessionError { IsCompeting: true }:
            // handle competing session
            break;
        case IbkrApiError e:
            logger.LogWarning("API error {Status}: {Message}", e.StatusCode, e.Message);
            break;
    }
}
```

**Match/Switch:**

```csharp
var accounts = (await client.Accounts.GetAccountsAsync())
    .Match(
        accounts => accounts,
        error => throw new ApplicationException($"Failed: {error.Message}"));
```

**Exception bridge (per-call):**

```csharp
var accounts = (await client.Accounts.GetAccountsAsync()).EnsureSuccess().Value;
```

**Exception bridge (global via option):**

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.ThrowOnApiError = true;
});

// All calls now throw IbkrApiException on failure — no need for EnsureSuccess()
var accounts = (await client.Accounts.GetAccountsAsync()).Value;
```

**Order endpoint with OneOf:**

```csharp
var result = await client.Orders.PlaceOrderAsync(accountId, order);
if (result.IsSuccess)
{
    result.Value.Switch(
        submitted => logger.LogInformation("Order {Id} placed", submitted.OrderId),
        confirmation => logger.LogInformation("Confirm reply {Id}", confirmation.ReplyId));
}
```

## Breaking Changes

- All `IXxxOperations` interface methods: `Task<T>` → `Task<Result<T>>`
- All Refit interfaces: `Task<T>` → `Task<IApiResponse<T>>`
- `IbkrOrderRejectedException`, `IbkrRateLimitException`, `IbkrSessionException` removed as separate types
- `IbkrApiException` simplified to single class wrapping `IbkrError`
- Consumers catching typed exceptions switch to `IbkrApiException` + pattern match on `.Error`

## Scope

### In scope
- `Result<T>` struct
- `IbkrError` record hierarchy
- `ResultFactory` shared helper
- Simplified `IbkrApiException`
- `IbkrClientOptions.ThrowOnApiError` option (default `false`)
- Refit interface return type changes
- Facade return type changes and ResultFactory integration
- `TokenRefreshHandler` modification (repeated 401 returns response)
- Remove `ErrorNormalizationHandler`
- Remove `ResilienceHandler`
- Update all integration tests
- Update all unit tests

### Out of scope
- Polly rate limiter consolidation (separate effort)
- Streaming/WebSocket error handling (separate pipeline)
- Flex client error handling (separate HTTP client)
- `IbkrConfigurationException` changes
- `RateLimitRejectedException` changes
- `IbkrSchemaViolationException` changes

## IBKR Error Response Patterns

Discovered through live API recordings. The ResultFactory must handle all three:

| Pattern | Occurrences | Example |
|---------|------------|---------|
| JSON error object | 100 | `{"error": "Invalid account ID", "statusCode": 500}` |
| Empty body | 23 | 401 with empty string (portfolio endpoints, invalid account) |
| Plain text / HTML | 9 | `"Error 403 - Access Denied"` or `<html>..Resource not found..</html>` |

## Files Touched

| Area | Files | Change |
|------|-------|--------|
| New types | 3 | `Result<T>`, `IbkrError` hierarchy, `ResultFactory` |
| Refit interfaces | ~8 | `Task<T>` → `Task<IApiResponse<T>>` |
| Operations interfaces | ~8 | `Task<T>` → `Task<Result<T>>` |
| Operations implementations | ~8 | Add ResultFactory call |
| Error types | ~4 | Simplify exceptions, add IbkrError records |
| Handlers | 3 | Remove 2, modify 1 |
| DI registration | 2 | Remove handler registrations |
| Integration tests | All | Update for Result pattern |
| Unit tests | All | Update for new return types |
