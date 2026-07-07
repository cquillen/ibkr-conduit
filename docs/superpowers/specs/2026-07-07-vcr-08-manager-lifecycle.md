# VCR-08 — Manager lifecycle integrity

**Story:** VCR-08 (`docs/backlog.md`) · **Findings:** MGR-1 (high), MGR-2, MGR-3, MGR-6 (verified 2026-07-07 — see below) · **Decides by:** rule-settled cites below + operator decision 2026-07-07 (MGR-1 mechanism) · **Semver:** `fix:` (no public-surface change) · **Risk:** high (credential handling)

**MGR-6 verification (was UNVERIFIED):** CONFIRMED 2026-07-07 — `ValidateOptions` is `private static` in `ServiceCollectionExtensions.cs:163` with its only call site at line 56 (`AddIbkrClient`); `IbkrClientManager.AddAsync` (lines 41-45) and `TenantBuilder.BuildAsync` build the effective options with zero validation.

## Decisions (all closed)

- **MGR-1 mechanism (operator-decided):** internal bound, **no new public surface** — the caller's `CancellationToken` threads into teardown via a linked CTS with an internal cap on the best-effort logout; the duplicate logout is dropped. (Public `IManagedTenant.DisposeAsync(CancellationToken)` rejected — the internal bound delivers the guarantee without growing the published interface.)
- **CT obligation is rule-settled:** `.claude/rules/code-style.md` — every async method passes its `CancellationToken` through the entire call chain.
- **MGR-2 authority is the documented contract:** `AddAsync`'s unconditional credential ownership is already documented; the code is fixed to it (no contract change).

## Scope

1. **Cancellable, bounded teardown (MGR-1):** `RemoveAsync(CancellationToken)` threads the token through `ManagedTenant.DisposeAsync`'s teardown: the best-effort `LogoutAsync` runs under a linked CTS (caller token + internal cap, so teardown is bounded even with `CancellationToken.None`); the redundant second logout (ManagedTenant's explicit + SessionManager.DisposeAsync's) is deduplicated to one. Cancellation abandons the logout, never the resource disposal.
2. **Credential ownership on all throw paths (MGR-2):** `AddAsync` disposes `normalized`/`credentials` on every failure path that precedes builder ownership — the throwing `configureOverrides` callback, the `ArgumentException` guard, and the `ObjectDisposedException` guard (track whether `builder.BuildAsync` was invoked; the stale "builder already disposed" comment is corrected).
3. **Add/dispose race (MGR-3):** after its successful `TryUpdate`, `AddAsync` re-checks `Volatile.Read(ref _disposed)`; if disposed meanwhile → `TryRemove`, `await tenant.DisposeAsync()`, throw `ObjectDisposedException`. `DisposeAsync` drains until the dictionary is empty (loops over sentinels/late adds) instead of a one-shot key snapshot.
4. **Manager-path validation (MGR-6):** `ValidateOptions` becomes `internal` and `AddAsync` invokes it on the effective (cloned + overridden) options **before** sentinel-holding network work, failing fast with the same `ArgumentException` shapes as `AddIbkrClient` — and disposing credentials per (2).

## Out of scope

- Rate-limiter/gauge disposal — VCR-09 (metrics hygiene).
- Baseline validation in `AddIbkrClientManager` itself is included only insofar as `AddAsync` validates the effective options — per-tenant overrides are the confirmed gap.

## Acceptance criteria

- A `RemoveAsync` with a cancelled/short-timeout token returns promptly (logout abandoned, resources still disposed); with no token, teardown is bounded by the internal cap (fake a hanging logout via WireMock delay).
- Every `AddAsync` failure path disposes the caller's credentials exactly once (throwing override callback, bad args, disposed manager — assert via a tracking-disposable credentials fake).
- `DisposeAsync` racing an in-flight `AddAsync` leaves no live tenant: the late-built tenant is disposed and `AddAsync` throws `ObjectDisposedException` (deterministic interleaving via a gate inside a test override).
- An invalid per-tenant override (`TickleIntervalSeconds = -1`, malformed `BaseUrl`) fails `AddAsync` fast with the `AddIbkrClient`-shaped `ArgumentException`, before any network call.

## Test plan (TDD)

Red tests from the findings' suggested regression tests (MGR-1/2/3/6): unit tests on `IbkrClientManager` with fake tenants/builders for the race and ownership paths; WireMock integration for the bounded-teardown timing; validation tests mirror the existing `AddIbkrClient` validation suite.
