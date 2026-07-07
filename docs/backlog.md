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
**Status:** ✅ Done — #243 · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-02-streaming-delivery-observability.md
Implements ADR-0002: observable evictions (itemDropped → Warning + `ibkr.conduit.streaming.frames.dropped` counter tagged tenant/topic/cause, first-drop-per-topic log throttle), wire-topic drop logging, observer-failure honesty (OCE ≠ graceful completion), consumer-visible connection-lifecycle events, single-observer `Stream` (second `Subscribe` throws), default buffer 256→2048 (findings FIL-1 critical, GAP2-4, FIL-3, FIL-4, FIL-5). **Breaking-behavioral — `feat!:`.**
**Done when:** no streaming frame is lost without a counter increment + log; reconnects emit Disconnected/Reconnected events with replayed topics; a second concurrent Subscribe throws; the default-buffer pin test reads 2048.
**TDD notes:** mock-WS harness (`BroadcastTextAsync`) drives overflow/mapper/observer/reconnect scenarios; metrics via `MeterListener`.

#### VCR-03 — Streaming mapper robustness
**Status:** ✅ Done — #247 · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Pattern-following fixes, decided: `TradeExecutionMapper.MapMany` isolates failures per `args` element (materialize before yield; log-and-skip only the bad element, keeping the observable-level catch as last resort) so one malformed execution no longer discards the frame's tail (FIL-2 — the `str` snapshot frame carries up to a day's fills); `SessionStatusMapper` parses `authenticated` with tolerant boolean logic mirroring `FlexibleBoolJsonConverter` instead of raw `GetBoolean()` (GAP2-1). Lane note: build after VCR-02 (shared files).
**Done when:** a `str` frame with one malformed execution yields all remaining executions (with the malformed one counted/logged per VCR-02's drop taxonomy), and an `sts` frame with a string-encoded `authenticated` still surfaces a session-status event; both pinned by mock-WS tests.
**TDD notes:** red tests = FIL-2/GAP2-1 suggested regression tests (multi-execution frame with one bad element; `"authenticated": "false"` frame).

#### VCR-04 — 📦 Order-outcome classification & 401 replay gate
**Status:** ✅ Done — #242 · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-vcr-04-order-outcome-replay-gate.md
Implements ADR-0003: order-mutating POSTs excluded from automatic 401 replay, surfacing a new `IbkrAmbiguousOrderError`; reply 2xx routes through `ResultFactory.FromResponse`; array-wrapped/empty 200 shapes classify as refusals with raw body; `FlexibleStringJsonConverter` on `OrderSubmissionResponse.OrderId`/`Id`; 2xx-unparseable surfaces as a classified error (findings AMB-2 high, AMB-3, AMB-4, WIR-4). AMB-2's empirical question is tolerated by design. **Breaking-behavioral — `feat!:`.**
**Done when:** WireMock 401-then-success on order POSTs yields the ambiguous error with exactly one upstream POST while GET/DELETE keep replay-and-succeed; the documented 200-OK reject/edge shapes classify instead of throwing; numeric `order_id` deserializes.
**TDD notes:** red tests = AMB-2/3/4 + WIR-4 suggested regression tests as DI-stack WireMock scenarios; 401-recovery tests updated to the gate semantics.

#### VCR-05 — 📦 Live-orders priming & filters/sor interaction
**Status:** ✅ Done — #245 · **Stream:** VCR · **Depends on:** none
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
**Status:** ✅ Done — #249 · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
All three findings CONFIRMED (MGR-4 by the review; FIL-7, MGR-5 by the 2026-07-07 verification: the WS gauge closure pins disposed clients alive via the static Meter; the 9 instance-registered REST limiters are never disposed by MSDI and the 2 Flex limiters live only in handler closures). Decided scope (per the findings' fix directions + `architecture.md` no-static-state): the rate-limiter queue-depth gauge registers once per tenant against the limiter singleton with a tenant tag; the WS `connection_state` gauge gains a tenant tag and its registration is disposed (or callback disposal-gated) with the client; limiters register via factory lambdas so the container owns disposal, with the Flex pair wrapped in a container-owned disposable holder.
**Done when:** tenant add/remove churn accumulates no stale gauges (assert via `MeterListener` across add→remove→add) and no live replenishment timers (limiters disposed with the provider); per-tenant instruments carry the tenant tag.
**TDD notes:** red tests = MGR-4/FIL-7/MGR-5 suggested regression tests; gauge identity/tag assertions via `MeterListener`; limiter disposal via a disposal-tracking wrapper.

#### VCR-10 — Response-schema validation net hardening
**Status:** ✅ Done — #248 · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Decided scope (internal safety net, no public surface): `ResponseSchemaValidationHandler` validates **every** element of collection bodies (endpoint payloads are bounded — e.g. live-orders caps at 1000); extra-field detection runs even when the DTO has `[JsonExtensionData]` (diff and report via the populated `AdditionalData`); str/sor mappers gain a required-money-field census signal (log + counter per VCR-02's drop taxonomy) when a required streaming money field is absent (finding WIR-5).
**Done when:** a field missing on a non-first collection element, an extra field on an extension-data DTO, and an absent required money field on a streaming frame each produce the validation signal; pinned by unit/WireMock tests.
**TDD notes:** red tests = WIR-5's suggested regression tests (element[1] drift fixture; extension-data extra-field fixture; sparse money frame).

#### VCR-11 — Order-type documentation vs captured wire enum
**Status:** ✅ Done — #246 · **Stream:** VCR · **Depends on:** none
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

### Stream PVR — post-VCR full-library review fixes

> **DRAFTED 2026-07-07 — not groomed. Design items D1–D7 closed 2026-07-07** (operator-attended design pass; recorded in ADR-0005/ADR-0006 + the design-doc sections cited below). Every entry is `Spec: pending` with no `Risk`; the remaining inline flags are below-contract grooming forks and empirical unknowns. `ship-backlog` must bounce this stream until groomed.

**What this decomposes:** the 50 verified + 1 unverified findings of [`docs/findings/2026-07-07-multi-agent-code-review.md`](findings/2026-07-07-multi-agent-code-review.md) (full-library adversarial sweep at `main` @ `18c6a23`, run after the entire Stream VCR fix set merged; 12 high, 24 medium, 14 low). The findings doc is **immutable evidence** — entries cite finding IDs; fix work never edits the review. The 2 refuted claims (STR-1, PRB-4.1) and the 126 clean areas produce no stories. The 1 unverified finding (RST-6) is folded into PVR-09 with a grooming-verify flag.

**Design decisions (closed — the drafted stream's D1–D7, now recorded):**

- **D1 → [ADR-0005](adr/0005-subscription-scoped-streaming-delivery.md)** + design doc §12.8/§12.5 — subscription-scoped delivery: target-qualified topics route by full wire-topic identity; target-less/unsolicited keep prefix routing; same-target duplicates fan out; unmatched frames drop observably; facade validates subscribe inputs. (Findings PRB-1.1/1.2/3.1/1.3.)
- **D2 → design doc §6.5** (story-scoped, no ADR) — money/quantity fields on public DTOs are `decimal` (`decimal?` when wire-optional), never `double`/`float`. (Finding WIR-4.)
- **D3 → [ADR-0006](adr/0006-order-confirmation-window.md)** + §9.10 — reply-immediately is a documented consumer obligation; invalidated-confirmation replies classify as a typed definitive refusal; every 2xx reply shape classifies. Held-lock/auto-reply rejected for now (recorded as a possible future opt-in). (Finding ORD-3.)
- **D4 → design doc §11.10** — Flex fidelity: nullable money + observable parse-failure signal (raw text preserved); raw timestamp strings, no offset guessing; wall-clock poll bound. (Findings RST-1/RST-3/RST-6.)
- **D5 → design doc §5.4** — facade `DisposeAsync` is the full-client teardown in `ManagedTenant` order, idempotent via atomic guard. (Finding PRB-4.3.)
- **D6 → surface lines recorded** — §16.4 (subaccounts2 `{metadata, subaccounts}` wrapper, PVR-03) · §12.5 (`AccountSummaryRow` `value` + extension data, PVR-04) · §9.7 (`TrailingAmt`/`TrailingType` added with fail-fast validation — enum retraction rejected, PVR-05) · §7.7 (health staleness consumer-configurable, tickle-interval-derived defaults, PVR-07) · §15.2 (`ToString` redaction; tenant label defaults to literal `"default"`, explicit `tenantId` override, PVR-08).
- **D7 → design doc §9.9 + §6.6** — order-mutating 200-with-error classifies as `IbkrOrderRejectedError` (hidden-error stays for non-order surfaces); uninitialized `Result<T>` member access throws `InvalidOperationException`. (Findings ERR-4/ERR-5.)

**Empirical unknowns for grooming** (verify against `recordings/`/the paper account per `.claude/rules/contract-design.md` — documented ≠ verified): subaccounts2 wrapper shape needs an FA-structure probe (PVR-03) · no recording pins the `ssd`/`sld` wire shape (PVR-04) · Flex wire number/timestamp formats (PVR-09) · suppress-endpoint response shape/status (PVR-14) · server-side OAuth base-string canonicalization on space-bearing queries (PVR-17 — the story may collapse to a no-op) · filters+`force=true` single-call sufficiency vs `sor` suppression (PVR-18) · server-side preflight reset on re-auth (PVR-23).

**Build-order map (v1.1, 2026-07-07 — supersedes v1.0; all design items closed):**

- **All 23 stories are groomable** — no design dependencies remain.
- **Lane notes (shared files, not DAG deps):** `IbkrWebSocketClient`/streaming lane — PVR-15 → PVR-16 → PVR-01; `SessionManager` lane — PVR-13 → PVR-14; PVR-04 touches `StreamingOperations` wiring — coordinate with PVR-01 if both in flight.
- **Semver:** grooming decides breaking-vs-additive per 📦 story; breaking candidates should ride one release cut so RTOS re-pins once (mirror the VCR release-train note).

<details><summary>Build-order map v1.0 (2026-07-07, historical — superseded by v1.1)</summary>

- **Blocked on design items:** PVR-01 (D1) · PVR-02 (D2, retype half) · PVR-06 (D3) · PVR-09 (D4) · PVR-21 (D5) · PVR-03/04/05/07/08 (their D6 lines) · PVR-10 (D7).
- **Buildable after grooming, no design dependency:** PVR-11 · PVR-12 · PVR-13 · PVR-14 · PVR-15 · PVR-16 · PVR-17 · PVR-18 · PVR-19 · PVR-20 · PVR-22 · PVR-23.

</details>

#### PVR-01 — 📦 Subscription-scoped streaming topic routing
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings PRB-1.1, PRB-1.2, PRB-3.1 (all high, CONFIRMED) + PRB-1.3 (low): solicited per-target subscriptions register under bare topic prefixes (`smd`/`ssd`/`sld`, `StreamingOperations.cs`) and `ProcessMessage` routes by prefix only, so two concurrent subscriptions for **different** conids/accounts each receive both targets' frames — silently wrong market/account data unless the consumer knows to filter, which nothing in the public surface states. Additionally, consumer-supplied conid/accountId/fields are interpolated into subscribe messages unescaped and unvalidated (PRB-1.3). Implements [ADR-0005](adr/0005-subscription-scoped-streaming-delivery.md) (D1): full-topic-identity routing for target-qualified topics, prefix for target-less/unsolicited, observable unmatched-frame drops, facade input validation.
**Done when:** two concurrent market-data subscriptions for different conids each observe only their own target's frames, the same holds per-account for `ssd`/`sld`, and malformed target segments are rejected at the facade.

#### PVR-02 — 📦 Presence-preserving REST money DTOs — portfolio, account summary, event contracts
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings WIR-3 (high, PLAUSIBLE), WIR-4 (medium, CONFIRMED): `Position`/`LedgerEntry` money+quantity fields and the sixteen `AccountSummaryOverview`/`AccountSummaryCashBalance` money fields (plus event-contract strike/payout) erase presence (absent → 0) and/or are typed `double` — outside VCR-01's retrofit scope. Both halves are now recorded: nullability per ADR-0001 + §6.5 ("Streaming and REST alike"), and the `double`→`decimal` retype per the §6.5 money-numeric rule (D2). WIR-3's sparse-row trigger is unpinned upstream (PLAUSIBLE) — the retrofit is safe under both answers; grooming may verify with a recording.
**Done when:** absent/empty portfolio, account-summary, and event-contract money fields surface as `null` (not 0/0.0) and money fields are `decimal`-typed per §6.5.

#### PVR-03 — 📦 Paged sub-accounts response shape
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding RST-2 (medium, CONFIRMED): `GetSubAccountsPagedAsync` declares the `/portfolio/subaccounts2` response as a bare `List<SubAccount>`, but the captured spec pins an object wrapper `{metadata, subaccounts}` — the real shape cannot deserialize into the declared one, and the WireMock fixture enshrines the wrong shape. Introduce the paged DTO per the recorded surface line (design doc §16.4, D6) and correct the fixture. **Open (empirical, grooming):** verify the wrapper shape with an FA-structure probe if one is available; the captured spec is currently the only pin.
**Done when:** the spec-pinned wrapper shape deserializes into a paged DTO surfaced by the facade and the fixture matches the captured spec.

#### PVR-04 — 📦 Streaming mapper isolation wave 2 & ssd row completeness
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings WIR-1 (high, PLAUSIBLE), PRB-3.2, PRB-3.3 (medium, PLAUSIBLE), WIR-5 (low, PLAUSIBLE): the VCR-03 per-element isolation (`TradeExecutionMapper.MapMany`'s materialize-then-yield + `onElementDropped` → `RecordMapperDrop`, on `main` since #247) was applied only to `str` — `OrderUpdateMapper`/`PnlUpdateMapper` (`sor`/`spl`) and `AccountSummaryUpdateMapper`/`AccountLedgerUpdateMapper` (`ssd`/`sld`) still drop a whole frame on one bad element; `MarketDataTickMapper` reads `_updated`/`conid` without ValueKind guards (WIR-5); `AccountSummaryRow` lacks the `value` field and `[JsonExtensionData]` escape hatch its `sld` sibling has (PRB-3.3 — D6 surface line); the money-field census is wired only for `sor`/`str`. The `AccountSummaryRow` surface line is recorded (design doc §12.5, D6); the isolation work follows the recorded VCR-03 pattern. **Open (empirical, grooming):** no recording pins the `ssd`/`sld` wire shape — capture before building.
**Done when:** one malformed element in a `sor`/`spl`/`ssd`/`sld` frame drops only that element (observably, per the VCR-02 drop taxonomy) and an `ssd` row's non-monetary value survives mapping.

#### PVR-05 — 📦 Trailing-order parameters
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding ORD-4 (medium, CONFIRMED): `OrderRequest` documents `TRAIL`/`TRAILLMT` (per the VCR-11-pinned wire enum) but exposes no `trailingAmt`/`trailingType`, which the captured spec requires for those order types — a consumer can name a trailing order it cannot parameterize. Operator-decided 2026-07-07 (D6, design doc §9.7): add `TrailingAmt` (`decimal?`) / `TrailingType` (`string?`) with fail-fast validation when the order type requires them — the enum-retraction alternative was rejected. Related: deferred VCR-12 (`ExtOperator`) is the same additive-`OrderRequest` surface family — grooming may co-schedule.
**Done when:** a consumer can place a fully-parameterized trailing order through the facade, and a `TRAIL`/`TRAILLMT` request without the parameters fails fast before any wire activity.

#### PVR-06 — 📦 Question/reply confirmation-window contract
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings ORD-3 (medium, CONFIRMED), ORD-1 (medium, PLAUSIBLE): implements [ADR-0006](adr/0006-order-confirmation-window.md) + §9.10 (D3) — documented reply-immediately obligation, typed invalidated-confirmation refusal (held-lock rejected, recorded as possible future opt-in); and widen `ReplyAsync`'s classification net so a 2xx reply body that is empty/whitespace/non-JSON classifies as an error carrying the raw body instead of escaping as `InvalidOperationException` (ORD-1).
**Done when:** an invalidated-confirmation reply surfaces as the recorded classified outcome (not a generic 503/throw), and no 2xx reply shape escapes as an unclassified exception.

#### PVR-07 — 📦 Health-status options & validation completeness
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings RST-4, TEN-3 (medium, CONFIRMED): `HealthStatusOptions` is registered as a hardcoded `new HealthStatusOptions()` with no configuration hook, so staleness thresholds cannot follow a tenant's `TickleIntervalSeconds`; and `ValidateOptions` — documented as validating all fields — skips `TickleFailureIntervalSeconds`, `WebSocketHeartbeatIntervalSeconds`, and `StreamingBufferSize`. Expose the options per the recorded surface line (design doc §7.7, D6: consumer-configurable, tickle-interval-derived defaults); add the missing range checks with the existing `ArgumentOutOfRangeException` shapes.
**Done when:** health staleness thresholds are configurable with tickle-interval-derived defaults, and non-positive values for the three unvalidated options fail fast at registration on both facade paths.

#### PVR-08 — 📦 Credential hygiene & tenant identity
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings AUT-2 (medium, CONFIRMED), AUT-5 (low, CONFIRMED): `IbkrOAuthCredentials` is a public positional record with no `ToString` override — the compiler-generated form prints `AccessToken`/`EncryptedAccessTokenSecret`, one log/exception interpolation away from a credential leak (`.claude/rules/security.md`); and `OAuthCredentialsFactory` defaults `TenantId` to the raw `ConsumerKey`, spreading the consumer key into logs/metrics as a tenant label. Redact via a sealed `ToString` override; add the `tenantId` field/parameter per the recorded surface line (design doc §15.2, D6, operator-decided: the default tenant label is the literal `"default"` — never the consumer key; the manager path always supplies its own).
**Done when:** rendering the credentials object exposes no token material, and the tenant label defaults to `"default"` with an explicit `tenantId` override through both factory paths.

#### PVR-09 — 📦 Flex statement data fidelity
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings RST-1 (medium, PLAUSIBLE), RST-3 (medium, PLAUSIBLE), RST-6 (low, UNVERIFIED): `AttrDecimal` silently coerces any unparseable money attribute to `0m` (authoritative-looking zeros on Amount/Price/Proceeds/NetCash/Commission/Quantity/FxRateToBase); `ParseFlexDateTime` guesses offsets from an 8-abbreviation US table (wrong-or-null for CET/BST/HKT/…); `PollForStatementAsync` bounds only its own inter-poll delays, not HTTP round-trip + limiter time (RST-6 — **grooming: verify by code trace**, it fell to the review's verification cap). Implements design doc §11.10 (D4): nullable money + observable parse-failure signal with raw text preserved; raw timestamp strings, no offset guessing; wall-clock poll bound. **Open (empirical, grooming):** pin the wire number/timestamp formats against a real Flex recording.
**Done when:** an unparseable Flex money/timestamp value is distinguishable from a genuine 0/absent value per the recorded semantics, and the poll loop's timeout bounds wall-clock time.

#### PVR-10 — 📦 Error-taxonomy completeness wave 2
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings ERR-2 (medium, CONFIRMED), ERR-3, ERR-4, ERR-5 (low, CONFIRMED): `ValidateFlexTokenAsync` misclassifies transient transport failures as token errors and its 1012/1013/1015 mapping is bypassed under `ThrowOnApiError`; Flex send-retry exhaustion hardcodes `IsRetryable=false` for codes the library itself classifies transient; the order-endpoint 200-hidden-error subtype contradicts the XML docs (D7); `default(Result<T>)` yields `IsSuccess=false` with a null `Error` (NRE downstream). Implements design doc §9.9 + §6.6 (D7, operator-decided): order-mutating 200-with-error remaps to `IbkrOrderRejectedError`; uninitialized `Result<T>` member access throws `InvalidOperationException`.
**Done when:** startup flex validation classifies transport vs token errors truthfully under both throw settings, exhausted-transient Flex errors carry `IsRetryable=true`, the order hidden-error subtype matches the recorded taxonomy, and an uninitialized `Result` surfaces a clear invalid-use error.

#### PVR-11 — 401-retry-leg response integrity
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings ERR-1 (high, CONFIRMED), SES-3 (medium, CONFIRMED): on the 401-reauth-retry leg, `TokenRefreshHandler` returns the retry response whose `RequestMessage` is the clone, so `ResultFactory.GetCapturedBody` misses the `ResponseBodyCaptureHandler` stash — hidden-error (200-with-error-body) detection is disabled exactly on retried calls (see the finding's trace for how this differs from the previously-refuted AMB-1); and the reauth-failure `catch` has no `OperationCanceledException` exclusion, so the consumer's own cancellation mid-reauth is misreported as `IbkrSessionError`. Fix the Options plumbing per the finding's fix direction and add the caller-cancelled passthrough, keeping ADR-0003's ambiguous-outcome marking for order-mutating POSTs.
**Done when:** a 200-with-error-body on the 401-retry leg classifies as the hidden error (not silent success), and a caller-cancelled reauth surfaces as cancellation for non-order requests.

#### PVR-12 — Tickle-loop resilience & lifetime
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings SES-1, SES-2 (both high, CONFIRMED): a reauth failure thrown from the tickle loop's 401-branch `await _onFailure(...)` escapes the enclosing catch and kills the keepalive loop permanently — one transient blip during recovery rots the session (SES-1, a VCR-06/SES-2 residual); and the loop's lifetime CTS is linked to whichever caller's token happened to initialize the session, so that caller cancelling/disposing later silently stops keepalive (SES-2). Repairs within the recorded §7 lifecycle contract — no new contract.
**Done when:** a failed reauth attempt inside the tickle loop leaves the loop running at the failure cadence, and cancelling the initializing caller's token after init does not stop the keepalive loop.

#### PVR-13 — Session/auth internals concurrency hardening
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings CON-1 (high, CONFIRMED), AUT-3, AUT-4 (low, CONFIRMED): `SessionManager.DisposeAsync` neither cancels `_disposeCts` first nor serializes with an in-flight reauth — the ODE/leaked-tickle-timer window CON-1's corroboration traces end-to-end; `SessionTokenProvider`'s refresh dedupe misses acquisitions completed by the lazy path (redundant double-handshake); the LST-validation `CryptographicException` maps to misleading "signing" guidance instead of naming the credential fields actually implicated.
**Done when:** dispose during in-flight init/reauth neither throws unhandled nor leaves a live tickle loop, a refresh concurrent with lazy acquisition performs one handshake, and an LST-validation failure names the implicated credential fields.

#### PVR-14 — Question-suppression robustness in init/reauth
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings PRB-2.1, PRB-2.2 (medium, PLAUSIBLE), PRB-2.3 (low, CONFIRMED): a non-2xx from `POST /iserver/questions/suppress` escapes `EnsureInitializedAsync`/`ReauthenticateAsync` as a raw `Refit.ApiException` — unclassified, and failing an otherwise-successful authentication; the returned `SuppressResponse` is discarded unverified against the spec-pinned `"submitted"`; and a suppress-aborted reauth skips the lifecycle notification even though the server session was re-established (PRB-2.3). Classify per the existing taxonomy, verify/log the suppress result, and notify once ssodh/init succeeds. Lane: after PVR-13 (shared `SessionManager`). **Open (empirical, grooming):** the suppress response shape/status is unpinned — capture it.
**Done when:** a suppress failure surfaces classified without masking a successful re-auth from the lifecycle notifier, and a failed suppression is observable.

#### PVR-15 — WebSocket dispose/connect race hardening
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings STR-3, STR-2 (high, CONFIRMED), CON-2 (medium, CONFIRMED), STR-6 (low, CONFIRMED): `DisposeAsync` never acquires `_connectLock` — it disposes the semaphore under an in-flight reconnect and a straggler replay can resubscribe after dispose; `SubscriptionSlot.Dispose` frees the single-observer slot before the pump task exits (two pumps competing on one reader); subscribe-vs-dispose can add a channel writer after dispose completed the registries; a failed `ConnectAsync` leaks the factory-created adapter.
**Done when:** dispose during reconnect/subscribe neither throws nor leaves live registrations, pumps, or adapters, and re-subscribe after slot disposal never yields two concurrent pumps.

#### PVR-16 — WebSocket subscribe/reconnect protocol integrity
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings STR-4, STR-5 (medium, CONFIRMED), CON-3 (low, CONFIRMED): a failed subscribe send leaves the already-committed registration in place — an orphan replayed on every reconnect with a never-drained channel; a stale reconnect trigger tears down a healthy connection established after the trigger was raised; and subscribe racing reconnect-replay can double-send a subscription. Per the findings' fix directions (rollback on failed send; connection-epoch check under the lock; send-under-lock or generation-marked replay). Lane: after PVR-15 (same file).
**Done when:** a failed subscribe leaves no registered state, a pre-connection reconnect trigger is a no-op against the fresh connection, and replay+subscribe races send exactly one subscription per topic.

#### PVR-17 — OAuth signature wire-form alignment
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding AUT-1 (high, PLAUSIBLE): the OAuth signature is computed over `Uri.ToString()` (the SafeUnescaped form) while the wire request-target carries the escaped form — divergent for `%20`/non-ASCII query values (e.g. class-share symbols like `BRK B`), which would 401 deterministically and churn a full reauth per attempt. **Open (empirical, grooming):** an attended live probe of a space-bearing query (`secdef/search?symbol=BRK B`) pins the server's accepted base-string form — the story collapses to a no-op if IBKR canonicalizes to the unescaped form; the sign-what-you-send fix direction is safe under both answers but grooming decides after the probe.
**Done when:** (post-probe) the signed base string matches the form IBKR verifies for escapable query values, or the probe refutes the divergence and the result is recorded here.

#### PVR-18 — Filtered live-orders follow-up latency & sufficiency
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings ORD-2 (medium, CONFIRMED), ORD-5 (low, PLAUSIBLE): the VCR-05 auto force-clear follow-up is awaited inline behind the endpoint rate limiter *before* the already-computed result is returned — adding up to a limiter-window of latency to every filtered `GetLiveOrdersAsync`; and the follow-up-skip when the caller passes filters+`force=true` together assumes single-call sufficiency the captured spec does not pin. **Open (grooming):** background-tracked follow-up vs limiter bypass (ORD-2's fix fork). **Open (empirical, grooming):** probe filters+`force=true` followed by a `sor` subscription; drop the exemption if suppression persists (ORD-5).
**Done when:** a filtered call returns without waiting on the follow-up, which still happens exactly once observably, and the filters+force skip matches probed IBKR behavior.

#### PVR-19 — Schema-validation net descent & strict-mode parity
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Findings WIR-2, TEN-2 (medium, CONFIRMED): the VCR-10 validation net still diffs only top-level DTO fields — wrapper-shaped endpoints' row elements are never validated (nested maps exist but are not recursed; `List<T>`-typed properties are not descended); and strict mode treats known string-returning endpoints as violations because `RefitEndpointMap` deliberately omits them (needs a known-raw sentinel, not a null entry).
**Done when:** a drifted field on a nested/wrapped row raises the validation signal, and strict mode passes string-returning endpoints while still failing truly unmapped ones.

#### PVR-20 — Active-probe health evidence flow
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding PRB-4.2 (medium, CONFIRMED): `CollectActiveSessionHealthAsync` returns the server-reported authenticated/competing/fail verdict only to its immediate caller and never feeds `SessionHealthState` — probe evidence is less durable than tickle/`sts`/ssodh evidence, contrary to ADR-0004's evidence model (recorded — cite, don't re-decide).
**Done when:** an active probe observing competing/failed session state updates `SessionHealthState` with the same durability as tickle evidence.

#### PVR-21 — Facade disposal ownership
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding PRB-4.3 (low, CONFIRMED): in the plain `AddIbkrClient` path, `IbkrClient.DisposeAsync` disposes only the container-owned `SessionManager` — `await using client` plus provider disposal double-runs the teardown, and the WebSocket client is untouched by the facade. Implements design doc §5.4 (D5, operator-decided): facade `DisposeAsync` performs the full-client teardown in `ManagedTenant` order, idempotent via atomic guard.
**Done when:** `await using client` plus provider disposal behaves per the recorded ownership contract with no double-run logout or gauge decrement.

#### PVR-22 — Tenant eager-init failure logout
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding TEN-1 (medium, CONFIRMED): `TenantBuilder` sets `SkipLogoutOnDispose=true` unconditionally before building, and its failure path disposes only the child provider and credentials — a tenant whose eager init succeeded but whose build fails afterward leaves the server-side brokerage session live with nothing to tear it down.
**Done when:** a post-init build failure issues the same bounded best-effort logout as `ManagedTenant` disposal (or the skip flag is set only once `ManagedTenant` takes ownership).

#### PVR-23 — Market-data preflight cache vs session re-auth
**Status:** Not started · **Stream:** PVR · **Depends on:** none
**Spec:** pending
Finding RST-5 (low, PLAUSIBLE): the preflight cache marks a conid preflighted for `PreflightCacheDuration` at retry-issue time, so a session re-auth inside the window can leave snapshot calls returning field-less rows that are treated as fresh. **Open (empirical, grooming):** whether IBKR resets server-side preflight state on re-auth is unpinned; the subscribe-to-`ISessionLifecycleNotifier` fix direction is safe under both answers.
**Done when:** after a re-auth, the next snapshot per conid re-preflights (cache invalidated on lifecycle notification).

## Deferred

- **VCR-12** — ExtOperator futures-compliance field (see entry above): future additive surface work; unblock = groom it (verify the compliance requirement, spec, set Risk). Related: **PVR-05** (trailing-order parameters) is the same additive-`OrderRequest` surface family — grooming may co-schedule the two.
