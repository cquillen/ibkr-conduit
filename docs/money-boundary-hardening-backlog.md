# Money-Boundary Hardening Backlog (Stream MBH)

Fix stream for the money-boundary defects found by the
[2026-07-04 RTOS venue-consumer review](findings/2026-07-04-rtos-venue-consumer-review.md)
(33 adversarially verified + 4 unverified findings). Design spec:
[2026-07-04-money-boundary-hardening-backlog-design.md](superpowers/specs/2026-07-04-money-boundary-hardening-backlog-design.md).
Task status is mirrored in [implementation-status.md](implementation-status.md) Milestone 9.

## Rules of the stream

- **Tier 1 (MBH-01…08) is the pre-soak set.** When its last PR merges, release-please cuts **0.9.0** and RTOS re-pins once. All public-surface and behavior-changing fixes live in Tier 1.
- **Tiers 2–3 are additive-only** (release as 0.9.x). A late-discovered breaking need moves the task to Tier 1 or waits for a planned 0.10.0.
- **One task = one branch = one PR.** TDD is mandatory; each task's starting red tests are the findings doc's suggested regression tests for its finding IDs.
- **The findings doc is immutable evidence** — reference finding IDs; never edit the review document from fix work.
- **Verify-first:** findings marked `*` are unverified — the task's first step is verify-or-refute; a refutation closes the item here with the counter-evidence recorded in the task section.
- **Public-surface decisions are pinned in plans before coding** (MBH-02 null semantics, MBH-04 ambiguous-error shape, MBH-05 return record), noting what RTOS's IBV-05 table must map.

## Status

| Task | Tier | Title | Findings | Max severity | Breaking | Status | PR |
|---|---|---|---|---|---|---|---|
| MBH-01 | 1 | No silent str/fill loss | FIL-1, FIL-2, GAP2-4 | critical | no (default change) | Not Started | — |
| MBH-02 | 1 | Presence-preserving money DTOs | WIR-1, WIR-3, FIL-6 | critical | yes | Not Started | — |
| MBH-03 | 1 | Truthful session/account status events | GAP2-1, GAP2-2, GAP2-3, GAP3-3 | medium | yes | Not Started | — |
| MBH-04 | 1 | Order-POST replay gate & classified refusals | AMB-2, AMB-3, AMB-4 | high | behavioral | Not Started | — |
| MBH-05 | 1 | Live-orders priming surface | GAP1-1, GAP1-2, GAP1-3 | high | yes | Not Started | — |
| MBH-06 | 1 | Session wedge family | SES-2, SES-3, SES-5, SES-6 | high | no | Not Started | — |
| MBH-07 | 1 | Competing-session truth | SES-1, GAP3-1, GAP3-2 | high | no | Not Started | — |
| MBH-08 | 1 | Health evidence wiring | SES-4 | high | no | Not Started | — |
| MBH-09 | 2 | Manager lifecycle integrity | MGR-1, MGR-2, MGR-3 | high | no | Not Started | — |
| MBH-10 | 2 | Streaming consumer robustness | FIL-3, FIL-4, FIL-5 | medium | no | Not Started | — |
| MBH-11 | 2 | Response-schema & wire hardening | WIR-4, WIR-5 | medium | no | Not Started | — |
| MBH-12 | 2 | Metrics & disposal hygiene | MGR-4, FIL-7\*, MGR-5\* | medium | no | Not Started | — |
| MBH-13 | 3 | Manager options validation | MGR-6\* | low | no | Not Started | — |
| MBH-14 | 3 | Order-type docs & futures compliance | WIR-6\* | low | no | Not Started | — |

**Tier 1 lanes** (parallel branches; serialize within a lane):
Lane A: MBH-01 · Lane B: MBH-02 → MBH-03 · Lane C: MBH-04 ∥ MBH-06 → MBH-07 · Lane D: MBH-05 ∥ MBH-08.

## Cross-cutting open questions

Recorded 2026-07-04 (post-design review); per-task questions live in each task's **Open questions** line and are resolved in that task's implementation plan.

- **0.9.0 migration notes are a Tier 1 exit criterion.** The release must ship consumer-facing notes covering the new ambiguous error type (MBH-04), the nullable money/status fields (MBH-02/03), and the live-orders return record (MBH-05) — RTOS updates its IBV-05 mapping against these *before* re-pinning.
- **MBH-07 needs RTOS input before its plan is written** (compete policy + success-path competing signal — see its Open questions). It is the one Tier 1 task whose design decision belongs to the consumer as much as the conduit.
- **AMB-2's empirical question stays open by design:** whether IBKR can process an order POST and then 401 is unpinned in either direction; RTOS's IBV-P first-call/behavior census measures it live. MBH-04's replay gate makes both answers non-catastrophic, so the stream does not block on it.

## Tier 1 — pre-soak (closes with the 0.9.0 cut)

### MBH-01 — No silent str/fill loss

- **Findings:** FIL-1 (critical), FIL-2 (high), GAP2-4 (medium)
- **Scope:** Per-subscriber channels move to the `Channel.CreateBounded(options, itemDropped)` overload emitting a Warning log and a new `ibkr.conduit.websocket.messages.dropped` counter (tenant + topic tags). `TradeExecutionMapper.MapMany` gains per-element try/catch so one malformed execution no longer discards the frame's tail. Dropped-frame logging records the wire topic (not the DTO type name) and increments a counter. `IbkrClientOptions.StreamingBufferSize` default raises 256 → 2048 (`SessionModelsTests` default pin updates with it).
- **Out of scope:** configurable `FullMode` (`Wait` for money topics) — future backpressure design.
- **Open questions:** the drop *counter* counts every eviction, but the Warning *log* must not flood under a stalled consumer (thousands of drops/sec) — decide the throttle shape in the plan (e.g. first drop per topic per connection, or rate-limited).
- **Files:** `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs`, `Streaming/Mappers/TradeExecutionMapper.cs`, `Streaming/FanOutChannelObservable.cs`, `Streaming/ChannelObservable.cs`, `Streaming/StreamingObservableLog.cs`, `Session/IbkrClientOptions.cs`
- **Red tests:** findings doc entries FIL-1, FIL-2, GAP2-4 (suggested regression tests per entry)

### MBH-02 — Presence-preserving money DTOs *(breaking)*

- **Findings:** WIR-1 (critical), WIR-3 (medium), FIL-6 (low)
- **Scope:** One rule across streaming and REST money DTOs: **absent ≠ default — nullable types; null means "not present in this payload."** `OrderUpdate` quantities/status/side/conid go nullable; `TradeExecution.Size` and the remaining non-nullable `Trade`/`TradeExecution` money fields likewise; empty-string→0 coercion on non-nullables disappears with the nullability. XML docs state the null semantics. The null-shape decision is recorded in this task's plan before coding.
- **Open questions:** (a) does *present-but-empty* (`""`) map to null (same as absent) or stay distinguishable from absence? (b) `Conid` going nullable may break consumers' dictionary keying — nullable, or a documented non-null invariant instead?
- **Files:** `src/IbkrConduit/Streaming/StreamingModels.cs`, `Orders/IIbkrOrderApiModels.cs`, `Serialization/*`
- **Red tests:** findings doc entries WIR-1, WIR-3, FIL-6

### MBH-03 — Truthful session/account status events *(breaking)*

- **Findings:** GAP2-1, GAP2-2, GAP2-3, GAP3-3 (mediums gating the `IsPaper` real-money interlock and session-death detection)
- **Scope:** `sts`/`act` mappers stop fabricating: `ValueKind` guards replace raw `GetBoolean()`; a frame missing `authenticated` maps to `Authenticated = null`, never synthesized `false`; `IsPaper` becomes nullable; `SessionStatusEvent` surfaces the sts fields currently dropped (competing, fail reason).
- **Open questions:** (a) a frame with missing/unparseable `authenticated`: *deliver* it with `Authenticated = null` (leaning this way — the consumer sees the frame happened) or suppress into the malformed path? Delivering changes what "no event" means. (b) Which additional sts fields to surface — source the list from `docs/ibkr-websocket-api-reference.md`.
- **Files:** `src/IbkrConduit/Streaming/Mappers/SessionStatusMapper.cs`, `Streaming/Mappers/AccountStatusMapper.cs`, `Streaming/StreamingModels.cs`
- **Depends on:** MBH-02 (shared `StreamingModels.cs`)
- **Red tests:** findings doc entries GAP2-1, GAP2-2, GAP2-3, GAP3-3

### MBH-04 — Order-POST replay gate & classified refusals *(behavioral break)*

- **Findings:** AMB-2 (high, PLAUSIBLE), AMB-3 (medium), AMB-4 (low)
- **Scope:** The trichotomy task. The 401 buffer-and-replay gets a method/endpoint gate: order-mutating POSTs (`/iserver/account/*/orders`, order modify, `/iserver/reply/*`) are not silently replayed; the call surfaces a **distinct ambiguous error shape** — design pin: a new `IbkrAmbiguousError` in the `Result` taxonomy carrying replay/phase context, exact shape recorded in this task's plan with the RTOS IBV-05 mapping (→ InDoubt). `ReplyAsync` routes 2xx through `ResultFactory.FromResponse`; the two unrecognized-200-shape throws become classified refusals carrying body context.
- **Open questions:** (a) error shape: new `IbkrAmbiguousError` type vs. a flag on an existing error; which context fields (phase: 401-after-send vs. never-answered; `ReplayAttempted`; original status/body). (b) Gate boundaries: does DELETE-cancel stay replayable (idempotent-ish)? Is `/iserver/reply` order-mutating for gating purposes (it mutates, but a lost reply has different repair semantics)? (c) Coordination: RTOS's IBV-05 table must map the new shape → InDoubt *before* it re-pins 0.9.0.
- **Files:** `src/IbkrConduit/Session/TokenRefreshHandler.cs`, `Client/OrderOperations.cs`, `Errors/Result.cs`, `Errors/ResultFactory.cs`
- **Red tests:** findings doc entries AMB-2, AMB-3, AMB-4

### MBH-05 — Live-orders priming surface *(breaking: return shape)*

- **Findings:** GAP1-1 (high), GAP1-2 (high), GAP1-3 (medium)
- **Scope:** Per the "conduit owns IBKR quirks" charter: `GetLiveOrdersAsync` auto-re-polls until `snapshot:true` (bounded retries, loud failure) **and** returns a richer record exposing `IsSnapshot` (shape recorded in this task's plan); after any filtered call the documented `force=true` cache-clear is issued automatically (or lazily at `sor` subscribe); the misleading unprimed-shape empty-orders fixture is replaced.
- **Open questions:** (a) the record shape (`Result<LiveOrdersResult>` — name and members). (b) The re-poll bound: attempts/delay, and whether it's an `IbkrClientOptions` knob or a constant. (c) What "fails loud" is when priming never succeeds — dedicated error vs. timeout shape. (d) Eager `force=true` immediately after a filtered call vs. lazy at `sor` subscribe.
- **Files:** `src/IbkrConduit/Client/OrderOperations.cs`, `Client/IOrderOperations.cs`, `Orders/IIbkrOrderApiModels.cs`, `tests/IbkrConduit.Tests.Integration` fixtures
- **Red tests:** findings doc entries GAP1-1, GAP1-2, GAP1-3

### MBH-06 — Session wedge family

- **Findings:** SES-2 (high), SES-3 (high), SES-5 (medium), SES-6 (medium)
- **Scope:** Tickle 401s become a reauth trigger rather than generic transport noise; the cached LST is expiry-checked before use; proactive refresh retries instead of dying one-shot; failed init/reauth resets `_state` and stops leaking tickle timers.
- **Open questions:** (a) retry/backoff parameters for the proactive-refresh retry. (b) Does the LST expiry check reuse `ProactiveRefreshMargin` or get its own margin? (c) Tickle-401 reauth must route through the epoch-deduped `ReauthenticateAsync` path (no parallel reauth mechanism) — confirm in the plan.
- **Files:** `src/IbkrConduit/Session/TickleTimer.cs`, `Session/SessionManager.cs`, `Auth/SessionTokenProvider.cs`
- **Red tests:** findings doc entries SES-2, SES-3, SES-5, SES-6

### MBH-07 — Competing-session truth

- **Findings:** SES-1 (high), GAP3-1 (high), GAP3-2 (medium)
- **Scope:** `SsodhInitResponse` is captured instead of discarded; competing evidence flows into `IbkrSessionError.IsCompeting` (the currently-dead branch RTOS maps to SessionLost); health state stops force-clearing `competing:false` after every reauth.
- **Open questions — needs RTOS input before the plan (defines their SessionLost trigger):** plumbing `IsCompeting` is necessary but *insufficient* — the GAP3-1 skeptic showed a competing steal makes reauth **succeed** (steal-back with `Compete=true` default), so the `IbkrSessionError` path never fires in exactly the steal scenario. Decide: (a) default compete policy — unconditional steal-back vs. a `CompetePolicy` option (steal-back / stand-down / bounded steal-back with backoff); two processes on the same credentials currently ping-pong indefinitely. (b) The success-path signal — how competing evidence reaches the consumer when reauth succeeds: likely a `SessionLifecycleNotifier` competing event, since neither the error taxonomy nor health polling reliably catches the sub-tickle-interval window.
- **Files:** `src/IbkrConduit/Session/SessionManager.cs`, `Session/TokenRefreshHandler.cs`, `Health/SessionHealthState.cs`
- **Depends on:** MBH-04 and MBH-06 (shared `TokenRefreshHandler.cs` / `SessionManager.cs`)
- **Red tests:** findings doc entries SES-1, GAP3-1, GAP3-2

### MBH-08 — Health evidence wiring

- **Findings:** SES-4 (high)
- **Scope:** Tickle traffic feeds `LastSuccessfulCall`/health evidence (session-pipeline wiring), so passive health cannot report a dead session as Authenticated indefinitely.
- **Files:** `src/IbkrConduit/Http/SessionServiceRegistration.cs`, `src/IbkrConduit/Health/*`
- **Red tests:** findings doc entry SES-4

## Tier 2 — soak-parallel (additive, 0.9.x)

### MBH-09 — Manager lifecycle integrity

- **Findings:** MGR-1 (high), MGR-2 (medium), MGR-3 (medium)
- **Scope:** `RemoveAsync` honors its `CancellationToken` with bounded teardown; `AddAsync` disposes credentials on every throw path (the ownership contract RTOS's provisioning saga relies on); `_disposed` races with concurrent `AddAsync` closed.
- **Escalation flag:** if the soak's deliberate session-competition recovery test is blocked by unbounded `RemoveAsync`, pull this task into the pre-soak window.
- **Open questions:** (a) bounded-teardown semantics — what happens to in-flight requests when `RemoveAsync` is cancelled? (b) Does a cancelled `RemoveAsync` still attempt best-effort logout (ties to the Milestone 8 deferred "best-effort logout on eager-init failure" item)?
- **Files:** `src/IbkrConduit/Client/IbkrClientManager.cs`, `Client/TenantBuilder.cs`, `Client/ManagedTenant.cs`
- **Red tests:** findings doc entries MGR-1, MGR-2, MGR-3

### MBH-10 — Streaming consumer robustness

- **Findings:** FIL-3 (medium), FIL-4 (medium), FIL-5 (medium)
- **Scope:** Consumer `OnNext` exceptions distinguished from malformed wire frames; reconnects emit an observable gap signal so consumers know to reconcile via REST; multi-`Subscribe` on a single-reader channel either works or throws loudly.
- **Open questions:** (a) the gap-signal surface — event/callback on the subscription vs. a marker item in the stream. (b) FIL-5's fix may not be additive: throw-on-second-`Subscribe` is a behavior change and per-subscriber channels change the surface — if it can't be done additively, it moves to Tier 1 per the stream rules.
- **Files:** `src/IbkrConduit/Streaming/FanOutChannelObservable.cs`, `Streaming/ChannelObservable.cs`, `Streaming/IbkrWebSocketClient.cs`
- **Red tests:** findings doc entries FIL-3, FIL-4, FIL-5

### MBH-11 — Response-schema & wire hardening

- **Findings:** WIR-4 (medium, PLAUSIBLE), WIR-5 (medium)
- **Scope:** `OrderSubmissionResponse.OrderId` gets `FlexibleStringJsonConverter`; the schema-validation safety net loses its blind spots (element[0]-only collection validation and the other gaps WIR-5 documents).
- **Files:** `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`, `Http/ResponseSchemaValidationHandler.cs`
- **Red tests:** findings doc entries WIR-4, WIR-5

### MBH-12 — Metrics & disposal hygiene *(verify-first)*

- **Findings:** MGR-4 (medium, verified) + FIL-7\*, MGR-5\* (unverified)
- **Scope:** Verify-or-refute FIL-7 (connection-state gauge lifetime) and MGR-5 (rate-limiter/timer disposal on tenant remove); then fix the confirmed set together with MGR-4 (gauge re-registration on the process-wide static Meter).
- **Files:** `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs`, `Http/RateLimitingAndResilienceRegistration.cs`, `Streaming/IbkrWebSocketClient.cs`, `Client/ManagedTenant.cs`
- **Red tests:** findings doc entries MGR-4, FIL-7, MGR-5

## Tier 3 — deferred

### MBH-13 — Manager options validation *(verify-first)*

- **Findings:** MGR-6\* (unverified; the recorded Milestone 8 deferred follow-up)
- **Scope:** Verify, then run `ValidateOptions` on the manager path (effective cloned+overridden options) with the same fail-fast shapes as `AddIbkrClient`, before sentinel-holding network work.
- **Files:** `src/IbkrConduit/Client/IbkrClientManager.cs`, `Http/ServiceCollectionExtensions.cs`, `Client/TenantBuilder.cs`
- **Red tests:** findings doc entry MGR-6

### MBH-14 — Order-type docs & futures compliance *(verify-first)*

- **Findings:** WIR-6\* (unverified)
- **Scope:** Verify against the captured spec; correct the `OrderRequest.OrderType` doc enum with a doc-locked constants test; decide and document `ExtOperator` for futures placement.
- **Files:** `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`, `docs/ibkr-web-api-spec.md` (reference only)
- **Red tests:** findings doc entry WIR-6
