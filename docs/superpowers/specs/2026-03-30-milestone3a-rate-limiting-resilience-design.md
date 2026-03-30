# Milestone 3a — Rate Limiting + Resilience

**Date:** 2026-03-30
**Status:** Draft
**Goal:** Add rate limiting and resilience to the HTTP pipeline so requests respect IBKR's rate limits and recover from transient failures.

---

## Scope

M3a adds two DelegatingHandlers for rate limiting (global and per-endpoint) and one for resilience (Polly retry with exponential backoff). After M3a, the library:

1. Enforces a global 10 req/s token bucket per tenant
2. Enforces stricter per-endpoint limits for known slow endpoints
3. Waits asynchronously when rate-limited (transparent to consumers)
4. Retries transient HTTP errors (5xx, 408, 429) with exponential backoff
5. Rejects requests with a clear exception when queues are full

### Deferred

- **429 adaptive control loop** — the design doc §8.5 describes dynamically tightening/relaxing the rate limiter on 429. Deferred per YAGNI — the local token bucket is the primary defense, and 429s should be rare. Add later if observed in practice.
- **Circuit breaker** — `SessionManager` already handles session-level failures. No competing recovery mechanism needed.
- **Order management** — M3b scope.

---

## Architecture

### HTTP Pipeline (updated)

```
Consumer API call
    │
    ▼
Refit (generated client)
    │
    ▼
TokenRefreshHandler (catches 401, retries once after re-auth)
    │
    ▼
ResilienceHandler (Polly retry: 5xx, 408, 429 with exponential backoff)
    │
    ▼
GlobalRateLimitingHandler (10 req/s token bucket, queue 500)
    │
    ▼
EndpointRateLimitingHandler (per-endpoint token buckets, queue 50)
    │
    ▼
OAuthSigningHandler (HMAC-SHA256 signing + User-Agent)
    │
    ▼
HttpClient → IBKR API
```

Retries go back through rate limiting and signing — each retry acquires a fresh rate limit token and gets a fresh OAuth timestamp.

### Internal Session API Pipeline

```
Refit (IIbkrSessionApi)
    │
    ▼
ResilienceHandler
    │
    ▼
GlobalRateLimitingHandler
    │
    ▼
EndpointRateLimitingHandler
    │
    ▼
OAuthSigningHandler
    │
    ▼
HttpClient → IBKR API
```

Same as consumer pipeline but without `TokenRefreshHandler` (avoids recursive re-auth).

---

## Task 3a.1 — RateLimitRejectedException + NuGet Dependencies

### RateLimitRejectedException

```csharp
public class RateLimitRejectedException : Exception
{
    public RateLimitRejectedException(string message) : base(message) { }
    public RateLimitRejectedException(string message, Exception innerException) : base(message, innerException) { }
}
```

Thrown when the rate limiter queue is full and cannot accept more requests.

### NuGet Dependencies

- `Microsoft.Extensions.Resilience` — Polly v8 integration for retry pipeline
- `System.Threading.RateLimiting` is in-box for .NET 8+ (no package needed for the main library, but may need a package reference for the net8.0 target if not implicitly available)

---

## Task 3a.2 — GlobalRateLimitingHandler

### GlobalRateLimitingHandler : DelegatingHandler

**Constructor:** Takes a `RateLimiter` (the global token bucket instance, injected as singleton per-tenant).

**`SendAsync`:**
1. Call `_limiter.AcquireAsync(1, cancellationToken)` to wait for a token
2. If the lease is not acquired (`lease.IsAcquired == false`), throw `RateLimitRejectedException`
3. Call `base.SendAsync(request, cancellationToken)`
4. Dispose the lease

**Token Bucket Configuration:**
```csharp
new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 10,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    TokensPerPeriod = 10,
    AutoReplenishment = true,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 500,
})
```

The limiter is a singleton (per-tenant) injected into the transient handler.

---

## Task 3a.3 — EndpointRateLimitingHandler

### EndpointRateLimitingHandler : DelegatingHandler

**Constructor:** Takes a `IReadOnlyDictionary<string, RateLimiter>` mapping URL path patterns to their limiters.

**`SendAsync`:**
1. Extract the request URL path
2. Find the first matching pattern in the dictionary (match if the URL path contains the pattern)
3. If a match is found: call `_limiter.AcquireAsync(1, cancellationToken)`, throw `RateLimitRejectedException` if not acquired
4. If no match: pass through immediately
5. Call `base.SendAsync(request, cancellationToken)`

### Known Endpoint Limits

| Path Pattern | Token Limit | Replenish Period | Queue Limit |
|---|---|---|---|
| `/iserver/account/orders` | 1 | 5 seconds | 50 |
| `/iserver/account/trades` | 1 | 5 seconds | 50 |
| `/iserver/account/pnl/partitioned` | 1 | 5 seconds | 50 |
| `/iserver/marketdata/snapshot` | 10 | 1 second | 50 |
| `/iserver/scanner/params` | 1 | 15 minutes | 50 |
| `/iserver/scanner/run` | 1 | 1 second | 50 |
| `/portfolio/accounts` | 1 | 5 seconds | 50 |
| `/portfolio/subaccounts` | 1 | 5 seconds | 50 |

All use `TokenBucketRateLimiter` with `AutoReplenishment = true` and `QueueProcessingOrder.OldestFirst`.

The dictionary of limiters is a singleton (per-tenant) injected into the transient handler.

---

## Task 3a.4 — ResilienceHandler

### ResilienceHandler : DelegatingHandler

**Constructor:** Takes a `ResiliencePipeline<HttpResponseMessage>` (Polly v8 pipeline, injected as singleton).

**`SendAsync`:**
1. Execute `base.SendAsync` through the resilience pipeline
2. The pipeline handles retry logic transparently

### Polly Pipeline Configuration

```csharp
new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError)
            .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
            .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
        UseJitter = true,
        OnRetry = args => { /* log retry attempt */ },
    })
    .Build()
```

- **Retries:** 5xx, 408 (Request Timeout), 429 (Too Many Requests)
- **Does NOT retry:** Other 4xx errors (400, 401, 403, 404, etc.)
- **Backoff:** 1s → 2s → 4s with jitter
- **429 with Retry-After:** If the 429 response includes a `Retry-After` header, respect it (Polly's `HttpRetryStrategyOptions` supports this via `DelayGenerator`)

The pipeline is a singleton injected into the transient handler.

---

## Task 3a.5 — Pipeline Wiring

### ServiceCollectionExtensions Changes

Update `AddIbkrClient<TApi>` to register:

**Singletons (per-tenant):**
- Global `TokenBucketRateLimiter` (10 req/s, queue 500)
- Endpoint `IReadOnlyDictionary<string, RateLimiter>` (all known endpoint limiters)
- `ResiliencePipeline<HttpResponseMessage>` (Polly retry pipeline)

**Transient handlers:**
- `GlobalRateLimitingHandler`
- `EndpointRateLimitingHandler`
- `ResilienceHandler`

**Consumer API pipeline:**
```
Refit → TokenRefreshHandler → ResilienceHandler → GlobalRateLimitingHandler → EndpointRateLimitingHandler → OAuthSigningHandler → HttpClient
```

**Internal session API pipeline:**
```
Refit → ResilienceHandler → GlobalRateLimitingHandler → EndpointRateLimitingHandler → OAuthSigningHandler → HttpClient
```

---

## Task 3a.6 — Integration Tests

### WireMock Tests

**Test 1: Transient error retry**
- First request to `/v1/api/portfolio/accounts` → 503
- Second request → 200 with account data
- Assert: consumer sees 200, retry happened transparently

**Test 2: 429 retry with backoff**
- First request → 429
- Second request → 200
- Assert: consumer sees 200, delay between attempts

**Test 3: Non-retryable error passes through**
- Request → 400 Bad Request
- Assert: consumer sees 400 immediately, no retry

**Test 4: Rate limit queue rejection**
- Configure a very small rate limiter (1 token, 0 queue) for testing
- Submit multiple concurrent requests
- Assert: at least one throws `RateLimitRejectedException`

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Http/
    GlobalRateLimitingHandler.cs
    EndpointRateLimitingHandler.cs
    ResilienceHandler.cs
    RateLimitRejectedException.cs

tests/IbkrConduit.Tests.Unit/
  Http/
    GlobalRateLimitingHandlerTests.cs
    EndpointRateLimitingHandlerTests.cs
    ResilienceHandlerTests.cs

tests/IbkrConduit.Tests.Integration/
  Http/
    RateLimitingAndResilienceTests.cs
```

### Modified Files

```
src/IbkrConduit/Http/ServiceCollectionExtensions.cs
Directory.Packages.props (add Microsoft.Extensions.Resilience)
src/IbkrConduit/IbkrConduit.csproj (add PackageReferences)
```

---

## Dependency Graph

```
Task 3a.1 (exception + NuGet deps)
         │
         ├──────────────┬───────────────┐
         ▼              ▼               ▼
Task 3a.2 (global)  Task 3a.3 (endpoint)  Task 3a.4 (resilience)
         │              │               │
         └──────────────┴───────────────┘
                        │
                        ▼
                Task 3a.5 (pipeline wiring)
                        │
                        ▼
                Task 3a.6 (integration tests)
```

**Parallel opportunities:** Tasks 3a.2, 3a.3, and 3a.4 are independent (all depend only on 3a.1).
