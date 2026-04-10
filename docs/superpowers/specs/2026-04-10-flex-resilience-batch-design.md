# Flex Resilience & Validation Batch — Design Spec

## Goal

Add proactive rate limiting, SendRequest retry, richer timeout diagnostics, retry jitter, attempt-count metrics, and startup Flex token validation to the Flex pipeline. These are six interrelated enhancements that collectively make the Flex Web Service integration production-grade.

## Background

The Flex pipeline currently has no proactive rate limiting (IBKR enforces 1 req/sec + 10 req/min per token), no retry on the initial SendRequest call, minimal timeout diagnostics, deterministic retry delays, no attempt-count observability, and no way to validate the Flex token at startup. Each of these was identified in the safety features analysis and confirmed during the diagnostic session where a Cash Transactions query hung indefinitely.

## Design

### 1. Token-Level Rate Limiter

Reuse the existing `GlobalRateLimitingHandler` (a `DelegatingHandler` that wraps `System.Threading.RateLimiting.TokenBucketRateLimiter`) by wiring it into the Flex HttpClient pipeline. This follows the same architectural pattern as the Refit consumer pipeline, which stacks `GlobalRateLimitingHandler` + `EndpointRateLimitingHandler`.

Two handler instances are stacked to enforce two independent constraints:

**Burst limiter (1 req/sec):** `TokenBucketRateLimiter` with `TokenLimit=1, ReplenishmentPeriod=1s, TokensPerPeriod=1, QueueLimit=50`.

**Sustained limiter (10 req/min):** `TokenBucketRateLimiter` with `TokenLimit=10, ReplenishmentPeriod=1min, TokensPerPeriod=10, QueueLimit=50`.

Both are created in `StreamingAndFlexRegistration` and wired into the Flex named HttpClient pipeline:

```csharp
var flexBurstLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 1,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    TokensPerPeriod = 1,
    AutoReplenishment = true,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 50,
});

var flexSustainedLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 10,
    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
    TokensPerPeriod = 10,
    AutoReplenishment = true,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 50,
});

services.AddHttpClient(flexClientName, c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("IbkrConduit/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
})
.AddHttpMessageHandler(sp =>
    new GlobalRateLimitingHandler(
        flexBurstLimiter,
        sp.GetRequiredService<ILogger<GlobalRateLimitingHandler>>()))
.AddHttpMessageHandler(sp =>
    new GlobalRateLimitingHandler(
        flexSustainedLimiter,
        sp.GetRequiredService<ILogger<GlobalRateLimitingHandler>>()));
```

**Key benefits of this approach:**
- Reuses `GlobalRateLimitingHandler` as-is — no new handler class
- Rate limiting lives in the HTTP pipeline (transport layer), not in `FlexOperations` (application layer) — architecturally correct
- Logging, metrics, queue depth, and rejection handling are inherited from the existing handler
- `FlexOperations` doesn't need to know about rate limits — they're transparent
- The existing poll delay schedule in `FlexOperations` still applies on top (defense in depth)

**No `FlexRateLimiter` class needed.** The spec originally proposed a custom class; this revision eliminates it entirely by reusing existing infrastructure.

### 2. SendRequest Retry on Transient Errors

Wrap the SendRequest + `ExtractReferenceCode` call in a retry loop in `FlexOperations.ExecuteInternalAsync`:

```
maxSendAttempts = 3
for attempt in 1..maxSendAttempts:
    await rateLimiter.WaitAsync(ct)
    sendResult = await FlexClient.SendRequestAsync(...)
    if transport error → return Result.Failure (don't retry network errors)
    referenceCodeResult = ExtractReferenceCode(sendResult)
    if success → proceed to poll loop
    if permanent error → return Result.Failure
    if retryable → log, delay with jitter, continue
return Result.Failure (last error seen)
```

Uses the same `FlexErrorCodes` classification that `BuildFlexErrorFromResponse` already produces (`IbkrFlexError.IsRetryable`). Max 3 attempts (not configurable — SendRequest failures are rare). Delays use the same ramp-up schedule as the poll loop (1s, 2s, 3s) with jitter applied.

Rate limit errors (1018) get the same 10-second backoff used in the poll loop.

### 3. Richer Timeout Context

When the poll loop (or SendRequest retry) exhausts the timeout, the `IbkrFlexError` includes:

**Current message:**
```
Flex statement generation did not complete within 120s for reference code '123456'.
```

**New message:**
```
Flex statement generation did not complete within 120s for reference code '123456'.
Attempted 24 polls; last response: code 1019 (Statement generation in progress).
If using an Activity Flex query with 'Breakout by Day' enabled, disabling it in
the IBKR portal may significantly reduce generation time.
```

Implementation: track `lastErrorCode` and `lastErrorMessage` across poll iterations. Include them in the timeout `IbkrFlexError.Message`. The breakout hint is always included (cheap string, useful when it applies, ignored when it doesn't).

For SendRequest timeout (all 3 attempts returned retryable errors):
```
Flex SendRequest did not succeed after 3 attempts for query '1464458'.
Last response: code 1018 (Too many requests).
```

### 4. Retry Jitter

Apply ±20% random jitter to all delays in both the poll loop and SendRequest retry loop. Uses `Random.Shared` (thread-safe in .NET 8+):

```csharp
private static int ApplyJitter(int baseDelayMs)
{
    var jitter = (int)(baseDelayMs * 0.2 * (Random.Shared.NextDouble() * 2 - 1));
    return Math.Max(100, baseDelayMs + jitter); // floor at 100ms to avoid zero/negative delays
}
```

Applied after selecting the base delay from the schedule (for poll loop) or the ramp-up (for SendRequest retry). The 10-second 1018 rate-limit backoff also gets jitter.

### 5. Attempt-Count Metric

New histogram instrument on `FlexOperations`:

```csharp
private static readonly Histogram<int> _pollAttempts =
    IbkrConduitDiagnostics.Meter.CreateHistogram<int>(
        "ibkr.conduit.flex.poll.attempts", "attempts",
        "Number of GetStatement polls per query execution");
```

Recorded once per `ExecuteInternalAsync` completion (success or failure), with the final attempt count. Lets consumers track p50/p99 attempt counts for SLO dashboards. Includes a `status` tag: `"success"`, `"timeout"`, or `"error"`.

### 6. Startup Flex Token Validation

`ValidateConnectionAsync` gains a `validateFlex` parameter:

```csharp
// IIbkrClient
Task ValidateConnectionAsync(
    bool validateFlex = true,
    CancellationToken cancellationToken = default);
```

- `validateFlex: true` (default) — validates Flex if token + at least one query ID are configured
- `validateFlex: false` — skips Flex validation entirely

**Implementation in `IbkrClient`:**

```csharp
public async Task ValidateConnectionAsync(
    bool validateFlex = true, CancellationToken cancellationToken = default)
{
    await _sessionManager.EnsureInitializedAsync(cancellationToken);

    if (validateFlex && _options.FlexToken is not null)
    {
        var queryId = _options.FlexQueries.CashTransactionsQueryId
            ?? _options.FlexQueries.TradeConfirmationsQueryId;

        if (queryId is not null)
        {
            await ValidateFlexTokenAsync(queryId, cancellationToken);
        }
        else
        {
            LogFlexValidationSkipped();
        }
    }
}
```

**`ValidateFlexTokenAsync`** calls `_flex.ExecuteQueryAsync(queryId, ct)` and interprets the result:

- `Result.Success` → Flex is working, no action needed
- `Result.Failure` with `IbkrFlexError { ErrorCode: 1015 }` (token invalid) → throw `IbkrConfigurationException` with `CredentialHint = "FlexToken"` and message: "Flex token is invalid — generate a new token in the IBKR portal (Reports → Flex Queries → Flex Web Configuration)"
- `Result.Failure` with `IbkrFlexError { ErrorCode: 1012 }` (token expired) → throw `IbkrConfigurationException` with `CredentialHint = "FlexToken"` and message: "Flex token has expired — generate a new token in the IBKR portal"
- `Result.Failure` with `IbkrFlexError { ErrorCode: 1013 }` (IP restriction) → throw `IbkrConfigurationException` with `CredentialHint = "FlexToken"` and message: "Flex token rejected due to IP restriction — check the allowed IP list in the IBKR portal"
- `Result.Failure` with any other error → log a warning but don't throw. The token was accepted (the error is query-level, not auth-level).
- `Result.Failure` with `IbkrApiError` (transport error) → throw `IbkrConfigurationException` wrapping the transport error: "Could not reach the Flex Web Service — check network connectivity"

**Documentation:** `ValidateConnectionAsync` XML doc comment will note that Flex validation runs a real query, which takes a few seconds. Consumers who want faster startup can pass `validateFlex: false`. At least one query ID must be configured in `FlexQueries` for Flex validation to run.

## Files

### Modified Files

| Path | Change |
|------|--------|
| `src/IbkrConduit/Http/StreamingAndFlexRegistration.cs` | Create two `TokenBucketRateLimiter` instances (burst + sustained), wire `GlobalRateLimitingHandler` into Flex HttpClient pipeline |
| `src/IbkrConduit/Client/FlexOperations.cs` | SendRequest retry loop, richer timeout message, jitter, attempt-count metric |
| `src/IbkrConduit/Client/IbkrClient.cs` | `validateFlex` parameter + Flex token validation logic |
| `src/IbkrConduit/Client/IIbkrClient.cs` | `validateFlex` parameter on `ValidateConnectionAsync` + doc update |

## Testing

### Unit Tests

**Flex rate limiting (via pipeline):**
- Flex HttpClient pipeline has two `GlobalRateLimitingHandler` instances (burst + sustained)
- Integration test: concurrent Flex calls are rate-limited (don't produce 1018 errors)
- The existing `GlobalRateLimitingHandler` unit tests already cover handler behavior; no new handler tests needed

**SendRequest retry:**
- Retryable error (1018) → retries up to 3 times then fails with last error
- Retryable error on first attempt, success on second → returns reference code
- Permanent error → immediate failure, no retry
- Transport error → immediate failure, no retry
- Rate limiter is called before each attempt

**Richer timeout context:**
- Timeout message includes attempt count and last error code
- Timeout message includes breakout-by-day hint

**Jitter:**
- Delays are not exactly equal to base schedule (statistical test: run N times, verify variance > 0)
- Delays stay within ±20% of base (verify bounds)
- Floor of 100ms is respected

**Attempt-count metric:**
- Successful query records attempt count with `status=success`
- Timed-out query records attempt count with `status=timeout`
- Failed query records attempt count with `status=error`

**Flex validation:**
- Flex token + query ID configured → runs query, success → no exception
- Flex token + query ID configured → query returns 1015 → throws `IbkrConfigurationException` with `CredentialHint = "FlexToken"`
- Flex token + query ID configured → query returns 1012 → throws `IbkrConfigurationException`
- Flex token configured but no query IDs → skips validation, no exception
- Flex token not configured → skips validation entirely
- `validateFlex: false` → skips validation regardless of configuration
- Transport error during validation → throws `IbkrConfigurationException`

### Integration Tests

- `ValidateConnectionAsync()` with valid Flex token + query ID → no exception
- `ValidateConnectionAsync(validateFlex: false)` → skips Flex, no exception
- Rate limiter doesn't interfere with single-query normal flow (no unexpected delays)

## Scope Boundaries

### In Scope
- Flex HttpClient rate limiting via two stacked `GlobalRateLimitingHandler` instances (burst 1/sec + sustained 10/min)
- SendRequest retry loop (max 3 attempts)
- Richer timeout error messages with attempt count, last code, breakout hint
- ±20% jitter on all retry/poll delays
- `ibkr.conduit.flex.poll.attempts` histogram metric
- `validateFlex` parameter on `ValidateConnectionAsync`
- Flex token validation using a configured query ID
- Unit and integration tests for all 6 items

### Out of Scope
- Making rate limit parameters configurable via `IbkrClientOptions` (hardcoded 1/sec + 10/min matches IBKR's documented limits — expose as options only if consumers need to adjust)
- Rate limiting on the non-Flex pipeline (already handled by `GlobalRateLimitingHandler` and `EndpointRateLimitingHandler`)
- New `IbkrTimeoutError` subtype (timeout stays as `IbkrFlexError(ErrorCode: 0)`)
- Changes to `FlexClient` transport layer (unchanged — all logic in `FlexOperations`)
- Changes to `FlexResultParser` (unchanged)
