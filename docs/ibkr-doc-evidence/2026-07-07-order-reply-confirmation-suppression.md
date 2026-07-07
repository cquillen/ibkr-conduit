# Order reply, confirmation invalidation & suppression — IBKR doc evidence

**Question:** What do IBKR's live docs claim about `POST /iserver/reply/{replyId}` (response shapes, reply timing/expiry/invalidation), the suppress endpoints, and the effect of other submissions on a pending confirmation? (Re-grooms ADR-0006's claim-side citation — previously the deprecated mirror ~4559 — plus PVR-06/PVR-14's doc-claims.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-03 (retrieved 2026-07-07, `<section id="place-order-reply">` "Place Order Reply Confirmation"; suppress: `#questions-suppress`, `#suppressible-id`, `#reset-questions-suppress`)

**The invalidation claim — the live counterpart of the mirror's note, exact:**

> "Orders must be replied to immediately after receiving the reply message. Submitting other orders or other requests will cancel the order and attempts to acknowledge the reply will result in a 503 error."

(The page's sole functional "503 error" occurrence.) No "expire"/"invalidate"/"will not be resubmitted" language anywhere in the reply context (all page-wide `expir`/`invalidat`/`resubmit` hits checked — unrelated). No documented body/schema for that 503. Reply request `{"confirmed": bool}` ("true will agree… false will decline the message and discard the order"); success response the `[{order_id, order_status, encrypt_message}]` array; chained-confirmation note: "you may receive additional reply messages. These confirmation messages must also be responded to before the order will submit." Suppress: body `messageIds` (string array, "supports up to 51 messages… Any additional values will result in a system error"), only IDs in the Suppressible MessageIds table (`o163, o354, o382, o383, o403, o451, o2136, o2137, o2165, o10082, o10138, o10151, o10152, o10153, o10164, o10223, o10288, o10331–o10336, p6, p12`); success `{"status": "submitted"}`; reset endpoint same shape, no body. No documented behavior for empty/out-of-list ids. (Separate mechanism noted, not conflated: `POST /iserver/notification` answers `ntf` websocket prompts — distinct from the REST reply flow.)

### DOC-01 (retrieved 2026-07-07, `paths./iserver/reply/{replyId}.post` + suppress paths; live `info.version` 2.35.0, matches registry)

Reply request: `confirmed` boolean ("Value of true answers the question in the affirmative and proceeds with order submission."). **The 200 response is a documented `oneOf` five schemas** — the concrete shape set PVR-06's ORD-1 net must classify:

- `orderSubmitSuccess` — array of `{order_id, order_status, encrypt_message}`;
- `orderReplyMessage` — a **further question** `{id, isSuppressed, message[], messageIds[]}` (`isSuppressed`: "Internal use. Always delivers value 'false'.");
- `orderSubmitError` — `{"error": "..."}`, "order reply message or submission was not accepted" (example `{"error": "Order not confirmed "}`);
- `orderReplyNotFound` — `{"error": "reply id not found: '…'"}`;
- `advancedOrderReject` — `{orderId, reqId, dismissable, text, options[], type, messageId, prompt}`.

**No reply timing/expiry/invalidation semantics anywhere** (searched `expir`/`invalidat`/`timeout`/`stale`/`promptly` — all hits unrelated). Suppress: request `messageIds` with a 25-value enum; response `{"status": …}` — schema description says "Always returns \"Submitted\"" while the example shows lowercase `{"status": "submitted"}` (internal casing contradiction, reported as-is); reset endpoint documented identically. No documented invalid/empty-id behavior; non-200s are only the shared 401/500/503 refs.

### DOC-05 (retrieved 2026-07-07, h3 "Order Reply Messages" `#order-reply-messages-27`, "Order Reply Suppression" `#order-reply-suppression-28`)

Prose walkthrough of the reply flow (confirm-`true` only — no reject shape); names the path parameter `{messageId}` where DOC-01/DOC-03 use `{replyId}` (naming drift only). **Zero coverage of timing/expiry/invalidation** or of pending-confirmation vs other submissions ("Order Rejections" and "Previewing Orders" h3s are "Documentation coming soon" stubs). Suppress: matches the others — `{"status": "submitted"}`, session-scoped, "You do not need to have received a given messageID value previously in order to suppress it", "please resend the complete array" to add more, reset restores delivery.

## Wire observations

- Reply on an invalidated confirmation (paper account, 2026-07-07, `recordings/order-probe-2026-07-07.log`, 1 sequence): two same-type confirmations pending (both `o354`); reply to the first → **`503 {"error":"Service Unavailable","statusCode":503}`** (fully generic — no invalidation marker), and the "cancelled" order **later became a live Submitted order** (released by confirming the other same-type question). Question issuance observed non-deterministic (identical order: no question one run, `o354` the next).
- Suppress success shape (paper account, committed live-capture fixture `tests/.../Fixtures/Session/POST-suppress.json`, 1 sample): `{"status": "submitted"}` — lowercase, matching every source's example (and contradicting DOC-01's "Always returns \"Submitted\"" description text). ApiCapture edge entries additionally pin 500-on-empty-ids and 200-on-invalid-id — both undocumented in all sources.

## Reconciliation

- **Agreed (all three sources):** the reply flow (confirm boolean → success array, possible chained questions) and the suppress flow (`messageIds` → `{"status": "submitted"}`, session-scoped, reset endpoint).
- **Conflicts — resolved by the wire:** DOC-03 claims "Submitting other orders or other requests **will cancel the order**"; the probe showed the order **not** cancelled — it went live after its reply 503'd. The 503 half of the claim is wire-confirmed; the cancellation half is **wire-falsified** (1 sequence). This is precisely why ADR-0006 classifies a failed reply on an invalidated confirmation as an **ambiguous outcome** (reconcile before resubmitting), never a definitive refusal: IBKR's own documentation makes a cancellation promise the wire breaks, and acting on the documented semantics double-places.
- **Conflicts — unresolved, cosmetic:** DOC-01's `{"status"}` description ("Submitted") vs every example + the wire (lowercase "submitted") — the library compares case-insensitively or against the wire form; `{replyId}` (DOC-01/DOC-03) vs `{messageId}` (DOC-05) path-param naming.
- **Gaps:** no source documents the 503's body (the generic shape is wire-only knowledge), reply expiry timing, empty/invalid suppress-id behavior (wire-only: 500/200), or the reject (`confirmed:false`) response shape (DOC-01's oneOf presumably covers it; DOC-05 omits it).
- **Presence claims:**
  - Reply-immediately obligation + 503-on-stale-reply: **documented (DOC-03 only) + 503 observed (1 sequence)**.
  - "Other submissions cancel the pending order": **documented (DOC-03), wire-contradicted (1 sequence)** — treat the documented cancellation as unreliable; outcome is ambiguous.
  - The five reply 200-shapes: **documented (DOC-01)**; `orderSubmitSuccess` + chained `orderReplyMessage` also wire-observed (probe + repo recordings); `orderSubmitError`/`orderReplyNotFound`/`advancedOrderReject` have no wire sample — fixtures come from DOC-01's examples.
  - Suppress `{"status": "submitted"}`: **documented + observed (1 sample, committed fixture)**.

**Answer for the consuming decisions (ADR-0006 §9.10, PVR-06, PVR-14):** the revised ADR-0006 stands and is now *better* supported — the serialized-round + ambiguous-classification design is the only one safe under both the documented and the observed behavior. ADR-0006's claim-side citation migrates from the mirror (~4559) to DOC-03's live sentence above. PVR-06's ORD-1 test plan gains DOC-01's five documented 200-shapes as fixture sources. PVR-14's "verify suppress result against the pinned `submitted`" stands (wire + all examples agree; compare against the lowercase wire form).
