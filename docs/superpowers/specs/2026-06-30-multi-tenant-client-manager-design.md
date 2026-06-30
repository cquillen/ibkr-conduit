# Design Spec — Dynamic Multi-Tenant Client Manager

- **Date:** 2026-06-30
- **Status:** Approved (ready for implementation planning)
- **Related:** [`multi-instance-readiness-review.md`](../../../multi-instance-readiness-review.md) (the multi-agent review that motivated this), [`ibkr_conduit_design.md`](../../ibkr_conduit_design.md) §8 (rate limiting)

## 1. Context & Motivation

A consuming application needs to host **multiple concurrent IbkrConduit instances in one process**, each bound to a different set of IBKR OAuth 1.0a credentials (a different account), and to **add and remove those instances at runtime without restarting**.

The multi-instance readiness review confirmed the library has **zero mutable static state** and that each credential's runtime graph (session, websocket, rate limiters, caches, health) is fully isolated — but the **only supported hosting model today is one `IServiceProvider` per credential**, which is not idiomatic .NET and exposes a foot-gun: calling `AddIbkrClient` twice on one `IServiceCollection` silently corrupts the pipeline. This spec adds a first-class, idiomatic, runtime-managed multi-tenant lifecycle.

Two facts ground the design:

- `AddIbkrClient` already delegates to four `Register(services, credentials, options, baseUrl)` methods — per-tenant graph construction is **already parameterized by credentials**, so a manager can reuse it almost verbatim.
- `IbkrOAuthCredentials` carries a `TenantId` and is `IDisposable`. **On disk** (`OAuthCredentialsFactory.FromFile`/`FromJson`) `TenantId` is set to the `ConsumerKey` — there is no distinct `tenantId` field in the credential JSON. From environment it is `IBKR_TENANT_ID ?? ConsumerKey`. Therefore `credentials.TenantId` is **not a reliable unique identity** for multi-tenancy (accounts sharing a third-party consumer key would collide), which is why the manager takes an explicit `tenantId`.

## 2. Goals, Scope & Non-Goals

### In scope
- **A — Dynamic manager core:** `IIbkrClientManager` that builds / gets / removes a fully-isolated per-tenant graph at runtime, with graceful teardown, a thread-safe registry, and credential ownership.
- **C — Per-tenant telemetry tagging:** stamp `TenantId` onto metrics, spans, and logs so tenants are distinguishable in one process.
- **D — Double-registration guard:** make the existing `AddIbkrClient`-twice foot-gun fail loud and point at the manager.

### Deferred (seam defined here, built later)
- **B — Shared process-wide IP rate governor** (protects the common-IP penalty box / concurrency ceiling). This spec defines the integration seam (a no-op `ISharedRateGovernor` seeded into every tenant and a pre-wired call site) so B is a pure implementation swap.
- **Two-account E2E test:** requires a second real paper account; gated follow-up.

### Non-goals
- No change to the single-account `AddIbkrClient` → `IIbkrClient` path or to any per-call trading/streaming API.
- No credential sourcing, secret-store integration, persistence of the active-tenant set, or onboarding triggers — those are the **application's** responsibility. The library owns the *mechanism* (build/teardown/isolate); the app owns the *policy* (which tenants, from where, when).
- No managed in-flight drain on remove (see §7); the app decides when removal is safe.

## 3. Architecture & Approach

A singleton `IIbkrClientManager`, registered in the application's root container via `AddIbkrClientManager`, builds **one child `ServiceProvider` per tenant** at runtime by running the existing `Register(...)` methods with that tenant's credentials + options. Each tenant therefore gets the exact fully-isolated graph the review verified — constructed at runtime rather than at startup. The manager owns the registry `{ TenantId → (childProvider, ownedCredentials, IIbkrClient) }` and those lifetimes. Disposing a tenant's child provider tears its whole graph down in one move — the lifecycle backbone.

**Supporting refactor:** extract the body of `AddIbkrClient` into an internal `BuildTenantServices(IServiceCollection, credentials, options, baseUrl)`. Both `AddIbkrClient` and the manager's per-tenant builder call it — one source of truth for a correctly-wired tenant graph, so the two paths cannot drift.

**Why child providers (rejected alternatives):** dynamic add/remove rules out every registration-time approach — `IServiceCollection` is frozen once built, so you cannot add keys, named `HttpClient`s, or handler chains for a tenant discovered at runtime. **Keyed DI** (can't add keys post-build) and a **single shared pipeline with per-request credential resolution** (reintroduces shared mutable state, loses clean per-tenant teardown) were both rejected. Child providers are the only model that supports runtime add *and* deterministic teardown.

**Placement:** in-core, additive. The `Register(...)` methods are `internal`, so a separate package would force exposing internals. The single-account path is untouched; the manager is opt-in.

## 4. Public API Surface

**Registration (root container, once):**

```csharp
services.AddIbkrClientManager(baseline =>
{
    baseline.TickleIntervalSeconds = 60;
    baseline.ProactiveRefreshMargin = TimeSpan.FromHours(1);
    // common defaults for every tenant; Credentials is NOT set here
});
```

Registers the `IIbkrClientManager` singleton, the no-op `ISharedRateGovernor` (B seam), and stores the baseline `IbkrClientOptions` as the per-tenant template.

**The manager interface:**

```csharp
public interface IIbkrClientManager : IAsyncDisposable
{
    Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken ct = default);

    bool TryGetClient(string tenantId, out IIbkrClient client);
    IIbkrClient GetClient(string tenantId);                 // throws if absent
    Task<bool> RemoveAsync(string tenantId, CancellationToken ct = default);
    IReadOnlyCollection<string> ActiveTenants { get; }
}
```

**Tenant identity.** The explicit `tenantId` is **authoritative** — registry key and the single identity propagated everywhere (HTTP client names, telemetry tags). The manager normalizes with `credentials with { TenantId = tenantId }` so one value flows through the graph. `credentials.TenantId` is **ignored** (no mismatch check — because on disk it defaults to the consumer key and would routinely differ from an app-chosen id).

**Effective-options flow per `AddAsync`:** clone the baseline → apply `configureOverrides` → set `Credentials` from the argument. Requires an internal `IbkrClientOptions.Clone()` that deep-copies the mutable members (`SuppressMessageIds`, `FlexQueries`) so an override cannot mutate the shared baseline. Credentials are conceptually removed from the per-tenant options surface — they are the `AddAsync` argument, the single source of truth.

**Consistency:** `AddAsync` returns the same `IIbkrClient` facade a single-account consumer gets; the entire per-call API is identical. The manager governs lifecycle only.

## 5. Internals

**Child-provider construction.** Per `AddAsync`: new `ServiceCollection` → `BuildTenantServices(...)` → seed shared root services → `BuildServiceProvider()` → store; resolve `IIbkrClient` from it.

**Seeded root services** (must be shared, not per-tenant): the app's `ILoggerFactory` (tenant logs flow to the same sinks) and the shared-governor instance (below). Everything else stays per-tenant as today.

**B seam (shared IP governor).** `AddIbkrClientManager` registers one root-level `ISharedRateGovernor` with a **no-op pass-through** implementation. The manager seeds *that same instance* into every child collection. The call site is **pre-wired now**: `GlobalRateLimitingHandler` calls `await _governor.AcquireAsync(ct)` before its own per-tenant limiter. Today this is a no-op; spec B replaces the implementation with the adaptive IP governor — no manager or handler change required. Per-tenant limiters are untouched; the governor is an additional shared gate.

**Telemetry tagging (C).** Seed a per-provider `TenantContext { string TenantId }` singleton (value = the explicit `tenantId`) into each child collection. The emitting components identified by the review — the rate-limit and signing handlers, the operations classes, `SessionManager`, `TickleTimer`, `IbkrWebSocketClient` — take it via DI and stamp `LogFields.TenantId` on their metric `Add`/`Record` `TagList`s and span `SetTag`s, plus a `BeginScope` for logs. Comprehensive (no blind spots). The static `ActivitySource`/`Meter`/metric instruments stay process-global and correct; only the emitted *measurements* gain the tenant dimension.

**Double-registration guard (D).** `AddIbkrClient` adds a private marker service; a second `AddIbkrClient` on the same collection throws `InvalidOperationException` whose message points at `IIbkrClientManager`. `AddIbkrClientManager` carries its own independent marker guarding against a double-call. The two **may coexist** in one container (managed tenants live in child providers, not the root), so that combination is not blocked.

**Testability seam.** The manager depends on an internal `ITenantBuilder` that performs `BuildTenantServices` + eager init + WS connect and returns a `ManagedTenant` (child provider + client + owned credentials). Unit tests fake it (no network); integration tests use the real builder against WireMock.

## 6. Lifecycle, Concurrency & Error Handling

**Registry & concurrency.** A thread-safe registry keyed by `tenantId`. `AddAsync` atomically *reserves* the key first — a concurrent duplicate add loses the race and throws; the slow eager work then runs **outside** any lock. `TryGetClient`/`GetClient` only return fully-initialized tenants — a tenant mid-build is treated as not-present until ready.

**`AddAsync` (eager):** validate + normalize creds → reserve slot → `BuildTenantServices` into a child provider → resolve `IIbkrClient` → force session init → `ConnectAsync` the WebSocket → publish entry as ready → return the client. A successful return means the tenant is authenticated and streaming. **On any failure** (bad creds, network, cancellation): dispose the child provider (unwinds anything started), dispose the owned credentials, drop the reservation, and propagate the underlying exception. No half-built tenant is left behind.

**Duplicate tenant:** `AddAsync` with an already-active `tenantId` throws `InvalidOperationException`. Credential rotation is `RemoveAsync` then `AddAsync` (an explicit `ReplaceAsync` may be added later if warranted — YAGNI for now).

**Credential ownership.** The manager **takes ownership** of the credentials passed to `AddAsync` and disposes them on remove, on failed add, and on manager disposal. Callers must not dispose them afterward (documented). This resolves the prior "credentials disposed too early" finding.

**`RemoveAsync` (abrupt teardown):**
1. Atomically remove the entry from the registry (absent → return `false`; concurrent remove → one wins).
2. Stop the tickle timer.
3. **Abrupt-cancel in-flight requests** (no managed drain; the app owns "is it safe to remove now", aided by `cOID`/`order_ref` reconciliation).
4. Close the WebSocket gracefully.
5. Best-effort `POST /logout` to end the IBKR brokerage session immediately (frees the server-side session slot; swallow + log failures).
6. Dispose the child `ServiceProvider` (tears down session manager, limiters, cache, etc.).
7. Dispose the owned credentials.

**Manager disposal.** `IIbkrClientManager` is `IAsyncDisposable`; `DisposeAsync` tears down all active tenants via the remove path, so disposing the manager on app shutdown logs out and cleans up every account.

**Error contract:** duplicate add → throws; failed add → throws + full cleanup; `GetClient` absent → throws; `TryGetClient`/`RemoveAsync` absent → `false`; teardown-step errors (e.g. `/logout`) → logged, non-fatal.

## 7. Testing Strategy

Per repo rules: xUnit v3, Shouldly (no `Assert`); unit = no I/O; integration = WireMock + full DI stack (no fakes); TDD red→green→refactor throughout.

**Unit (`Tests.Unit`)** — manager logic via the faked `ITenantBuilder`:
- duplicate `tenantId` → throws; add → `GetClient`/`ActiveTenants` reflect it; `GetClient` absent → throws; `TryGet`/`Remove` absent → `false`; concurrent same-id add → one wins / one throws.
- credential ownership: trackable fake creds assert `Dispose` on remove, on **failed** add, and on manager `DisposeAsync`; a failed add leaves nothing registered and disposes the partial provider.
- `IbkrClientOptions.Clone()` deep-copy isolation (override cannot mutate baseline).
- registration guard (D): second `AddIbkrClient` throws; second `AddIbkrClientManager` throws; the two coexist.
- telemetry (C): in-memory `MeterListener`/`ActivityListener` confirms a representative tagged component (one handler + `SessionManager`) emits the `TenantId` tag.

**Integration (`Tests.Integration`, WireMock, via `AddIbkrClientManager`):**
- eager `AddAsync` full flow: LST → `ssodh/init` → WS connect → a real API call succeeds.
- **two-tenant isolation (headline):** add two tenants concurrently with distinct ids/creds; assert WireMock sees each request signed with its own consumer key and each WS stream is independent.
- `RemoveAsync` hits `/logout`; subsequent `GetClient` throws.
- **401 recovery within a managed tenant** (mandatory per testing rules): a managed client's 401 → `TokenRefreshHandler` re-auths → retry succeeds.
- telemetry attribution: a call on each tenant carries its own `TenantId` tag.

**E2E (deferred):** a real two-account E2E gated on a second `EnvironmentFact` credential set; the maintainer currently has one paper account.

**TDD ordering (the plan will sequence):** `IbkrClientOptions.Clone` + guard (D) → `BuildTenantServices` extraction (covered by existing `AddIbkrClient` tests) → `ITenantBuilder` seam → manager registry/lifecycle (unit) → eager add + teardown (integration) → telemetry tagging (C) → governor seam (B no-op + call site).

`[ExcludeFromCodeCoverage]` only on trivial holders (`TenantContext`, `ManagedTenant`); the manager and builder are fully tested.

## 8. Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| Scope | A + C + D; B seam-only; E2E deferred | Ship the needed lifecycle now; B is design-heavy and independent |
| Approach | Factory-managed child `ServiceProvider`s | Only model supporting runtime add + deterministic teardown |
| Placement | In-core, additive | `Register(...)` methods are `internal` |
| `AddAsync` session init | Eager (auth + init + WS connect; throws on failure) | Fail-fast at onboarding beats deferred surprise |
| Duplicate `tenantId` | Throw | Explicit, symmetric verbs; never tear down a live session as a side effect |
| Tenant identity | Explicit `tenantId` authoritative; normalize creds; no mismatch check | On-disk `credentials.TenantId` = consumer key, not a reliable unique id |
| Per-tenant options | Baseline + per-tenant override; creds out of options | Tenants are near-identical; removes the creds-in-options foot-gun |
| Remove in-flight policy | Abrupt cancel (no managed drain) | App owns "safe to remove"; `cOID` reconciliation already exists |
| Remove teardown | Best-effort `/logout` before dispose | Frees the IBKR session slot immediately; avoids orphaned sessions on churn |
| Credential ownership | Manager owns + disposes | Resolves prior "disposed too early" finding |

## 9. Deferred / Follow-ups

- **Spec B:** adaptive shared IP rate governor (replace the no-op `ISharedRateGovernor`); covers the IP penalty-box back-off and the 5-concurrent-request ceiling. Its own brainstorm → spec → plan.
- **Two-account E2E** once a second paper account is available.
- **`ReplaceAsync`** for credential rotation, only if a real need emerges.

## 10. References

- `multi-instance-readiness-review.md` — the multi-agent readiness review (verdict: Ready with caveats) that motivated this work.
- `docs/ibkr_conduit_design.md` §8 — rate-limit semantics (per-username steady-state limit; IP-based penalty box).
- `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` — current `AddIbkrClient` and the four `Register(...)` methods to be reused.
- `src/IbkrConduit/Auth/OAuthCredentialsFactory.cs` — where `TenantId` defaults to `ConsumerKey`.
