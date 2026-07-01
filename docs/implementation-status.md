# IbkrConduit — Implementation Status

Tracks implementation progress against [ibkr_conduit_design.md](ibkr_conduit_design.md).
Updated at the end of each implementation session.

## Status Key

| Status | Meaning |
|---|---|
| Not Started | No work begun |
| Spec'd | Design spec written |
| In Progress | Implementation underway |
| Done | Implemented and tested |

## Workflow

Each task follows TDD (Red-Green-Refactor) and the superpowers workflow (brainstorm, spec, plan, implement). Each task = 1 PR. Unit tests are baked into every task — not separate. Milestones are vertical slices validated against a real IBKR paper account.

---

## Repo Scaffolding (Done)

| Task | Status |
|---|---|
| Git init and foundation files | Done |
| Solution and project structure | Done |
| CI/CD pipelines | Done |
| Open source documents | Done |
| GitHub templates and config | Done |
| Claude Code configuration | Done |
| NuGet packaging and metadata | Done |

---

## Milestone 1 — First Authenticated API Call to Paper Account

**Goal:** Prove the OAuth 1.0a pipeline works end-to-end by calling `GET /portfolio/accounts` against a real IBKR paper account.

| # | Task | Status |
|---|---|---|
| 1.1 | OAuth key generation script | Done |
| 1.2 | OAuth credentials model + crypto primitives | Done |
| 1.3 | OAuth signature and header builder | Done |
| 1.4 | Live session token client | Done |
| 1.5 | OAuthSigningHandler + HTTP pipeline | Done |
| 1.6 | Portfolio accounts endpoint + paper account validation | Done |

---

## Milestone 2 — Session Lifecycle Management

**Goal:** Library can initialize a brokerage session, keep it alive, and recover from expiry — validated against a paper account.

| # | Task | Status |
|---|---|---|
| 2.1 | Refit session interfaces + response models | Done |
| 2.2 | SessionTokenProvider refresh support | Done |
| 2.3 | Tickle timer | Done |
| 2.4 | Session manager | Done |
| 2.5 | Token refresh handler (reactive 401 + proactive) | Done |
| 2.6 | Pipeline wiring + IbkrClientOptions | Done |
| 2.7 | Session lifecycle integration tests | Done |
| 2.8 | Suppressible message IDs + constants | Done |

---

## Milestone 3 — Order Management

**Goal:** Submit and cancel orders against a paper account, with rate limiting and resilience protecting the pipeline.

| # | Task | Status |
|---|---|---|
| 3a.1 | RateLimitRejectedException + NuGet deps | Done |
| 3a.2 | GlobalRateLimitingHandler (10 req/s) | Done |
| 3a.3 | EndpointRateLimitingHandler (per-endpoint) | Done |
| 3a.4 | ResilienceHandler (Polly retry) | Done |
| 3a.5 | Pipeline wiring | Done |
| 3a.6 | Integration tests | Done |
| 3b.1 | Contract Refit interface + models | Done |
| 3b.2 | Order Refit interface + models | Done |
| 3b.3 | Operations interfaces | Done |
| 3b.4 | OrderOperations (question/reply loop) | Done |
| 3b.5 | IIbkrClient facade | Done |
| 3b.6 | DI wiring update | Done |
| 3b.7 | Integration tests + SPY order E2E | Done |

---

## Milestone 4 — Portfolio + Market Data

**Goal:** Retrieve positions, account summary, and market data from a paper account.

| # | Task | Status |
|---|---|---|
| 4.1 | Portfolio Refit expansion + models | Done |
| 4.2 | MarketData Refit interface + models + MarketDataFields (110 constants) | Done |
| 4.3 | IPortfolioOperations expansion (10 new methods) | Done |
| 4.4 | IMarketDataOperations + pre-flight handling (MemoryCache) | Done |
| 4.5 | IIbkrClient facade update + DI wiring | Done |
| 4.6 | Integration tests + E2E (positions, summary, snapshot) | Done |

---

## Milestone 5 — WebSocket Streaming

**Goal:** Stream real-time order updates and market data from a paper account via WebSocket.

| # | Task | Status |
|---|---|---|
| 5.1 | ISessionLifecycleNotifier + wire into SessionManager | Done |
| 5.2 | IbkrWebSocketClient (heartbeat, message pump, reconnect) | Done |
| 5.3 | Streaming response models | Done |
| 5.4 | ChannelObservable + IStreamingOperations | Done |
| 5.5 | IIbkrClient facade + DI wiring | Done |
| 5.6 | Tests + WebSocket E2E | Done |

---

## Milestone 6 — Flex Web Service

**Goal:** Execute a Flex query against a paper account and parse trade confirmations and open orders.

| # | Task | Status |
|---|---|---|
| 6.1 | FlexClient + models + FlexQueryException | Done |
| 6.2 | IFlexOperations + FlexQueryResult (typed Trades/OpenPositions) | Done |
| 6.3 | IIbkrClient facade + DI wiring | Done |
| 6.4 | Tests + Flex E2E (paper account query) | Done |

---

## Observability — Tracing, Metrics, Structured Logging

**Goal:** Production-grade observability with zero external dependencies.

| # | Task | Status |
|---|---|---|
| O.1 | IbkrConduitDiagnostics + LogFields foundation | Done |
| O.2 | Distributed tracing (38 spans) | Done |
| O.3 | Metrics (34 instruments) | Done |
| O.4 | Structured logging audit (17 components) | Done |
| O.5 | Tests + observability consumer guide | Done |

---

## Milestone 7 — Production Readiness

**Goal:** Library is documented and has working samples demonstrating all major features.

| # | Task | Status |
|---|---|---|
| 7.1 | Samples project | Not Started |
| 7.2 | API documentation audit | Not Started |

---

## Dependency Upgrades

| Upgrade | Status | Notes |
|---|---|---|
| Refit `10.1.6` → `11.0.1` | Done | Refit 11 reworks the error model — pre-response send failures (transport faults, caller cancellation, handler-thrown exceptions) are now captured into `IApiResponse.Error` as `ApiRequestException` instead of propagating. Added `RefitResponseExtensions.ThrowOnSendFailure` (wired into both `ResultFactory.FromResponse` overloads and the Order/Fyi/Portfolio error paths) to re-throw the captured exception via `ExceptionDispatchInfo`, preserving the library's existing Refit-10 throwing semantics ("Option A"). The failures-as-values alternative ("Option B") is recorded in [future-enhancements.md](future-enhancements.md). |

---

## Order Placement — Native Bracket / OCA Support

| Enhancement | Status | Notes |
|---|---|---|
| Native bracket/OCA group placement + cOID correlation | Done | Added `cOID`, `parentId`, `isSingleGroup`, `outsideRTH` to `OrderRequest`/`OrderWireModel` (omit-when-null on the wire); `local_order_id`/`oca_group_id` on `OrderSubmissionResponse`; `PlaceOrdersAsync` for a single linked bracket/OCA group (validates linkage and returns the parent result — IBKR returns one response element per group and rejects unrelated bulk with 400); and typed `order_ref` on `LiveOrder` and streaming `OrderUpdate`. `OrderStatus` is intentionally excluded — its response carries no `order_ref` (spec, OpenAPI, and a live recording all confirm). |

---

## Milestone 8 — Dynamic Multi-Tenant Client Manager

**Goal:** Host multiple isolated IbkrConduit instances (one per credential/tenant) in a single process, with runtime add/remove. Spec: [multi-tenant-client-manager-design](superpowers/specs/2026-06-30-multi-tenant-client-manager-design.md).

| # | Task | Status |
|---|---|---|
| 8.1 | `IbkrClientOptions.Clone()` for per-tenant options | Done |
| 8.2 | Double-registration guard on `AddIbkrClient` / `AddIbkrClientManager` | Done |
| 8.3 | Extract `BuildTenantServices` (shared per-tenant graph builder) | Done |
| 8.4 | `ISharedRateGovernor` no-op seam (for a future shared IP governor) | Done |
| 8.5 | Per-tenant telemetry tagging (`TenantId` on metrics/spans/logs) | Done |
| 8.6 | `IManagedTenant` / `ITenantBuilder` / `TenantBuilder` | Done |
| 8.7 | `IIbkrClientManager` + `AddIbkrClientManager` | Done |
| 8.8 | `WebSocketBaseUrl` option (configurable WS endpoint) | Done |
| 8.9 | Integration tests (WireMock + mock WS): eager add, two-tenant isolation, remove/logout, 401 recovery, telemetry attribution | Done |

**Deferred follow-ups:** two-account real E2E (needs a second paper account); adaptive shared IP rate governor (replaces the no-op); option validation on the manager path; best-effort logout on eager-init failure.
