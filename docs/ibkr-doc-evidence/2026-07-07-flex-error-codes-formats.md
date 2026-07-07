# Flex Web Service error codes & statement formats — IBKR doc evidence

**Question:** What do IBKR's live docs claim about the Flex error-code table (esp. 1012/1013/1015 and transient-vs-permanent), the `/SendRequest`/`/GetStatement` response shapes, statement content formats (money/timestamp), and polling guidance? (Re-grooms PVR-09/PVR-10's claim side, design doc §11.10.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-07, DOC-03 (the only registered Flex sources; DOC-01's Flex-adjacent endpoints are a different surface)

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-07 (retrieved 2026-07-07, anchors `#error-codes`, `#response-sendrequest-success/-failure`, `#retrieve-report`, `#request-getstatement`; page `dateModified` 2026-05-07)

**Error codes** — 20-row table, "returnable by the /SendRequest and /GetStatement endpoints when a server-side failure occurs": 1001, 1003–1021 (**1002 absent** — a numbering gap in IBKR's own table). Key rows, exact: **1012 "Token has expired."** · **1013 "IP restriction."** · **1015 "Token is invalid."** · 1019 "Statement generation in progress. Please try again shortly." · 1018 "Too many requests… Limited to one request per second, 10 requests per minute (per token)." **No transient/retryable classification exists in the table** — the "Please try again shortly" phrasing (1001, 1004–1009, 1018, 1019, 1021) vs its absence (1003, 1010–1017, 1020) is the only signal, an inference, not a documented claim.

**Response shapes** — `/SendRequest` success `<FlexStatementResponse timestamp="…"><Status>Success</Status><ReferenceCode>…</ReferenceCode><url>…</url>` ("url. This is a legacy URL. Should be ignored."); failure `<Status>Fail</Status><ErrorCode>1012</ErrorCode><ErrorMessage>…</ErrorMessage>`. **No worked not-ready example for `/GetStatement`** — that it returns the same Fail envelope (e.g. 1019) is implied by the shared table only. **The successful statement body's schema is not documented at all** (the sample just writes raw bytes to a file).

**Content formats** — **zero coverage.** Full-page search for date/number-format terms (`yyyyMMdd`, `HHmmss`, decimal/thousands separators, timezone abbreviations, query format options): no matches as content-format documentation. The only timestamp shown is the response-envelope attribute `timestamp="28 August, 2012 10:37 AM EDT"` — one dated example, not a format spec.

**Polling** — qualitative only: Activity statements update once daily at close of business; Trade Confirmation data appears "within 5 to 10 minutes"; "not suitable for active polling"; "permit some flexibility… either via an explicit 'wait'… or via periodic reattempts". Hard limit: "1 request per second. A maximum of 10 requests per minute" (`/SendRequest`). The sample code's `time.sleep(20)` is example code, not documented guidance.

### DOC-03 (retrieved 2026-07-07, Flex Web Service h2 `#flex-intro` → `#error-codes`)

**Verbatim agreement with DOC-07 on everything above** — identical 20-row error table (1002 likewise absent), identical response-shape prose and XML samples (failure example likewise 1012), identical usage-notes/polling prose, identical rate-limit statement, and the same total absence of statement-content format documentation (searched `yyyyMMdd`, `HHmmss`, separators, `EST`/`CET`, `DateFormat`, `NumericFormat`, `PeriodicDate` within the Flex section — zero content matches). The two pages appear to share the same underlying content for this topic.

## Wire observations

None for statement content — **no Flex query/token is configured on the paper account** (the named follow-on in the Stream PVR Evidence: pin wire formats against a real statement once Craig configures one). The repo's Flex parsing behaviors under test today come from synthetic fixtures, not captures.

## Reconciliation

- **Agreed (both sources, verbatim):** the full error-code table incl. 1012/1013/1015 meanings; the `/SendRequest` success/failure envelope shapes; the legacy-`url`-ignore instruction; the 1/sec + 10/min token rate limit; the once-daily Activity / 5-10-min Trade-Confirmation freshness model.
- **Conflicts:** none — the sources are textually identical on this topic.
- **Gaps (both sources):** no retryability classification (the library's transient-vs-permanent mapping for 1001/1004–1009/1019/1021 vs 1003/1010–1017/1020 rests on the message-wording inference — a reasonable reading, but IBKR states no contract); no `/GetStatement` not-ready worked example; **no statement-body schema**; **no money/number/timestamp format documentation whatsoever**; no numeric polling cadence.
- **Presence claims:** the error envelope (`Status`/`ErrorCode`/`ErrorMessage`) is **documented, no wire samples** in this repo (no Flex token). Statement content formats are **absent from both** doc sources *and* unobserved — genuinely unpinned in every tier.

**Answer for the consuming decisions (PVR-09 §11.10, PVR-10):** the groomed designs stand and the doc evidence *mandates* them. PVR-09's format-agnostic posture (nullable money + observable parse-failure with raw text preserved; raw timestamp strings, no offset guessing) is the only defensible design when no tier — doc or wire — pins the formats; RST-3's US-timezone-table guessing is confirmed unsupported by any documentation. PVR-10's 1012/1013/1015 token-error mapping matches the live table; its transient-classification work should treat the "Please try again shortly" grouping as the library's own documented-inference policy (record in XML docs/design doc §11.10 that IBKR provides no retryability contract). The wall-clock poll bound (RST-6) aligns with IBKR's "periodic reattempts" + rate-limit framing. Named follow-on unchanged: pin statement wire formats once a Flex query/token exists on the paper account.
