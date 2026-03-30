# Milestone 2 — Session Lifecycle Management

**Date:** 2026-03-30
**Status:** Draft
**Goal:** Library can initialize a brokerage session, keep it alive, and recover from failures — validated against a paper account.

---

## Scope

M2 builds on M1's working OAuth pipeline to add full session lifecycle management. After M2, the library automatically:

1. Lazily initializes a brokerage session on first API request
2. Keeps the session alive with periodic tickle
3. Detects dead sessions and re-authenticates
4. Proactively refreshes the LST before its 24h expiry
5. Retries API requests that fail with 401 due to stale tokens
6. Shuts down cleanly (stop tickle, logout)

### Deferred

- **Multi-tenant wrapper** — M2 builds a single-tenant `SessionManager` with clean interfaces. A multi-tenant registry that manages N independent `SessionManager` instances is deferred until needed (likely M3 or M4).
- **Rate limiting and resilience** — M3 scope.
- **Maintenance window awareness** — not built explicitly. The re-auth logic naturally recovers from maintenance outages without calendar knowledge.

---

## Architecture

### HTTP Pipeline

```
Consumer API call
    │
    ▼
Refit (generated client)
    │
    ▼
TokenRefreshHandler : DelegatingHandler
    │  - Passes request through
    │  - If 401 response: triggers SessionManager.ReauthenticateAsync(), clones request, retries once
    │  - Skips retry for /tickle requests (dead tickle = dead session, not a retry case)
    │
    ▼
OAuthSigningHandler : DelegatingHandler
    │  - Calls SessionManager.EnsureInitializedAsync() (lazy init on first request)
    │  - Signs request with HMAC-SHA256 using LST from ISessionTokenProvider
    │  - Adds User-Agent header
    │
    ▼
HttpClient → IBKR API
```

### SessionManager (Internal)

```
SessionManager
    ├── ISessionTokenProvider  (LST acquisition + caching + refresh)
    ├── ITickleTimer           (periodic POST /tickle, failure notification)
    ├── IIbkrSessionApi        (Refit client for session endpoints)
    ├── IbkrClientOptions      (compete flag, suppression IDs)
    └── SemaphoreSlim(1,1)     (serializes all state transitions)
```

---

## Task 2.1 — Refit Session Interfaces + Response Models

### IIbkrSessionApi

```csharp
public interface IIbkrSessionApi
{
    [Post("/v1/api/iserver/auth/ssodh/init")]
    Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
        [Body] SsodhInitRequest request);

    [Post("/v1/api/tickle")]
    Task<TickleResponse> TickleAsync();

    [Post("/v1/api/iserver/questions/suppress")]
    Task<SuppressResponse> SuppressQuestionsAsync(
        [Body] SuppressRequest request);

    [Post("/v1/api/logout")]
    Task<LogoutResponse> LogoutAsync();
}
```

### Request/Response Models

**SsodhInitRequest:**
```csharp
public record SsodhInitRequest(
    [property: JsonPropertyName("publish")] bool Publish,
    [property: JsonPropertyName("compete")] bool Compete);
```

**SsodhInitResponse:**
```csharp
public class SsodhInitResponse
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    [JsonPropertyName("connected")]
    public bool Connected { get; init; }

    [JsonPropertyName("competing")]
    public bool Competing { get; init; }
}
```

**TickleResponse:**
```csharp
public class TickleResponse
{
    [JsonPropertyName("session")]
    public string Session { get; init; } = string.Empty;

    [JsonPropertyName("iserver")]
    public TickleIserverStatus? Iserver { get; init; }
}

public class TickleIserverStatus
{
    [JsonPropertyName("authStatus")]
    public TickleAuthStatus? AuthStatus { get; init; }
}

public class TickleAuthStatus
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    [JsonPropertyName("competing")]
    public bool Competing { get; init; }

    [JsonPropertyName("connected")]
    public bool Connected { get; init; }
}
```

**SuppressRequest:**
```csharp
public record SuppressRequest(
    [property: JsonPropertyName("messageIds")] List<string> MessageIds);
```

**SuppressResponse:**
```csharp
public class SuppressResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
```

**LogoutResponse:**
```csharp
public class LogoutResponse
{
    [JsonPropertyName("confirmed")]
    public bool Confirmed { get; init; }
}
```

---

## Task 2.2 — SessionTokenProvider Refresh Support

Extend the existing `ISessionTokenProvider` and `SessionTokenProvider` from M1:

### ISessionTokenProvider (updated)

```csharp
public interface ISessionTokenProvider
{
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);
    Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken);
}
```

### SessionTokenProvider Changes

- `RefreshAsync` acquires a new LST via `ILiveSessionTokenClient`, replaces the cached value, and returns the new token.
- Thread-safe: uses the existing `SemaphoreSlim(1,1)` — concurrent refresh calls are serialized, second caller gets the already-refreshed token.
- `GetLiveSessionTokenAsync` remains unchanged — returns cached token or acquires on first call.

---

## Task 2.3 — TickleTimer

### ITickleTimer

```csharp
internal interface ITickleTimer
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
```

### TickleTimer Implementation

- Constructor takes `IIbkrSessionApi` (for `POST /tickle`) and a failure callback `Func<CancellationToken, Task>` (to notify `SessionManager`)
- Uses `PeriodicTimer` with 60-second interval
- Runs in a background `Task` via `Task.Run` on `StartAsync`
- On each tick:
  1. Call `_sessionApi.TickleAsync()`
  2. Check `response.Iserver?.AuthStatus?.Authenticated`
  3. If `false` or if the call throws → invoke failure callback
- Single failed tickle logs a warning but doesn't immediately trigger re-auth — use a configurable threshold (default: 1 consecutive failure triggers re-auth). This avoids overreacting to transient network blips while still recovering promptly.
- `StopAsync` cancels the `CancellationTokenSource` and awaits the background task
- The timer is fire-and-forget safe — unhandled exceptions are caught and logged, never crash the host

---

## Task 2.4 — SessionManager

### ISessionManager

```csharp
internal interface ISessionManager : IAsyncDisposable
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);
    Task ReauthenticateAsync(CancellationToken cancellationToken);
}
```

Consumers access `ShutdownAsync` via `IAsyncDisposable.DisposeAsync()`.

### SessionManager Implementation

**State:**
```csharp
private enum SessionState { Uninitialized, Initializing, Ready, Reauthenticating, ShuttingDown }
```

**Fields:**
- `_state`: `SessionState`
- `_semaphore`: `SemaphoreSlim(1,1)` — serializes all state transitions
- `_sessionTokenProvider`: `ISessionTokenProvider`
- `_tickleTimer`: `ITickleTimer`
- `_sessionApi`: `IIbkrSessionApi`
- `_options`: `IbkrClientOptions`
- `_proactiveRefreshCts`: `CancellationTokenSource` for the proactive refresh timer

**`EnsureInitializedAsync`:**
1. If `_state == Ready` → return (fast path, no lock)
2. Acquire semaphore
3. Double-check `_state` (another caller may have initialized while we waited)
4. Acquire LST via `_sessionTokenProvider.GetLiveSessionTokenAsync()`
5. Call `_sessionApi.InitializeBrokerageSessionAsync(new SsodhInitRequest(true, _options.Compete))`
6. If `_options.SuppressMessageIds` is non-empty: call `_sessionApi.SuppressQuestionsAsync(new SuppressRequest(_options.SuppressMessageIds))`
7. Start tickle timer
8. Schedule proactive refresh (`Task.Delay` for LST expiry minus 1 hour, then call `ReauthenticateAsync`)
9. Set `_state = Ready`

**`ReauthenticateAsync`:**
1. Acquire semaphore — concurrent callers wait
2. If `_state == Ready` and the LST is not near expiry → return (another caller already refreshed)
3. Set `_state = Reauthenticating`
4. Stop tickle timer
5. Call `_sessionTokenProvider.RefreshAsync()` for new LST
6. Re-init brokerage session
7. Re-suppress questions
8. Restart tickle timer
9. Reschedule proactive refresh
10. Set `_state = Ready`

**`DisposeAsync`:**
1. Set `_state = ShuttingDown`
2. Stop tickle timer
3. Cancel proactive refresh timer
4. Call `_sessionApi.LogoutAsync()` (best-effort, don't throw)
5. Dispose semaphore

**Tickle failure callback:**
- Called by `TickleTimer` when session is detected as dead
- Calls `ReauthenticateAsync` — the semaphore ensures only one re-auth happens even if multiple tickle failures fire

---

## Task 2.5 — TokenRefreshHandler

### TokenRefreshHandler : DelegatingHandler

**Constructor:** Takes `ISessionManager`

**`SendAsync`:**
1. Clone/buffer the request content before sending (for potential replay)
2. Call `base.SendAsync(request, cancellationToken)`
3. If response status is **not 401** → return response
4. If request URL path contains `/tickle` → return response as-is (no retry for tickle)
5. Call `_sessionManager.ReauthenticateAsync(cancellationToken)`
6. Build a new `HttpRequestMessage` clone from the buffered content
7. Call `base.SendAsync(clone, cancellationToken)`
8. Return retry response (even if 401 again — single retry only, no loops)

**Request cloning:**
- Before the first send, read and buffer `request.Content` if present (small JSON payloads)
- Clone method: copy method, URI, headers, and buffered content into a new `HttpRequestMessage`
- This is necessary because `HttpRequestMessage` is single-use in .NET

---

## Task 2.6 — Pipeline Wiring + IbkrClientOptions

### IbkrClientOptions

```csharp
public record IbkrClientOptions
{
    public bool Compete { get; init; } = true;
    public List<string> SuppressMessageIds { get; init; } = new();
}
```

### ServiceCollectionExtensions Changes

Update `AddIbkrClient<TApi>` signature:

```csharp
public static IServiceCollection AddIbkrClient<TApi>(
    this IServiceCollection services,
    IbkrOAuthCredentials credentials,
    IbkrClientOptions? options = null) where TApi : class
```

Registers:
- `ILiveSessionTokenClient` (singleton) — unchanged from M1
- `ISessionTokenProvider` (singleton) — now with `RefreshAsync`
- `IIbkrSessionApi` — internal Refit client through the signing pipeline (but NOT through `TokenRefreshHandler` — session API calls made by `SessionManager` should not trigger recursive refresh)
- `ISessionManager` → `SessionManager` (singleton)
- `TokenRefreshHandler` (transient)
- `OAuthSigningHandler` (transient) — updated to call `EnsureInitializedAsync`
- Consumer's `TApi` Refit client: `Refit → TokenRefreshHandler → OAuthSigningHandler → HttpClient`

**Important:** The `IIbkrSessionApi` used internally by `SessionManager` must go through `OAuthSigningHandler` (for signing) but NOT through `TokenRefreshHandler` (to avoid recursive re-auth loops). This means two separate named `HttpClient` registrations.

---

## Task 2.7 — Integration Test: Session Lifecycle

### WireMock Tests

**Test 1: Full initialization flow**
- Mock `/oauth/live_session_token` → 200 with DH response
- Mock `/iserver/auth/ssodh/init` → 200 with `authenticated: true`
- Mock `/iserver/questions/suppress` → 200
- Mock `/tickle` → 200 with `authenticated: true`
- Mock `/portfolio/accounts` → 200
- Assert: all endpoints called in correct order, portfolio call succeeds

**Test 2: Tickle detects dead session → re-auth → recovery**
- First `/tickle` returns `authenticated: true`
- Second `/tickle` returns `authenticated: false`
- Mock fresh `/oauth/live_session_token` → 200
- Mock `/iserver/auth/ssodh/init` → 200
- Assert: re-auth triggered, new LST acquired, session re-initialized

**Test 3: 401 on API call → TokenRefreshHandler retries**
- First `/portfolio/accounts` → 401
- Mock fresh `/oauth/live_session_token` → 200
- Mock `/iserver/auth/ssodh/init` → 200
- Second `/portfolio/accounts` → 200
- Assert: consumer sees 200, re-auth happened transparently

**Test 4: Clean shutdown**
- Initialize session
- Call `DisposeAsync`
- Assert: `/logout` called, tickle timer stopped

### Paper Account E2E Test (skipped by default)

- Load credentials from environment
- Wire full pipeline with `SessionManager`
- Call `GET /portfolio/accounts` (triggers lazy init)
- Assert: response contains account, brokerage session initialized
- Dispose (triggers logout)

---

## Task 2.8 — Extract ibind Question Suppression IDs

After M2 is working and paper-tested:

1. Search ibind source for known suppressible message IDs
2. Cross-reference with IBKR's official documentation (suppressible-id section)
3. Document discovered IDs in a reference file (`docs/ibkr-suppressible-message-ids.md`)
4. These become the recommended default list for consumers to pass via `IbkrClientOptions.SuppressMessageIds`

This is a research/documentation task, not a code change.

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Session/
    ISessionManager.cs
    SessionManager.cs
    ITickleTimer.cs
    TickleTimer.cs
    IIbkrSessionApi.cs
    SsodhInitRequest.cs
    SsodhInitResponse.cs
    TickleResponse.cs
    SuppressRequest.cs
    SuppressResponse.cs
    LogoutResponse.cs
    IbkrClientOptions.cs
    TokenRefreshHandler.cs

tests/IbkrConduit.Tests.Unit/
  Session/
    SessionManagerTests.cs
    TickleTimerTests.cs
    TokenRefreshHandlerTests.cs
    SessionTokenProviderRefreshTests.cs

tests/IbkrConduit.Tests.Integration/
  Session/
    SessionLifecycleTests.cs
```

### Modified Files

```
src/IbkrConduit/
  Auth/
    ISessionTokenProvider.cs      — add RefreshAsync
    SessionTokenProvider.cs       — implement RefreshAsync
    OAuthSigningHandler.cs        — call EnsureInitializedAsync
  Http/
    ServiceCollectionExtensions.cs — register new components, add IbkrClientOptions
```

---

## Dependency Graph

```
Task 2.1 (session interfaces)    Task 2.2 (token refresh support)
         │                                │
         ├────────────────┬───────────────┘
         │                │
         ▼                │
Task 2.3 (tickle timer)   │
         │                │
         ├────────────────┘
         ▼
Task 2.4 (session manager)
         │
         ▼
Task 2.5 (token refresh handler)
         │
         ▼
Task 2.6 (pipeline wiring)
         │
         ▼
Task 2.7 (integration tests)
         │
         ▼
Task 2.8 (ibind suppression IDs)
```

**Parallel opportunities:** Tasks 2.1 and 2.2 can run in parallel. Tasks 2.3–2.8 are sequential.
