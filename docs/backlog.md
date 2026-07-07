# IbkrConduit backlog

The pipeline-managed story tracker: `draft-backlog` inserts drafted streams here, `groom-backlog` makes them loop-ready, `ship-backlog` drains them. Format authority: `.claude/rules/backlog-format.md` (entry schema) + `.claude/rules/backlog-status.md` (status hygiene).

> **Scope note:** the inaugural stream is **Stream VCR** below — a fresh decomposition of the [RTOS venue-consumer review](findings/2026-07-04-rtos-venue-consumer-review.md) findings, drafted 2026-07-06 on the operator's directive **without consulting** the pre-pipeline [money-boundary hardening backlog](money-boundary-hardening-backlog.md) (Stream MBH, prefix reserved). **Resolved 2026-07-07:** the operator retired the MBH tracker in favor of Stream VCR (no MBH task had started; MBH is retained as history with a retirement banner). This backlog is the only buildable tracker for the findings.

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

> **GROOMED 2026-07-07 — loop-ready** except VCR-12 (deferred follow-on). Every open fork was closed with the operator on 2026-07-06/07; the design items are recorded in ADR-0001..0004 + design doc §6.5/§7.7/§9.9/§10.6/§12.8; empirics were verified (see Evidence). `ship-backlog` may build this stream.

**What this decomposes:** the 33 verified + 4 unverified findings of [`docs/findings/2026-07-04-rtos-venue-consumer-review.md`](findings/2026-07-04-rtos-venue-consumer-review.md) (reviewed at `main` @ `c7a07fd`, v0.8.0). The findings doc is **immutable evidence** — entries cite finding IDs; fix work never edits the review. The 2 refuted claims (AMB-1, WIR-2) and the 53 clean areas produce no stories.

**Design decisions (closed — the drafted stream's D1–D5, now recorded):**

- **D1 → [ADR-0001](adr/0001-nullable-as-presence-wire-fidelity.md)** + design doc §6.5 — nullable-as-presence on wire-optional DTO fields (breaking).
- **D2 → [ADR-0002](adr/0002-streaming-delivery-guarantee.md)** + §12.8 — at-most-once, loss-is-observable; DropOldest kept; default buffer 2048; single-observer streams; consumer-visible reconnect events.
- **D3 → [ADR-0003](adr/0003-order-post-replay-gate.md)** + §9.9 — order-mutating POSTs excluded from 401 replay; dedicated ambiguous-outcome error; AMB-2's unpinned process-then-401 question is tolerated by design, not resolved.
- **D4 → design doc §10.6** (story-scoped, no ADR) — `GetLiveOrdersAsync` returns `{Orders, IsSnapshot}`; the library auto-issues `force=true` after filtered calls.
- **D5 → [ADR-0004](adr/0004-competing-session-truth-and-health-evidence.md)** + §7.7 — truthful ssodh handling, real `IsCompeting`, no literal health writes, compete backoff, tickle-as-liveness.

**Evidence (grooming verifications, 2026-07-07):** the four unverified findings are settled — FIL-7, MGR-5, MGR-6 **CONFIRMED** by code trace (recorded in the affected entries/specs), WIR-6 **CONFIRMED** against the captured spec (`docs/ibkr-web-api-spec.md:4507`). Live paper-account probe captured 2026-07-07 as `recordings/priming/001-003` (local per repo convention — `recordings/` is gitignored; sanitized WireMock fixtures carry the shapes into the committed tests): a filtered live-orders call returns fake-empty `snapshot:false` while orders exist; `force=true` returns the documented blank array; the next call returns `snapshot:true`. The probe also live-confirmed `"price": ""` on a filled order (VCR-01 evidence). Residual unpinned upstream behaviors (`sor`-suppression effect of filtered calls; process-then-401) are handled by designs that are safe under both answers — no story builds on them.

**Release train:** the five breaking stories (VCR-01, 02, 04, 05, 07 — all `feat!:`) should all merge before the release-please release PR is accepted, so one minor cut carries the full breaking set and RTOS re-pins once. `fix:` stories may land in the same or later cuts. Consumer migration notes are an acceptance item on each breaking story's spec.

**Build-order map (v1.1, 2026-07-07 — supersedes v1.0):**

- **Wave 1 (independent):** VCR-01 📦 · VCR-02 📦 · VCR-04 📦 · VCR-05 📦 · VCR-06 · VCR-08 · VCR-09 · VCR-10 · VCR-11. Lane note: VCR-02 and VCR-03 touch the same observable/mapper files — run VCR-03 after VCR-02 in one lane.
- **Wave 2:** VCR-03 (after VCR-02, lane ordering) · VCR-07 (deps: VCR-01, VCR-06).
- **Deferred:** VCR-12 (ExtOperator follow-on — not in this loop).

<details><summary>Build-order map v1.0 (2026-07-06, historical — superseded by v1.1)</summary>

- Buildable after grooming, no design dependency: VCR-03, VCR-06, VCR-08, VCR-09, VCR-10, VCR-11.
- Blocked on design items: VCR-01 (D1) · VCR-02 (D2) · VCR-04 (D3) · VCR-05 (D4) · VCR-07 (D1+D5, story-depends on VCR-01).

</details>

#### VCR-01 — 📦 Presence-preserving wire DTOs (streaming + REST)
**Status:** ✅ Done — #238 · **Stream:** VCR · **Depends on:** none · **Blocks:** VCR-07
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-01-presence-preserving-dtos.md
Nullable-as-presence retrofit per ADR-0001 across `OrderUpdate`, `TradeExecution`, REST `Trade`/`LiveOrder`, `SessionStatusEvent.Authenticated`, `AccountStatusEvent.IsPaper`/`IsFT` — `null` means "absent from this frame/row"; no fabricated verdicts (findings WIR-1 critical, WIR-3, FIL-6, GAP2-2, GAP2-3). **Breaking — `feat!:`.**
**Done when:** the live-captured sparse `sor` frame deserializes with `null` (not `0m`/`""`) for every omitted field; REST rows with empty-string/omitted money fields yield `null`; absent `authenticated`/`isPaper` yield `null`; the spec's acceptance list is green in the offline suite.
**TDD notes:** red tests = the findings' suggested regression tests per finding ID, with sanitized fixtures derived from the 2026-07-07 live captures; update the pinned-coercion tests.

#### VCR-02 — 📦 Streaming delivery observability & subscription semantics
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-02-streaming-delivery-observability.md
Implements ADR-0002: observable evictions (itemDropped → Warning + `ibkr.conduit.streaming.frames.dropped` counter tagged tenant/topic/cause, first-drop-per-topic log throttle), wire-topic drop logging, observer-failure honesty (OCE ≠ graceful completion), consumer-visible connection-lifecycle events, single-observer `Stream` (second `Subscribe` throws), default buffer 256→2048 (findings FIL-1 critical, GAP2-4, FIL-3, FIL-4, FIL-5). **Breaking-behavioral — `feat!:`.**
**Done when:** no streaming frame is lost without a counter increment + log; reconnects emit Disconnected/Reconnected events with replayed topics; a second concurrent Subscribe throws; the default-buffer pin test reads 2048.
**TDD notes:** mock-WS harness (`BroadcastTextAsync`) drives overflow/mapper/observer/reconnect scenarios; metrics via `MeterListener`.

#### VCR-03 — Streaming mapper robustness
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Pattern-following fixes, decided: `TradeExecutionMapper.MapMany` isolates failures per `args` element (materialize before yield; log-and-skip only the bad element, keeping the observable-level catch as last resort) so one malformed execution no longer discards the frame's tail (FIL-2 — the `str` snapshot frame carries up to a day's fills); `SessionStatusMapper` parses `authenticated` with tolerant boolean logic mirroring `FlexibleBoolJsonConverter` instead of raw `GetBoolean()` (GAP2-1). Lane note: build after VCR-02 (shared files).
**Done when:** a `str` frame with one malformed execution yields all remaining executions (with the malformed one counted/logged per VCR-02's drop taxonomy), and an `sts` frame with a string-encoded `authenticated` still surfaces a session-status event; both pinned by mock-WS tests.
**TDD notes:** red tests = FIL-2/GAP2-1 suggested regression tests (multi-execution frame with one bad element; `"authenticated": "false"` frame).

#### VCR-04 — 📦 Order-outcome classification & 401 replay gate
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-04-order-outcome-replay-gate.md
Implements ADR-0003: order-mutating POSTs excluded from automatic 401 replay, surfacing a new `IbkrAmbiguousOrderError`; reply 2xx routes through `ResultFactory.FromResponse`; array-wrapped/empty 200 shapes classify as refusals with raw body; `FlexibleStringJsonConverter` on `OrderSubmissionResponse.OrderId`/`Id`; 2xx-unparseable surfaces as a classified error (findings AMB-2 high, AMB-3, AMB-4, WIR-4). AMB-2's empirical question is tolerated by design. **Breaking-behavioral — `feat!:`.**
**Done when:** WireMock 401-then-success on order POSTs yields the ambiguous error with exactly one upstream POST while GET/DELETE keep replay-and-succeed; the documented 200-OK reject/edge shapes classify instead of throwing; numeric `order_id` deserializes.
**TDD notes:** red tests = AMB-2/3/4 + WIR-4 suggested regression tests as DI-stack WireMock scenarios; 401-recovery tests updated to the gate semantics.

#### VCR-05 — 📦 Live-orders priming & filters/sor interaction
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-05-live-orders-priming.md
Implements design doc §10.6: `GetLiveOrdersAsync` returns `LiveOrdersSnapshot(Orders, IsSnapshot)` so an unprimed empty response is distinguishable from "no orders"; the library auto-issues the `force=true` follow-up after any filtered call (library-owns-quirks); fixtures/tests stop enshrining the unprimed shape as canonical (findings GAP1-1 high, GAP1-2 high, GAP1-3). Evidence: the `recordings/orders/001-002` and 2026-07-07 `recordings/priming/001-003` live captures (local; fixtures carry the shapes). **Breaking — `feat!:`.**
**Done when:** the recorded unprimed/primed/force-cleared shapes surface faithfully through `IsSnapshot`; a filtered call is followed by exactly one `force=true` request (asserted call sequence); the unprimed-then-primed WireMock scenario is pinned.
**TDD notes:** red tests = GAP1-1/2/3 suggested regression tests; sanitized fixtures derived from the recorded shapes.

#### VCR-06 — Session lifecycle state-machine hardening
**Status:** ✅ Done — #240 · **Stream:** VCR · **Depends on:** none · **Blocks:** VCR-07
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-06-session-lifecycle-hardening.md
Repairs within the recorded §7 lifecycle contract: tickle 401 triggers re-auth (and truthful health) instead of log-and-rot; LST expiry is checked and failed init/reauth resets state (no permanent wedge); proactive refresh retries with backoff and fires immediately when already due; re-init stops the old tickle timer (no leaked loops) (findings SES-2 high, SES-3 high, SES-5, SES-6). Health-state writes follow ADR-0004. **`fix:`.**
**Done when:** the spec's four scenario tests pass — 401-tickle→single reauth cycle, expired-LST transparent recovery + clean re-entry, retried proactive refresh, no duplicate tickle loops after failed-reauth→re-init.
**TDD notes:** red tests = SES-2/3/5/6 suggested regression tests extending the existing `TickleTimerTests`/session WireMock suites; timer accounting via mock-server tickle counts.

#### VCR-07 — 📦 Competing-session truth & health evidence
**Status:** ✅ Done — #244 · **Stream:** VCR · **Depends on:** VCR-01, VCR-06
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-07-competing-session-truth.md
Implements ADR-0004: 200 ssodh with `authenticated=false` fails init/reauth with competing evidence carried into `IbkrSessionError.IsCompeting` (the literal-`false` site eliminated); health fed from server responses with sticky competing evidence; compete backoff under `Compete=false`; `SessionStatusEvent` gains `Competing`/`FailReason` (ADR-0001 shapes — hence dep on VCR-01); tickle successes are liveness evidence (findings SES-1 high, GAP3-1 high, GAP3-2, GAP3-3, SES-4 high). **Breaking-behavioral — `feat!:`.**
**Done when:** the spec's acceptance list is green — failed-init-on-authenticated:false with `IsCompeting`, sticky competing health, `sts` fields surfaced, spaced-out reauth under compete-off, healthy-while-idle tickling session.
**TDD notes:** red tests = SES-1/4 + GAP3-1/2/3 suggested regression tests; WireMock ssodh/tickle scenarios + mock-WS `sts` frames; backoff via the mock-clock pattern.

#### VCR-08 — Manager lifecycle integrity
**Status:** ✅ Done — #239 · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-08-manager-lifecycle.md
Cancellable bounded teardown for `RemoveAsync` via internal linked-CTS bound (operator-decided: no new public surface; CT obligation rule-settled by `code-style.md`); credential disposal on every `AddAsync` throw path per the documented ownership contract; the add/dispose race can no longer orphan a live tenant; the manager path validates effective options like `AddIbkrClient` (findings MGR-1 high, MGR-2, MGR-3, MGR-6 — all CONFIRMED, incl. the 2026-07-07 verification of MGR-6). **`fix:`.**
**Done when:** the spec's acceptance list is green — prompt cancellable teardown with resources still disposed, exactly-once credential disposal on all failure paths, race-free dispose, fail-fast invalid overrides.
**TDD notes:** red tests = MGR-1/2/3/6 suggested regression tests; tracking-disposable credential fakes; deterministic race interleaving via a test gate.

#### VCR-09 — Metrics registration & disposal hygiene
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
All three findings CONFIRMED (MGR-4 by the review; FIL-7, MGR-5 by the 2026-07-07 verification: the WS gauge closure pins disposed clients alive via the static Meter; the 9 instance-registered REST limiters are never disposed by MSDI and the 2 Flex limiters live only in handler closures). Decided scope (per the findings' fix directions + `architecture.md` no-static-state): the rate-limiter queue-depth gauge registers once per tenant against the limiter singleton with a tenant tag; the WS `connection_state` gauge gains a tenant tag and its registration is disposed (or callback disposal-gated) with the client; limiters register via factory lambdas so the container owns disposal, with the Flex pair wrapped in a container-owned disposable holder.
**Done when:** tenant add/remove churn accumulates no stale gauges (assert via `MeterListener` across add→remove→add) and no live replenishment timers (limiters disposed with the provider); per-tenant instruments carry the tenant tag.
**TDD notes:** red tests = MGR-4/FIL-7/MGR-5 suggested regression tests; gauge identity/tag assertions via `MeterListener`; limiter disposal via a disposal-tracking wrapper.

#### VCR-10 — Response-schema validation net hardening
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Decided scope (internal safety net, no public surface): `ResponseSchemaValidationHandler` validates **every** element of collection bodies (endpoint payloads are bounded — e.g. live-orders caps at 1000); extra-field detection runs even when the DTO has `[JsonExtensionData]` (diff and report via the populated `AdditionalData`); str/sor mappers gain a required-money-field census signal (log + counter per VCR-02's drop taxonomy) when a required streaming money field is absent (finding WIR-5).
**Done when:** a field missing on a non-first collection element, an extra field on an extension-data DTO, and an absent required money field on a streaming frame each produce the validation signal; pinned by unit/WireMock tests.
**TDD notes:** red tests = WIR-5's suggested regression tests (element[1] drift fixture; extension-data extra-field fixture; sparse money frame).

#### VCR-11 — Order-type documentation vs captured wire enum
**Status:** Not started · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
CONFIRMED against the captured spec (`docs/ibkr-web-api-spec.md:4507`): the wire enum is `LMT, MKT, STP, STOP_LIMIT, MIDPRICE, TRAIL, TRAILLMT`; the XML docs on `OrderRequest.OrderType` (`IIbkrOrderApiModels.cs:23`) wrongly list `STP_LMT, MOC, LOC`. Decided scope: correct the XML docs to the pinned enum, including STOP_LIMIT's dual `price`+`auxPrice` requirement (WIR-6). ExtOperator is out of scope → VCR-12. **`fix:`** (shipped XML docs are consumer-facing).
**Done when:** the `OrderType` XML docs (both `OrderRequest` and `OrderWireModel`) match the captured wire enum verbatim and state the STOP_LIMIT price requirements; no code behavior change.
**TDD notes:** doc-only — no new tests; existing suites stay green.

#### VCR-12 — ExtOperator futures-compliance field
**Status:** Deferred — future additive surface work, split from VCR-11 by operator decision 2026-07-07; not groomed in this pass
**Spec:** pending
The WIR-6 finding also suggested adding `ExtOperator` to `OrderRequest` for futures compliance — a 📦 additive surface change with its own (unverified) compliance question. Deliberately split from the doc fix; groom before building (verify the compliance requirement against the captured spec/IBKR docs, then spec).
**Done when:** (rough) `OrderRequest` supports the ExtOperator field where futures compliance requires it, or the requirement is refuted and recorded here.

## Deferred

- **VCR-12** — ExtOperator futures-compliance field (see entry above): future additive surface work; unblock = groom it (verify the compliance requirement, spec, set Risk).
