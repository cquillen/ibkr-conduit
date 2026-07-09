# Corporate-actions API (splits / dividends / mergers) — IBKR doc evidence

**Question:** Is there an IBKR CP Web API for corporate actions — stock splits, reverse splits, dividends, spinoffs, mergers — exposing (a) upcoming/scheduled events per instrument, (b) historical events, (c) notifications, or (d) corp-action data as fields on another endpoint?
**Date:** 2026-07-08 · **Sources consulted:** DOC-01 (OpenAPI v2.35.0), DOC-03 (v1 narrative), DOC-05 (trading), DOC-09 (changelog) · **Wire probe:** paper account, market open, delayed data.

> ⚠️ Doc sections record what IBKR's documentation CLAIMS as of the date above — not wire-verified.
> Presence claims are never per-message guarantees: WS is confirmed sparse; REST sparseness unconfirmed.
> Wire sections cite recordings/ paths and sample counts. On a paper account an EMPTY field is ambiguous
> (deprecation vs. unentitled vs. no-data); a POPULATED field is definitive presence.

## Bottom line

**There is no dedicated corporate-actions API.** Splits, reverse splits, spinoffs, and mergers have **zero footprint** across all four sources — no endpoint, no field, no notification type. Dividend-adjacent data exists only in scattered, partial forms. The single design-relevant conflict (docs say dividend fields available; changelog says dividend fundamental tags deprecated) was **resolved by a live wire probe**: the aggregate dividend snapshot fields 7671/7672 are **live**; the named fundamental tags (7286–7291 range) are **absent from the wire**.

## Per-source findings

### DOC-01 — OpenAPI JSON (retrieved 2026-07-08, `info.version` 2.35.0)
- **No** path/operationId/schema named "corporate action[s]". Literal `split`, `spinoff`, `merger` = **0 hits** in the 864 KB document.
- Snapshot field descriptions (schema `mdFields` on `GET /iserver/marketdata/snapshot`): `7671` = "Dividends … total of the expected dividend payments over the next twelve months per share", `7672` = "Dividends TTM … over the last twelve months". `7683–7690` = "Upcoming/Recent Event…" fields, each annotated **"Requires Wall Street Horizon subscription."** No split-ratio or ex-date field.
- FYI `typecodes` enum includes `DA` - Dividends Advisory and `TO` - Takeover; **no split/spinoff typecode**. Notifications (`GET /fyi/notifications`) are unstructured HTML blobs, not structured corp-action data.
- `GET /gw/api/v1/tax-vouchers/dividends` (op `fetchDividends_1`, provider tag `x-ib-gateway-provider-name: corpaction`) returns `DividendDTO { currency, exDate, payDate, corpactionId, isin, symbol, … }` — but account/year/country-scoped **tax vouchers** on the Account-Management (`gw/api/v1`, OAuth-2.0) product, not an instrument-scoped corp-action feed.
- `contractInfo` / `trsrvSecDefResponse` schemas carry **no** dividend/split fields.

### DOC-03 — v1 narrative (retrieved 2026-07-08)
- No dedicated corp-actions endpoint; `split`/`spinoff`/`merger`/`ex-date` = 0 hits.
- Same market-data fields (7671/7672 aggregate; 7683–7690 WSH-gated events).
- Same FYI typecodes `DA` (Dividends Advisory) + `TO` (Takeover); no example payload documented.
- `POST /pa/transactions` (PortfolioAnalyst) description: *"Types of transactions include dividend payments, buy and sell transactions, transfers."* — account-scoped realized dividends, no per-event schema detail. Account ledger carries a `dividends` cash figure.

### DOC-05 — trading docs (retrieved 2026-07-08)
- **No corp-action coverage at all.** Instrument Discovery is a "Documentation coming soon" stub; the snapshot field list is deferred entirely to DOC-01/DOC-02. Only FYI examples shown are `PF`/`PT` (not the full typecode set).

### DOC-09 — changelog (retrieved 2026-07-08, newest entry 2026-04-14, no drift)
- **Jan 6, 2026** (`warning IBKR APIs`): *"Fundamental Data tags, including Dividend Amount, Dividend Yield %, Ex-Date, P/E, Market Capitalization, EPS, and Beta are deprecated and no longer available via API."* — a **removal**, names labels not field-codes; silent on whether 7671/7672 are affected. This is the source of the conflict the wire probe resolves.
- No entry adds any corp-action/events/calendar/WSH capability. `split`/`spinoff`/`merger` = 0 relevant hits.

## Wire observations (paper account, 2026-07-08 ~12:06–12:08 EDT, US session OPEN, data = **Delayed** per field 6509=`DPB`)

Snapshot `GET /iserver/marketdata/snapshot` requesting `55,31,7671,7672,7286,7287,7288,7289,7290,7291,7683,7684`, 3 calls/instrument (call 1 = sparse preflight; warmed n=2). Captures: **`recordings/dividendprobe/001-009-GET-iserver-marketdata-snapshot.json`** (Authorization redacted; snapshot URL carries no account id).

| Field | IBM (8314) | AAPL (265598) | SPY (756733) |
|---|---|---|---|
| 31 last (liveness) | `300.68` 2/2 | `309.72` 2/2 | `740.14` 2/2 |
| 55 symbol | `IBM` 2/2 | `AAPL` 2/2 | `SPY` 2/2 |
| **7671 Dividends** | `6.76` 2/2 | `1.09` 1/2 | `7.32` 2/2 |
| **7672 Dividends TTM** | `6.73` 2/2 | `1.05` 1/2 | `7.53` 2/2 |
| **7286–7291** | absent 0/2 | absent 0/2 | absent 0/2 |
| 7683 Upcoming Event | `Erng Call` 2/2 | `Erng Call` 2/2 | absent 0/2 |
| 7684 Upcoming Event Date | `07/22 Aftr Mkt` 2/2 | `07/30 Aftr Mkt` 2/2 | absent 0/2 |

- **7671/7672 → observed, 3/3 instruments.** IBKR omits unavailable fields entirely (no nulls).
- **7286–7291 → absent, 0/9 warmed samples.** Consistent with the Jan-6 deprecation; inconclusive on true removal (delayed-paper, presence/absence asymmetry).
- **7683/7684 → observed on both single-stock equities** (SPY the ETF has no earnings — consistent). Returned **without** a full WSH subscription. Shape: 7683 = short event-type label string; **7684 = `"MM/DD <session>"` string (e.g. `07/22 Aftr Mkt`), NOT an ISO date.**

### Library cross-check
`MarketDataFields` defines 7671/7672 and 7683–7690 but **not** 7286–7291 — consistent with the wire. `MarketDataSnapshotRaw` already models `conidEx`/`_updated`/`server_id`/`6509` explicitly + a `[JsonExtensionData]` bag for all numeric fields, and `MarketDataSnapshot` surfaces every field as `string?` (no typed date parse). So the wire's envelope fields and the `"07/22 Aftr Mkt"` string shape are all tolerated — **no parse bug, no missing constant.**

## Reconciliation

- **Agreed (all four sources):** no dedicated corporate-actions endpoint; no splits/reverse-splits/spinoffs/mergers anywhere; dividends appear only as (i) aggregate snapshot fields 7671/7672, (ii) FYI `DA`/`TO` advisory notifications, (iii) account-scoped realized records (`/pa/transactions`, account ledger, tax-vouchers), (iv) Flex activity statements (already supported by the library).
- **Conflict, resolved by wire:** DOC-01 lists 7671/7672 as available; DOC-09 (Jan 6 2026) deprecates dividend *fundamental tags*. Probe shows **7671/7672 live** and **7286–7291 absent** → the deprecation hit the named fundamental-ratio tags (the 7286-range), **not** the 7671/7672 aggregate dividend fields. Both docs are correct about different field families.
- **Control falsified:** 7683/7684 were expected empty on paper without WSH; they returned IBKR-native earnings-calendar data. Any doc/test asserting "7683/7684 empty without WSH" is wrong.
- **Gaps:** no registered source exposes an instrument-scoped corp-action calendar (upcoming ex-dates, split ratios/dates). The `corpaction` provider exists internally at IBKR (seen only via the tax-voucher lens on the OAuth-2.0 account-management product). For realized corp actions on the account, **Flex activity statements** remain the most complete route.

## Presence claims (taxonomy)

- Dividend snapshot fields 7671/7672 — **documented + observed** (5/6 warmed samples, 3/3 instruments; delayed data).
- Fundamental tags 7286–7291 — **documented-then-deprecated (DOC-09); absent from wire (0/9)** — deprecation consistent, absolute removal not proven from one paper account.
- Upcoming-event fields 7683/7684 — **documented (WSH-gated) + observed without full WSH** (4/6; both equities). String-shaped, not ISO date.
- FYI typecodes `DA`/`TO` — **documented** (DOC-01 + DOC-03 agree); not wire-probed (no example payload documented).
- Splits / reverse splits / spinoffs / mergers as an API — **absent from all four docs and not probed** — weakly suggests non-existence in the CP Web API surface (OpenAPI absence is not proof).
