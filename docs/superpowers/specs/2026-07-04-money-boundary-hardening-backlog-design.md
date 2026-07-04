# Money-Boundary Hardening Backlog (Stream MBH) — Design

**Date:** 2026-07-04 · **Status:** Approved (brainstorm session with operator)
**Source evidence:** [2026-07-04-rtos-venue-consumer-review.md](../../findings/2026-07-04-rtos-venue-consumer-review.md) (PR #229 — merge before the backlog doc lands, so its links resolve)
**Consumer context:** RTOS `IbkrVenue` (sibling repo `realtest-order-steward`, ADR-0028 + Stream IBV) builds directly on this library and starts a live-paper soak imminently; fill-side validation begins at Monday market open.

## Goal

Turn the 37 findings of the RTOS venue-consumer review (33 adversarially verified, 4 unverified) into an executable, tiered backlog — **Stream MBH** — such that everything money-critical lands before the RTOS soak, breaking changes ship as one coordinated release, and no finding is silently dropped.

## Decisions (operator-confirmed)

1. **Scope:** every finding gets a backlog home — verified and unverified alike. Nothing is tracked only in the findings doc.
2. **Venue:** a committed backlog document, `docs/money-boundary-hardening-backlog.md`, plus a **Milestone 9 — Money-Boundary Hardening** table in `docs/implementation-status.md` ticked as PRs merge. No GitHub-issue tracking.
3. **Granularity:** tasks are **root-cause clusters** (1–4 findings sharing files, cause, and tests). Each task = one branch + one PR, per the repo workflow. 14 tasks total.
4. **Releases:** all public-surface and behavior-changing fixes are front-loaded into Tier 1; when Tier 1 completes, release-please cuts a single **0.9.0** and RTOS re-pins once. Tiers 2–3 are additive-only (0.9.x); any late-discovered break moves to Tier 1 or waits for a planned 0.10.0.
5. **Buffer default:** `IbkrClientOptions.StreamingBufferSize` default raises **256 → 2048** as part of MBH-01 (operator: 256 is too tight). Frames-per-subscriber bound; single-digit-MB worst case per subscriber at a few KB/frame; `DropOldest` + the new drop counter remain the backstop. Behavior tweak (`feat`), not a break; the `SessionModelsTests` default pin updates with it.

## Tier structure

The tier boundary is the release boundary.

| Tier | Meaning | Release |
|---|---|---|
| 1 — pre-soak | Criticals + trichotomy/session-liveness highs; all breaking changes | Closes with the 0.9.0 cut |
| 2 — soak-parallel | Hardening/observability; additive only | 0.9.x as they merge |
| 3 — deferred | Lows, docs, known-territory items | Whenever |

## Tier 1 tasks (MBH-01…08 → 0.9.0)

### MBH-01 — No silent str/fill loss *(additive + default change)*
**Findings:** FIL-1 (critical), FIL-2 (high), GAP2-4 (medium).
Channel creation moves to the `Channel.CreateBounded(options, itemDropped)` overload emitting a Warning log and a new `ibkr.conduit.websocket.messages.dropped` counter tagged tenant + topic; `TradeExecutionMapper.MapMany` gains per-element try/catch so one malformed execution no longer discards the frame's tail; dropped-frame logging records the wire topic (not the DTO type name) and increments a counter; `StreamingBufferSize` default 256 → 2048.
**Out of scope:** making `FullMode` configurable (`Wait` for money topics) — a backpressure design of its own; drop-observability converts silent loss into a reconcilable event, which is the pre-soak requirement.
**Files:** `Streaming/IbkrWebSocketClient.cs`, `Streaming/Mappers/TradeExecutionMapper.cs`, `Streaming/FanOutChannelObservable.cs`, `Streaming/ChannelObservable.cs`, `Streaming/StreamingObservableLog.cs`, `Session/IbkrClientOptions.cs`.

### MBH-02 — Presence-preserving money DTOs *(breaking)*
**Findings:** WIR-1 (critical), WIR-3 (medium), FIL-6 (low).
One rule applied across streaming and REST money DTOs: **absent ≠ default — nullable types, null means "not present in this payload."** `OrderUpdate` quantities/status/side/conid go nullable; `TradeExecution.Size` and remaining non-nullable `Trade`/`TradeExecution` money fields likewise; empty-string→0 coercion on non-nullables disappears with the nullability. XML docs state the null semantics.
**Files:** `Streaming/StreamingModels.cs`, `Orders/IIbkrOrderApiModels.cs`, `Serialization/*`.

### MBH-03 — Truthful session/account status events *(breaking)*
**Findings:** GAP2-1, GAP2-2, GAP2-3, GAP3-3 (mediums gating the `IsPaper` real-money interlock and session-death detection).
`sts`/`act` mappers stop fabricating: `ValueKind` guards replace raw `GetBoolean()`; a frame missing `authenticated` maps to `Authenticated = null`, never synthesized `false`; `IsPaper` becomes nullable; `SessionStatusEvent` surfaces the sts fields currently dropped (competing, fail reason).
**Files:** `Streaming/Mappers/SessionStatusMapper.cs`, `Streaming/Mappers/AccountStatusMapper.cs`, `Streaming/StreamingModels.cs`. Sequenced after MBH-02 (shared `StreamingModels.cs`).

### MBH-04 — Order-POST replay gate & classified refusals *(behavioral break)*
**Findings:** AMB-2 (high, PLAUSIBLE), AMB-3 (medium), AMB-4 (low).
The trichotomy task. The 401 buffer-and-replay gets a method/endpoint gate: order-mutating POSTs (`/iserver/account/*/orders`, order modify, `/iserver/reply/*`) are not silently replayed; the call surfaces a **distinct ambiguous error shape**. *Design pin:* a new `IbkrAmbiguousError` in the `Result` taxonomy carrying replay/phase context — exact shape finalized in this task's plan, noting the mapping RTOS's IBV-05 table needs (→ InDoubt). `ReplyAsync` routes 2xx through `ResultFactory.FromResponse` so hidden errors are caught; the two unrecognized-200-shape throws become classified refusals carrying body context.
**Files:** `Session/TokenRefreshHandler.cs`, `Client/OrderOperations.cs`, `Errors/Result.cs`, `Errors/ResultFactory.cs`.

### MBH-05 — Live-orders priming surface *(breaking: return shape)*
**Findings:** GAP1-1 (high), GAP1-2 (high), GAP1-3 (medium).
Per the "conduit owns IBKR quirks" charter: `GetLiveOrdersAsync` auto-re-polls until `snapshot:true` (bounded retries, loud failure) **and** returns a richer record exposing `IsSnapshot`; after any filtered call the documented `force=true` cache-clear is issued automatically (or lazily at `sor` subscribe); the misleading unprimed-shape empty-orders fixture is replaced.
**Files:** `Client/OrderOperations.cs`, `Client/IOrderOperations.cs`, `Orders/IIbkrOrderApiModels.cs`, integration fixtures.

### MBH-06 — Session wedge family *(behavioral, non-breaking surface)*
**Findings:** SES-2 (high), SES-3 (high), SES-5 (medium), SES-6 (medium).
Tickle 401s become a reauth trigger rather than generic transport noise; the cached LST is expiry-checked before use; proactive refresh retries instead of dying one-shot; failed init/reauth resets `_state` and stops leaking tickle timers.
**Files:** `Session/TickleTimer.cs`, `Session/SessionManager.cs`, `Auth/SessionTokenProvider.cs`.

### MBH-07 — Competing-session truth *(depends on MBH-04 and MBH-06)*
**Findings:** SES-1 (high), GAP3-1 (high), GAP3-2 (medium).
`SsodhInitResponse` is captured instead of discarded; competing evidence flows into `IbkrSessionError.IsCompeting` (the currently-dead branch RTOS maps to SessionLost); health state stops force-clearing `competing:false` after every reauth. Sequenced last in its lane: builds on MBH-06's SessionManager changes and MBH-04's TokenRefreshHandler changes.
**Files:** `Session/SessionManager.cs`, `Session/TokenRefreshHandler.cs`, `Health/SessionHealthState.cs`.

### MBH-08 — Health evidence wiring *(additive)*
**Findings:** SES-4 (high).
Tickle traffic feeds `LastSuccessfulCall`/health evidence (session-pipeline wiring), so passive health cannot report a dead session as Authenticated indefinitely.
**Files:** `Http/SessionServiceRegistration.cs`, `Health/*`.

**Tier 1 lanes** (parallel branches; within a lane, serialize):
- Lane A: MBH-01
- Lane B: MBH-02 → MBH-03
- Lane C: MBH-04 ∥ MBH-06 → MBH-07
- Lane D: MBH-05 ∥ MBH-08

## Tier 2 tasks (MBH-09…12, additive, 0.9.x)

### MBH-09 — Manager lifecycle integrity
**Findings:** MGR-1 (high), MGR-2 (medium), MGR-3 (medium).
`RemoveAsync` honors its `CancellationToken` with bounded teardown; `AddAsync` disposes credentials on every throw path (the ownership contract RTOS's provisioning saga relies on); `_disposed` races with concurrent `AddAsync` closed.
**Escalation flag:** if the soak's deliberate session-competition recovery test is blocked by unbounded `RemoveAsync`, pull this task into the pre-soak window.

### MBH-10 — Streaming consumer robustness
**Findings:** FIL-3 (medium), FIL-4 (medium), FIL-5 (medium).
Consumer `OnNext` exceptions distinguished from malformed wire frames; reconnects emit an observable gap signal so consumers know to reconcile via REST; multi-`Subscribe` on a single-reader channel either works or throws loudly.

### MBH-11 — Response-schema & wire hardening
**Findings:** WIR-4 (medium, PLAUSIBLE), WIR-5 (medium).
`OrderSubmissionResponse.OrderId` gets `FlexibleStringJsonConverter`; the schema-validation safety net loses its blind spots (element[0]-only collection validation and the other two gaps WIR-5 documents).

### MBH-12 — Metrics & disposal hygiene *(verify-first)*
**Findings:** MGR-4 (medium, verified) + FIL-7, MGR-5 (unverified).
First verify-or-refute FIL-7 (connection-state gauge lifetime) and MGR-5 (rate-limiter/timer disposal on tenant remove); then fix the confirmed set together with MGR-4 (gauge re-registration on the process-wide static Meter).

## Tier 3 tasks (MBH-13…14, deferred)

### MBH-13 — Manager options validation *(verify-first)*
**Findings:** MGR-6 (unverified; the recorded Milestone 8 deferred follow-up).
Verify, then run `ValidateOptions` on the manager path (effective cloned+overridden options) with the same fail-fast shapes as `AddIbkrClient`, before sentinel-holding network work.

### MBH-14 — Order-type docs & futures compliance *(verify-first)*
**Findings:** WIR-6 (unverified).
Verify against the captured spec; correct the `OrderRequest.OrderType` doc enum with a doc-locked constants test; decide and document `ExtOperator` for futures placement.

## Process rules

1. **One task = one branch = one PR.** TDD is mandatory; each task's starting red tests are the findings doc's suggested regression tests for its findings.
2. **Findings doc is immutable evidence.** Tasks reference finding IDs; the review document is never edited by fix work.
3. **Public-surface decisions are pinned in plans.** MBH-02 (null semantics), MBH-04 (ambiguous error shape), MBH-05 (return record) each record the decision in their implementation plan (`docs/superpowers/plans/`) before coding, noting what RTOS's IBV-05 mapping consumes.
4. **Verify-first protocol.** Unverified findings (FIL-7, MGR-5, MGR-6, WIR-6) start with verify-or-refute; a refutation closes the item in the backlog doc with the counter-evidence recorded.
5. **Status flows in every task PR:** tick the backlog doc's status table and the Milestone 9 row in `implementation-status.md`.
6. **Release gate:** release-please cuts 0.9.0 when MBH-01…08 are merged; breaking commits use conventional-commit `!` markers. Tier 2/3 changes must be additive.

## Finding-to-task map (completeness check)

| Task | Findings |
|---|---|
| MBH-01 | FIL-1, FIL-2, GAP2-4 |
| MBH-02 | WIR-1, WIR-3, FIL-6 |
| MBH-03 | GAP2-1, GAP2-2, GAP2-3, GAP3-3 |
| MBH-04 | AMB-2, AMB-3, AMB-4 |
| MBH-05 | GAP1-1, GAP1-2, GAP1-3 |
| MBH-06 | SES-2, SES-3, SES-5, SES-6 |
| MBH-07 | SES-1, GAP3-1, GAP3-2 |
| MBH-08 | SES-4 |
| MBH-09 | MGR-1, MGR-2, MGR-3 |
| MBH-10 | FIL-3, FIL-4, FIL-5 |
| MBH-11 | WIR-4, WIR-5 |
| MBH-12 | MGR-4, FIL-7*, MGR-5* |
| MBH-13 | MGR-6* |
| MBH-14 | WIR-6* |

\* unverified — verify-first. All 33 verified + 4 unverified findings are assigned; none tracked elsewhere.

## Out of scope

- `FullMode=Wait`/backpressure configurability for money topics (noted in MBH-01; future enhancement).
- The review's "not reviewed" areas (Flex path, watchlists, alerts, FYI, market data, portfolio endpoints) — no fix tasks without findings.
- RTOS-side mitigations (merge-cache heuristics, prime-then-read helpers) — this stream is conduit-side only.

## Deliverables of this design

1. `docs/money-boundary-hardening-backlog.md` — the Stream MBH backlog document (status table + task subsections above).
2. `docs/implementation-status.md` — new Milestone 9 table (one row per MBH task).
3. Both land in one docs PR after findings PR #229 merges.
