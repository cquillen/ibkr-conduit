# IbkrConduit — Implementation Status

Tracks implementation progress against [ibkr_conduit_design.md](ibkr_conduit_design.md).
Updated at the end of each implementation session.

## Status Key

| Status | Meaning |
|---|---|
| Not Started | No work begun |
| Spec'd | Design spec written in `docs/superpowers/specs/` |
| In Progress | Implementation underway |
| Done | Implemented and tested |

## Workflow

Each task follows TDD (Red-Green-Refactor) and the superpowers workflow (brainstorm, spec, plan, implement). Each task = 1 PR. Unit tests are baked into every task — not separate. Milestones are vertical slices validated against a real IBKR paper account.

---

## Repo Scaffolding (Done)

| Task | Status | Spec |
|---|---|---|
| Git init and foundation files | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Solution and project structure | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| CI/CD pipelines | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Open source documents | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| GitHub templates and config | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Claude Code configuration | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| NuGet packaging and metadata | Done | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |

---

## Milestone 1 — First Authenticated API Call to Paper Account

**Goal:** Prove the OAuth 1.0a pipeline works end-to-end by calling `GET /portfolio/accounts` against a real IBKR paper account.

| # | Task | Status | Spec |
|---|---|---|---|
| 1.1 | OAuth key generation script | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |
| 1.2 | OAuth credentials model + crypto primitives | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |
| 1.3 | OAuth signature and header builder | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |
| 1.4 | Live session token client | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |
| 1.5 | OAuthSigningHandler + HTTP pipeline | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |
| 1.6 | Portfolio accounts endpoint + paper account validation | Done | [M1 spec](superpowers/specs/2026-03-26-milestone1-oauth-pipeline-design.md) |

---

## Milestone 2 — Session Lifecycle Management

**Goal:** Library can initialize a brokerage session, keep it alive, and recover from expiry — validated against a paper account.

| # | Task | Status | Spec |
|---|---|---|---|
| 2.1 | Refit session interfaces + response models | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.2 | SessionTokenProvider refresh support | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.3 | Tickle timer | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.4 | Session manager | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.5 | Token refresh handler (reactive 401 + proactive) | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.6 | Pipeline wiring + IbkrClientOptions | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.7 | Session lifecycle integration tests | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |
| 2.8 | Suppressible message IDs + constants | Done | [M2 spec](superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md) |

---

## Milestone 3 — Order Management

**Goal:** Submit and cancel orders against a paper account, with rate limiting and resilience protecting the pipeline.

| # | Task | Status | Spec |
|---|---|---|---|
| 3a.1 | RateLimitRejectedException + NuGet deps | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3a.2 | GlobalRateLimitingHandler (10 req/s) | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3a.3 | EndpointRateLimitingHandler (per-endpoint) | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3a.4 | ResilienceHandler (Polly retry) | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3a.5 | Pipeline wiring | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3a.6 | Integration tests | Done | [M3a spec](superpowers/specs/2026-03-30-milestone3a-rate-limiting-resilience-design.md) |
| 3b.1 | Contract Refit interface + models | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.2 | Order Refit interface + models | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.3 | Operations interfaces | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.4 | OrderOperations (question/reply loop) | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.5 | IIbkrClient facade | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.6 | DI wiring update | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |
| 3b.7 | Integration tests + SPY order E2E | Done | [M3b spec](superpowers/specs/2026-03-31-milestone3b-order-management-design.md) |

---

## Milestone 4 — Portfolio + Market Data

**Goal:** Retrieve positions, account summary, and market data from a paper account.

| # | Task | Status | Spec |
|---|---|---|---|
| 4.1 | Portfolio Refit expansion + models | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |
| 4.2 | MarketData Refit interface + models + MarketDataFields (110 constants) | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |
| 4.3 | IPortfolioOperations expansion (10 new methods) | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |
| 4.4 | IMarketDataOperations + pre-flight handling (MemoryCache) | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |
| 4.5 | IIbkrClient facade update + DI wiring | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |
| 4.6 | Integration tests + E2E (positions, summary, snapshot) | Done | [M4 spec](superpowers/specs/2026-03-31-milestone4-portfolio-marketdata-design.md) |

---

## Milestone 5 — WebSocket Streaming

**Goal:** Stream real-time order updates and market data from a paper account via WebSocket.

| # | Task | Status | Spec |
|---|---|---|---|
| 5.1 | ISessionLifecycleNotifier + wire into SessionManager | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |
| 5.2 | IbkrWebSocketClient (heartbeat, message pump, reconnect) | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |
| 5.3 | Streaming response models | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |
| 5.4 | ChannelObservable + IStreamingOperations | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |
| 5.5 | IIbkrClient facade + DI wiring | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |
| 5.6 | Tests + WebSocket E2E | Done | [M5 spec](superpowers/specs/2026-03-31-milestone5-websocket-streaming-design.md) |

---

## Milestone 6 — Flex Web Service

**Goal:** Execute a Flex query against a paper account and parse trade confirmations and open orders.

| # | Task | Status | Spec |
|---|---|---|---|
| 6.1 | FlexClient + models + FlexQueryException | Done | [M6 spec](superpowers/specs/2026-03-31-milestone6-flex-web-service-design.md) |
| 6.2 | IFlexOperations + FlexQueryResult (typed Trades/OpenPositions) | Done | [M6 spec](superpowers/specs/2026-03-31-milestone6-flex-web-service-design.md) |
| 6.3 | IIbkrClient facade + DI wiring | Done | [M6 spec](superpowers/specs/2026-03-31-milestone6-flex-web-service-design.md) |
| 6.4 | Tests + Flex E2E (paper account query) | Done | [M6 spec](superpowers/specs/2026-03-31-milestone6-flex-web-service-design.md) |

---

## Observability — Tracing, Metrics, Structured Logging

**Goal:** Production-grade observability with zero external dependencies.

| # | Task | Status | Spec |
|---|---|---|---|
| O.1 | IbkrConduitDiagnostics + LogFields foundation | Done | [Observability spec](superpowers/specs/2026-03-31-observability-design.md) |
| O.2 | Distributed tracing (38 spans) | Done | [Observability spec](superpowers/specs/2026-03-31-observability-design.md) |
| O.3 | Metrics (34 instruments) | Done | [Observability spec](superpowers/specs/2026-03-31-observability-design.md) |
| O.4 | Structured logging audit (17 components) | Done | [Observability spec](superpowers/specs/2026-03-31-observability-design.md) |
| O.5 | Tests + observability consumer guide | Done | [Observability spec](superpowers/specs/2026-03-31-observability-design.md) |

---

## Milestone 7 — Production Readiness

**Goal:** Library is documented and has working samples demonstrating all major features.

| # | Task | Status | Spec |
|---|---|---|---|
| 7.1 | Samples project | Not Started | — |
| 7.2 | API documentation audit | Not Started | — |
