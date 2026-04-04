# Safety Features Report — Failure Mode Analysis & Mitigations

## Purpose

This report analyzes how the IbkrConduit wrapper library can break and proposes safety features to mitigate each failure mode. The analysis covers the entire stack: orders (real money), session lifecycle, data integrity, configuration, and operational concerns.

## Methodology

Each failure mode is categorized by severity (Critical/High/Medium/Lower), assessed for current coverage, and paired with a concrete mitigation. Mitigations follow the library's design principles: configurable (opt-in where appropriate), non-opinionated about business logic, and focused on catching library-level errors.

---

## Critical Severity — Real Money at Risk

### 1. No Order Guardrails

**Failure mode:** The library sends whatever `OrderRequest` it receives to the IBKR API without validation. A bug in consumer code could submit:
- Quantity of 1000 instead of 1 (lot size confusion or decimal point error)
- BUY instead of SELL (string enum typo)
- Wrong conid (wrong instrument entirely)
- Limit price of $1.00 instead of $100.00 (decimal point error)
- Market order on an illiquid instrument (terrible fill, no price protection)

**Current coverage:** None. `OrderOperations.PlaceOrderAsync()` passes the `OrderRequest` directly to `ToWireModel()` and sends it to the API. All validation is the consumer's responsibility.

**Risk:** Catastrophic financial loss. Order errors are irreversible once filled.

**Mitigation:** Add an optional `OrderGuardrails` configuration on `IbkrClientOptions`:

```csharp
public class OrderGuardrails
{
    /// <summary>Maximum quantity per order. Orders exceeding this throw before submission.</summary>
    public decimal? MaxQuantity { get; set; }

    /// <summary>Maximum notional value (quantity × price) per order.</summary>
    public decimal? MaxNotionalValue { get; set; }

    /// <summary>Maximum number of orders per account per day. Resets at midnight UTC.</summary>
    public int? MaxDailyOrders { get; set; }

    /// <summary>When true, PlaceOrderAsync calls WhatIfOrderAsync first and includes the
    /// preview in the result. Lets consumers inspect margin/commission impact before confirming.</summary>
    public bool RequireWhatIfPreview { get; set; }
}
```

Validation runs in `OrderOperations` before the request hits the wire. Throws `IbkrOrderGuardrailException` with details about which limit was exceeded. Disabled by default (null). Consumers opt in with:

```csharp
services.AddIbkrClient(opts =>
{
    opts.OrderGuardrails = new OrderGuardrails
    {
        MaxQuantity = 100,
        MaxNotionalValue = 50_000,
    };
});
```

**Effort:** Medium. New exception type, validation in `OrderOperations`, optional daily counter (atomic increment, UTC reset), optional what-if integration.

---

### 2. No Duplicate Order Detection

**Failure mode:** The same logical order is submitted twice due to:
- Network timeout → consumer retries → original request also succeeds
- Consumer bug submits in a loop
- UI double-click in consumer application
- Polly retry in the resilience pipeline re-submits after a 5xx error that actually succeeded server-side

The per-account semaphore in `OrderOperations` prevents *concurrent* submissions to the same account but does not prevent *sequential* duplicate submissions.

**Current coverage:** None. No order submission history, no idempotency keys, no deduplication.

**Risk:** Double position size, double commission, unintended exposure.

**Mitigation:** Short-lived deduplication cache in `OrderOperations`:

- Key: `(accountId, conid, side, quantity, orderType, price)` — the logical identity of an order
- Window: configurable, default 5 seconds
- Behavior: if the same logical order is submitted within the window, throw `IbkrDuplicateOrderException` instead of submitting
- Implementation: `ConcurrentDictionary<string, DateTimeOffset>` with periodic cleanup
- Opt-in via `IbkrClientOptions.OrderGuardrails.DeduplicationWindowSeconds`
- Applies to `PlaceOrderAsync` and `ModifyOrderAsync`

Does NOT apply to:
- Orders with different parameters (intentional scaling)
- Orders to different accounts
- Orders after the window expires (intentional re-submission)

**Effort:** Medium. Cache implementation, hash key construction, periodic cleanup, configurable window.

---

### 3. Auto-Confirm All Order Questions

**Failure mode:** The library's order confirmation flow currently auto-confirms ALL questions from IBKR. Some questions are routine warnings (e.g., "submitting without market data"), but others are safety-critical signals:
- "This order will exceed your buying power"
- "This order would result in a short position"
- "This will close an existing position"
- "Price is significantly away from the current market"

Auto-confirming these suppresses safety signals that exist to prevent mistakes.

**Current coverage:** Partial. `SuppressibleMessages` provides a curated list of known message IDs. `IbkrClientOptions.SuppressMessageIds` lets consumers suppress known IDs at session init (preventing questions from being asked at all). But if a question IS asked, the library always answers "Yes."

**Risk:** Financial loss from orders that should have been rejected based on the question content.

**Mitigation:** Already identified in `docs/future-enhancements.md` as "Selective Order Question Answers." Concrete design:

- Add optional `QuestionHandler` callback on `IbkrClientOptions`: `Func<OrderConfirmationRequired, bool>?`
- When a question is received, invoke the callback. If it returns `false`, cancel the order.
- Default behavior (null callback): auto-confirm as today for backwards compatibility.
- The callback receives the full `OrderConfirmationRequired` with message text and message IDs, allowing consumers to make per-question decisions.
- For automated systems: provide a `SafeQuestionHandler` that auto-confirms known-safe IDs (from `SuppressibleMessages.AutomatedTrading`) and rejects unknown questions.

**Effort:** Medium. Callback plumbing in `OrderOperations.PlaceOrderAsync` confirmation loop, new default handler implementation, documentation.

---

## High Severity — Session & Authentication

### 4. Credential Validation is Lazy

**Failure mode:** Bad credentials are not detected until the first API call triggers session initialization. A consumer could:
- Start their trading application
- Believe it's ready to trade
- Attempt a time-sensitive order
- Fail with a cryptic DH exchange error 30 seconds into startup

Common credential issues that would be caught late:
- Wrong consumer key (typo)
- Expired access token (rotated in portal)
- Mismatched signing/encryption keys
- RSA key in wrong format

**Current coverage:** Only `ArgumentNullException.ThrowIfNull(credentials)` at registration time. No validation of credential *validity*.

**Risk:** Delayed failure during critical operations. The consumer thinks the system is ready when it's not.

**Mitigation:** Optional `ValidateCredentialsOnStartup` flag on `IbkrClientOptions` (default `false`):

When enabled, `AddIbkrClient` (or a post-build validation step) performs:
1. LST acquisition (tests DH exchange, consumer key, access token, signing key, encryption key)
2. `POST /iserver/auth/ssodh/init` (tests session establishment)
3. `GET /iserver/auth/status` (confirms authenticated=true)

If any step fails, throws `IbkrConfigurationException` with a clear message identifying which credential is likely wrong. This happens at startup, not mid-trade.

Since `AddIbkrClient` is synchronous (returns `IServiceCollection`), the validation would either:
- (a) Run as a hosted service on first resolution, or
- (b) Be exposed as `ISessionManager.ValidateAsync()` that consumers call explicitly after building the service provider

Option (b) is simpler and doesn't change the registration pattern:
```csharp
var provider = services.BuildServiceProvider();
var session = provider.GetRequiredService<ISessionManager>();
await session.ValidateAsync(); // throws IbkrConfigurationException on failure
```

**Effort:** Low. The validation is just calling existing methods (`GetLiveSessionTokenAsync`, `InitializeBrokerageSessionAsync`, `GetAuthStatusAsync`) and wrapping failures in a descriptive exception.

---

### 5. No Session Health Observable

**Failure mode:** The session can be in several states — healthy, reauthenticating, dead (competing session took over), or degraded (tickle failing intermittently). Currently, the consumer has no way to know the session state without making an API call and seeing if it fails.

Scenarios:
- Tickle fails → SessionManager re-authenticates → brief outage during re-auth → consumer doesn't know
- Competing session kicks us out → session is dead → consumer discovers on next API call
- Token expires and proactive refresh fails → session degrades → no notification
- Internet connectivity lost → all calls fail → consumer can't distinguish from API outage

**Current coverage:** `SessionManager` has internal `SessionState` enum (`Uninitialized`, `Initializing`, `Ready`, `Reauthenticating`, `ShuttingDown`) but it's private. No events, no observable, no health check endpoint.

**Risk:** Consumer operates on stale assumption that session is healthy. Trading decisions made without knowing the session is degraded.

**Mitigation:** Expose session state as a public observable:

```csharp
public interface ISessionManager : IAsyncDisposable
{
    // Existing methods...

    /// <summary>Current session state.</summary>
    SessionHealth CurrentHealth { get; }

    /// <summary>Fires when session health changes.</summary>
    event EventHandler<SessionHealthChangedEventArgs>? HealthChanged;
}

public enum SessionHealth
{
    Uninitialized,
    Initializing,
    Ready,
    Reauthenticating,
    Degraded,  // tickle failed, attempting recovery
    Dead,      // recovery failed, requires manual intervention
}
```

Consumers can:
- Wire `HealthChanged` to monitoring/alerting
- Check `CurrentHealth` before submitting orders
- Implement health check endpoints for container orchestrators
- Pause order submission during `Reauthenticating` state

**Effort:** Medium. Promote existing internal state, add event emission at state transitions, add `Degraded` and `Dead` states for tickle failure paths, expose on `ISessionManager`.

---

### 6. SuppressMessageIds Accepted Blindly

**Failure mode:** `IbkrClientOptions.SuppressMessageIds` accepts any string without validation. Issues:
- Typo in message ID (e.g., `"o345"` instead of `"o354"`) → silently ignored by IBKR, question still asked at order time
- Consumer suppresses a safety-critical message ID they don't understand → dangerous questions auto-confirmed
- Unknown message ID format (should be `"o###"` or `"p#"`) → sent to API, no effect

**Current coverage:** `SuppressibleMessages` provides a curated list with the `AutomatedTrading` subset, but usage is advisory only. No validation at registration time.

**Risk:** False sense of safety — consumer believes they suppressed a question but it still fires, or they suppressed a critical safety message unknowingly.

**Mitigation:** Validation at registration time:

```csharp
// In options validation:
foreach (var id in options.SuppressMessageIds)
{
    if (!SuppressibleMessages.AllKnownIds.Contains(id))
    {
        if (options.StrictResponseValidation)
            throw new IbkrConfigurationException($"Unknown suppress message ID: '{id}'");
        else
            logger.LogWarning("Unknown suppress message ID: '{Id}' — may not have any effect", id);
    }

    if (SuppressibleMessages.SafetyCriticalIds.Contains(id))
    {
        logger.LogWarning("Suppressing safety-critical message ID: '{Id}' — {Description}",
            id, SuppressibleMessages.GetDescription(id));
    }
}
```

Add to `SuppressibleMessages`:
- `AllKnownIds` — set of all documented message IDs
- `SafetyCriticalIds` — subset that are dangerous to suppress (margin warnings, position warnings)
- `GetDescription(id)` — human-readable description of what the message warns about

**Effort:** Low. Validation logic, expand `SuppressibleMessages` with classification metadata.

---

## Medium Severity — Data Integrity

### 7. Response Type Drift Not Detected

**Failure mode:** The `ResponseSchemaValidationHandler` checks field *presence* (extra/missing field names) but not field *types*. If IBKR changes a field from `number` to `string` or from `object` to `array`:

- `decimal` field receiving a string → `JsonNumberHandling.AllowReadingFromString` may handle it, or may produce 0
- `int` field receiving a string like `"N/A"` → deserialization exception or default(int) = 0
- `object` field receiving `null` → nullable types handle this, but non-nullable types get default
- `bool` field receiving `0`/`1` instead of `true`/`false` → JsonException or silent default

**Current coverage:** The schema validator checks field name presence only. `[JsonNumberHandling(AllowReadingFromString)]` on some DTOs handles the known string-number quirk. `[JsonExtensionData]` captures unknown fields.

**Risk:** Silent data corruption. A position quantity of 0 instead of the real value could cause the consumer to believe they have no position.

**Mitigation:** Extend `ResponseSchemaValidationHandler` to optionally check JSON value types:

For each field in the response, compare the JSON `ValueKind` (`String`, `Number`, `True`/`False`, `Object`, `Array`, `Null`) against the expected CLR type on the DTO. Flag mismatches:
- JSON `String` but DTO expects `int`/`decimal`/`long` (unless `[JsonNumberHandling]` is present)
- JSON `Number` but DTO expects `string`
- JSON `Array` but DTO expects a scalar
- JSON `Null` but DTO field is non-nullable

Same strict/non-strict behavior as field name validation. Add a separate `StrictTypeValidation` option or fold into the existing `StrictResponseValidation` flag.

**Effort:** Medium. Extend `DtoFieldMap` to include expected CLR types. Add type comparison logic to handler. Handle the `JsonNumberHandling` attribute exception.

---

### 8. Stale Position Data

**Failure mode:** IBKR's Client Portal API caches position and account data aggressively. A consumer requesting positions may receive data that's seconds or minutes old. If the consumer makes trading decisions based on stale position data:
- Believe they have 0 shares when they actually have 100 → open a duplicate position
- Believe they have 100 shares when they already sold → attempt to sell shares they don't own
- P&L calculations based on stale prices → wrong risk assessment

**Current coverage:** The library exposes `POST /portfolio/{accountId}/positions/invalidate` as `InvalidatePortfolioCacheAsync()`, but it's not called automatically. The consumer must know to call it.

**Risk:** Trading decisions based on stale data. Severity depends on how stale and what decision is being made.

**Mitigation:** Optional auto-invalidation before position reads:

```csharp
public class IbkrClientOptions
{
    /// <summary>
    /// When true, automatically calls positions/invalidate before each GetPositionsAsync call.
    /// Adds one extra API call per positions request but ensures fresh data.
    /// Default is false.
    /// </summary>
    public bool InvalidateCacheBeforePositionReads { get; set; }
}
```

In `PortfolioOperations.GetPositionsAsync`:
```csharp
if (_options.InvalidateCacheBeforePositionReads)
{
    await _api.InvalidatePositionCacheAsync(accountId, cancellationToken);
}
return await _api.GetPositionsAsync(accountId, page, cancellationToken);
```

Simple, predictable, consumer controls the trade-off between freshness and API call volume.

**Effort:** Low. One conditional call in `PortfolioOperations`, one option property.

---

### 9. Position DTO Reused Across Incompatible Endpoints

**Failure mode:** The `Position` record type is used for both:
- `GET /portfolio/{accountId}/positions/{page}` — returns fields like `mktPrice`, `mktValue`, `contractDesc`
- `GET /portfolio2/{accountId}/positions` — returns fields like `marketPrice`, `marketValue`, `description`

These are different wire formats mapping to the same DTO. Fields from the wrong endpoint land in `[JsonExtensionData]` instead of named properties. A consumer accessing `position.MarketPrice` gets the correct value from one endpoint but `0` from the other.

**Current coverage:** The schema validator flagged this mismatch. The `[JsonExtensionData]` property captures the "wrong-name" fields, but consumers would need to know to look there.

**Risk:** Silent data loss. Consumer code that works with standard positions silently breaks with real-time positions (or vice versa).

**Mitigation:** Create a separate `RealTimePosition` record type for the `/portfolio2` endpoint with its own field names matching the wire format:

```csharp
public record RealTimePosition(
    [property: JsonPropertyName("position")] decimal Quantity,
    [property: JsonPropertyName("conid")] string Conid,  // Note: string in portfolio2, int in portfolio
    [property: JsonPropertyName("marketPrice")] decimal MarketPrice,
    [property: JsonPropertyName("marketValue")] decimal MarketValue,
    [property: JsonPropertyName("description")] string? Description,
    // ... portfolio2-specific fields
);
```

Update `IPortfolioOperations.GetRealTimePositionsAsync` to return `List<RealTimePosition>` instead of `List<Position>`.

**Effort:** Low-Medium. New DTO, update Refit interface return type, update operations interface and implementation, update tests.

---

## Lower Severity — Configuration & Operational

### 10. Options Not Validated

**Failure mode:** `IbkrClientOptions` properties accept any value without range validation. Invalid configurations cause failures at runtime with unhelpful error messages:

- `TickleIntervalSeconds = 0` → tickle fires in a tight loop, floods the API with requests, likely triggers rate limiting or ban
- `TickleIntervalSeconds = -1` → `TimeSpan.FromSeconds(-1)` creates a negative timespan → `PeriodicTimer` throws `ArgumentOutOfRangeException` deep in the timer implementation
- `ProactiveRefreshMargin = TimeSpan.FromDays(365)` → proactive refresh never triggers (margin exceeds token lifetime) → token expires unexpectedly
- `PreflightCacheDuration = TimeSpan.Zero` → every market data call makes a pre-flight request → doubles API call volume
- `BaseUrl = "not-a-url"` → `UriFormatException` during Refit client construction with unhelpful stack trace

**Current coverage:** Only `Credentials != null` is validated. All other properties use their values directly.

**Risk:** Misconfiguration causes confusing runtime failures. Low severity individually but high annoyance factor.

**Mitigation:** Validate all options immediately after the configure lambda runs, before any DI registration:

```csharp
private static void ValidateOptions(IbkrClientOptions options)
{
    ArgumentNullException.ThrowIfNull(options.Credentials, "IbkrClientOptions.Credentials");

    if (options.TickleIntervalSeconds <= 0)
        throw new IbkrConfigurationException(
            $"TickleIntervalSeconds must be positive, got {options.TickleIntervalSeconds}");

    if (options.PreflightCacheDuration <= TimeSpan.Zero)
        throw new IbkrConfigurationException(
            $"PreflightCacheDuration must be positive, got {options.PreflightCacheDuration}");

    if (options.ProactiveRefreshMargin <= TimeSpan.Zero)
        throw new IbkrConfigurationException(
            $"ProactiveRefreshMargin must be positive, got {options.ProactiveRefreshMargin}");

    if (options.BaseUrl is not null && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        throw new IbkrConfigurationException(
            $"BaseUrl is not a valid absolute URI: '{options.BaseUrl}'");
}
```

New `IbkrConfigurationException` type (extends `InvalidOperationException`, not `IbkrApiException` — this is a configuration error, not an API error).

**Effort:** Low. Validation method, new exception type, call from `AddIbkrClient`.

---

### 11. No Request/Response Audit Log

**Failure mode:** When something goes wrong in production, there's no way to answer "what did the library actually send and receive?" The existing observability (OTel spans + metrics + structured logging) captures:
- ✅ Timing and duration
- ✅ Status codes
- ✅ Error messages
- ✅ Span hierarchy (which handler processed what)

But does NOT capture:
- ❌ Request body (what order was actually submitted?)
- ❌ Response body (what did IBKR actually return?)
- ❌ Request headers (was the auth header correct?)
- ❌ Full URL with query parameters

Without payloads, debugging production issues requires reproducing the problem and adding ad-hoc logging.

**Current coverage:** None. `ErrorNormalizationHandler` reads the response body for error detection but doesn't log it. `OAuthSigningHandler` constructs the auth header but doesn't log the signed request.

**Risk:** Extended debugging time in production. Not a safety risk per se, but significantly impacts incident response time.

**Mitigation:** Optional `AuditLogHandler` in the pipeline:

```csharp
public class IbkrClientOptions
{
    /// <summary>
    /// When true, logs request and response bodies at Debug level.
    /// Bodies are sanitized: Authorization headers are redacted, only the first
    /// 4KB of response bodies are logged. Disabled by default.
    /// </summary>
    public bool EnableRequestAuditLog { get; set; }
}
```

Handler logs at `Debug` level (invisible unless consumer configures Debug logging for the `IbkrConduit.Http.AuditLogHandler` category):
```
[Debug] → POST /v1/api/iserver/account/U1234567/orders
         Body: {"orders":[{"conid":756733,"side":"BUY","quantity":1,...}]}
[Debug] ← 200 OK (143ms)
         Body: [{"order_id":"602801486","order_status":"PreSubmitted"}]
```

Sanitization rules:
- Authorization header: replaced with `"REDACTED"`
- Response body: truncated to 4KB with `[truncated]` marker
- Request body: logged as-is (contains order parameters, not secrets)

Pipeline position: outermost handler (before `TokenRefreshHandler`) to capture the full request/response including retry behavior.

**Effort:** Medium. New `DelegatingHandler`, sanitization logic, body buffering (same pattern as `ErrorNormalizationHandler`), pipeline wiring, option flag.

---

## Summary

| # | Finding | Severity | Current Coverage | Mitigation Effort |
|---|---------|----------|-----------------|-------------------|
| 1 | No order guardrails | **Critical** | None | Medium |
| 2 | No duplicate order detection | **Critical** | None | Medium |
| 3 | Auto-confirm all order questions | **Critical** | Partial (suppress list) | Medium |
| 4 | Credential validation is lazy | **High** | Null check only | Low |
| 5 | No session health observable | **High** | Internal state only | Medium |
| 6 | SuppressMessageIds accepted blindly | **High** | Advisory constants | Low |
| 7 | Response type drift not detected | **Medium** | Field names only | Medium |
| 8 | Stale position data | **Medium** | Manual invalidation | Low |
| 9 | Position DTO reused across incompatible endpoints | **Medium** | Schema validator flagged | Low-Medium |
| 10 | Options not validated | **Lower** | Credentials only | Low |
| 11 | No request/response audit log | **Lower** | OTel spans only | Medium |

## Recommended Implementation Order

**Phase 1 — Quick wins (Low effort, high/critical impact):**
- #10 Options validation
- #4 Startup credential validation
- #6 SuppressMessageIds validation

**Phase 2 — Order safety (Medium effort, critical impact):**
- #1 Order guardrails
- #2 Duplicate order detection
- #3 Selective question answers

**Phase 3 — Operational maturity (Medium effort, high/medium impact):**
- #5 Session health observable
- #9 Position DTO split
- #7 Response type validation

**Phase 4 — Nice to have:**
- #8 Auto-invalidate position cache
- #11 Audit log handler
