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
| 2.1 | Brokerage session initialization + auth status | Not Started | — |
| 2.2 | Question suppression | Not Started | — |
| 2.3 | Tickle timer | Not Started | — |
| 2.4 | Multi-tenant session manager | Not Started | — |
| 2.5 | Token refresh (proactive + reactive 401) | Not Started | — |
| 2.6 | Session lifecycle integration test | Not Started | — |

---

## Milestone 3 — Order Management

**Goal:** Submit and cancel orders against a paper account, with rate limiting and resilience protecting the pipeline.

| # | Task | Status | Spec |
|---|---|---|---|
| 3.1 | Rate limiting infrastructure | Not Started | — |
| 3.2 | Polly resilience pipeline + 429 adaptive response | Not Started | — |
| 3.3 | Conid resolution + caching | Not Started | — |
| 3.4 | Order submission with question/reply flow | Not Started | — |
| 3.5 | Order cancellation + live orders + trades | Not Started | — |

---

## Milestone 4 — Portfolio + Market Data

**Goal:** Retrieve positions, account summary, and market data from a paper account.

| # | Task | Status | Spec |
|---|---|---|---|
| 4.1 | Position and account data retrieval | Not Started | — |
| 4.2 | Market data snapshot with pre-flight handling | Not Started | — |
| 4.3 | Historical market data | Not Started | — |

---

## Milestone 5 — WebSocket Streaming

**Goal:** Stream real-time order updates and market data from a paper account via WebSocket.

| # | Task | Status | Spec |
|---|---|---|---|
| 5.1 | WebSocket client infrastructure | Not Started | — |
| 5.2 | Order updates topic (sor) | Not Started | — |
| 5.3 | Market data + P&L streaming (smd, spl) | Not Started | — |

---

## Milestone 6 — Flex Web Service

**Goal:** Execute a Flex query against a paper account and parse trade confirmations and open orders.

| # | Task | Status | Spec |
|---|---|---|---|
| 6.1 | Flex credentials model + HTTP client | Not Started | — |
| 6.2 | Two-step async retrieval | Not Started | — |
| 6.3 | XML response parsing | Not Started | — |

---

## Milestone 7 — Production Readiness

**Goal:** Library is documented and has working samples demonstrating all major features.

| # | Task | Status | Spec |
|---|---|---|---|
| 7.1 | Samples project | Not Started | — |
| 7.2 | API documentation audit | Not Started | — |
