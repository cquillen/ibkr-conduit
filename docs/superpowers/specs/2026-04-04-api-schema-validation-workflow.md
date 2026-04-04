# API Schema Validation Workflow

## Goal

Systematically validate and fix the entire IBKR Client Portal API surface by comparing our DTOs, Refit interfaces, and integration tests against live API recordings and the OpenAPI spec.

## Workflow (per category)

1. **Pick a category** — choose a subset of endpoints (e.g., Session, Alerts, Portfolio)
2. **Query OpenAPI spec** — extract endpoint info (method, path, request body schema, 200 response schema) from `docs/ibkr-web-api-openapi.json` using the python comparison tools
3. **Build capture scenarios** — add/update entries in `tools/ApiCapture/EndpointTable.cs` for the category. Consider ordering for stateful flows (create before get/modify/delete). Include success, edge case, and failure scenarios
4. **Run captures** — execute `dotnet run --project tools/ApiCapture -- <category> -v`, fix expected status codes and request bodies until all scenarios pass with good coverage
5. **Fix DTOs** — compare recording response shapes against our model classes in `src/IbkrConduit/`. Fix field names, types, wrapper structures, missing fields
6. **Add missing Refit endpoints** — check if any endpoints from the OpenAPI spec are missing from our Refit interfaces. Add them with correct return types
7. **Fix integration tests** — update/create fixtures from recordings, update test assertions to match new DTOs, ensure 401 recovery test for every endpoint

## Categories and Priority

Based on the comparison report, prioritize categories with the most DTO mismatches.

| Category | Endpoints | Capture Status | DTO Status | Integration Tests |
|----------|-----------|---------------|------------|-------------------|
| Watchlists | 4 | Done (17 scenarios) | Fixed | Done (8 tests) |
| Session | 6 | Existing (8 scenarios) | Needs review | Existing (needs review) |
| Alerts | 6 | Existing (4 scenarios) | Needs review | Needs review |
| Accounts | 6 | Existing (4 scenarios) | Needs review | Existing (needs review) |
| FYIs & Notifications | 12 | Existing (13 scenarios) | Needs review | Needs review |
| Contract | 16 | Existing (18 scenarios) | Large mismatches (secdef, algos) | Existing (needs review) |
| Market Data | 5 endpoints + refs | Existing (12 scenarios) | Large mismatches (snapshot) | Existing (needs review) |
| Orders | 7 + refs | Existing (11 scenarios) | Moderate mismatches | Existing (needs review) |
| Portfolio | 14 | Existing (32 scenarios) | Large mismatches (summary: 114 fields) | Existing (needs review) |
| Portfolio Analyst | 3 | Existing (5 scenarios) | Moderate mismatches | Needs review |
| Scanner | 3 | Existing (3 scenarios) | Needs review | Needs review |
| FA Allocation | 8 + refs | Not captured | Unknown | Not tested |
| Event Contracts | 5 + refs | Not captured | Unknown | Not tested |

## Known Issues from Comparison Reports

### DTO mismatches discovered via recordings vs OpenAPI:
- **Portfolio Summary**: 114 fields in OA, we have 6 (generic value object structure)
- **Live Orders**: OA has 37 fields, we matched 29-31 depending on session state
- **Contract Info/Rules**: Nested `rules` object has many sub-fields we don't expand
- **Brokerage Accounts**: 21 additional fields in OA (asset type flags, UI settings)
- **Place/Modify/Reply Order**: 13 prompt/error alternate response fields in OA

### Field naming mismatches confirmed by recordings:
- `faclient` vs `faClient` (portfolio accounts/meta/subaccounts)
- `isFT` vs `isFt` (brokerage accounts)
- `alert_name` vs `alertName` (MTA alert)
- `stopprice` vs `stopPrice` (contract rules)
- `startNAV` vs `startNav` (PA all periods)

### IBKR API quirks discovered:
- Watchlist `id` must be numeric string
- Duplicate watchlist ID = silent full overwrite
- Delete nonexistent watchlist returns 200 on warm session, 503 on cold
- Empty body to watchlist create returns 400 (the one proper 4xx)
- IBKR generally returns 500/503 for what should be 4xx errors

## Tools

- `tools/compare_api_endpoints.py` — URL comparison between MD spec and OpenAPI
- `tools/compare_api_schemas.py` — Response schema field comparison (deep $ref resolution)
- `tools/compare_recordings_vs_openapi.py` — Three-way comparison: recordings vs OpenAPI vs MD
- `tools/ApiCapture` — Live API capture tool with `--verbose` flag
- Fixture files: `tests/IbkrConduit.Tests.Integration/Fixtures/{Category}/`

## Approach

Work one category at a time. Each category = one branch + one PR. Start with smaller/simpler categories (Session, Alerts) before tackling large ones (Portfolio, Orders, Contract).
