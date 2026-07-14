# ADR-0009 — Positions/Trades cold-read: heuristic auto-retry-once

**Status:** Accepted · **Date:** 2026-07-13 — timing assumption verified 2026-07-14 by live probe (see Context)
**Relates to:** findings P3, NC2 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); design doc §10.6 (sibling decision), new §10.7; live probe `recordings/coldread-rpd06/` (2026-07-14). **Implemented by:** RPD-06 (`docs/backlog.md`, Stream RPD).

## Context

`GetLiveOrdersAsync`'s cold-read behavior (design doc §10.6) is addressed by a **wire-reported** signal — IBKR's own `snapshot: bool` field on the live-orders response, which the library surfaces via `LiveOrdersSnapshot.IsSnapshot`. RTOS's 2026-07-13 probe found the same family of behavior on two endpoints that carry **no such wire signal**:

- `GET /portfolio/{accountId}/positions/{pageId}` returned field-sparse rows (`name`/`ticker` missing) on the session's first read, enriched on a second read (findings doc P3).
- `GET /iserver/account/trades` returned an empty result on the first call of a session despite trades existing (findings doc NC2, explicitly scoped by RTOS as *not* a conduit issue — it primes around this on its own side today).

Doc scouting (findings doc, headline cross-cutting observation) found this same pattern independently documented for a third endpoint (Watchlist: *"The first request may only return the values C, conid, and name values. Subsequent requests will add additional contract information"* — DOC-03) but found **no server-reported freshness/completeness field for Positions or Trades** anywhere in DOC-01, DOC-03, or DOC-05. Unlike LiveOrders, there is no wire-level flag this design can surface to distinguish "definitively enriched" from "possibly cold" for these two endpoints.

**2026-07-14 live-probe verification** (`ibkr-live-probe`, 3 fresh-session repetitions against the paper account — `recordings/coldread-rpd06/`): both hypotheses observed 3/3. Every fresh session's first Positions read was missing `name`/`ticker` (21 keys vs. 46 on the enriched read); every fresh session's first Trades read was `[]` despite the account holding 2 same-day trades. An **immediate, no-artificial-delay** second call reprimed both endpoints in all 3 repetitions — confirming the timing assumption in Decision point 1 rather than leaving it assumed. Sampling was homogeneous (one account, one instrument set, ~2 minutes) — this proves the pattern reliably appears and reprimes under these conditions; it does not rule out the residual adverse-timing race noted in Consequences. The same probe run incidentally found that `Position.strike` changes JSON type between the sparse first read (a number) and the enriched second read (a string) for the same rows — a nuance for RPD-05 (`docs/backlog.md`), not this ADR, but evidence that the cold/enriched read distinction this ADR addresses reaches beyond field presence into field typing.

## Decision

1. `GetPositionsAsync` and `GetTradesAsync` **transparently retry once** when the first read of a session "looks sparse": for Positions, a returned row missing `name`/`ticker`; for Trades, an empty result on the first call after session (re-)initialization. The retry is immediate (no artificial delay) — verified sufficient 3/3 by live probe (see Context), not merely assumed by analogy to LiveOrders.
2. The retry is internal and does **not** change either method's public return type — no `IsSnapshot`-style wrapper is added for these two endpoints, in contrast to `GetLiveOrdersAsync`. The retry is capped at one attempt (never a loop), bounding worst-case latency and rate-limit budget cost.
3. An `Activity` tag/event records whether a retry occurred, for observability, without surfacing it on the DTO (per `.claude/rules/code-style.md`'s per-method `Activity` span convention).
4. This is accepted as a heuristic, not a verified contract: with no server signal, "looks sparse" cannot be distinguished from "is legitimately sparse" (a thin account with genuinely few populated fields, or a quiet trading day with zero real trades). A false-positive retry costs one extra API call and a small latency addition; it does not corrupt data or change the result the consumer ultimately sees.

## Alternatives considered

- **Client-tracked `IsSnapshot`-style wrapper** (mirror `LiveOrdersSnapshot`'s shape, computed from "is this the first call since session init" rather than a wire flag): keeps the public-surface pattern consistent with §10.6, but pushes the retry-or-trust decision onto every consumer, when the library is positioned to absorb exactly this class of quirk (`.claude/rules/architecture.md`: "The library handles IBKR API quirks... so consumers don't have to"). Rejected as the sole mechanism; may be revisited by a superseding ADR if the retry-once heuristic proves too unreliable in practice.
- **Document-only consumer obligation, no code change**: cheapest, but leaves the burden on every consumer despite the library's stated mission, and doesn't match how `GetLiveOrdersAsync` (§10.6) already treats the same class of problem as the library's responsibility. Rejected.
- **Automatic internal priming call after session init** (a throwaway warm-up read before the consumer's first real call): best potential UX, and the 2026-07-14 probe's confirmation that an immediate call reliably primes both endpoints means this alternative is now more plausible than when this ADR was first drafted — it could subsume the retry-once mechanism by moving the "wasted" call to session-init time instead of the first real read. Still rejected for now: it adds latency to every session init (even ones that never read Positions/Trades), where retry-once only costs latency on the calls that need it. May be revisited by a superseding ADR if session-init latency proves not to matter in practice.

## Consequences

- Not a 📦 public-surface change: `GetPositionsAsync`/`GetTradesAsync` return types are unchanged; only their internal read behavior gains a bounded retry. `feat:` vs `fix:` is decided at grooming from the spec.
- Reduces, does not eliminate, the chance a consumer observes a cold/sparse read — a consumer racing session init with its own first read faster than the retry can complete could still observe sparseness. This decision does not claim to make cold reads structurally impossible, only less likely, in contrast to §10.6's `IsSnapshot` which makes the sparse case *observable* even when it isn't prevented.
- Cost: every genuinely-cold-and-correctly-empty first read (a new account with zero positions, a session with zero trades that day) pays one wasted retry. Accepted as low-severity — latency and rate-limit budget only, bounded to one extra call.
- Follow-on: RPD-06's spec can lock the "looks sparse" predicate against the 2026-07-14 probe captures (Positions: `name`/`ticker` missing; Trades: empty array) rather than guessing — the immediate-retry timing question `.claude/rules/backlog-format.md`'s loop-ready-empirics requirement flagged is now verified (homogeneous 3/3), with the residual adverse-timing race noted above as the only remaining caveat.

## Relationships

Design doc §10.6 (sibling — same session-lifecycle guarantee family), new §10.7; findings doc P3/NC2 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); live probe `recordings/coldread-rpd06/` (2026-07-14); `.claude/rules/architecture.md` (the "library absorbs IBKR quirks" mission this leans on); implemented by RPD-06 (`docs/backlog.md`, Stream RPD).
