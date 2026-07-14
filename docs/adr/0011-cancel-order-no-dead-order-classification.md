# ADR-0011 — Cancel-order outcome: no typed dead-order classification

**Status:** Accepted · **Date:** 2026-07-13
**Relates to:** findings P6 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); design doc §9.9 (sibling — same error-classification guarantee family), new §9.13.

## Context

RTOS's dossier flagged, as a lower-priority "consider" item, that cancelling an already-`Inactive` order and, separately, cancelling an order that no longer exists both surface as generic `IbkrApiError`s the consumer must pattern-match by message text (`"Order is inactive"` vs. `"OrderID ... doesn't exist"`), and asked whether a typed/classified cancel outcome could let a consumer treat "nothing to cancel" as the benign case it is.

Doc scouting found all three sources (DOC-01, DOC-03, DOC-05) agree the CP Web API's cancel endpoint (`DELETE /iserver/account/{accountId}/order/{orderId}`) gives conduit no structured signal to classify on: one generic `{"error": "<string>"}` shape covers every cancel failure, with no distinct schema, enum, or status code separating "already inactive" from "already cancelled" from "never existed." DOC-03 explicitly contrasts this against Place Order's dedicated status-code error table, which Cancel Order has no equivalent of. Separately, DOC-01's schema documents that even a cancel **success** acknowledgment *"does not report whether the cancellation can or will ultimately be enacted"* — meaning "nothing to cancel" and "cancel probably worked" are not cleanly separable from this endpoint's response at all, success or failure.

## Decision

IbkrConduit does **not** build a typed/classified cancel-outcome distinguishing "already inactive" / "already cancelled" / "never existed." These cases continue to surface as `IbkrApiError` (today's generic 400-class error path), carrying IBKR's raw message text unchanged. A typed classification here can only be built by pattern-matching that message text — the CP Web API supplies no other signal — which is the same message-text-sniffing anti-pattern ADR-0008 moves the order-submission path *away* from; building it here would reintroduce that fragility deliberately, in a place where the platform itself offers no better foundation to build on.

## Alternatives considered

- **Message-text-pattern-matched typed error** (parse known phrases like "doesn't exist" / "is inactive" into a distinct `CancelTargetNotFoundError`/`CancelTargetInactiveError`-style split): rejected. IBKR documents no stable message vocabulary for this endpoint — each source gives only a single illustrative example, not an enumerated list — so the classification would be built on undocumented, unstable text; a future IBKR wording change silently breaks it with no way for conduit to detect the break. The safety upside (letting a consumer treat "nothing to cancel" as benign) is real but doesn't justify a contract built on text matching alone.
- **Do it anyway, document the fragility**: keeps the option available to RTOS but embeds an admittedly-fragile contract into the public surface, where this library's zero-warnings/high-bar posture (`.claude/rules/build-quality.md`) and its money-boundary review history argue against shipping a guarantee already known to be unreliable. Rejected.
- **Wait for a future doc/wire signal, revisit then**: adopted implicitly — this decision is revisitable by a superseding ADR if a future doc scout or live probe finds a structured signal (a status code, an error code field) this decision does not currently have evidence of.

## Consequences

- No code change for the cancel-classification question itself; `CancelOrderResponse.Account` (RPD-01) and its documented sentinel pairing with `Conid` still ship independently — this ADR only closes the "should we also classify cancel failures" question RTOS raised as a "consider."
- Consumers wanting "nothing to cancel" treated as benign must do their own message-text handling if they choose to, with the same fragility this ADR declines to build into the library — a documented known limitation, not a silent absence.
- No semver consequence — declining to add a surface, not removing one.

## Relationships

Design doc §9.9 (sibling), new §9.13; findings doc P6 (`docs/findings/2026-07-13-rtos-probe-dossier-doc-scouting.md`); ADR-0008 (the message-text-classification anti-pattern this decision explicitly declines to reintroduce).
