# Account summary `cushion` field — IBKR doc evidence

**Question:** What do IBKR's live docs say about the response fields of `GET /iserver/account/{accountId}/summary`, and can a consumer rely on `cushion` always being present?
**Date:** 2026-07-07 · **Sources consulted:** DOC-01, DOC-03, DOC-05 (selected per registry `Covers`/`Overlaps`; DOC-02 is a rendering of DOC-01; DOC-04 orientation-only; DOC-06 different product; DOC-07 Flex)

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed (and see Reconciliation — DOC-03 itself documents key-set variability for `/portfolio/{accountId}/summary`).
> Wire sections cite recordings/ paths and sample counts.

## Per-source findings

### DOC-01 (retrieved 2026-07-07, `paths./iserver/account/{accountId}/summary.get` + `components.schemas.accountSummaryResponse`; live `info.version` 2.35.0, matches registry)

Documents the endpoint ("Provides a general overview of the account details such as balance values."). Response schema fields, none in any `required` array (absence-of-constraint, not an explicit "optional"):

> `accountType, status, balance, SMA, buyingPower, availableFunds, excessLiquidity, netLiquidationValue, equityWithLoanValue, regTLoan, securitiesGVP, totalCashValue, accruedInterest, regTMargin, initialMargin, maintenanceMargin, cashBalances[]{currency, balance, settledCash}`

`cushion` is **not** among them, nor in the endpoint's example. `cushion` appears in exactly one place in the whole document — `GET /portfolio/{accountId}/summary` (`portfolioSummary.properties.cushion`):

> "Margin cushion as a decimal ratio, (ELV-Maintenance)/ELV. (value)"

wrapped in the shared `portfolioSummaryValue` envelope `{amount, currency, isNull, severity, timestamp, value}`. IBKR's own example: `"cushion": {"amount": 0.0, "currency": null, "isNull": false, "severity": 0, "timestamp": 1712156105000, "value": "0.994598"}` — note the ratio lives in the `value` **string**, not `amount`, and `currency` is `null` despite being schema-typed `number`.

### DOC-05 (retrieved 2026-07-07, h3 "Querying Equity and Margin", `#querying-equity-and-margin-43`)

Does **not** document `/iserver/account/{accountId}/summary` (zero occurrences) and does **not** mention `cushion` anywhere. For `/portfolio/{accountId}/summary` it states:

> "The /portfolio/{accountId}/summary endpoint delivers a wide variety of values related to an account's equity, margin use, and accrued balances. Values are presented in aggregate form for the entire U-account …, as well as diasaggregated by the account's underlying regulatory segments…" [sic]

Its example response is truncated by the source itself (literal `...`) — only `accountcode` and `indianstockhaircut` shown, one wrapped with `isNull`, the other with `isNone` (inconsistency is in IBKR's own example).

### DOC-03 (retrieved 2026-07-07, h3 "Portfolio Summary" under Portfolio family; page structure matched registry)

Does **not** document `/iserver/account/{accountId}/summary` (exhaustive path + heading search; the Accounts family covers PnL, dynamic accounts, signatures, switch, brokerage accounts only) and contains **zero** occurrences of `cushion` (REST or WebSocket). For the sibling `GET /portfolio/{accountId}/summary`:

> "The /summary endpoint returns a Key: Value Object structure. This returns a total of 45-135 unique values used to summarize the account. Responses will come as the base value … followed by an identical response name with a trailing '-c' or '-s'."

Wrapper fields documented as `amount` (float), `currency` (String), `isNull` (bool), `timestamp` (int), `value` (String), `severity` (int, "Internal use only"). The WebSocket `ssd` (Subscribe Account Summary) topic lists only example keys ("AccruedCash-S", "ExcessLiquidity-S") with no exhaustive enumeration; no cushion key shown.

## Wire observations

None — no tier-2 probe run for this question yet. If a spec decision needs the actual key set of either endpoint, probe read-only with multiple samples (flat vs positioned account states if available).

## Reconciliation

- **Agreed (all three sources):** `cushion` is not documented on `GET /iserver/account/{accountId}/summary` by any registered source. The only documented home of `cushion` is `GET /portfolio/{accountId}/summary` (DOC-01), as a `portfolioSummaryValue`-wrapped key.
- **Notable single-source situation:** `/iserver/account/{accountId}/summary` is documented **only** by DOC-01 — the source the operator flags as gappy. Neither narrative source (old v1 nor new trading docs) covers this endpoint at all. Any claim about this endpoint currently rests on the OpenAPI alone.
- **Conflicts:** wrapper `currency` type — DOC-01 schema types it `number`, DOC-03 narrative says `String`, and IBKR's own examples show `null` / `"USD"` (string). Unresolved; the wire decides if it ever matters. DOC-05/DOC-03 share an example containing IBKR's own `isNull` vs `isNone` key inconsistency — treat `isNone` as a doc typo until wire-observed.
- **Gaps:** no source enumerates the full key set of `/portfolio/{accountId}/summary`; DOC-03 explicitly says the count **varies (45–135)** — IBKR's own documentation of REST key-set variability for this endpoint. DOC-05's example is self-truncated.
- **Presence claims (taxonomy):**
  - `cushion` on `/iserver/account/{accountId}/summary`: **absent from both** (all doc sources + no wire samples) — weakly suggests non-existence; absence from the known-gappy OpenAPI proves nothing on its own.
  - `cushion` on `/portfolio/{accountId}/summary`: **documented, no wire samples yet** — and even as documented, never a per-response guarantee: the wrapper has `isNull`, and the endpoint's key count varies per DOC-03.

**Answer for the consuming decision:** don't expect `cushion` from `/iserver/account/{accountId}/summary`; if cushion is wanted, target `/portfolio/{accountId}/summary` (treating presence as per-sample, `value`-string-carried), or compute (ELV − maintenance)/ELV client-side from `equityWithLoanValue` + `maintenanceMargin` — both documented (not `required`) on the iserver summary. A read-only probe should precede any DTO work on either endpoint.
