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

- **Contract:** `docs/ibkr_conduit_design.md` (the living design doc — canonical) · `docs/adr/` (decisions going forward) · live IBKR docs via `scout-ibkr-docs` (registry `docs/ibkr-doc-sources.md`, dated snapshots `docs/ibkr-doc-evidence/`) = the claim tier · `recordings/` + attended probes = the only verification tier. The old local mirrors (`docs/ibkr-web-api-spec.md` etc.) are deprecated snapshots — never cite as authority.
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
- **Deferred:** ~~VCR-12 (ExtOperator follow-on)~~ — groomed loop-ready 2026-07-07 (independent; buildable any wave). **VCR-13** (order-type doc widening, drafted 2026-07-07) likewise independent.

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

#### VCR-12 — 📦 ExtOperator futures-compliance field
**Status:** ✅ Done — #253 · **Stream:** VCR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Split from VCR-11 (operator decision 2026-07-07); **groomed loop-ready 2026-07-07** during the PVR re-groom session — the WIR-6 claim behind it is now verified at the claim tier (`docs/ibkr-doc-evidence/2026-07-07-extoperator-field.md`): `extOperator` is a documented order-body string on place/modify/whatif (DOC-01 schema + DOC-03), documented as required "when trading Futures and Futures Options contracts to remain in compliance with CME Group Rule 536-B" (DOC-03, which hangs the identical sentence on the already-shipped `manualIndicator`). The library's gap is exactly the body field: `ManualIndicator` and the cancel-side `extOperator`/`manualIndicator` query params are shipped; `OrderRequest`/`OrderWireModel` lack `ExtOperator`. Decided scope (operator, 2026-07-07 — design doc §9.7): add `ExtOperator` (`string?`) as a **pure pass-through** per the PVR-05 pattern — `[JsonPropertyName("extOperator")]`, omitted from the wire when null, **no client-side gating** (enforcement is documented-not-verified; pass-through is safe under both answers), XML docs stating the CME 536-B condition. **Additive — `feat:`.**
**Done when:** a consumer can set `ExtOperator` on an order request and it serializes to the wire as `extOperator` (omitted when null, on place/modify/whatif paths); XML docs state the documented futures/futures-options condition; existing suites green.
**TDD notes:** red tests = wire-model serialization pins (present when set, absent when null) mirroring the PVR-05 trailing-param tests; no WireMock scenario changes needed beyond fixture echo.

#### VCR-13 — Order-type XML docs widening (probe-verified extra values)
**Status:** ✅ Done — #252 · **Stream:** VCR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Follow-up to VCR-11, drafted+groomed 2026-07-07 during the PVR re-groom (operator-decided via the probe ack): the VCR-11-pinned seven-value `orderType` list is wire-falsified **as a closed set** — an operator-acked live probe accepted `LOC`, `MOC`, `MIT`, `LIT`, `REL` (1 sample each, all live `PreSubmitted` then cancelled; `recordings/ordertype-probe-2026-07-07.log`), matching DOC-08's CP API examples. Evidence: `docs/ibkr-doc-evidence/2026-07-07-ordertype-enum-trailing-params.md`. Decided scope: widen the `OrderRequest.OrderType`/`OrderWireModel.OrderType` XML docs (`IIbkrOrderApiModels.cs:24,:106`) to list the seven documented-core values plus the five probe-verified extras with a per-sample caveat ("accepted in live probes; not a closed set — IBKR documents no admissible enum"), and record the observed submission→read-side name mapping (`LOC→LIMITONCLOSE`, `MOC→MARKETONCLOSE`, `REL→RELATIVE`, `MIT`/`LIT` unchanged) where the read-side DTO documents `order_type`. No behavior change; no validation added (the field stays a pass-through string — `OrderType` is a bare string RTOS consumes as-is). **`fix:`** (shipped XML docs are consumer-facing; additive wording, non-breaking).
**Done when:** both `OrderType` XML doc lines list the widened value set with the per-sample caveat and the read-side mapping note; no code behavior change; existing suites green.
**TDD notes:** doc-only — no new tests.

### Stream PVR — post-VCR full-library review fixes

> **GROOMED 2026-07-07 — loop-ready.** Every fork was closed with the operator on 2026-07-07 (design items D1–D7 in the same-day design pass — ADR-0005/ADR-0006 + the design-doc sections cited below — with D3/ADR-0006 **revised on probe evidence** during grooming); empirics were verified by same-day live probes (see Evidence). PVR-17 was closed by its probe (no code change). **RE-GROOMED 2026-07-07 (same day, post-#251):** every doc-claim the original groom carried from the deprecated local mirrors was re-closed against the live registry via `scout-ibkr-docs` (13 scouts over DOC-01/03/05/07) — five evidence files under `docs/ibkr-doc-evidence/` (see Doc evidence below). **No groomed decision changed**; the live docs strengthened ADR-0006 (the invalidation claim exists live and its cancellation half is wire-falsified) and PVR-18 (the live prose prescribes the follow-up call). One new cross-source conflict surfaced (orderType enum vs DOC-08's examples) and was **resolved same-day by an operator-acked probe** (all five extras accepted) → follow-up story **VCR-13** (doc widening, loop-ready), no PVR impact. `ship-backlog` may build this stream.

**What this decomposes:** the 50 verified + 1 unverified findings of [`docs/findings/2026-07-07-multi-agent-code-review.md`](findings/2026-07-07-multi-agent-code-review.md) (full-library adversarial sweep at `main` @ `18c6a23`, run after the entire Stream VCR fix set merged; 12 high, 24 medium, 14 low). The findings doc is **immutable evidence** — entries cite finding IDs; fix work never edits the review. The 2 refuted claims (STR-1, PRB-4.1) and the 126 clean areas produce no stories. The 1 unverified finding (RST-6) is folded into PVR-09 with a grooming-verify flag.

**Design decisions (closed — the drafted stream's D1–D7, now recorded):**

- **D1 → [ADR-0005](adr/0005-subscription-scoped-streaming-delivery.md)** + design doc §12.8/§12.5 — subscription-scoped delivery: target-qualified topics route by full wire-topic identity; target-less/unsolicited keep prefix routing; same-target duplicates fan out; unmatched frames drop observably; facade validates subscribe inputs. (Findings PRB-1.1/1.2/3.1/1.3.)
- **D2 → design doc §6.5** (story-scoped, no ADR) — money/quantity fields on public DTOs are `decimal` (`decimal?` when wire-optional), never `double`/`float`. (Finding WIR-4.)
- **D3 → [ADR-0006](adr/0006-order-confirmation-window.md)** + §9.10 — reply-immediately is a documented consumer obligation; invalidated-confirmation replies classify as a typed definitive refusal; every 2xx reply shape classifies. Held-lock/auto-reply rejected for now (recorded as a possible future opt-in). (Finding ORD-3.)
- **D4 → design doc §11.10** — Flex fidelity: nullable money + observable parse-failure signal (raw text preserved); raw timestamp strings, no offset guessing; wall-clock poll bound. (Findings RST-1/RST-3/RST-6.)
- **D5 → design doc §5.4** — facade `DisposeAsync` is the full-client teardown in `ManagedTenant` order, idempotent via atomic guard. (Finding PRB-4.3.)
- **D6 → surface lines recorded** — §16.4 (subaccounts2 `{metadata, subaccounts}` wrapper, PVR-03) · §12.5 (`AccountSummaryRow` `value` + extension data, PVR-04) · §9.7 (`TrailingAmt`/`TrailingType` added with fail-fast validation — enum retraction rejected, PVR-05) · §7.7 (health staleness consumer-configurable, tickle-interval-derived defaults, PVR-07) · §15.2 (`ToString` redaction; tenant label defaults to literal `"default"`, explicit `tenantId` override, PVR-08).
- **D7 → design doc §9.9 + §6.6** — order-mutating 200-with-error classifies as `IbkrOrderRejectedError` (hidden-error stays for non-order surfaces); uninitialized `Result<T>` member access throws `InvalidOperationException`. (Findings ERR-4/ERR-5.)

**Evidence (grooming verifications, 2026-07-07 — live paper-account probes; raw logs local under `recordings/` per the repo convention, sanitized fixtures carry the shapes into committed tests):**

- **Topic-echo + ssd/sld shapes pinned** (`recordings/streaming-probe-2026-07-07.log`): the wire echoes full topic identities (`smd+756733`, `ssd+DUO873728`, `sld+DUO873728`) — ADR-0005's routing key exists on every target-qualified frame; captured ssd frames carry 114 monetary rows *and 21 non-monetary rows whose `value` field today maps nowhere* (PRB-3.3 live-confirmed); the 24-key sld row shape is pinned.
- **OAuth space-query divergence REFUTED** (`probe-oauth-space`): `secdef/search?symbol=BRK B` succeeds through the current signing (3 results, conid 72063691) — AUT-1's 401 scenario does not manifest; PVR-17 closed with no code change.
- **Confirmation invalidation pinned + decision-changing** (`recordings/order-probe-2026-07-07.log`): reply on an invalidated confirmation → `503 {"error":"Service Unavailable","statusCode":503}` (no marker), and the invalidated order **still went live afterwards** — falsifying the drafted "definitive refusal → re-place" semantics; ADR-0006 revised to serialized-round + ambiguous classification (operator-decided). Question issuance observed non-deterministic.
- **TRAIL acceptance pinned** (same log): raw order with `trailingAmt:50, trailingType:"amt"` → question `o10331` → reply → `order_id 261920143, PreSubmitted` → cancelled. The PVR-05 surface matches what the wire accepts.
- **Suppress response already pinned** by the committed live-capture fixture `tests/.../Fixtures/Session/POST-suppress.json` (`{"status":"submitted"}`); the reply endpoint was additionally observed missing from `RefitEndpointMap` at fail level during the probes (PVR-19 evidence).
- **subaccounts2**: the committed live-capture fixture shows the paper account returning a **bare array**, vs the captured spec's `{metadata, subaccounts}` wrapper (likely FA-only — operator assessment; paper accounts don't support sub-accounts) — PVR-03 handles **both shapes** (operator-decided); §16.4 corrected accordingly.
- **RST-6 CONFIRMED by code trace** (`FlexOperations.cs:333` — `totalWaited` sums only sleeps; the HTTP round-trip at :281 never counts), settling the review's one unverified finding.
- **Residual unpinned behaviors, handled by safe-under-both designs** (VCR precedent — no story builds on them): WIR-3's REST sparse-row trigger (retrofit safe either way) · filters+`force` single-call sufficiency (PVR-18 drops the exemption — always follow up, per §10.6's defensive posture) · server-side preflight reset on re-auth (PVR-23 clears the cache on re-auth regardless) · Flex wire number/timestamp formats (PVR-09's design is format-agnostic; **named follow-on:** pin formats against a real statement once a Flex query/token is configured on the paper account).

**Doc evidence (re-groom, 2026-07-07 — live-doc claim tier via `scout-ibkr-docs`; each file supersedes the corresponding deprecated-mirror citation):**

- `docs/ibkr-doc-evidence/2026-07-07-subaccounts2-response-shape.md` — PVR-03/§16.4: the wrapper-vs-bare-array conflict lives inside IBKR's own current docs (DOC-01 schema + DOC-03 prose claim the wrapper; DOC-03's own example is a bare array); both-shapes decision confirmed.
- `docs/ibkr-doc-evidence/2026-07-07-ordertype-enum-trailing-params.md` — PVR-05/§9.7 (+ VCR-11 historical): the seven-value enum and trailing/dual-price requirements confirmed live (DOC-03); `trailingType ∈ {"amt","%"}` (DOC-01+DOC-03); **new conflict, probe-resolved:** DOC-08's CP API examples submit LOC/MOC/MIT/LIT/REL and an operator-acked probe accepted all five (1 sample each) — no PVR impact; remedied by **VCR-13**.
- `docs/ibkr-doc-evidence/2026-07-07-order-reply-confirmation-suppression.md` — ADR-0006/PVR-06/PVR-14: the reply-immediately + 503 invalidation claim exists live (DOC-03) and its "will cancel the order" half is wire-falsified — ADR-0006's ambiguous classification is the only design safe under both; DOC-01's five documented reply-200 shapes feed PVR-06's ORD-1 test fixtures; suppress `{"status":"submitted"}` agreed everywhere.
- `docs/ibkr-doc-evidence/2026-07-07-live-orders-filters-force.md` — §10.6/PVR-18: the sor-suppression warning exists live (DOC-03) and prescribes a **follow-up** `force=true` call (its own example combines both in one call, unexplained) — always-follow-up confirmed; the unprimed fake-empty first call remains observed-but-undocumented.
- `docs/ibkr-doc-evidence/2026-07-07-flex-error-codes-formats.md` — PVR-09/PVR-10/§11.10: the 1012/1013/1015 table confirmed live (DOC-07 = DOC-03 verbatim); **no tier documents statement content formats or a retryability contract** — format-agnostic design mandated; poll/rate guidance qualitative only (1/sec, 10/min hard cap).

**Release train (operator-decided 2026-07-07, stricter behavioral-is-breaking partition):** the eight breaking stories — PVR-01, 02, 03, 06, 08, 09, 10, 21, all `feat!:` — merge before the release-please release PR is accepted, so **one minor cut carries the full PVR breaking set** and RTOS re-pins once. `feat:` (PVR-04, 05, 07) and `fix:` stories may land in the same or later cuts. Consumer migration notes are an acceptance item on each breaking story.

**Build-order map (v1.2, 2026-07-07 — supersedes v1.1; groomed):**

- **Wave 1 (independent):** PVR-02 📦 · PVR-03 📦 · PVR-05 📦 · PVR-07 📦 · PVR-08 📦 · PVR-09 📦 · PVR-10 📦 · PVR-11 · PVR-12 · PVR-13 · PVR-15 · PVR-18 · PVR-19 · PVR-20 · PVR-21 📦 · PVR-22 · PVR-23.
- **Lanes (shared files, sequential within a lane, not DAG deps):** streaming-client lane — PVR-15 → PVR-16 → PVR-01 📦; `SessionManager` lane — PVR-13 → PVR-14; PVR-04 📦 (mappers/models) coordinates with PVR-01 (registries) if both in flight; PVR-06 📦 (OrderOperations lock scope) after PVR-18 (same file) in one lane.
- **Closed:** PVR-17 (probe refuted the finding — no build).
- Semver per story is decided and marked on each entry (release train above).

<details><summary>Build-order map v1.1 (2026-07-07, historical — superseded by v1.2)</summary>

- All 23 stories groomable — no design dependencies remain (D1–D7 closed).
- Lane notes: streaming lane PVR-15 → PVR-16 → PVR-01; SessionManager lane PVR-13 → PVR-14; PVR-04 coordinates with PVR-01.

</details>

<details><summary>Build-order map v1.0 (2026-07-07, historical — superseded by v1.1)</summary>

- **Blocked on design items:** PVR-01 (D1) · PVR-02 (D2, retype half) · PVR-06 (D3) · PVR-09 (D4) · PVR-21 (D5) · PVR-03/04/05/07/08 (their D6 lines) · PVR-10 (D7).
- **Buildable after grooming, no design dependency:** PVR-11 · PVR-12 · PVR-13 · PVR-14 · PVR-15 · PVR-16 · PVR-17 · PVR-18 · PVR-19 · PVR-20 · PVR-22 · PVR-23.

</details>

#### PVR-01 — 📦 Subscription-scoped streaming topic routing
**Status:** ✅ Done — #275 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-pvr-01-subscription-scoped-routing.md
Findings PRB-1.1, PRB-1.2, PRB-3.1 (all high, CONFIRMED) + PRB-1.3 (low): solicited per-target subscriptions register under bare topic prefixes (`smd`/`ssd`/`sld`, `StreamingOperations.cs`) and `ProcessMessage` routes by prefix only, so two concurrent subscriptions for **different** conids/accounts each receive both targets' frames — silently wrong market/account data unless the consumer knows to filter, which nothing in the public surface states. Additionally, consumer-supplied conid/accountId/fields are interpolated into subscribe messages unescaped and unvalidated (PRB-1.3). Implements [ADR-0005](adr/0005-subscription-scoped-streaming-delivery.md) (D1): full-topic-identity routing for target-qualified topics, prefix for target-less/unsolicited, observable unmatched-frame drops, facade input validation. **Breaking-behavioral — `feat!:`.**
**Done when:** two concurrent market-data subscriptions for different conids each observe only their own target's frames, the same holds per-account for `ssd`/`sld`, and malformed target segments are rejected at the facade.
**TDD notes:** red tests per the spec test plan (mock-WS cross-target isolation, unmatched-frame counter via MeterListener, input validation).

#### PVR-02 — 📦 Presence-preserving REST money DTOs — portfolio, account summary, event contracts
**Status:** ✅ Done — #271 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-pvr-02-rest-money-dto-retrofit.md
Findings WIR-3 (high, PLAUSIBLE), WIR-4 (medium, CONFIRMED): `Position`/`LedgerEntry` money+quantity fields and the sixteen `AccountSummaryOverview`/`AccountSummaryCashBalance` money fields (plus event-contract strike/payout) erase presence (absent → 0) and/or are typed `double` — outside VCR-01's retrofit scope. Both halves are now recorded: nullability per ADR-0001 + §6.5 ("Streaming and REST alike"), and the `double`→`decimal` retype per the §6.5 money-numeric rule (D2). WIR-3's sparse-row trigger is unpinned upstream — handled as safe-under-both (see stream Evidence); no story dependency. **Breaking — `feat!:`.**
**Done when:** absent/empty portfolio, account-summary, and event-contract money fields surface as `null` (not 0/0.0) and money fields are `decimal`-typed per §6.5.
**TDD notes:** red tests per the spec test plan (presence + decimal-precision per model family; reflection sweep pinning no-double-money).

#### PVR-03 — 📦 Paged sub-accounts response shape
**Status:** ✅ Done — #257 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Finding RST-2 (medium, CONFIRMED): `GetSubAccountsPagedAsync` declares the `/portfolio/subaccounts2` response as a bare `List<SubAccount>`, while the live docs claim an object wrapper `{metadata, subaccounts}` (DOC-01 schema + DOC-03 prose — though DOC-03's own example shows a bare array; `docs/ibkr-doc-evidence/2026-07-07-subaccounts2-response-shape.md`). Grooming evidence (2026-07-07): the committed live-capture fixture shows the paper account returning a **bare array** — the wrapper is verified nowhere and is likely FA-structure-only (operator assessment, aligned with the docs' explicit tiered/FA scoping; paper accounts don't support sub-accounts). Operator-decided: deserialize **both shapes** — wrapper or bare array — normalizing into one paged DTO (§16.4 as corrected); safe under both answers. **Breaking — `feat!:`** (return shape changes to the paged DTO).
**Done when:** both the live-captured bare-array shape and the spec-claimed wrapper shape deserialize into the same paged DTO through the facade, with page metadata null-absent for the bare-array form.
**TDD notes:** red tests = one WireMock scenario per shape; the existing sanitized live fixture is the bare-array case.

#### PVR-04 — 📦 Streaming mapper isolation wave 2 & ssd row completeness
**Status:** ✅ Done — #273 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-pvr-04-streaming-mapper-isolation-wave2.md
Findings WIR-1 (high, PLAUSIBLE), PRB-3.2, PRB-3.3 (medium, PLAUSIBLE), WIR-5 (low, PLAUSIBLE): the VCR-03 per-element isolation (`TradeExecutionMapper.MapMany`'s materialize-then-yield + `onElementDropped` → `RecordMapperDrop`, on `main` since #247) was applied only to `str` — `OrderUpdateMapper`/`PnlUpdateMapper` (`sor`/`spl`) and `AccountSummaryUpdateMapper`/`AccountLedgerUpdateMapper` (`ssd`/`sld`) still drop a whole frame on one bad element; `MarketDataTickMapper` reads `_updated`/`conid` without ValueKind guards (WIR-5); `AccountSummaryRow` lacks the `value` field and `[JsonExtensionData]` escape hatch its `sld` sibling has (PRB-3.3 — D6 surface line); the money-field census is wired only for `sor`/`str`. The `AccountSummaryRow` surface line is recorded (design doc §12.5, D6); the isolation work follows the recorded VCR-03 pattern. Empirics pinned by the 2026-07-07 live probe — full topic echoes plus the 21 non-monetary `value` rows and the 24-key `sld` row shape (see stream Evidence + the spec). **Additive — `feat:`.**
**Done when:** one malformed element in a `sor`/`spl`/`ssd`/`sld` frame drops only that element (observably, per the VCR-02 drop taxonomy) and an `ssd` row's non-monetary value survives mapping.
**TDD notes:** red tests from sanitized probe-derived fixtures per the spec test plan (mixed valid/malformed frames per topic; the Cushion-row pin).

#### PVR-05 — 📦 Trailing-order parameters
**Status:** ✅ Done — #254 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Finding ORD-4 (medium, CONFIRMED): `OrderRequest` documents `TRAIL`/`TRAILLMT` (per the VCR-11-pinned wire enum, re-confirmed live 2026-07-07 — `docs/ibkr-doc-evidence/2026-07-07-ordertype-enum-trailing-params.md`) but exposes no `trailingAmt`/`trailingType`, which the live docs require for those order types ("You must specify both trailingType and trailingAmt for TRAIL and TRAILLMT order", DOC-03) — a consumer can name a trailing order it cannot parameterize. Operator-decided 2026-07-07 (D6, design doc §9.7): add `TrailingAmt` (`decimal?`) / `TrailingType` (`string?`) with fail-fast validation when the order type requires them — the enum-retraction alternative was rejected. Wire acceptance pinned by the 2026-07-07 live probe: `trailingAmt:50, trailingType:"amt"` → question `o10331` → `order_id 261920143, PreSubmitted` (see stream Evidence). Related: VCR-12 (`ExtOperator`, groomed loop-ready 2026-07-07) is the same additive-`OrderRequest` surface family. **Additive — `feat:`.**
**Done when:** a consumer can place a fully-parameterized trailing order through the facade, and a `TRAIL`/`TRAILLMT` request without the parameters fails fast before any wire activity.
**TDD notes:** red tests = wire-model serialization pins (trailingAmt/trailingType present for TRAIL, omitted when null) + fail-fast validation cases; WireMock fixture derived from the probe capture.

#### PVR-06 — 📦 Question/reply confirmation-window contract
**Status:** ✅ Done — #272 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-pvr-06-confirmation-window.md
Findings ORD-3 (medium, CONFIRMED), ORD-1 (medium, PLAUSIBLE): implements [ADR-0006](adr/0006-order-confirmation-window.md) + §9.10 **as revised on the 2026-07-07 probe evidence** (the invalidated order went live after its reply 503'd — "refusal → re-place" double-places; see stream Evidence): the confirmation round is **serialized in-process** (per-account lock held from confirmation-returning placement until reply/dismiss/timeout, new `ConfirmationTimeout` option), a failed reply on an invalidated confirmation classifies as an **ambiguous order outcome** (ADR-0003 family — reconcile before resubmitting), and every 2xx reply shape classifies (ORD-1). Lane: after PVR-18 (shared `OrderOperations`). **Breaking-behavioral — `feat!:`.**
**Done when:** a second same-account placement waits for a pending confirmation round (and proceeds after reply/dismiss/timeout), a reply 503 surfaces as the ambiguous outcome with reconcile guidance, and no 2xx reply shape escapes as an unclassified exception.
**TDD notes:** red tests per the spec test plan (lock retention/timeout via fake TimeProvider; 503 fixture = the probe body verbatim; ORD-1 shapes).

#### PVR-07 — 📦 Health-status options & validation completeness
**Status:** ✅ Done — #256 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Findings RST-4, TEN-3 (medium, CONFIRMED): `HealthStatusOptions` is registered as a hardcoded `new HealthStatusOptions()` with no configuration hook, so staleness thresholds cannot follow a tenant's `TickleIntervalSeconds`; and `ValidateOptions` — documented as validating all fields — skips `TickleFailureIntervalSeconds`, `WebSocketHeartbeatIntervalSeconds`, and `StreamingBufferSize`. Expose the options per the recorded surface line (design doc §7.7, D6: consumer-configurable, tickle-interval-derived defaults); add the missing range checks with the existing `ArgumentOutOfRangeException` shapes. **Additive — `feat:`.**
**Done when:** health staleness thresholds are configurable with tickle-interval-derived defaults, and non-positive values for the three unvalidated options fail fast at registration on both facade paths.

#### PVR-08 — 📦 Credential hygiene & tenant identity
**Status:** ✅ Done — #274 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings AUT-2 (medium, CONFIRMED), AUT-5 (low, CONFIRMED): `IbkrOAuthCredentials` is a public positional record with no `ToString` override — the compiler-generated form prints `AccessToken`/`EncryptedAccessTokenSecret`, one log/exception interpolation away from a credential leak (`.claude/rules/security.md`); and `OAuthCredentialsFactory` defaults `TenantId` to the raw `ConsumerKey`, spreading the consumer key into logs/metrics as a tenant label. Redact via a sealed `ToString` override; add the `tenantId` field/parameter per the recorded surface line (design doc §15.2, D6, operator-decided: the default tenant label is the literal `"default"` — never the consumer key; the manager path always supplies its own). **Breaking-behavioral — `feat!:`** (telemetry label default changes; `tenantId` field/parameter is additive).
**Done when:** rendering the credentials object exposes no token material, and the tenant label defaults to `"default"` with an explicit `tenantId` override through both factory paths.

#### PVR-09 — 📦 Flex statement data fidelity
**Status:** ✅ Done — #276 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Findings RST-1 (medium, PLAUSIBLE), RST-3 (medium, PLAUSIBLE), RST-6 (low, UNVERIFIED): `AttrDecimal` silently coerces any unparseable money attribute to `0m` (authoritative-looking zeros on Amount/Price/Proceeds/NetCash/Commission/Quantity/FxRateToBase); `ParseFlexDateTime` guesses offsets from an 8-abbreviation US table (wrong-or-null for CET/BST/HKT/…); `PollForStatementAsync` bounds only its own inter-poll delays, not HTTP round-trip + limiter time (RST-6 — **CONFIRMED by grooming code trace**, `FlexOperations.cs:333`). Implements design doc §11.10 (D4): nullable money + observable parse-failure signal with raw text preserved; raw timestamp strings, no offset guessing; wall-clock poll bound. The design is format-agnostic (operator-decided: loop-ready without a live format pin — no Flex query is configured on the paper account); pinning the wire formats against a real statement is the **named follow-on** in the stream Evidence. **Breaking — `feat!:`** (Flex DTO money fields become nullable).
**Done when:** an unparseable Flex money/timestamp value is distinguishable from a genuine 0/absent value (null + parse-failure signal + raw text), and the poll loop's timeout bounds wall-clock elapsed time (fake TimeProvider test).
**TDD notes:** red tests = synthetic statements per RST-1/RST-3 suggested regression tests (unparseable amounts, non-US timezone suffixes, absent attributes) + a wall-clock timeout test with slow stubbed polls.

#### PVR-10 — 📦 Error-taxonomy completeness wave 2
**Status:** ✅ Done — #277 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings ERR-2 (medium, CONFIRMED), ERR-3, ERR-4, ERR-5 (low, CONFIRMED): `ValidateFlexTokenAsync` misclassifies transient transport failures as token errors and its 1012/1013/1015 mapping is bypassed under `ThrowOnApiError`; Flex send-retry exhaustion hardcodes `IsRetryable=false` for codes the library itself classifies transient; the order-endpoint 200-hidden-error subtype contradicts the XML docs (D7); `default(Result<T>)` yields `IsSuccess=false` with a null `Error` (NRE downstream). Implements design doc §9.9 + §6.6 (D7, operator-decided): order-mutating 200-with-error remaps to `IbkrOrderRejectedError`; uninitialized `Result<T>` member access throws `InvalidOperationException`. **Breaking-behavioral — `feat!:`.**
**Done when:** startup flex validation classifies transport vs token errors truthfully under both throw settings, exhausted-transient Flex errors carry `IsRetryable=true`, the order hidden-error subtype matches the recorded taxonomy, and an uninitialized `Result` surfaces a clear invalid-use error.

#### PVR-11 — 401-retry-leg response integrity
**Status:** ✅ Done — #267 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings ERR-1 (high, CONFIRMED), SES-3 (medium, CONFIRMED): on the 401-reauth-retry leg, `TokenRefreshHandler` returns the retry response whose `RequestMessage` is the clone, so `ResultFactory.GetCapturedBody` misses the `ResponseBodyCaptureHandler` stash — hidden-error (200-with-error-body) detection is disabled exactly on retried calls (see the finding's trace for how this differs from the previously-refuted AMB-1); and the reauth-failure `catch` has no `OperationCanceledException` exclusion, so the consumer's own cancellation mid-reauth is misreported as `IbkrSessionError`. Fix the Options plumbing per the finding's fix direction and add the caller-cancelled passthrough, keeping ADR-0003's ambiguous-outcome marking for order-mutating POSTs. **`fix:`.**
**Done when:** a 200-with-error-body on the 401-retry leg classifies as the hidden error (not silent success), and a caller-cancelled reauth surfaces as cancellation for non-order requests.
**TDD notes:** red tests = WireMock 401-then-200-error-body scenario asserting hidden-error classification on the retry leg; cancellation-mid-reauth case per SES-3.

#### PVR-12 — Tickle-loop resilience & lifetime
**Status:** ✅ Done — #262 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings SES-1, SES-2 (both high, CONFIRMED): a reauth failure thrown from the tickle loop's 401-branch `await _onFailure(...)` escapes the enclosing catch and kills the keepalive loop permanently — one transient blip during recovery rots the session (SES-1, a VCR-06/SES-2 residual); and the loop's lifetime CTS is linked to whichever caller's token happened to initialize the session, so that caller cancelling/disposing later silently stops keepalive (SES-2). Repairs within the recorded §7 lifecycle contract — no new contract. **`fix:`.**
**Done when:** a failed reauth attempt inside the tickle loop leaves the loop running at the failure cadence, and cancelling the initializing caller's token after init does not stop the keepalive loop.
**TDD notes:** red tests extend TickleTimerTests: reauth-throw inside the 401 branch leaves the loop ticking (mock-server tickle counts); cancel-initializer-token case.

#### PVR-13 — Session/auth internals concurrency hardening
**Status:** ✅ Done — #260 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings CON-1 (high, CONFIRMED), AUT-3, AUT-4 (low, CONFIRMED): `SessionManager.DisposeAsync` neither cancels `_disposeCts` first nor serializes with an in-flight reauth — the ODE/leaked-tickle-timer window CON-1's corroboration traces end-to-end; `SessionTokenProvider`'s refresh dedupe misses acquisitions completed by the lazy path (redundant double-handshake); the LST-validation `CryptographicException` maps to misleading "signing" guidance instead of naming the credential fields actually implicated. **`fix:`.**
**Done when:** dispose during in-flight init/reauth neither throws unhandled nor leaves a live tickle loop, a refresh concurrent with lazy acquisition performs one handshake, and an LST-validation failure names the implicated credential fields.
**TDD notes:** deterministic race tests with test gates (VCR-08 pattern) for dispose-vs-reauth; version-dedupe unit tests for AUT-3.

#### PVR-14 — Question-suppression robustness in init/reauth
**Status:** ✅ Done — #268 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings PRB-2.1, PRB-2.2 (medium, PLAUSIBLE), PRB-2.3 (low, CONFIRMED): a non-2xx from `POST /iserver/questions/suppress` escapes `EnsureInitializedAsync`/`ReauthenticateAsync` as a raw `Refit.ApiException` — unclassified, and failing an otherwise-successful authentication; the returned `SuppressResponse` is discarded unverified against the pinned `"submitted"` (wire fixture + all live sources agree on the lowercase form — `docs/ibkr-doc-evidence/2026-07-07-order-reply-confirmation-suppression.md`); and a suppress-aborted reauth skips the lifecycle notification even though the server session was re-established (PRB-2.3). Classify per the existing taxonomy, verify/log the suppress result, and notify once ssodh/init succeeds. Lane: after PVR-13 (shared `SessionManager`). Empirics: the success shape is pinned by the committed live-capture fixture `Fixtures/Session/POST-suppress.json` (`{"status":"submitted"}`); ApiCapture's edge entries additionally pin 500-on-empty-ids and 200-on-invalid-id. **`fix:`.**
**Done when:** a suppress failure surfaces classified without masking a successful re-auth from the lifecycle notifier, and a failed suppression is observable.

#### PVR-15 — WebSocket dispose/connect race hardening
**Status:** ✅ Done — #261 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-07-pvr-15-websocket-dispose-race-hardening.md
Findings STR-3, STR-2 (high, CONFIRMED), CON-2 (medium, CONFIRMED), STR-6 (low, CONFIRMED): `DisposeAsync` never acquires `_connectLock` — it disposes the semaphore under an in-flight reconnect and a straggler replay can resubscribe after dispose; `SubscriptionSlot.Dispose` frees the single-observer slot before the pump task exits (two pumps competing on one reader); subscribe-vs-dispose can add a channel writer after dispose completed the registries; a failed `ConnectAsync` leaks the factory-created adapter. **`fix:`.**
**Done when:** dispose during reconnect/subscribe neither throws nor leaves live registrations, pumps, or adapters, and re-subscribe after slot disposal never yields two concurrent pumps.
**TDD notes:** red tests per the spec test plan (fake adapter factory + deterministic gates per race).

#### PVR-16 — WebSocket subscribe/reconnect protocol integrity
**Status:** ✅ Done — #269 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings STR-4, STR-5 (medium, CONFIRMED), CON-3 (low, CONFIRMED): a failed subscribe send leaves the already-committed registration in place — an orphan replayed on every reconnect with a never-drained channel; a stale reconnect trigger tears down a healthy connection established after the trigger was raised; and subscribe racing reconnect-replay can double-send a subscription. Per the findings' fix directions (rollback on failed send; connection-epoch check under the lock; send-under-lock or generation-marked replay). Lane: after PVR-15 (same file). **`fix:`.**
**Done when:** a failed subscribe leaves no registered state, a pre-connection reconnect trigger is a no-op against the fresh connection, and replay+subscribe races send exactly one subscription per topic.
**TDD notes:** red tests: failed-send rollback, stale-trigger no-op via connection epoch, replay/subscribe single-send.

#### PVR-17 — OAuth signature wire-form alignment
**Status:** ✅ Done — #250 (closed by grooming probe; no code change) · **Stream:** PVR · **Depends on:** none
**Spec:** trivial-skip
Finding AUT-1 (high, PLAUSIBLE): the OAuth signature is computed over `Uri.ToString()` (the SafeUnescaped form) while the wire request-target carries the escaped form — a deterministic-401 hazard for `%20`-bearing query values, if IBKR verified the raw request-target. **Resolved by live probe 2026-07-07:** `secdef/search?symbol=BRK B` **succeeds** through the current signing (200, 3 results, conid 72063691; control `SPY` and not-found `BRK.B` behaved as expected) — IBKR's verifier accepts the library's base-string form for space-bearing queries, so the divergence does not manifest and no code change is warranted. Scope note: the probe pins the space case (the only known IBKR symbology need); non-ASCII query values remain theoretical with no known use.
**Done when:** ~~(post-probe) the signed base string matches the form IBKR verifies~~ — satisfied by the recorded probe refutation (this entry).

#### PVR-18 — Filtered live-orders follow-up latency & sufficiency
**Status:** ✅ Done — #270 · **Stream:** PVR · **Depends on:** none
**Risk:** high
**Spec:** trivial-skip
Findings ORD-2 (medium, CONFIRMED), ORD-5 (low, PLAUSIBLE): the VCR-05 auto force-clear follow-up is awaited inline behind the endpoint rate limiter *before* the already-computed result is returned — adding up to a limiter-window of latency to every filtered `GetLiveOrdersAsync`; and the follow-up-skip when the caller passes filters+`force=true` together assumes single-call sufficiency no doc tier pins (the live DOC-03 prose prescribes a **follow-up** call while its own example combines both, unexplained — `docs/ibkr-doc-evidence/2026-07-07-live-orders-filters-force.md`). Operator-decided 2026-07-07: the follow-up runs as a **background-tracked task** through the normal rate limiters (§8 wait-not-fail intact), logged on failure, awaited/cancelled on dispose. ORD-5 is rule-settled by §10.6's defensive posture (the `sor`-suppression effect is documented-not-observable-on-demand): **drop the filters+`force` exemption — always issue the follow-up after any filtered call**, safe under both answers. Lane: before PVR-06 (shared `OrderOperations`). **`fix:`.**
**Done when:** a filtered call returns without waiting on the follow-up; the follow-up happens exactly once observably (asserted call sequence) including when the caller passed `force=true` with filters; dispose awaits/cancels a pending follow-up.
**TDD notes:** red tests = WireMock call-sequence assertions (immediate return + exactly-one deferred force call) and a dispose-with-pending-follow-up case.

#### PVR-19 — Schema-validation net descent & strict-mode parity
**Status:** ✅ Done — #259 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Findings WIR-2, TEN-2 (medium, CONFIRMED): the VCR-10 validation net still diffs only top-level DTO fields — wrapper-shaped endpoints' row elements are never validated (nested maps exist but are not recursed; `List<T>`-typed properties are not descended); and strict mode treats known string-returning endpoints as violations because `RefitEndpointMap` deliberately omits them (needs a known-raw sentinel, not a null entry). Grooming evidence: the 2026-07-07 probes observed `POST /iserver/reply/{id}` logged at fail level as unmapped — the reply endpoint must land in the map (or the sentinel) as part of this story. **`fix:`.**
**Done when:** a drifted field on a nested/wrapped row raises the validation signal, and strict mode passes string-returning endpoints while still failing truly unmapped ones.

#### PVR-20 — Active-probe health evidence flow
**Status:** ✅ Done — #264 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Finding PRB-4.2 (medium, CONFIRMED): `CollectActiveSessionHealthAsync` returns the server-reported authenticated/competing/fail verdict only to its immediate caller and never feeds `SessionHealthState` — probe evidence is less durable than tickle/`sts`/ssodh evidence, contrary to ADR-0004's evidence model (recorded — cite, don't re-decide). **`fix:`.**
**Done when:** an active probe observing competing/failed session state updates `SessionHealthState` with the same durability as tickle evidence.

#### PVR-21 — 📦 Facade disposal ownership
**Status:** ✅ Done — #258 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Finding PRB-4.3 (low, CONFIRMED): in the plain `AddIbkrClient` path, `IbkrClient.DisposeAsync` disposes only the container-owned `SessionManager` — `await using client` plus provider disposal double-runs the teardown, and the WebSocket client is untouched by the facade. Implements design doc §5.4 (D5, operator-decided): facade `DisposeAsync` performs the full-client teardown in `ManagedTenant` order, idempotent via atomic guard. **Breaking-behavioral — `feat!:`** (operator-decided stricter partition: dispose now tears down the WS client and session where it was session-only).
**Done when:** `await using client` plus provider disposal behaves per the recorded ownership contract with no double-run logout or gauge decrement.

#### PVR-22 — Tenant eager-init failure logout
**Status:** ✅ Done — #265 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Finding TEN-1 (medium, CONFIRMED): `TenantBuilder` sets `SkipLogoutOnDispose=true` unconditionally before building, and its failure path disposes only the child provider and credentials — a tenant whose eager init succeeded but whose build fails afterward leaves the server-side brokerage session live with nothing to tear it down. **`fix:`.**
**Done when:** a post-init build failure issues the same bounded best-effort logout as `ManagedTenant` disposal (or the skip flag is set only once `ManagedTenant` takes ownership).

#### PVR-23 — Market-data preflight cache vs session re-auth
**Status:** ✅ Done — #266 · **Stream:** PVR · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Finding RST-5 (low, PLAUSIBLE): the preflight cache marks a conid preflighted for `PreflightCacheDuration` at retry-issue time, so a session re-auth inside the window can leave snapshot calls returning field-less rows that are treated as fresh. The server-side preflight-reset behavior is unpinned and not observable on demand; rule-settled as safe-under-both (see stream Evidence): clear the cache on lifecycle notification regardless. **`fix:`.**
**Done when:** after a re-auth, the next snapshot per conid re-preflights (cache invalidated on lifecycle notification).

## Deferred

*(empty — VCR-12, formerly here, was groomed loop-ready on 2026-07-07 after its compliance claim was verified against the live docs; see its entry.)*

## Stream FO — Post-Stream-PVR follow-ons

> Review nits & named follow-ons from the 2026-07-07/08 ship-backlog run (Stream VCR + Stream PVR, PRs #252–#277). **Groomed loop-ready 2026-07-09** (open-question sweep: no operator-forks remained after FO-3/FO-9 were settled attended; no empirical blockers — all client-side/tooling). None blocked a merge. FO-5/FO-6 deferred (Flex); FO-9 resolved (ERR-1 retracted in place).
>
> **SHIPPED 2026-07-09 (ship-backlog run):** FO-3 (#281, `feat!:` — folds into the 0.9.0 train), FO-2 (#284), FO-4 (#282), FO-7 (#285), FO-1 (#286), FO-8b (#287) all merged — post-merge offline suite **1579/0**. **FO-8a deferred** (premise invalid — draft #283 unmerged; see its entry). FO-5/FO-6 remain Flex-deferred. FO-3 must land in **release-please #241** before that 0.9.0 cut is accepted.
>
> **GROOMED 2026-07-09 (second pass) — loop-ready:** the mid-run follow-ons were groomed. An empirical tool survey (build of every `tools/*.csproj`) found exactly two broken by the recent breaking changes — `QueryAccount` and `DiagnosticLst`; all `examples/` are CI-gated and green. Resulting stories: **FO-10** (STR-4 reap-symmetry, spec'd, high) · **FO-11** (rewire QueryAccount to the public surface — fix, not retire) · **FO-12** (retire the obsolete `DiagnosticLst` bring-up tool — operator-decided) · **FO-13** (add a CI tools-build gate so tools can't silently re-rot; depends FO-11+FO-12). All loop-ready; `ship-backlog` may build them (FO-13 after FO-11/FO-12).

#### FO-1 — Bounded single-account dispose logout
**Status:** ✅ Done — #286 · **Stream:** FO · **Depends on:** FO-2
**Risk:** standard
**Spec:** trivial-skip
`SessionManager.DisposeAsync`'s single-account logout uses `CancellationToken.None`, not `LogoutTimeout` — §5.4's "bounded" best-effort logout is only enforced on the `ManagedTenant` path. Thread a `LogoutTimeout`-capped CTS into the single-account dispose logout so both paths honour the same bound. (PVR-21 review.) Ordered after FO-2 (same `DisposeAsync`/logout region). **`fix:`.**
**Done when:** a hanging single-account dispose logout is cancelled at `LogoutTimeout` (not awaited unbounded), matching the `ManagedTenant` path; dispose still completes.
**TDD notes:** red test = a fake logout that never returns; assert `DisposeAsync` completes within ~`LogoutTimeout` via a controllable `TimeProvider`.

#### FO-2 — EnsureInitializedAsync post-auth-throw session leak
**Status:** ✅ Done — #284 · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
A narrow window where a post-`authenticated=true` step (SuppressQuestions / tickle `StartAsync`) throws before `_sessionEstablished` is set leaks the server-side session on both the single-account and `TenantBuilder` paths. Move `_sessionEstablished`/logout-eligibility to the moment ssodh reports `authenticated=true`, so a later-step throw still leaves the session logout-eligible on dispose. Repairs within the recorded §7 lifecycle contract — no new contract. (PVR-22 review; pre-existing.) **`fix:`.**
**Done when:** if a post-`authenticated=true` init step throws, `DisposeAsync` still issues the best-effort server logout (no leaked brokerage session) on both the single-account and `TenantBuilder` paths.
**TDD notes:** red test = fake SuppressQuestions/tickle-start that throws after ssodh reports `authenticated=true`; assert dispose issues a logout (mock server logout count == 1).

#### FO-3 — 📦 Unify session-path Refit error classification
**Status:** ✅ Done — #281 · **Stream:** FO · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-09-fo-3-session-error-classification.md
Session-path failures classify inconsistently: `ClassifySuppressFailure` (PVR-14) splits 429/5xx→transient vs 4xx→config, while `WrapCredentialException` sends every ssodh/init Refit `ApiException` to `IbkrConfigurationException` via the `_` fallback (probe-verified 2026-07-09: Refit 12's `ApiException` doesn't match the `HttpRequestException` branch), so transient 5xx/429 are mis-reported as permanent config errors. Introduce one shared status→category helper both classifiers call, add an `ApiException` arm to `WrapCredentialException` keyed on `ApiException.StatusCode`, and give a 401/403 suppress a status-specific Warning. Decision of record: [ADR-0007](adr/0007-session-path-error-classification.md); design doc §7.8. **Breaking-behavioral — `feat!:`; folds into the 0.9.0 train, must land before release-please #241 is cut.**
**Done when:** a session-path failure classifies identically whether it arrives as a raw `HttpRequestException` or a Refit `ApiException` — 5xx/429→`IbkrTransientException`, 401/403→`IbkrConfigurationException`, other 4xx/non-HTTP→configuration with the path hint; `ssodh/init` 503 surfaces as transient; a 401/403 suppression logs an authorization-specific Warning.
**TDD notes:** red tests = `ApiException` 500/503/429→transient, 401/403/400/404→config in `SessionManagerWrapCredentialExceptionTests` (built via the existing `ApiException.Create` helper); WireMock `ssodh/init` 503 → `IbkrTransientException`; raw-`HttpRequestException` tests stay green.

#### FO-4 — Reap empty streaming subscriber entries
**Status:** ✅ Done — #282 · **Stream:** FO · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-09-fo-4-subscriber-map-reap.md
After the last unsubscribe, `IbkrWebSocketClient._subscribers` keeps an empty writer-list keyed by the (now full-identity) routing key — bounded per key but unbounded over the lifetime of a client rotating many conids/accounts. Reap the key when its last writer unsubscribes, value-conditionally under the existing `_subscriptionLock`/`lock(writers)`, and extend the CON-2 subscribe-side guard so a subscribe racing a reap re-attempts (benign) while a subscribe racing a dispose still fails (ODE). (PVR-01 review; deferred at ship time to protect the race invariants.) **`fix:` — internal only, no public surface.**
**Done when:** the key is removed once its last writer unsubscribes; a key with surviving writers is retained and keeps delivering; a subscribe racing a reap is routed (never orphaned); a subscribe racing a dispose still fails.
**TDD notes:** deterministic-gate race tests (VCR-08/PVR-13 pattern) — reap on last-unsubscribe; no premature reap with a surviving writer; gated reap-vs-subscribe asserts the new subscription receives a later broadcast; existing dispose-vs-subscribe CON-2 test stays green. Add an `internal` `_subscribers`-count seam if none exists.

#### FO-7 — Redact the consumer key in the QueryAccount diagnostic tool
**Status:** ✅ Done — #285 · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
`tools/QueryAccount/Program.cs` echoes the full OAuth consumer key to the console — outside the library's `IbkrOAuthCredentials.ToString` redaction (PVR-08). Truncate it in the diagnostic tool, consistent with the `ToString` redaction convention. **Scope (operator-decided 2026-07-09): the `QueryAccount` diagnostic echo only — the `tools/IbkrConduit.Setup` wizard legitimately displays the key (the user copies it into the IBKR portal) and is left unchanged.** Tools-only change; the library's runtime credential handling already ships redacted (PVR-08), hence `standard` risk. **`fix:` (or `chore:`).**
**Done when:** `QueryAccount` prints a redacted/truncated consumer key matching the `IbkrOAuthCredentials.ToString` form; the Setup wizard is unchanged.
**TDD notes:** trivial — the tool has no test project; verify by inspection / a manual run. The redaction helper (if extracted) can carry a unit test.

#### FO-8a — MarketDataTickMapper invariant-culture numeric parse
**Status:** Deferred — premise invalid (draft PR #283, not merged); route to grooming to drop or re-file · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
`MarketDataTickMapper`'s string→number parse uses `CurrentCulture`, so a host in a comma-decimal culture misparses streaming price/size fields (PVR-04). Align to `InvariantCulture` (IBKR wire numerics are invariant). A real correctness fix, split out from the FO-8 test-only nits. **`fix:`.**
**Done when:** `MarketDataTickMapper` parses numerics with `InvariantCulture`; a test under a comma-decimal culture asserts a wire value like `"1.5"` parses to `1.5`, not `15`.
**TDD notes:** red test sets `CultureInfo.CurrentCulture` to `de-DE` and asserts the mapped tick numeric is correct.
> **Deferred 2026-07-09 (ship-backlog sweep) — premise does not hold for this library.** Implementation + independent review (draft PR #283, left unmerged) found: `MarketDataTickMapper` **integer-parses only** (conid, `_updated`, field-id keys) and stores price/size field *values* verbatim as `string`s — it never decimal-parses. `int/long.TryParse` with `NumberStyles.Integer` rejects `.`/`,` in **every** culture, so no real integer wire token diverges under a comma-decimal culture; the actual decimal/double wire parser (`EmptyTolerantNumberParsing`) **already** uses `InvariantCulture`. The Done-when's observable red test (`"1.5"`→`15`) is therefore unachievable here (the added test passed against both pre- and post-change code — a tautology, not a regression guard). The genuine comma-decimal risk, if any, lives in a **consumer** that parses the `Fields<string,string>` values, not in this library. **Grooming decision needed:** drop FO-8a, or re-file the explicit-`InvariantCulture` int/long-parse hardening as a `chore:`/`refactor:` (safe, convention-matching, but a no-op for all real inputs — the loop declined to merge a no-op under a `fix:` label).

#### FO-8b — Test-strength hardening (PVR-20/06/18)
**Status:** ✅ Done — #287 · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Test-strength nits from the PVR panels, bundled: PVR-20 — strengthen the healthy-active-probe test to seed `competing:true`→`false` clearing; PVR-06 — add a `ThrowOnApiError=true` + 503/timeout reply-ordering test; PVR-18 — add a provider-only dispose-direction test (client `DisposeAsync` never called) for the facade teardown, and add the small belt-and-suspenders `IbkrClient.DisposeAsync` → dispose `Orders`. **`test:` (+ the one tiny dispose line).**
**Done when:** the three test gaps are covered and green, and `IbkrClient.DisposeAsync` disposes `Orders` defensively.
**TDD notes:** each sub-item is its own focused test; the dispose line gets a test asserting `Orders` is disposed on facade teardown.

#### FO-10 — Reap-symmetry on the STR-4 send-failure path
**Status:** ✅ Done — #293 · **Stream:** FO · **Depends on:** none
**Risk:** high
**Spec:** docs/superpowers/specs/2026-07-09-fo-10-str4-reap-symmetry.md
Groomed loop-ready 2026-07-09. Surfaced by both FO-4 quality lenses on PR #282. FO-4 reaps empty `_subscribers` entries on the *unsubscribe* path, but the STR-4 **send-failure rollback** in `IbkrWebSocketClient.SubscribeTopicAsync` (the `catch` around the immediate `SendTextAsync`) removes the just-added writer *without* reaping a now-empty list — leaving exactly the empty full-topic-identity entry (`smd+<conid>`) FO-4 targets. Same leak class, on the sibling path FO-4's spec explicitly scoped out. Bounded/rare (send failures are uncommon; a later subscribe on that conid reuses the empty list, whose eventual unsubscribe reaps it), so it is a symmetry/hygiene fix, not a live leak. The spec pins the one subtlety — the interaction with FO-4's subscribe retry — and confirms the value-conditional, lock-atomic reap introduces no new race. No open fork; no upstream-behavior dependency (internal data structure). **`fix:` — internal only, no public surface.**
**Done when:** a subscribe whose immediate `SendTextAsync` throws as the sole writer for a routing key leaves no empty `_subscribers` entry mapped (value-conditional reap on the rollback path, mirroring FO-4's unsubscribe reap and preserving the CON-2/CON-3 race invariants); a send failure with a surviving co-writer on the same key retains the key and keeps delivering.
**TDD notes:** gated race/rollback test in the FO-4 / VCR-08 / PVR-13 style — drive a send failure on a sole-writer subscribe, assert the key is reaped; assert a surviving co-writer on the same key is retained; the existing CON-2 dispose-vs-subscribe and FO-4 reap tests stay green (run the streaming suite a few times for nondeterminism).

#### FO-11 — Rewire the QueryAccount diagnostic tool to the public surface
**Status:** ✅ Done — #291 · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Groomed loop-ready 2026-07-09 (fork closed — **fix, don't retire**; QueryAccount is a useful data-query diagnostic). `tools/QueryAccount` no longer compiles against `main` (empirically verified 2026-07-09): `GetAccountsAsync` / `GetLiveOrdersAsync` / `GetTradesAsync` now return `Result<T>` (not raw collections), orders return `LiveOrdersSnapshot`, and its manual pipeline wiring references now-`internal` `OAuthSigningHandler` / `ISessionTokenProvider` / `ISessionManager`. Rewire it onto the **public consumer surface** exactly as `.claude/rules/testing.md` / the example apps do — `AddIbkrClient(opts => opts.Credentials = ...)` DI, resolve `IIbkrClient`, and unwrap each `Result<T>` via the `IbkrError` taxonomy (`src/IbkrConduit/Errors/`) — dropping all manual handler/session wiring (that's what pulled in the internals). Read `LiveOrdersSnapshot.Orders`/`.IsSnapshot`. **Preserve FO-7's consumer-key redaction** (the `[redacted]` echo). Rule-settled (consumers use the DI pipeline + `Result<T>`); no public-surface change to the library. CI gating of the compiled result is FO-13 (not this story). **`fix:` — tools-only.**
**Done when:** `tools/QueryAccount` compiles against the current public surface using `AddIbkrClient` + `IIbkrClient` + `Result<T>` unwrap (no `internal`-type references, no manual handler wiring); orders read `LiveOrdersSnapshot`; the FO-7 consumer-key redaction is retained; `dotnet build tools/QueryAccount/QueryAccount.csproj -c Release` succeeds with zero warnings.
**TDD notes:** the tool has no test project — verify by a clean `dotnet build` of the tool csproj (0 warnings under `TreatWarningsAsErrors`) and by inspection that every call unwraps `Result<T>` (no `Result<T>` used as a collection). A live run is attended-only (paper account) and out of the unattended loop.

#### FO-12 — Retire the obsolete DiagnosticLst tool
**Status:** ✅ Done — #292 · **Stream:** FO · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Groomed loop-ready 2026-07-09 — **operator-decided retirement** (2026-07-09): DiagnosticLst no longer provides value. It is a Milestone-1 OAuth bring-up tool (`=== ssodh/init 401 Investigation ===`) — five frozen request permutations that hand-build OAuth-signed calls to **live production** `api.ibkr.com` to debug a signing/header problem that has long since been resolved (the library establishes sessions correctly; VCR/PVR shipped). Keeping it compiling would require granting a non-test tool `InternalsVisibleTo` the crypto primitives (`HmacSha256Signer`, `StandardBaseStringBuilder`, `OAuthHeaderBuilder`, `LiveSessionTokenClient`) — eroding encapsulation for an answered question — and it echoes the LST token + auth headers to the console. Remove the `tools/DiagnosticLst/` project. No library code changes (the tool is not referenced by `IbkrConduit.slnx` or any project). **`chore:` — tools-only removal.**
**Done when:** `tools/DiagnosticLst/` is removed; no dangling references remain (grep confirms nothing references `DiagnosticLst`); the solution and full offline suite build/pass unchanged.
**TDD notes:** removal — verify by `dotnet build` (solution) + full offline suite staying green, and a repo-wide grep showing no residual `DiagnosticLst` references.

#### FO-13 — Gate the tools in CI (tools-build step)
**Status:** ✅ Done — #294 · **Stream:** FO · **Depends on:** FO-11, FO-12
**Risk:** standard
**Spec:** trivial-skip
Groomed loop-ready 2026-07-09 (operator-decided: **add a CI tools-build step**). The two broken tools (QueryAccount, DiagnosticLst) rotted silently because `tools/` is neither in `IbkrConduit.slnx` nor built by CI — unlike `examples/` (both the `.csproj` examples in the solution and the single-file `examples/*.cs` that CI builds in a loop). Add a CI step that builds every `tools/*.csproj` (mirroring the existing "Build single-file example apps" step in `.github/workflows/ci.yml`) so a future breaking change that breaks a tool **fails CI** instead of silently rotting it. Depends on FO-11 (QueryAccount compiles) and FO-12 (DiagnosticLst removed) so the new gate is green on introduction. Build-only (no tests — the explicit KeyGenerator suite in `IbkrConduit.Setup` stays opt-in per `.claude/rules/explicit-tests.md`). Extend the workflow's `paths` filter so `tools/**` changes trigger the job. **`ci:` — CI only.**
**Done when:** CI builds every `tools/*.csproj` (`ApiCapture`, `CaptureFlexQuery`, `IbkrConduit.Setup`, `QueryAccount`) on each PR and fails if any tool fails to compile; `tools/**` is in the workflow `paths` filter; the step is green on `main` after FO-11/FO-12 land.
**TDD notes:** CI-config change — validate by a green run on the PR (all tools build) and, as a one-off local check, `dotnet build` each `tools/*.csproj`. A deliberately-broken tool should fail the new step (verify manually/by reasoning; do not commit the break).

### Deferred (Flex — operator-deferred 2026-07-09)

#### FO-5 — Flex zone-less timestamp offset
**Status:** Deferred — Flex, deferred with FO-6 (operator, 2026-07-09) · **Stream:** FO · **Depends on:** none
`ParseFlexDateTime`'s general parse assumes the host-local offset for a zone-less hyphenated timestamp (e.g. tz column disabled). Pass `DateTimeStyles.AssumeUniversal` or accept only explicit-offset forms. Pre-existing, outside RST-3's tz-abbreviation scope. (PVR-09 review.) **Unblock:** operator lifts the Flex deferral.

#### FO-6 — Pin Flex wire formats against a real statement
**Status:** Deferred — Flex; blocked on a configured Flex token/query (operator, 2026-07-09) · **Stream:** FO · **Depends on:** none
PVR-09's design is format-agnostic (no live Flex statement is configured on the paper account). Once a Flex query/token is configured, capture a real statement and pin the money/timestamp wire formats. (Grooming named follow-on.) 2026-07-08 scouting reconfirmed Flex query IDs are Client-Portal-UI-sourced only. **Unblock:** a Flex token + query ID configured on the paper account, then capture via `tools/ApiCapture/CaptureFlexQuery`.

### Resolved (no build story)

- **FO-9 — Re-annotate ERR-1 in the findings doc — RESOLVED 2026-07-09 (retracted in place).** ERR-1's verdict was flipped to `⚠️ FALSIFIED` in the summary table and a dated correction block added above the preserved original (`docs/findings/2026-07-07-multi-agent-code-review.md`). The CONFIRMED public-surface silently-wrong-data consequence is false (Refit's `ApiResponse<T>.RequestMessage` already returns the original request); PVR-11's `retryResponse.RequestMessage = request` reassignment was still load-bearing (removed a dangling disposed-clone reference + hardened the non-Refit `GetAmbiguousOrderOutcome` path). No build story.

## Stream RPD — RTOS 2026-07-13 probe-dossier fixes

> **GROOMED 2026-07-14 — loop-ready.** Design pass (2026-07-13, ADR-0008..0011) and live-probe verification (2026-07-14) both folded in; the stream-wide open-question sweep closed the two remaining operator-forks (RPD-03's sample-sufficiency call, RPD-07's DTO shape) via a batched `AskUserQuestion`, and specced RPD-03/RPD-06 (real designs) while `trivial-skip`ping the five pattern-following field-promotion stories. Every entry now has `Spec:` (a path or `trivial-skip`) and `Risk` set. `ship-backlog` may build this stream.

**Grooming — closed 2026-07-14 (operator-attended):**

- **RPD-03 sample sufficiency:** operator chose to proceed with the n=2 array-shape evidence rather than gather more live-probe samples first — the classifier design is field-signature-based (not an enumerated shape list), so it's expected to generalize; the spec's defensive fallback (an unrecognized row classifies `IbkrAmbiguousOrderError`, never a silent success) covers shapes not yet observed. Specced: [`docs/superpowers/specs/2026-07-14-rpd-03-bracket-per-leg-outcome-classification.md`](superpowers/specs/2026-07-14-rpd-03-bracket-per-leg-outcome-classification.md).
- **RPD-07 DTO shape:** operator chose a nullable per-entry field over a response-level marker — the single-real-currency test account can't actually distinguish "always USD" from "settlement currency" from "last real-currency entry," so committing to a response-level promotion rule right now would be guessing past the evidence; a nullable per-entry field needs no such guess and matches this stream's other nullable-as-presence stories.
- **RPD-02 Risk, a grooming judgment call (not an operator-fork):** set `high`, not the `standard` a strict read of `.claude/rules/backlog-format.md`'s four listed categories would give a read-only field addition — `parentId`/`ocaGroupId` directly feed RTOS's bracket child-census reconciliation logic, which RTOS itself scoped "⭐ load-bearing" and safety-relevant; a defect here has order-management consequences even though the endpoint itself only reads.
- **RPD-06 specced** (not `trivial-skip`) despite carrying no 📦 marker — the retry-once heuristic and its WireMock call-count-sequenced test design are non-trivial enough to warrant review, per this stream's "design worth review → spec" rule even for non-breaking stories. See [`docs/superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md`](superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md).
- **RPD-05's `strike` typing is rule-settled, not asked:** this repo already has tolerant number-or-string converters for exactly this instability (`EmptyTolerantNullableDecimalConverter`, `src/IbkrConduit/Serialization/EmptyTolerantNumberConverters.cs`) — applying the existing pattern needed no operator input.

**What this decomposes:** [`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`](findings/2026-07-13-rtos-probe-dossier-doc-scouting.md) — RTOS's IBV-P live-paper probe suite run 2026-07-13 against paper account `DUO873728` through IbkrConduit 0.9.0 (source dossier: `RTOS-PROBE-DOSSIER-2026-07-13.md`, repo root, preserved verbatim inside the findings doc), merged with a `scout-ibkr-docs` pass against DOC-01/03/05/08 answering "what do IBKR's live docs say" for every item — including the two RTOS explicitly scoped as *not* conduit issues. The findings doc is the evidence trail; entries below cite it by item id (P1–P6, NC1, NC2).

**Design pass — closed 2026-07-13 (operator-attended):**

- **RPD-03's premise** (order-submission rejection classification) → **[ADR-0008](adr/0008-bracket-per-leg-outcome-classification.md)** + design doc §9.11. Decision: `PlaceOrdersAsync` (multi-leg groups only) classifies **per leg** — transmitted / definitely rejected / ambiguous (reusing ADR-0003/0006's `IbkrAmbiguousOrderError`) — instead of collapsing the response array to one group-level outcome. 📦 breaking; `PlaceOrderAsync` (single-order) is unaffected. RPD-04 attaches to whichever per-leg shape RPD-03's spec settles on.
- **RPD-06's premise** (extending the cold-read guarantee to Positions/Trades) → **[ADR-0009](adr/0009-positions-trades-cold-read-retry.md)** + design doc §10.7. Decision: `GetPositionsAsync`/`GetTradesAsync` transparently retry once on a heuristically-sparse first read (no wire signal exists for these two endpoints, unlike LiveOrders' `snapshot:false`) — an internal, non-📦 behavior change, not an `IsSnapshot`-style wrapper.
- **NC1** (bracket-order atomicity) → **[ADR-0010](adr/0010-bracket-atomicity-no-emulation.md)** + design doc §9.12. Decision: no client-side atomicity emulation; documentation-only, no story.
- **P6's "consider" item** (typed dead-order cancel classification) → **[ADR-0011](adr/0011-cancel-order-no-dead-order-classification.md)** + design doc §9.13. Decision: not built — the platform gives no structured signal to classify on, and doing it via message-text matching would reintroduce the anti-pattern ADR-0008 moves away from; documentation-only, no story.

**Live probe verification (2026-07-14) — 5 probes run serially against the paper account, all cleanup confirmed, folded back into ADRs/design doc/entries below:**

- **Ledger `endofbundle`** (RPD-07): observed 5/5 — reliably present on the real-currency (`USD`) entry, absent from `BASE`; explains RTOS's "absent 4/4" as a per-entry-validator artifact, not the field being fictional. Found a live bug in the same area: `EndOfBundle` is modeled non-nullable `int`, so absence silently defaults to `0` (ADR-0001 violation, independent of which shape RPD-07 lands on). `recordings/ledger-endofbundle-probe/`.
- **Positions/Trades cold-read timing** (RPD-06): both hypotheses observed 3/3; immediate no-delay retry confirmed sufficient to reprime both endpoints. ADR-0009 updated from "assumed" to "verified." Same probe found `Position.strike`'s JSON type changes between the sparse and enriched read of the same row. `recordings/coldread-rpd06/`.
- **`strike` wire type** (RPD-05): no options position existed on the paper account — not manufactured, per the no-mutation constraint on this probe — so the options-strike question stays open. Incidental finding upgraded the story instead: `strike` on the *positions* endpoint is type-unstable within one account/session, tied to read-freshness (see cold-read probe above), not just cross-source doc disagreement.
- **`parentId`/`ocaGroupId` presence** (RPD-02): answered from existing recordings (a 2026-06-28 clean bracket+OCA submission) plus RTOS's independent 2026-07-13 observation — two dates agree on presence. New nuance: `ocaGroupId`'s shape differs by origin — a prefixed `"oco-<orderId>"` string for an explicit OCA group vs. a bare integer string (= the parent's `orderId`) for a bracket's own exit legs. `parentId` also has a request/response type asymmetry (cOID string in, integer `orderId` out).
- **Invalid-child bracket rejection shape** (RPD-02/RPD-03): the big one — 3 different invalidity mechanisms produced 3 structurally different wire responses (no-array hard reject; sentinel array with the *whole bracket* rejected together; real-ID array with the parent transmitting via a confirmation-question chain). Root-caused the original defect precisely: where an array does appear, the child is structurally `responses[0]` and the parent `responses[1]` — not failure-mode-dependent reordering as ADR-0008 originally speculated. Found a new undocumented field `parent_order_id` on a child row. Found that the "broken `parentId` linkage" invalidity mechanism is already prevented client-side (`ValidateOrderGroup` throws before any network call) — unreachable via the library. Folded into [ADR-0008](adr/0008-bracket-per-leg-outcome-classification.md) (revised 2026-07-14) and design doc §9.11. `recordings/rpd02-invalidchild-{a,b,c}-*/`.

**Build-order map (v1.2, 2026-07-14 — supersedes v1.1, loop-ready):**

- **Wave 1 (independent):** RPD-01 · RPD-02 · RPD-03 · RPD-05 · RPD-06 · RPD-07.
- **Wave 2:** RPD-04 (depends: RPD-03).
- **Lane note:** RPD-05 and RPD-06 both touch `Position`/its mapper — run RPD-06 after RPD-05 in one lane.
- **Risk summary:** `high` — RPD-02, RPD-03. `standard` — RPD-01, RPD-04, RPD-05, RPD-06, RPD-07.

---

#### RPD-01 — 📦 Typed-field promotion sweep: `LiveOrder.order_cancellation_by_system_reason`, `ContractSearchResult` (`showPrips`, `legSecType`), `CancelOrderResponse.account`
**Status:** ✅ Done — #299 · **Stream:** RPD · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Three independently clean field promotions bundled as one mechanical sweep — each is either doc-confirmed or a total (harmless) doc gap, none carries an open fork:
- `order_cancellation_by_system_reason` on the live-orders row: **documented + observed** — IBKR's own OpenAPI JSON (DOC-01) already models it (`"Only present for Cancelled orders. Provides the reason for order to have been cancelled or rejected by the system."`); today it's only reachable via `[JsonExtensionData]`. (Findings doc P1.)
- `showPrips`, `legSecType` on `ContractSearchResult` (`GET /iserver/secdef/search`): observed on the wire, absent from every registered doc source with no adjacent doc claim of any kind. (Findings doc P5.)
- `account` on `CancelOrderResponse`: **documented + observed** across all three narrative/schema sources, which additionally document a specific sentinel pairing worth modeling explicitly — `account: null` paired with `conid: -1` means "order was immediately cancelled on request" (DOC-01, DOC-03). (Findings doc P6, schema half.)
All three follow the existing nullable-as-presence pattern (ADR-0001) — no new design decision required.
**Grooming (2026-07-14):** the `account`/`conid` sentinel pairing gets plain nullable fields with an XML doc note explaining the "immediately cancelled" meaning — no dedicated accessor type (three similar nullable fields don't warrant an abstraction).
**Done when:** `LiveOrder`, `ContractSearchResult`, and `CancelOrderResponse` expose these five fields as typed nullable properties (per ADR-0001), populated from the wire, no longer surfacing only via `AdditionalData`; a 401-recovery test covers each of the three touched endpoints per `.claude/rules/testing.md`.
**TDD notes:** WireMock fixtures per field (present/absent cases); no new production types, just added properties + mapping.

#### RPD-02 — 📦 Typed `parentId`/`ocaGroupId` on `LiveOrder` for bracket/OCA parent-child linkage ⭐ load-bearing
**Status:** ✅ Done — #301 · **Stream:** RPD · **Depends on:** none
**Risk:** high — grooming judgment call (see stream preamble): feeds RTOS's bracket child-census reconciliation, not itself order placement/modification.
**Spec:** trivial-skip
RTOS's bracket child-id census resolves child→parent linkage from `parentId` and groups via `ocaGroupId`, both observed on the wire on bracket/cancelled rows but reachable today only via `AdditionalData`. Doc scouting found **no source documenting either field on the live-orders response**: `parentId` is documented only as a *request*-side bracket-child field (DOC-01, DOC-03, DOC-05 all agree); `ocaGroupId` appears in **zero** IBKR sources anywhere, including the TWS-API side of DOC-08, whose OCA mechanism uses a differently-named `ocaGroup` attribute in a different API surface entirely (no CP Web API content exists for Bracket/OCA order types on DOC-08 at all). (Findings doc P1.)
**Verified 2026-07-14 by live probe** (existing 2026-06-28 clean-submission recordings + a fresh probe run — `recordings/bracket/`, `recordings/oca/`, `recordings/rpd02-invalidchild-{a,b,c}-*/`): presence confirmed across two independent dates (2026-06-28 recording, 2026-07-13 RTOS run). Two nuances now pinned that the story description above didn't anticipate: **`ocaGroupId`'s shape differs by origin** — an explicit OCA group (`isSingleGroup:true` on every leg) gets a prefixed `"oco-<orderId>"` string, but a bracket's own take-profit/stop exit legs also carry `ocaGroupId`, as a **bare integer string equal to the parent's `orderId`** (no prefix) — grouping logic must handle both forms, never assume the `"oco-"` prefix. **`parentId` has a request/response type asymmetry**: the request sends the parent's cOID as a string; the response echoes back the parent's server-assigned `orderId` as an integer-valued string — RTOS's/this story's child→parent resolution correctly keys on the response-side integer, not the request-side cOID.
**Done when:** `LiveOrder` exposes `parentId` and `ocaGroupId` as typed nullable fields, documented to cover both observed `ocaGroupId` shapes (prefixed and bare) and the `parentId` type asymmetry; no longer surfacing only via `AdditionalData`; a 401-recovery test covers `GET /iserver/account/orders`.
**TDD notes:** WireMock fixtures from `recordings/bracket/004-GET...json` (bracket exit legs, bare-integer `ocaGroupId`) and `recordings/oca/001...json`'s follow-up GET (explicit OCA, prefixed `ocaGroupId`) — both shapes need their own test case, not just one.

#### RPD-03 — 📦 Bracket/OCA per-leg outcome classification for `PlaceOrdersAsync`
**Status:** ✅ Done — #302 · **Stream:** RPD · **Depends on:** none
**Risk:** high — order placement/modification.
**Spec:** [`docs/superpowers/specs/2026-07-14-rpd-03-bracket-per-leg-outcome-classification.md`](superpowers/specs/2026-07-14-rpd-03-bracket-per-leg-outcome-classification.md)
**[ADR-0008](adr/0008-bracket-per-leg-outcome-classification.md) + design doc §9.11 (closed 2026-07-13, revised 2026-07-14 after live-probe verification).** Submitting a bracket group with a deliberately invalid child returned a row the conduit surfaced as a normal `OrderSubmitted` with `order_id=-1`, `status=Failed` — RTOS's probe logged it as `POST outcome: ACCEPTED?! order_id=-1 status=Failed` — while the parent order actually went live. `PlaceOrdersAsync`'s classifier reads only `responses[0]` and treats it as the group's single outcome; the fix is a **per-leg breakdown**, not a smarter single-value classifier: every leg in IBKR's response array classifies independently as transmitted (`OrderSubmitted`), definitely rejected (`IbkrOrderRejectedError`), or — a requested leg missing from the response — ambiguous (`IbkrAmbiguousOrderError`, reusing ADR-0003/0006's "sent, outcome unknown, reconcile before resubmitting" semantics). Applies only to `PlaceOrdersAsync` (multi-leg groups); `PlaceOrderAsync` (single order) is unchanged. (Findings doc P1/P2 — the "behavioral half" of P2, the more important one per RTOS.)
**Verified and sharpened 2026-07-14 by live probe** (3 invalidity mechanisms — `recordings/rpd02-invalidchild-{a,b,c}-*/`): the response is **heterogeneous across invalidity mechanisms, not one sentinel shape**. A bogus child conid produced a bare `{"error": "..."}` object, no array, whole group hard-rejected. A negative child price reproduced RTOS's `order_id=-1`/`status=Failed` sentinel in a 2-element array — but the **parent didn't go live either** (`status=Inactive`, whole bracket rejected together — RTOS's "parent live" outcome is not universal). A mismatched-instrument child produced a real-ID array (no sentinel) with the parent live via a confirmation-question chain — matching RTOS's original scenario but through a different shape. Where an array does appear, the **child is structurally `responses[0]`, the parent `responses[1]`** in both samples — root-causing the original defect precisely — but classification must key on field signature (`local_order_id` marks the parent; the new field `parent_order_id`, previously undocumented, links a child back) and never on position, since a 2-sample correlation isn't a documented contract. The "broken `parentId` linkage" mechanism turned out unreachable via the library — `ValidateOrderGroup` already throws client-side before any network call.
**Grooming (2026-07-14):** operator decided to proceed with the n=2 array-shape evidence (see stream preamble) rather than gather more samples first. The terminal `order_status` set (`Failed`, `Inactive` observed) is deliberately left open/extensible in the spec — an unrecognized future status degrades to `IbkrAmbiguousOrderError` (the safe direction) rather than requiring a closed enum.
**Done when:** per the spec — `PlaceOrdersAsync` returns `IReadOnlyList<OneOf<OrderSubmitted, IbkrOrderRejectedError, IbkrAmbiguousOrderError>>` for the array case (unchanged whole-call rejection for the no-array case); a bracket submission with a rejected child never surfaces the child's sentinel row (or a status-terminal parent row) as `OrderSubmitted`; classification keys on field signature, never array position; `PlaceOrderAsync`'s existing classification is untouched; XML doc corrected.
**TDD notes:** see the spec's 11-step TDD plan — unit tests on the new `ClassifyGroupResponses` against all three probe-captured wire shapes, plus a bare-object-reject regression guard and the mandatory 401-recovery test (confirms composition with ADR-0003's no-replay gate, not a fresh replay test).

#### RPD-04 — 📦 Typed `text`, `warning_message`, `messageOptions`, `parent_order_id` on order-submission response models
**Status:** ✅ Done — #303 · **Stream:** RPD · **Depends on:** RPD-03
**Risk:** standard — DTO field exposure, not order placement logic itself.
**Spec:** trivial-skip
The schema half of P2 — three response fields carrying rejection/reply detail, at three different levels of doc corroboration: `text` is **documented + observed**, in-context, on `advancedOrderReject` (DOC-01). `warning_message` is observed on the wire in the orders context but undocumented there — the *same field name* is documented elsewhere (DOC-01, DOC-03) on an unrelated FYI/alert-creation endpoint as always returning `null`; flag this explicitly in the spec so that always-null assumption doesn't get carried over by mistake. `messageOptions` is documented only in a DOC-03 worked example (not in DOC-03's own field-list prose, and absent from DOC-01's formal schema) — partially corroborated. (Findings doc P2, schema half.) Depends on RPD-03 landing first since these fields attach to whichever per-leg outcome type ADR-0008 produces (per the 📦-first ordering rule).
**Added 2026-07-14 by live probe** (`recordings/rpd02-invalidchild-c-mismatchconid/`): a fourth field, `parent_order_id` — observed on a child's submission-response row in the confirmation-chain scenario, linking it to the parent's `order_id`. Undocumented anywhere, not in the original RTOS dossier, and distinct from both the request-side `parentId` field and `LiveOrder`'s response-side `parentId` (RPD-02) — a third, separate field carrying similar-but-not-identical linkage information at a different point in the flow.
**Done when:** the per-leg outcome type(s) RPD-03 establishes expose `text`, `warning_message`, `messageOptions`, and `parent_order_id` as typed nullable fields; where a leg classifies `IbkrOrderRejectedError` via RPD-03's terminal-status path, its message is enriched from `text`/`warning_message` when present instead of the generic `"Order not transmitted (status: ...)"` fallback.
**TDD notes:** WireMock fixture from `recordings/rpd02-invalidchild-b-negprice/001-POST...json` (sentinel row with `text`+`warning_message`) and `.../c-mismatchconid/001-POST...json` (question row with `messageOptions`, child row with `parent_order_id`).

#### RPD-05 — 📦 Typed `Position` fields: `baseMktValue`/`baseMktPrice`/`baseAvgCost`/`baseRealizedPnl`/`baseUnrealizedPnl`/`lastTradingDay`/`expiry`/`putOrCall`/`strike`
**Status:** Not started · **Stream:** RPD · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
Nine `Position` fields, all **documented + observed**: the five `base*` fields are documented only by DOC-01 (absent from DOC-03, which doesn't cover them at all); `lastTradingDay`/`expiry`/`putOrCall`/`strike` are documented by both DOC-01 and DOC-03. Follows the existing ADR-0001 nullable-as-presence pattern — no new design decision required. (Findings doc P3, documented-fields half.)
**Sharpened 2026-07-14 by live probe** (`recordings/coldread-rpd06/`, `recordings/rpd05-strike-type/`): `strike`'s wire type is inconsistent across every source that types it — DOC-01 types it `string` with no nullable flag; DOC-03's own examples disagree with themselves across sibling endpoints (`0.0` float vs. `"0"` string). The probe found this instability is **not just cross-source disagreement — it's a live, reproducible behavior of this one endpoint**: for the same STK position rows, `strike` serialized as a JSON number (`0.0`) on the sparse first read and a JSON string (`"0"`) on the enriched second read of the same session (see RPD-06). A separate options-focused probe found no options position on the account to test the options-strike case specifically (deferred, not manufactured), but did find `strike` serializes as an unquoted number (`704.0`) on `GET /iserver/secdef/info` for a real option — a different endpoint, so not conclusive for Positions, but another number-typed data point.
**Grooming (2026-07-14) — rule-settled, no operator ask needed:** this repo already has a converter built for exactly this instability — `EmptyTolerantNullableDecimalConverter` (`src/IbkrConduit/Serialization/EmptyTolerantNumberConverters.cs`), which accepts a JSON number, a quoted numeric string, or an empty/whitespace string, and normalizes to `decimal?`. Type `strike` as `decimal?` using that converter instead of inventing new handling.
**Done when:** `Position` exposes these nine fields as typed nullable properties per ADR-0001; `strike` is typed `decimal?` via `EmptyTolerantNullableDecimalConverter`, tolerant of both JSON string and JSON number on the wire; a test case covers both observed shapes (`0.0` and `"0"`) deserializing to the same value.
**TDD notes:** WireMock fixtures from `recordings/coldread-rpd06/s1-positions-1.json` (number-shaped `strike`) and `s1-positions-2.json` (string-shaped `strike`) — both must deserialize identically.

#### RPD-06 — Heuristic auto-retry-once on sparse first-read for Positions/Trades
**Status:** Not started · **Stream:** RPD · **Depends on:** none
**Risk:** standard — internal read-path behavior, not order placement/auth/credentials/streaming delivery.
**Spec:** [`docs/superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md`](superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md)
**[ADR-0009](adr/0009-positions-trades-cold-read-retry.md) + design doc §10.7 (closed 2026-07-13) — no 📦 marker: not a public-surface change.** RTOS observed the first positions read of a session returning field-sparse rows (`name`/`ticker` missing) with a second read enriched (findings doc P3), and separately flagged (as explicitly *not* a conduit issue) that the trades endpoint's first call of a session returns empty despite trades existing (findings doc NC2) — the same family of behavior this repo already wire-confirmed and designed around for `GetLiveOrdersAsync` (VCR D4, design doc §10.6), but neither Positions nor Trades carry a wire-reported freshness signal the way LiveOrders' `snapshot:false` does. Decision: `GetPositionsAsync`/`GetTradesAsync` transparently retry once, immediately, when the first read of a session looks sparse (Positions: `name`/`ticker` missing; Trades: empty result on first call) — no `IsSnapshot`-style wrapper, no public-surface change; a retry is observable only via an `Activity` tag.
**Verified 2026-07-14 by live probe** (3 fresh-session repetitions — `recordings/coldread-rpd06/`): both the sparse-first-read behavior and the immediate-no-delay-retry-reprimes assumption observed 3/3 for Positions and 3/3 for Trades. The "looks sparse" predicate is now pinned against real captures — Positions: `name`/`ticker` missing (21 keys vs. 46 enriched); Trades: `[]` on first call despite the account holding same-day trades. Residual, still-untested: a consumer racing session init faster than the retry (accepted per ADR-0009, not resolved by this probe — sampling was homogeneous, one account, ~2 minutes).
**Done when:** per the spec — `GetPositionsAsync`/`GetTradesAsync` retry once internally on a heuristically-sparse read per call (Positions: `name`/`ticker` missing on a non-empty list; Trades: empty result), capped at one attempt, tagged on the method's `Activity` span (`ibkr.cold_read_retry`) only when it fires; return types unchanged; composes correctly with existing 401 replay.
**TDD notes:** see the spec's 10-step TDD plan — WireMock call-count-sequenced fixtures from `recordings/coldread-rpd06/` for both endpoints (sparse→enriched, clean→no-retry, sparse→sparse-capped), plus the Activity-tag observability test and the mandatory 401-recovery composition test.

#### RPD-07 — 📦 Typed `endofbundle` on the ledger response
**Status:** ✅ Done — #306 · **Stream:** RPD · **Depends on:** none
**Risk:** standard
**Spec:** trivial-skip
`endofbundle` is absent from every source's *formal* ledger schema (DOC-01, DOC-03, DOC-05 all agree) but present in all three sources' own *worked examples* — consistently only on the `USD` currency block, consistently absent from `BASE` (and, in DOC-01's example, `AUD`). RTOS observed it missing on 4/4 live captures. (Findings doc P4.) The three-source agreement on the "one-currency-block-only" pattern is too consistent to be coincidental and points away from a simple "nullable per-entry field."
**Resolved 2026-07-14 by live probe** (5 calls, single session — `recordings/ledger-endofbundle-probe/`): observed 5/5 — `endofbundle` present (value `1`) on every `USD` entry, absent from every `BASE` entry. This account has only one real currency (USD), so "always USD" vs. "the account's settlement currency" vs. "the last real-currency entry serialized" remain indistinguishable without a multi-real-currency account to test against — but the core question is answered: it is a **per-response marker landing on one specific (real-currency) entry**, not a generic per-entry field, and RTOS's "absent 4/4" is explained as a per-entry schema validator flagging it missing on `BASE` (and any other unmarked entry), not evidence the field never appears. **Also found, independent of shape choice:** the current DTO models `EndOfBundle` as non-nullable `int`, so its absence on `BASE` silently deserializes to `0` — indistinguishable from a real `0` value. This is an ADR-0001 violation worth fixing regardless of which shape RPD-07 lands on.
**Grooming (2026-07-14):** operator decided on a nullable per-entry field over a response-level marker (see stream preamble) — the single-real-currency test account can't verify which entry a response-level promotion rule should hoist from, so committing to that shape now would guess past the evidence.
**Done when:** `LedgerEntry.EndOfBundle` is typed `int?` (or `bool?`, decided at implementation against whatever value range is actually observed — the probe only ever saw `1`), fixing today's silent-absent-defaults-to-`0` bug; present on the `USD`/real-currency entry, absent (null, not `0`) elsewhere, per the probe's 5/5 pattern.
**TDD notes:** WireMock fixtures from `recordings/ledger-endofbundle-probe/001-...json` through `005-...json` (all show the same USD-present/BASE-absent pattern — one fixture suffices, but assert on both entries in the same test to lock the per-entry distinction, not just presence-anywhere).

#### RPD-08 — 📦 Per-leg classify a bracket/OCA GROUP resolved through `ReplyAsync`
**Status:** Not started · **Stream:** RPD · **Depends on:** RPD-03
**Risk:** high — order placement/modification (delivery/classification semantics on the reply path).
**Spec:** pending
Follow-on surfaced by RPD-03's code review. RPD-03 gave `PlaceOrdersAsync`'s **direct** response a per-leg breakdown (`ClassifyGroupResponses`), but a group that returns `OrderConfirmationRequired` and is then resolved via `ReplyAsync` still routes the reply body through the **single-order** `ClassifyOrderResponses`, which reads `responses[0]` only. Per ADR-0008's probe, the child is structurally `responses[0]` in a group array — so if a group-confirmation reply resolves to a multi-leg array whose index-0 child carries a sentinel/terminal shape (`order_id=-1` / `status=Failed`), the reply path can surface that rejected leg as an `OrderSubmitted` while the live parent (`responses[1]`) is hidden — the exact defect RPD-03 exists to kill, still reachable on the reply endpoint. RPD-03 mitigated this **by documentation only** (the `ReplyAsync` / `PlaceOrdersAsync` XML docs now warn that a group confirmed via a reply returns a single collapsed outcome that must be reconciled per leg via `GetLiveOrdersAsync`/`GetTradesAsync`); this story removes the sharp edge by classifying the reply-for-group response per leg like the direct path.
**Blocked on evidence:** the reply-for-group wire shape is **not wire-pinned** — no recording or probe captures what `POST /iserver/reply/{id}` returns when the confirmation being resolved belongs to a multi-leg group (RPD-03's probes captured the *direct*-submission array shapes, not the reply resolution of a group question chain). Grooming must first verify this shape via an attended live-paper probe (a group whose confirmation resolves to a per-leg array) before the classifier change can be specced loop-ready — do not guess the reply-for-group shape from the direct-submission shape.
**Done when:** a bracket/OCA group whose confirmation is resolved through `ReplyAsync` returns the same per-leg outcome model `PlaceOrdersAsync`'s direct response does (or an explicitly-specced equivalent), so a rejected `responses[0]` child never surfaces as `OrderSubmitted` and the live parent is never hidden; the reply path keys on field signature, never array position; single-order reply classification is untouched; the RPD-03 documentation sharp-edge warning is updated to reflect the closed gap.
**TDD notes:** needs a wire-pinned reply-for-group fixture first (grooming probe). Then unit tests on the group-reply classifier against the captured shape, a regression guard for the `responses[0]`-sentinel-child case, and the mandatory 401-recovery test on the reply endpoint.
