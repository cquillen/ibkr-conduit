# Strict Response Validation — Findings Report

## Context

Enabling `StrictResponseValidation = true` in TestHarness produced 23 test failures across the integration test suite. Analysis against real API recordings revealed most are false positives from the validator, not real fixture/DTO gaps.

## Real Mismatches (2)

### 1. `HistoricalDataResponse` missing fields
- **Endpoint:** `GET /iserver/marketdata/history`
- **Missing from DTO:** `points`, `travelTime`
- **Source:** Real API recording confirms these fields exist
- **Fix:** Add `points` (int?) and `travelTime` (long?) to `HistoricalDataResponse`

### 2. `OrderSubmissionResponse` fixture shape mismatch
- **Endpoint:** `POST /iserver/account/{id}/orders`
- **Issue:** The order placement endpoint returns two completely different response shapes — submitted orders (`order_id`, `order_status`, `encrypt_message`) and confirmation questions (`id`, `message`, `messageOptions`, `isSuppressed`, `messageIds`). The `OrderSubmissionResponse` DTO handles both via `[JsonExtensionData]`, but the validator sees one shape and flags fields from the other.
- **Fix:** Validator should skip `OneOf`-wrapped return types (already skipped at RefitEndpointMap level, but the Refit method returns `List<OrderSubmissionResponse>` not `OneOf`, so it's not caught)

## False Positives — Validator Logic Issues (21)

### Missing nullable fields flagged as errors
The validator flags DTO fields as "missing" when they don't appear in the JSON response. But many DTO fields are nullable and legitimately absent:
- `Position.Name`, `Position.Ticker` — only present for some asset types
- `ContractSearchResult.ConidEx`, `.SecType`, `.ListingExchange` — only in nested sections, not top-level
- `ContractDetails.ListingExchange` — field name mismatch between wire (`listing_exchange`) and DTO property

**Fix needed:** The validator should NOT flag nullable/optional DTO fields as missing. Only required (non-nullable) fields with no default value should be flagged.

### DTO reuse across endpoints with different wire formats
- `Position` is used for both `/portfolio/{id}/positions` and `/portfolio2/{id}/positions`, but portfolio2 uses different field names (`marketPrice` vs `mktPrice`, `description` vs `contractDesc`)
- The validator validates against the canonical `Position` type regardless of which endpoint returned it

**Fix needed:** Consider per-endpoint DTO type override, or accept that reused DTOs will have optional fields absent from some endpoints.

### Multi-shape responses
- `OrderSubmissionResponse` handles both order confirmations and submitted orders
- `IserverAccountsResponse.SelectedAccount` — present in recordings but the validator couldn't find it (may be a matching issue)
- `OrdersResponse.Snapshot` — present in both fixture and recording but flagged (possible bool default value issue)

## Fixture Coverage

- **49 total fixtures:** 23 derived from real recordings, 26 hand-crafted
- **Hand-crafted fixtures were verified** against recordings where available — 9 of 12 checked endpoints match exactly
- **Recording gaps:** `GET /iserver/account/trades` recording returns empty array (can't verify field names)

## Recommended Follow-Up

1. **Fix validator nullable logic** — skip missing nullable fields (biggest bang, eliminates ~15 false positives)
2. **Add 2 missing DTO fields** — `HistoricalDataResponse.Points` and `.TravelTime`
3. **Skip multi-shape endpoints** in validator — endpoints where the response type varies by content
4. **Re-enable strict mode in TestHarness** after fixes
5. **Consider per-endpoint DTO overrides** for portfolio2 position reuse
