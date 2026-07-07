# `/portfolio/subaccounts2` response shape — IBKR doc evidence

**Question:** Do IBKR's live docs claim an object wrapper `{metadata, subaccounts}` or a bare array for `GET /portfolio/subaccounts2`, and when does the endpoint apply? (Re-grooms PVR-03's claim previously cited from the deprecated local mirror.)
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05 (DOC-02 is a rendering of DOC-01; DOC-04 orientation-only; DOC-06 different product; DOC-07 Flex)

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-01 (retrieved 2026-07-07, `paths./portfolio/subaccounts2.get` + `components.schemas.subaccounts2Response`; live `info.version` 2.35.0, matches registry)

Documents the **object wrapper only** — no bare-array variant anywhere for this path:

> `subaccounts2Response`: `{"type": "object", "properties": {"metadata": {...}, "subaccounts": {"type": "array", ...}}}`

`metadata` = `pageNum` ("The active page number."), `pageSize` ("Items contained in the returning page."), `total` ("The total number of accounts returned for the page." — reads page-scoped, not structure-wide; the source doesn't clarify). Elements are `accountAttributes` (24 documented properties incl. `accountId`, `businessType` enum, `clearingStatus` enum, nested `parent`). Applicability, exact:

> "Used in tiered account structures (such as Financial Advisor and IBroker Accounts) to return a list of sub-accounts, paginated up to 20 accounts per page… If you have less than 100 sub-accounts use /portfolio/subaccounts."

Sibling `GET /portfolio/subaccounts` is documented as a **bare array** of `accountAttributes`. Gaps: no page-request parameter documented for subaccounts2 (only `accountId` path + `nocache` query) and no last-page indicator. Internal typo: schema `faClient` vs example `"faclient"`.

### DOC-03 (retrieved 2026-07-07, `<section id="portfolio-subaccounts2">` / `<section id="portfolio-subaccounts">`; anchors sit on the wrapping `<section>`, not the h3)

**Self-contradictory.** The prose "Response Object" claims the wrapper:

> "**metadata:** Object. Contains metadata about the response data. { **total:** int… **pageSize:** int… **pageNum:** int… } **subaccounts:** Array of Objects. Contains all of the accounts and their respective data."

…but the code sample directly beneath it is a **bare array**, byte-identical to the `/portfolio/subaccounts` example (no `metadata`/`subaccounts` keys at all). Request parameter, exact:

> "**page:** String. Required Indicate the page identifier that should be retrieved. Pagination begins at page 0. 20 accounts returned per page."

Applicability text matches DOC-01 verbatim (tiered/FA/IBroker; "If you have less than 100 sub-accounts use /portfolio/subaccounts"). Sibling `/portfolio/subaccounts`: prose and example agree on a bare array ("No params or body content", up to 100 sub-accounts, "If you have more than 100 sub-accounts use /portfolio/subaccounts2"). Example keys `PrepaidCrypto-Z`/`PrepaidCrypto-P`/`brokerageAccess` appear in samples but are never defined in prose. Same `faClient`/`faclient` spelling mismatch.

### DOC-05 (retrieved 2026-07-07, h3 "Querying Your Accounts" `#querying-your-accounts-41`)

**Does not document the shape.** Sole mention:

> "If you have more than 100 subaccounts use `/portfolio/subaccounts2`. To query a list of accounts the user can trade, see `/iserver/accounts`."

The natural home ("Advisor Features" h2, `#advisor-features-44`) is a stub: "Documentation coming soon." Sibling `/portfolio/subaccounts` fully documented as a bare array (example reproduced, same shape as DOC-03's).

## Wire observations

- Paper account (non-FA — sub-accounts unsupported): `GET /portfolio/subaccounts2` returned a **bare array** — committed sanitized live-capture fixture (tests integration fixtures), 1 sample. The wrapper shape has **never** been wire-observed by this repo (an FA/tiered structure is required to elicit it, which the paper account cannot create — that is why PVR-03 handles both shapes rather than picking one).

## Reconciliation

- **Agreed:** the endpoint is for tiered/FA/IBroker structures, paginated 20/page, with the ≶100-sub-accounts crossover to/from `/portfolio/subaccounts`; the sibling `/portfolio/subaccounts` is a bare array (all three sources + wire agree there).
- **Conflicts:** wrapper vs bare array for subaccounts2 — DOC-01 schema + DOC-03 prose claim the wrapper; DOC-03's **own example** shows a bare array; the only wire sample (paper, non-FA) is a bare array. IBKR's documentation is self-inconsistent; unresolved by docs. **Acted on by design:** PVR-03 deserializes both shapes into one paged DTO (operator-decided 2026-07-07) — safe under both answers. A second conflict: DOC-03 documents a **required `page` query parameter**; DOC-01 documents no page parameter at all (only `nocache`). Also unresolved; the facade already exposes a page argument, and an ignored-vs-required param is harmless under both readings.
- **Gaps:** no source documents a last-page indicator or the semantics of `metadata.total` (page-scoped vs structure-wide); DOC-05 omits the endpoint's shape entirely.
- **Presence claims:**
  - Wrapper `{metadata, subaccounts}` on subaccounts2: **documented, absent from samples** (docs claim it — DOC-01 + DOC-03 prose; the one wire sample, a non-FA paper account, showed the bare array instead). Refutes nothing — the wrapper may be FA-structure-only, which no sample here can exercise.
  - Bare array on subaccounts2: **observed (1 sample, paper/non-FA) + shown in DOC-03's own example** — real, but per-sample; not a guarantee for FA structures.

**Answer for the consuming decision (PVR-03, design doc §16.4):** the both-shapes normalization stands, now with live citations — the shape conflict is present inside IBKR's own current documentation, not an artifact of the old mirror. Page metadata must be nullable (absent in the bare-array form). No probe can settle the wrapper side from a paper account; revisit only if an FA-structured account ever becomes available.
