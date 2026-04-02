# Error Normalization Layer — Design Spec

## Goal

Abstract IBKR's inconsistent error responses into a consistent, well-typed error model for library consumers. Consumers should never need to know that IBKR returns 500 for bad requests, 503 for permission errors, or 200 OK for order rejections.

## Background

The IBKR Client Portal Web API has no standard error schema. Analysis of the v1 API documentation (see `docs/ibkr-error-patterns-report.md`) identified five distinct error formats, HTTP status code misuse (500 for 400 conditions, 503 for 403 conditions), and 200 OK responses with error content in the body. The most safety-critical case is order rejections arriving as 200 OK with `{"error": "..."}`.

## Architecture

Single `ErrorNormalizationHandler` (DelegatingHandler) positioned between `TokenRefreshHandler` and `ResilienceHandler` in the consumer HTTP pipeline. Combined with a shallow exception hierarchy and `OneOf`-based result types for polymorphic order endpoints.

**Pipeline after this change:**

```
TokenRefreshHandler → ErrorNormalizationHandler → ResilienceHandler → GlobalRateLimitingHandler → EndpointRateLimitingHandler → OAuthSigningHandler → HttpClient
```

**Why this position:**
- TokenRefreshHandler must see raw 401s to trigger session refresh. It runs first.
- ErrorNormalizationHandler remaps misused status codes (e.g., 500→400) so that ResilienceHandler makes correct retry decisions. Retrying a request that IBKR rejected as a bad request (reported as 500) is wasteful and potentially dangerous for trading operations.
- ResilienceHandler only retries genuine transient errors after normalization.

**Session pipeline is unchanged** — the error normalization handler is NOT added to the session API pipeline (`IIbkrSessionApi`). Session endpoints have their own auth status semantics handled by `SessionManager`.

**Flex Web Service is unchanged** — it has its own pipeline and already throws `FlexQueryException`.

## Exception Hierarchy

All exceptions live in a new `IbkrConduit.Errors` namespace.

### IbkrApiException (base)

Thrown for any IBKR API error after normalization. Carries the normalized (remapped) status code, not the original.

```csharp
public class IbkrApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ErrorMessage { get; }
    public string? RawResponseBody { get; }
    public string? RequestUri { get; }
}
```

- `StatusCode` — The normalized HTTP status code (after remapping). Consumers can branch on this for generic handling.
- `ErrorMessage` — Parsed from `{"error": "..."}` or `{"failure_list": "..."}` or other body patterns. Null if body was unparseable.
- `RawResponseBody` — Full response body for diagnostics/logging.
- `RequestUri` — The request URI that triggered the error (path only, no query string secrets).

### IbkrRateLimitException : IbkrApiException

Thrown when IBKR returns 429 or the response indicates rate limiting.

```csharp
public class IbkrRateLimitException : IbkrApiException
{
    public TimeSpan? RetryAfter { get; }
}
```

- `RetryAfter` — Parsed from the `Retry-After` header if present. Null if IBKR didn't include it (common). Callers can use this for custom backoff logic beyond what the resilience handler provides.

### IbkrSessionException : IbkrApiException

Thrown when the error indicates the session is dead and requires re-initialization (not just a token refresh).

```csharp
public class IbkrSessionException : IbkrApiException
{
    public bool IsCompeting { get; }
    public string? Reason { get; }
}
```

- `IsCompeting` — True if another session took over (IBKR allows only one active session per user).
- `Reason` — From the `fail` field in auth status responses, if available.

### IbkrOrderRejectedException : IbkrApiException

Thrown when an order placement or modification is rejected by IBKR. These arrive as 200 OK with `{"error": "..."}` — the handler detects this and throws.

```csharp
public class IbkrOrderRejectedException : IbkrApiException
{
    public string RejectionMessage { get; }
}
```

- `RejectionMessage` — The rejection reason from IBKR (e.g., "We cannot accept an order at the limit price", "Your account is not permissioned for this product").

## ErrorNormalizationHandler

### Behavior on Non-2xx Responses

The handler reads the response body and attempts to parse it as JSON. Based on the status code and body content, it either remaps and throws or throws with the original status code.

| Original Status | Body Pattern | Normalized Status | Exception Type |
|---|---|---|---|
| 500 | Request path is `/iserver/marketdata/unsubscribe` | 404 Not Found | `IbkrApiException` |
| 500 | Request path is `/iserver/auth/ssodh/init` with body indicating invalid params | 400 Bad Request | `IbkrApiException` |
| 500 | Any other path | 500 (unchanged, eligible for retry) | `IbkrApiException` |
| 503 | DYNACCT endpoints (`/iserver/dynaccount`, `/iserver/account/search`) with "unavailable" | 403 Forbidden | `IbkrApiException` |
| 503 | Order status endpoint with no cached data | 404 Not Found | `IbkrApiException` |
| 503 | Reply endpoint with timeout/invalidated reply | 410 Gone | `IbkrApiException` |
| 429 | Any body | 429 (unchanged) | `IbkrRateLimitException` |
| 401 | Any body (escaped TokenRefreshHandler) | 401 (unchanged) | `IbkrSessionException` |
| Any other non-2xx | Any body | Original (unchanged) | `IbkrApiException` |

**Remapping is conservative.** Unknown 500s stay as 500 and are retried by the resilience handler. Only well-understood patterns are remapped. New mappings are added as more quirks are discovered in E2E testing.

### Behavior on 200 OK Responses

The handler checks 200 OK response bodies for error indicators:

| Body Pattern | Detection | Action |
|---|---|---|
| `{"error": "non-null, non-empty string"}` (top-level) | JSON parse, check `error` field | Throw `IbkrOrderRejectedException` |
| `{"success": false, ...}` with `failure_list` (top-level) | JSON parse, check `success` field | Throw `IbkrApiException` (400) |
| `{"error": null}` or no `error` field | JSON parse | Pass through (not an error) |
| Order confirmation shape `[{"id": "...", "message": [...]}]` | Not checked by handler | Pass through (operations handle via `OneOf`) |
| Non-JSON body (plain text, empty) | Content-Type check | Pass through |

**Key safety rule:** The handler only treats `{"error": "non-null string"}` at the top level of the JSON as an error. Nested `error` fields (e.g., `WhatIfResponse.Error`, `ContractRules.Error`, `Tickle.Hmds.Error`) are part of the domain response and pass through untouched. The handler achieves this by deserializing only to a flat `{"error": ..., "success": ...}` shape, not into nested objects.

### Body Buffering

The handler reads `response.Content` into a string for inspection. After inspection (whether it throws or passes through), it replaces `response.Content` with a new `StringContent` preserving the original `Content-Type` and encoding. This allows downstream Refit deserialization to work normally.

Performance impact is negligible — the library is rate-limited to 10 req/sec and response bodies are small JSON payloads (typically < 10 KB).

### Error Detection Model

Internal model used only by the handler for JSON parsing:

```csharp
internal record IbkrErrorBody(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("success")] bool? Success,
    [property: JsonPropertyName("failure_list")] string? FailureList,
    [property: JsonPropertyName("statusCode")] int? StatusCode);
```

Deserialized with `JsonSerializerOptions { PropertyNameCaseInsensitive = true }` and wrapped in a try/catch so non-JSON bodies don't crash the handler.

## OneOf Result Types for Order Operations

### Dependency

`OneOf` NuGet package added to `Directory.Packages.props`. Referenced by the main `IbkrConduit` project (part of the public API surface).

### New Result Records

Located in `IbkrConduit.Orders` namespace alongside existing order models.

```csharp
/// <summary>
/// Confirms the order was accepted by IBKR.
/// </summary>
public sealed record OrderSubmitted(string OrderId, string OrderStatus);

/// <summary>
/// IBKR requires confirmation before proceeding. Caller must decide
/// whether to confirm via ReplyAsync or treat as a rejection.
/// </summary>
public sealed record OrderConfirmationRequired(
    string ReplyId,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> MessageIds);
```

`OrderSubmitted` replaces the existing `OrderResult` record. `OrderResult` is removed.

### Changed Signatures on IOrderOperations

```csharp
public interface IOrderOperations
{
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default);

    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default);

    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
        string replyId, bool confirmed,
        CancellationToken cancellationToken = default);

    // Unchanged:
    Task<CancelOrderResponse> CancelOrderAsync(...);
    Task<List<LiveOrder>> GetLiveOrdersAsync(...);
    Task<List<Trade>> GetTradesAsync(...);
    Task<WhatIfResponse> WhatIfOrderAsync(...);
    Task<OrderStatus> GetOrderStatusAsync(...);
}
```

### Consumer Usage

```csharp
var result = await client.Orders.PlaceOrderAsync(accountId, order, ct);

var finalResult = await result.Match<Task<OrderSubmitted>>(
    submitted => Task.FromResult(submitted),
    async confirmation =>
    {
        // Auto-confirm, or inspect confirmation.Messages first
        var reply = await client.Orders.ReplyAsync(confirmation.ReplyId, true, ct);
        return reply.Match(
            submitted => submitted,
            _ => throw new InvalidOperationException("Unexpected chained confirmation"));
    });

Console.WriteLine($"Order {finalResult.OrderId} placed: {finalResult.OrderStatus}");
```

### How It Flows

1. Consumer calls `PlaceOrderAsync`.
2. `OrderOperations` calls the Refit interface (which returns `IApiResponse<string>`).
3. `ErrorNormalizationHandler` inspects the 200 response:
   - If `{"error": "..."}` → throws `IbkrOrderRejectedException` (consumer never sees it as a result).
   - Otherwise → passes through.
4. `OrderOperations` receives the raw string response, deserializes it, and returns:
   - `OrderSubmitted` if `order_id` is present.
   - `OrderConfirmationRequired` if `id` and `message` are present.

### ReplyAsync Signature Change

Currently `OrderOperations` has internal reply logic that auto-confirms. The new design exposes `ReplyAsync` directly so consumers can inspect confirmation messages and decide. The auto-confirm convenience remains available as a pattern in documentation/samples, not as a hidden behavior.

## DI Registration Changes

In `ServiceCollectionExtensions.RegisterConsumerRefitClient<TApi>()`:

```csharp
.AddHttpMessageHandler(sp =>
    new TokenRefreshHandler(
        sp.GetRequiredService<ISessionManager>()))
.AddHttpMessageHandler(sp =>           // NEW
    new ErrorNormalizationHandler())    // NEW
.AddHttpMessageHandler(sp =>
    new ResilienceHandler(
        sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
// ... rest unchanged
```

`ErrorNormalizationHandler` is stateless (no constructor dependencies). It only reads the response and the request URI.

## Files

### New Files

| File | Responsibility |
|---|---|
| `src/IbkrConduit/Errors/IbkrApiException.cs` | Base exception with status code, message, raw body, URI |
| `src/IbkrConduit/Errors/IbkrRateLimitException.cs` | Rate limit exception with RetryAfter |
| `src/IbkrConduit/Errors/IbkrSessionException.cs` | Session death exception with IsCompeting, Reason |
| `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs` | Order rejection exception with RejectionMessage |
| `src/IbkrConduit/Http/ErrorNormalizationHandler.cs` | The DelegatingHandler |
| `src/IbkrConduit/Http/IbkrErrorBody.cs` | Internal error detection model |
| `src/IbkrConduit/Orders/OrderSubmitted.cs` | New result record (replaces OrderResult) |
| `src/IbkrConduit/Orders/OrderConfirmationRequired.cs` | New result record |
| `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs` | Handler unit tests |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsOneOfTests.cs` | OneOf result type tests |
| `tests/IbkrConduit.Tests.Integration/Http/ErrorNormalizationPipelineTests.cs` | WireMock pipeline tests |

### Modified Files

| File | Change |
|---|---|
| `Directory.Packages.props` | Add `OneOf` package version |
| `src/IbkrConduit/IbkrConduit.csproj` | Add `OneOf` PackageReference |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Insert ErrorNormalizationHandler in pipeline, update pipeline doc comments |
| `src/IbkrConduit/Client/IOrderOperations.cs` | Change PlaceOrderAsync, ModifyOrderAsync return types; add ReplyAsync |
| `src/IbkrConduit/Client/OrderOperations.cs` | Implement new signatures, remove auto-confirm loop, construct OneOf results |
| `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` | Remove `OrderResult`, keep `OrderSubmissionResponse` (wire model) |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs` | Update for new return types |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsReplyTests.cs` | Update for new ReplyAsync signature |

### Removed

| Item | Reason |
|---|---|
| `OrderResult` record | Replaced by `OrderSubmitted` |

## Testing Strategy

### Unit Tests — ErrorNormalizationHandler

| Test | Input | Expected |
|---|---|---|
| `NonSuccess_500WithErrorBody_ThrowsIbkrApiException` | 500 + `{"error": "invalid"}` | `IbkrApiException` with StatusCode=400 |
| `NonSuccess_503OnDynacct_ThrowsIbkrApiExceptionForbidden` | 503 + `{"error": "unavailable", "statusCode": 503}` on DYNACCT path | `IbkrApiException` with StatusCode=403 |
| `NonSuccess_503OnOrderStatus_ThrowsIbkrApiExceptionNotFound` | 503 on order status path | `IbkrApiException` with StatusCode=404 |
| `NonSuccess_503OnReply_ThrowsIbkrApiExceptionGone` | 503 on reply path | `IbkrApiException` with StatusCode=410 |
| `NonSuccess_429WithRetryAfter_ThrowsRateLimitException` | 429 + Retry-After header | `IbkrRateLimitException` with RetryAfter set |
| `NonSuccess_429WithoutRetryAfter_ThrowsRateLimitException` | 429, no header | `IbkrRateLimitException` with RetryAfter null |
| `NonSuccess_401_ThrowsSessionException` | 401 | `IbkrSessionException` |
| `NonSuccess_GenericError_ThrowsIbkrApiException` | 404 + any body | `IbkrApiException` with StatusCode=404 |
| `Success_200WithTopLevelError_ThrowsOrderRejectedException` | 200 + `{"error": "insufficient funds"}` | `IbkrOrderRejectedException` |
| `Success_200WithSuccessFalse_ThrowsIbkrApiException` | 200 + `{"success": false, "failure_list": "reason"}` | `IbkrApiException` with StatusCode=400 |
| `Success_200WithNullError_PassesThrough` | 200 + `{"error": null, ...}` | No exception, response returned |
| `Success_200WithOrderConfirmation_PassesThrough` | 200 + `[{"id": "x", "message": ["y"]}]` | No exception, response returned |
| `Success_200WithNormalBody_PassesThrough` | 200 + `{"accounts": [...]}` | No exception, response returned |
| `Success_200WithNonJsonBody_PassesThrough` | 200 + `"Success"` (text/plain) | No exception, response returned |
| `Success_200WithEmptyBody_PassesThrough` | 200 + empty | No exception, response returned |
| `BodyPreservedAfterInspection` | 200 + normal JSON | Response body readable by downstream |
| `NonJsonErrorBody_DoesNotCrash` | 500 + `<html>error</html>` | `IbkrApiException` with null ErrorMessage, raw body preserved |

### Unit Tests — Exception Types

| Test | Assertion |
|---|---|
| `IbkrApiException_CarriesAllProperties` | StatusCode, ErrorMessage, RawResponseBody, RequestUri all set |
| `IbkrRateLimitException_InheritsBase` | Can catch as IbkrApiException |
| `IbkrSessionException_CarriesExtraProperties` | IsCompeting and Reason accessible |
| `IbkrOrderRejectedException_CarriesRejectionMessage` | RejectionMessage set |

### Unit Tests — OrderOperations OneOf

| Test | Assertion |
|---|---|
| `PlaceOrder_SubmittedResponse_ReturnsOrderSubmitted` | `OneOf.IsT0`, correct OrderId |
| `PlaceOrder_ConfirmationResponse_ReturnsOrderConfirmationRequired` | `OneOf.IsT1`, correct ReplyId and Messages |
| `ModifyOrder_SubmittedResponse_ReturnsOrderSubmitted` | Same pattern |
| `Reply_SubmittedResponse_ReturnsOrderSubmitted` | Same pattern |
| `Reply_ChainedConfirmation_ReturnsOrderConfirmationRequired` | Same pattern |

### Integration Tests — WireMock Pipeline

| Test | Setup | Assertion |
|---|---|---|
| `Pipeline_500Remapped_NotRetried` | WireMock returns 500 with bad-request body | `IbkrApiException` with 400, resilience handler did NOT retry |
| `Pipeline_Genuine500_Retried` | WireMock returns 500 with generic error | Resilience handler retried (verify call count) |
| `Pipeline_200WithError_ThrowsOrderRejected` | WireMock returns 200 + `{"error": "..."}` | `IbkrOrderRejectedException` |
| `Pipeline_200WithConfirmation_PassesThrough` | WireMock returns 200 + confirmation JSON | No exception, Refit deserializes normally |
| `Pipeline_429_ThrowsRateLimit` | WireMock returns 429 | `IbkrRateLimitException` after retries exhausted |

## Scope Boundaries

### In Scope
- `ErrorNormalizationHandler` with status code remapping and 200-with-error detection
- Exception hierarchy (`IbkrApiException`, `IbkrRateLimitException`, `IbkrSessionException`, `IbkrOrderRejectedException`)
- `OneOf` result types for `PlaceOrderAsync`, `ModifyOrderAsync`, `ReplyAsync`
- `ReplyAsync` exposed on `IOrderOperations` (currently internal)
- Unit and integration tests for all new code

### Out of Scope
- Flex Web Service errors (already handled by `FlexQueryException`)
- Session pipeline error handling (handled by `SessionManager`)
- Auth status response interpretation (handled by `SessionManager`)
- WhatIf `warn`/`error` fields (domain-level, not HTTP errors)
- Nested `error` fields in contract rules, tickle responses (informational)
- Proactive rate limiting improvements (existing rate limiters are sufficient)
- Retry-after backoff improvements (existing resilience pipeline handles this)
