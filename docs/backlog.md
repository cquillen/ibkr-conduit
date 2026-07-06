# IbkrConduit backlog

The pipeline-managed story tracker: `draft-backlog` inserts drafted streams here, `groom-backlog` makes them loop-ready, `ship-backlog` drains them. Format authority: `.claude/rules/backlog-format.md` (entry schema) + `.claude/rules/backlog-status.md` (status hygiene).

> **Scope note:** the inaugural stream is **Stream VCR** below — a fresh decomposition of the [RTOS venue-consumer review](findings/2026-07-04-rtos-venue-consumer-review.md) findings, drafted 2026-07-06 on the operator's directive **without consulting** the pre-pipeline [money-boundary hardening backlog](money-boundary-hardening-backlog.md) (Stream MBH, prefix reserved). The two trackers therefore cover the same findings independently; **reconciling/retiring the MBH tracker against Stream VCR is a flagged operator follow-up** — do not build from both.

## How to read this document

Stories have a stable ID (`<PREFIX>-NN`), a **Status** line, a **Stream**, **dependencies**, a description, a **Done when**, and **TDD notes** — the schema in `.claude/rules/backlog-format.md`.

**Marker in the story heading:**

- **📦 PUBLIC SURFACE** — the story changes the **published API surface or wire-mapping contract** (public types/methods/options, DTO nullability semantics, `[JsonPropertyName]` mappings, streaming frame semantics). A 📦 story gets a semver review at grooming (breaking vs additive → `feat!:` vs `feat:`), lands **before** every story that consumes the new surface, and is reviewed knowing **RTOS (`realtest-order-steward`) is a live consumer** of this library.

**Status values** (updated *in the story's own PR* — the source of truth; see `.claude/rules/backlog-status.md`):

- `Not started` · `In progress — <owner/instance> #<PR>` · `✅ Done — #<PR>` · `Deferred — <reason>`

**Working the backlog (the autonomous loop):**

- One story = one branch = one PR, TDD per `.claude/rules/tdd-workflow.md`.
- Pick any story whose dependencies are all `✅ Done`. Build order per the stream's build-order map.
- Only **loop-ready** entries are buildable (`Spec:` a path or `trivial-skip`, `Risk` set, no open fork, empirics verified); `ship-backlog` bounces the rest to grooming.
- The committed backlog is the single source of truth for status; a PR completing a story flips its Status and carries a `Completes: <id>` trailer.

**Conventions every story inherits** (don't restate per story — read the rules): the contract layer + gap routing (`.claude/rules/contract-design.md`); error surfacing via `Result<T>` + the `IbkrError` taxonomy (`src/IbkrConduit/Errors/`); immutable positional-record DTOs + companion models files (`.claude/rules/design-patterns.md`); rate limiting per `docs/ibkr_conduit_design.md` §8; test tiering incl. the mandatory 401-recovery test and env-gated E2E (`.claude/rules/testing.md`); no global/static mutable state (`.claude/rules/architecture.md`); zero warnings + central package versions (`.claude/rules/build-quality.md`); semver via release-please (`docs/ibkr_conduit_design.md` §17.4).

## Key references (link, don't re-decide)

- **Contract:** `docs/ibkr_conduit_design.md` (the living design doc — canonical) · `docs/adr/` (decisions going forward) · `docs/ibkr-web-api-spec.md` + `recordings/` (upstream ground truth — verified, never decided).
- **Findings:** `docs/findings/` — adversarial reviews of the contract; the evidence trail behind fix streams.
- **Process:** `.claude/skills/draft-backlog/`, `.claude/skills/groom-backlog/`, `.claude/skills/ship-backlog/`, `.claude/skills/writing-adrs/`.

---

## Stories

### Stream VCR — venue-consumer review fixes

> **DRAFTED, NOT GROOMED** — every entry is `Spec: pending`, no `Risk` is set, and open questions are flagged, not closed. `ship-backlog` must bounce this stream until `groom-backlog` has run.

**What this decomposes:** the 33 verified + 4 unverified findings of [`docs/findings/2026-07-04-rtos-venue-consumer-review.md`](findings/2026-07-04-rtos-venue-consumer-review.md) (reviewed at `main` @ `c7a07fd`, v0.8.0). The findings doc is **immutable evidence** — entries cite finding IDs; fix work never edits the review. The 2 refuted claims (AMB-1, WIR-2) and the 53 clean areas produce no stories. Unverified findings (`FIL-7*`, `MGR-5*`, `MGR-6*`, `WIR-6*` — swept but not skeptic-checked) are folded into the component story that owns their code, each flagged **verify-or-refute first** per the findings doc's own verdict legend ("treat as unconfirmed").

**Route to design — needs a design-doc/ADR update BEFORE grooming.** The findings' *suggested fix directions* repeatedly imply contract decisions the record does not make: `docs/ibkr_conduit_design.md` currently records **nothing** on field-presence semantics, streaming delivery/backpressure guarantees, ambiguous order outcomes, live-orders priming, or competing-session signaling (checked 2026-07-06). Per `.claude/rules/contract-design.md`, a story spec must not be the first place these are written. Design items, with the stories that wait on them:

- **D1 — Field-presence semantics on the public DTO surface.** What does "absent from the wire" mean on public DTOs, and how is it represented (nullable-as-presence? a presence set? raw element access?) — for streaming *and* REST money/status fields. The findings suggest nullability (WIR-1, WIR-3, FIL-6, GAP2-2, GAP2-3) but that is a suggestion, not a recorded decision, and it is breaking for a live consumer. → blocks **VCR-01**, shapes **VCR-07**.
- **D2 — Streaming delivery & drop-observability guarantee.** What completeness does the library promise per topic; overflow policy (`DropOldest` today — findings FIL-1 — vs `Wait` for money topics vs configurable); how drops, reconnect gaps, and observer failures are surfaced (counter? log? `OnError`? a connection-lifecycle stream? — FIL-1, FIL-4, FIL-3); and whether `IIbkrSubscription<T>.Stream` is multicast or single-observer (FIL-5). → blocks **VCR-02**.
- **D3 — Ambiguous order-outcome classification & non-idempotent replay policy.** Whether the automatic 401 replay is gated for order-mutating POSTs and what error shape signals "ambiguous after send"; whether a distinct error type covers 2xx-body-unparseable (AMB-2, WIR-4). Today's order-outcome trichotomy is documented only from the consumer's seat in the findings doc — the conduit records no such guarantee. → blocks **VCR-04**.
- **D4 — Live-orders priming surface.** How primedness (`snapshot`) is exposed or handled — return-record change, a new method, or conduit-internal re-polling (GAP1-1 lists these as alternatives) — and how the captured spec's filtered-orders ↔ `sor` suppression warning is handled per the architecture rule that the library owns IBKR quirks (GAP1-2). → blocks **VCR-05**.
- **D5 — Competing-session truth & health-evidence semantics.** What the library promises about detecting and signaling a competing/lost session (`IbkrSessionError.IsCompeting`, `SsodhInitResponse` handling, `Compete=false` behavior — SES-1, GAP3-1, GAP3-2, GAP3-3) and what counts as liveness evidence for health status (tickle successes vs consumer calls — SES-4). → blocks **VCR-07**, shapes VCR-06's health-state writes.

**Build-order map (v1.0, 2026-07-06):**

- **Buildable after grooming, no design dependency:** VCR-03, VCR-06, VCR-08, VCR-09, VCR-10, VCR-11 — mutually independent.
- **Blocked on design items:** VCR-01 (D1) · VCR-02 (D2) · VCR-04 (D3) · VCR-05 (D4) · VCR-07 (D1+D5, and story-depends on VCR-01).
- 📦-first: VCR-01 precedes VCR-07 (shared `SessionStatusEvent` surface). VCR-02 and VCR-03 touch the same observable/mapper files — serialize them in one lane (no DAG edge; build-order only).

#### VCR-01 — 📦 Presence-preserving wire DTOs (streaming + REST)
**Status:** Not started · **Stream:** VCR · **Depends on:** none · **Blocks:** VCR-07
**Spec:** pending
Public DTOs erase field-presence on wire-optional fields, so consumers cannot distinguish "IBKR sent nothing" from a real zero/false/empty: `OrderUpdate`'s sparse-`sor` money/status fields are non-nullable (absent → `0m`/`""` — **WIR-1, critical**); REST `Trade`/`TradeExecution`/`LiveOrder` money fields coerce absent/empty to `0` via the global `EmptyTolerantDecimalConverter` (WIR-3, FIL-6); `SessionStatusEvent.Authenticated` fabricates a definite `false` from an args-less `sts` frame (GAP2-2); `AccountStatusMapper` collapses absent/null/string-encoded `isPaper` to `false` while the same flag on the `system` topic already has presence semantics (GAP2-3 — the inconsistency is recorded evidence). The representation of presence on the public surface is design item **D1** — this story implements whatever D1 records, across both streaming and REST DTOs, in one pass.
**Done when:** a consumer of the affected DTOs can distinguish absent-from-the-wire from genuine zero/false/empty values, per the D1-recorded semantics, on both streaming and REST surfaces.

#### VCR-02 — 📦 Streaming delivery observability & subscription semantics
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
Streaming loss is invisible end-to-end: channel overflow under `DropOldest` evicts frames with no log, metric, or callback (**FIL-1, critical** — the `itemDropped` overload is not used and no drop counter exists); mapper-dropped frames log the DTO type name instead of the wire topic and increment nothing (GAP2-4); every reconnect path replays subscriptions with no consumer-visible gap signal (FIL-4); a consumer `OnNext` exception is mislabeled as a malformed frame and an `OperationCanceledException` from the observer terminates the stream indistinguishably from deliberate unsubscribe (FIL-3); and a second `Subscribe` on one subscription's `Stream` silently splits deliveries between observers, violating the `IObservable` multicast expectation and the channel's `SingleReader` contract (FIL-5). What the library *promises* here — overflow policy, drop/gap surfacing, multicast-vs-single-observer — is design item **D2**; this story implements it. Open question (grooming): the findings suggest both minimal fixes (counter + log) and surface additions (connection-lifecycle events, per-topic `Wait` mode) — D2 decides the extent; the 📦 marker assumes at least some observable surface is added or its semantics pinned.
**Done when:** no streaming frame can be lost (overflow, mapper failure, observer failure, reconnect gap) without a consumer-observable signal, per the D2-recorded guarantee; multi-Subscribe behavior matches what D2 records.

#### VCR-03 — Streaming mapper robustness
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
Two mapper defects drop more than the bad input: `TradeExecutionMapper.MapMany` deserializes the frame's `args` array lazily inside one try/catch, so a single malformed execution discards **all subsequent executions in that frame** — and the `str` resubscribe snapshot is one frame carrying up to a whole day's fills (FIL-2, high); `SessionStatusMapper` reads `authenticated` with a raw `GetBoolean()` so a string-encoded boolean throws and the whole session-death frame is silently skipped (GAP2-1). Both fixes follow recorded conventions: per-element failure isolation keeps the observable-level catch as last resort (FIL-2's fix direction), and tolerant boolean parsing mirrors the existing `FlexibleBoolJsonConverter` logic (`src/IbkrConduit/Serialization/`) — rule-settled pattern, no new contract surface.
**Done when:** one malformed execution in a `str` frame no longer discards the frame's remaining executions, and a type-drifted `sts` `authenticated` value still surfaces as a session-status event.

#### VCR-04 — 📦 Order-outcome classification & 401 replay gate
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
The order path can misclassify the outcomes a venue consumer's money decisions hang on: `TokenRefreshHandler` unconditionally replays **order-mutating POSTs** after a 401 with no proof the original was unprocessed — if IBKR ever processes-then-401s, the conduit itself double-submits (**AMB-2, high/PLAUSIBLE**); `ReplyAsync` is the only order path whose 2xx responses bypass `ResultFactory.FromResponse` hidden-error detection, so a documented 200-OK reject shape surfaces as a context-free exception (AMB-3 — routing through `ResultFactory` is the existing code convention every other order path already follows); two plausible 200-OK shapes (array-wrapped reject, empty array) throw instead of classifying (AMB-4); and `OrderSubmissionResponse.OrderId` lacks the `FlexibleStringJsonConverter` that IBKR's demonstrated numeric `order_id` on other surfaces makes necessary, so a numeric value fails typed deserialization of a 2xx success (WIR-4 — converter usage is the existing wire-mapping convention; the *distinct error type for 2xx-unparseable* the finding also suggests is a D3 surface decision). The replay-gate semantics and any new ambiguous/error shape are design item **D3**. Open question (grooming, empirical): whether IBKR can process an order POST and then 401 is **unpinned in either direction** per the findings doc — probe or record as unpinnable.
**Done when:** a 401 on an order-mutating POST can no longer cause a silent duplicate submission, and every documented 200-OK reject/edge shape on the place/modify/reply paths surfaces as a classified result rather than a context-free exception, per D3.

#### VCR-05 — 📦 Live-orders priming & filters/sor interaction
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
`GetLiveOrdersAsync` discards `OrdersResponse.Snapshot` — the priming indicator added in #220 because IBKR's first `/iserver/account/orders` response is unprimed (`snapshot:false`, often empty) — so through the facade an empty success is indistinguishable from "no orders", and the internal `IIbkrOrderApi` leaves consumers no workaround (**GAP1-1, high**). The `filters` parameter is exposed with no handling or documentation of the captured spec's warning that filtering suppresses `sor` order-detail frames until a `force=true` follow-up (GAP1-2, high). The integration suite pins the dangerous mapping: the only "empty live orders" test asserts the **unprimed** shape surfaces as a successful empty list, and nothing covers `snapshot:true`, `force=true`, or the two-call priming sequence (GAP1-3). How primedness is surfaced or handled, and how the filters↔sor quirk is owned by the library, is design item **D4**; this story implements it plus the missing fixture/test coverage.
**Done when:** a consumer can no longer mistake an unprimed empty response for "no live orders", the filtered-orders↔sor suppression is handled or explicitly surfaced per D4, and the priming sequence has pinned test coverage.

#### VCR-06 — Session lifecycle state-machine hardening
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
Four session defects compound into silent rot or a permanent wedge (all against the recorded §7 lifecycle intent, no new contract surface): the tickle loop treats an HTTP 401 like a transport blip — logs it, never re-authenticates, never updates health — despite its own comment stating 401 means session-dead (SES-2, high); `GetLiveSessionTokenAsync` never checks LST `Expiry` and failed init/reauth never resets `_state`, so one transient failure at the wrong moment wedges every subsequent call into `IbkrConfigurationException` (SES-3, high); proactive LST refresh is one-shot — a single failed attempt is never retried, and an already-due refresh is silently skipped (SES-5); re-entering init after a failure overwrites `_tickleTimer` without stopping the old one, leaking concurrent tickle loops that multiply traffic and reauth storms (SES-6). Coordination flag: SES-2's health-state updates touch the semantics design item **D5** records — grooming aligns this story with D5's outcome (or sequences it after VCR-07's spec) so the two don't write conflicting health-state behavior.
**Done when:** a tickle 401 triggers re-authentication, an expired LST is re-acquired instead of wedging the session, a failed proactive refresh retries, and re-initialization no longer leaks tickle loops.

#### VCR-07 — 📦 Competing-session truth & health evidence
**Status:** Not started · **Stream:** VCR · **Depends on:** VCR-01
**Spec:** pending
The library's competing-session and liveness signals are untruthful: `EnsureInitializedAsync`/`ReauthenticateAsync` discard the `SsodhInitResponse` body, so a 200 with `authenticated=false` (lost compete, failed bridge) is treated as full success and the session marked Ready (**SES-1, high**); `IbkrSessionError.IsCompeting` is hardcoded `false` at its only construction site — the flag a consumer maps to session-loss recovery can never be true (GAP3-1, high); init/reauth unconditionally write `competing:false` into health state, erasing the competing evidence a tickle just recorded (GAP3-2); the `sts` mapper drops the `competing`/`fail`/`message` fields the repo's own WebSocket reference says that channel relays (GAP3-3); and tickle successes are never recorded as liveness evidence, so an idle-but-healthy session reports Unhealthy after 120s (SES-4, high). What the library *promises* about competing detection, signaling, and health evidence is design item **D5**; the `SessionStatusEvent` field additions follow **D1**'s presence semantics and VCR-01's landed surface (hence the dependency).
**Done when:** a competing/lost session is observable through the public error and health surfaces per D5 (no path fabricates `authenticated=true` or erases competing evidence), and an idle session with a live tickle no longer reports Unhealthy.

#### VCR-08 — Manager lifecycle integrity
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
`IbkrClientManager` teardown and construction violate their own contracts at the edges: `RemoveAsync` ignores its `CancellationToken` entirely and can block minutes uncancellably while the tenant is already invisible to `TryGetClient` — dead-per-consumer but live-per-IBKR (MGR-1, high; honoring the token is **rule-settled** by `.claude/rules/code-style.md`'s pass-`CancellationToken`-through-the-entire-call-chain rule); `AddAsync` leaks caller-owned credentials on several throw paths despite its documented unconditional credential ownership (MGR-2 — the documented contract is the authority; fix the code to it); a `DisposeAsync` racing an in-flight `AddAsync` can orphan a fully built tenant with a live tickle loop nobody owns (MGR-3); and the manager path never runs the options validation the single-client path enforces, letting an invalid per-tenant override degenerate into tickle spam or a misleading credential error (MGR-6*, low, **UNVERIFIED — verify-or-refute before scoping in**). Open question (grooming): MGR-1's fix mechanism — a public `DisposeAsync(CancellationToken)` on `IManagedTenant` vs an internal bounded timeout — the first is a 📦 surface addition, the second isn't; the finding lists both.
**Done when:** `RemoveAsync` honors cancellation, no `AddAsync` failure path leaks credentials, an add/dispose race cannot orphan a live tenant, and (if confirmed) manager-path options are validated like the single-client path.

#### VCR-09 — Metrics registration & disposal hygiene
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
Observability instruments accumulate as garbage under tenant churn, hiding the evidence needed to diagnose real incidents: `GlobalRateLimitingHandler` registers a new untagged `ObservableGauge` on the process-wide static `Meter` per handler construction — with factory-managed 2-minute handler lifetimes that's a growing pile of stale duplicate gauges plus pinned handler chains (MGR-4); the WebSocket `connection_state` gauge is per-instance, untagged, and never unregistered on dispose (FIL-7*, low, **UNVERIFIED**); each removed tenant strands ~9 auto-replenishing rate-limiter timers because pre-built limiter instances are registered without container ownership (MGR-5*, low, **UNVERIFIED**). Verify-or-refute the two unverified findings first; the verified fix direction (register gauges once per tenant, tagged, disposed with the provider) aligns with the no-static-mutable-state rule (`.claude/rules/architecture.md`).
**Done when:** tenant add/remove churn no longer accumulates stale gauges or (if confirmed) live limiter timers, and per-tenant instruments are tagged so dashboards can attribute them.

#### VCR-10 — Response-schema validation net hardening
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
The safety net meant to catch wire-shape drift has three recorded blind spots (WIR-5): collection responses validate only `element[0]`; extra-field detection is skipped whenever a DTO has `[JsonExtensionData]` — which the money DTOs all do; and streaming frames aren't covered at all, so drift on the highest-money path produces zero signal. Internal hardening of an existing mechanism; no public surface change.
**Done when:** wire-shape drift on collection elements beyond the first, on extension-data DTOs, and on streaming money frames produces an observable validation signal instead of silence.

#### VCR-11 — Order-type documentation vs captured wire enum
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Spec:** pending
`OrderRequest.OrderType`'s XML doc lists `MKT, LMT, STP, STP_LMT, MOC, LOC, TRAIL`, but the captured spec pins the wire enum as `LMT, MKT, STP, STOP_LIMIT, MIDPRICE, TRAIL, TRAILLMT` — a consumer following the conduit's own docs sends an invalid `orderType` on an unvalidated pass-through (WIR-6*, low, **UNVERIFIED — verify against `docs/ibkr-web-api-spec.md` first**; the check is documentary, against the already-captured spec). Open question (grooming): the finding also suggests adding `ExtOperator` to `OrderRequest` for futures compliance — that is a 📦 additive surface change; decide whether it's in scope or split out.
**Done when:** the `OrderType` documentation matches the captured wire enum (or the finding is refuted with the counter-evidence recorded here).

## Deferred

*(none)*
