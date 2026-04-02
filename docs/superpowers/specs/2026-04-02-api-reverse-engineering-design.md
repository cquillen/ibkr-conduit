# API Reverse Engineering & DTO Correction — Design Spec

## Goal

Systematically capture raw responses from every IBKR API endpoint, then auto-generate correct C# DTOs and WireMock fixtures from the actual data instead of relying on the inaccurate IBKR documentation.

## Background

The DTO audit revealed 6 critical bugs (wrong field names, wrong response shapes, missing fields) and 65 discrepancies between the IBKR documentation and actual API behavior. The documentation cannot be trusted as a source of truth for field names or types. The recorded E2E responses are ground truth but only cover a subset of the data we need. A systematic capture effort is required.

## Architecture

### Capture Pipeline

Each capture uses a minimal HTTP pipeline — no Refit, no DTOs, no error normalization. This avoids deserialization failures from wrong models and captures exactly what IBKR sends.

```
RecordingDelegatingHandler → OAuthSigningHandler → HttpClient
```

The `SessionTokenProvider` and `OAuthSigningHandler` are used for authentication. A manual `ssodh/init` call establishes the brokerage session. Everything else is raw `HttpClient.SendAsync` with the response body captured verbatim by the recording handler.

### Capture Tool

A traditional .NET console app at `tools/ApiCapture/` with per-module commands. This is a long-lived maintenance tool, not a throwaway script.

```
dotnet run --project tools/ApiCapture -- session
dotnet run --project tools/ApiCapture -- portfolio
dotnet run --project tools/ApiCapture -- contracts
dotnet run --project tools/ApiCapture -- marketdata
dotnet run --project tools/ApiCapture -- orders
dotnet run --project tools/ApiCapture -- alerts
dotnet run --project tools/ApiCapture -- watchlists
dotnet run --project tools/ApiCapture -- fyi
dotnet run --project tools/ApiCapture -- allocation
dotnet run --project tools/ApiCapture -- all
```

Each module command:
1. Initializes the brokerage session
2. Creates any prerequisite test data (orders, alerts, etc.)
3. Hits every endpoint for that module, printing status
4. Cleans up test data
5. Saves recordings to `recordings/{module}/`

Shared infrastructure in the tool:
- `CaptureContext` — OAuth credentials, HttpClient setup, session init, recording context
- Per-module command classes implementing a common interface
- Console output showing progress and any errors

### Recording Output

Recordings save to `recordings/` at the repo root (not in test binaries). This directory is committed to git so the captured responses serve as the canonical reference for DTO generation and WireMock fixtures.

Format: Same WireMock-compatible JSON as the existing `RecordingDelegatingHandler` produces.

```
recordings/
  session/
    001-POST-iserver-auth-ssodh-init.json
    002-POST-tickle.json
    003-GET-iserver-auth-status.json
    ...
  portfolio/
    001-GET-portfolio-accounts.json
    002-GET-portfolio-{accountId}-positions-0.json
    ...
  contracts/
    ...
```

### DTO Generation

A Python script (`tools/generate_dtos.py`) reads all recordings and generates C# record definitions:

1. Parse each recording's response body JSON
2. For each endpoint, infer the response shape (field names, types)
3. Merge multiple recordings of the same endpoint to get the full field set (some fields are null in one response but populated in another)
4. Generate a C# positional record with:
   - `[JsonPropertyName("...")]` for every field
   - `[JsonNumberHandling(AllowReadingFromString)]` where string-encoded numbers are observed
   - `[JsonConverter(typeof(FlexibleStringConverter))]` where numbers appear for string-typed fields
   - `[JsonConverter(typeof(FlexibleDecimalConverter))]` where empty strings appear for decimal fields
   - `[ExcludeFromCodeCoverage]` on all generated records
   - `[JsonExtensionData]` on records with object bodies (for future-proofing)
5. Output as `.cs` files that can be diffed against current models

The script does NOT auto-overwrite. It generates to a staging directory (`generated/`), then the developer reviews and copies.

### WireMock Fixture Generation

A second Python script (`tools/generate_wiremock_fixtures.py`) converts recordings to standalone fixture JSON files:

1. For each recording, strip the `Metadata` section
2. Save as `tests/IbkrConduit.Tests.Integration/Fixtures/{Module}/{endpoint-slug}.json`
3. Generate a helper class `FixtureLoader` that loads fixtures by endpoint name

The integration tests then switch from inline JSON strings to:
```csharp
_server.Given(
    Request.Create().WithPath("/v1/api/portfolio/accounts").UsingGet())
    .RespondWith(
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(FixtureLoader.Load("portfolio", "GET-portfolio-accounts")));
```

## Milestones

### M0: Move Recording Infrastructure to Capture Tool

Move `RecordingContext` and `RecordingDelegatingHandler` from `tests/IbkrConduit.Tests.Integration/Recording/` into `tools/ApiCapture/Recording/`. The `RecordingHandlerFilter` (which hooks into `IHttpMessageHandlerBuilderFilter` for DI-based pipelines) is not needed — the capture tool builds its `HttpClient` manually and wires the handler directly. Remove the recording infrastructure and `IBKR_RECORD_RESPONSES` env var handling from the test project entirely — the E2E tests no longer need recording capability since the capture tool replaces that purpose. Remove recording references from `E2eScenarioBase` and all scenario test files. Existing tests must still pass (they just lose the recording feature).

### M1: Spike — Capture Tool + Single Endpoint

Build the `tools/ApiCapture/` console app with shared infrastructure (`CaptureContext`, recording handler, session init, CLI routing). Implement a single `spike` command that hits `GET /portfolio/accounts` — the simplest endpoint that needs no setup and always returns data.

This validates the full pipeline end-to-end:
- OAuth signing works through the raw HttpClient pipeline
- Session initialization succeeds
- Recording handler captures the response
- Output file is valid WireMock-compatible JSON
- Sanitization strips sensitive data

Run: `dotnet run --project tools/ApiCapture -- spike`

Once the spike produces a valid recording, proceed to M2.

### M2: Session, Accounts, Portfolio

Implement three module commands using the infrastructure proven in M1:

**Session** (8 endpoints):
- `POST /iserver/auth/ssodh/init`
- `POST /tickle`
- `GET /iserver/auth/status`
- `GET /sso/validate`
- `POST /iserver/questions/suppress`
- `POST /iserver/questions/suppress/reset`
- `POST /logout`
- `POST /iserver/reauthenticate`

**Accounts** (5 endpoints):
- `GET /iserver/accounts`
- `GET /iserver/account/{accountId}`
- `GET /iserver/account/search/{pattern}`
- `POST /iserver/account` (switch account)
- `POST /iserver/dynaccount`

**Portfolio** (18 endpoints):
- `GET /portfolio/accounts`
- `GET /portfolio/{accountId}/positions/{page}`
- `GET /portfolio/{accountId}/summary`
- `GET /portfolio/{accountId}/ledger`
- `GET /portfolio/{accountId}/meta`
- `GET /portfolio/{accountId}/allocation`
- `GET /portfolio/{accountId}/position/{conid}` (use SPY conid)
- `GET /portfolio/positions/{conid}` (cross-account)
- `GET /portfolio/{accountId}/combo/positions`
- `GET /portfolio2/{accountId}/positions` (real-time)
- `GET /portfolio/subaccounts`
- `GET /portfolio/subaccounts2`
- `GET /iserver/account/pnl/partitioned`
- `POST /portfolio/{accountId}/positions/invalidate`
- `POST /pa/performance`
- `POST /pa/transactions`
- `POST /portfolio/allocation` (consolidated)
- `POST /pa/allperiods`

**Paper account prerequisites:**
- At least one position (SPY is currently held)
- No special setup needed

### M3: Contracts, Market Data

**Contracts** (12 endpoints):
- `GET /iserver/secdef/search` (search "SPY", "AAPL", "QQQ")
- `GET /iserver/contract/{conid}/info` (SPY conid)
- `GET /iserver/secdef/info` (option chain for SPY)
- `GET /iserver/secdef/strikes` (SPY options)
- `GET /trsrv/secdef` (by conid)
- `GET /trsrv/all-conids` (by exchange)
- `GET /trsrv/futures` (ES)
- `GET /trsrv/stocks` (AAPL)
- `GET /trsrv/secdef/schedule` (SPY)
- `GET /iserver/currency/pairs` (USD)
- `GET /iserver/exchangerate` (USD/EUR)
- `POST /iserver/contract/rules` (SPY)

**Market Data** (8 endpoints):
- `GET /iserver/marketdata/snapshot` (SPY, AAPL conids)
- `GET /iserver/marketdata/history` (SPY, 1d bars)
- `GET /md/regsnapshot` (SPY)
- `GET /iserver/marketdata/unsubscribeall`
- `GET /iserver/scanner/params`
- `POST /iserver/marketdata/unsubscribe` (SPY conid)
- `POST /iserver/scanner/run` (TOP_PERC_GAIN)
- `POST /hmds/scanner` (TOP_PERC_GAIN)

**Paper account prerequisites:**
- None for contracts
- Market data snapshots may return delayed data outside RTH — capture during market hours if possible, but delayed data still shows the response shape

### M4: Orders

**Orders** (8 endpoints):
- `POST /iserver/account/{accountId}/orders` (place a limit order)
- `GET /iserver/account/orders` (list — call twice per IBKR quirk)
- `GET /iserver/account/order/status/{orderId}` (for the placed order)
- `POST /iserver/account/{accountId}/order/{orderId}` (modify price)
- `POST /iserver/account/{accountId}/orders/whatif` (preview)
- `POST /iserver/reply/{replyId}` (if confirmation needed)
- `GET /iserver/account/trades` (after fill)
- `DELETE /iserver/account/{accountId}/order/{orderId}` (cancel)

The capture app places a limit buy for SPY at $1.00 (won't fill), captures all order endpoints, then cancels. Also places a market buy for 1 share to capture filled order status and trades.

**Paper account prerequisites:**
- Trading permissions for US equities
- Sufficient buying power (paper accounts have this by default)

### M5: Alerts, Watchlists, FYI, Allocation

**Alerts** (4 endpoints):
- `POST /iserver/account/{accountId}/alert` (create price alert on SPY)
- `GET /iserver/account/mta` (list alerts)
- `GET /iserver/account/alert/{alertId}` (detail)
- `DELETE /iserver/account/{accountId}/alert/{alertId}` (delete)

**Watchlists** (4 endpoints):
- `POST /iserver/watchlist` (create)
- `GET /iserver/watchlists` (list)
- `GET /iserver/watchlist` (detail by id)
- `DELETE /iserver/watchlist` (delete)

**FYI/Notifications** (12 endpoints):
- `GET /fyi/unreadnumber`
- `GET /fyi/settings`
- `GET /fyi/disclaimer/{typecode}`
- `GET /fyi/deliveryoptions`
- `GET /fyi/notifications`
- `GET /fyi/notifications/more`
- `POST /fyi/settings/{typecode}` (toggle a setting)
- `POST /fyi/deliveryoptions/device` (register)
- `PUT /fyi/deliveryoptions/email` (toggle email)
- `PUT /fyi/disclaimer/{typecode}` (mark read)
- `PUT /fyi/notifications/{notificationId}` (mark read)
- `DELETE /fyi/deliveryoptions/{deviceId}` (delete device)

**Allocation** (8 endpoints — FA only):
- Document as skipped with instructions: "Requires an FA (Financial Advisor) account. Individual accounts receive 403/503 from these endpoints."
- If FA account is available later, the same capture command works.

**Paper account prerequisites:**
- Alerts: trading permissions, a known conid (SPY)
- Watchlists: no special setup
- FYI: notifications may be empty on paper accounts — capture whatever is there
- Allocation: FA account required (skip on individual accounts)

### M6: Auto-Generate DTOs

Python script `tools/generate_dtos.py`:
1. Read all recordings from `recordings/`
2. Group by endpoint path
3. For each endpoint, merge all response bodies to get the full field set
4. Generate C# record definitions
5. Output to `generated/` directory
6. Run a diff against current `*Models.cs` files
7. Produce a summary report of changes needed

The developer reviews the generated output and applies changes. Breaking changes to public APIs are acceptable (pre-release library).

### M7: Auto-Generate WireMock Fixtures

Python script `tools/generate_wiremock_fixtures.py`:
1. Read all recordings from `recordings/`
2. For each, produce a clean fixture JSON (strip metadata, keep request/response)
3. Save to `tests/IbkrConduit.Tests.Integration/Fixtures/{module}/`
4. Generate a `FixtureLoader` helper class
5. Produce a migration guide showing which inline WireMock strings to replace

The developer updates integration tests to use fixture files. This also ensures WireMock tests use real IBKR response shapes instead of guessed ones.

## Files

### New Files
| Path | Purpose |
|---|---|
| `tools/ApiCapture/ApiCapture.csproj` | Capture tool project |
| `tools/ApiCapture/Program.cs` | CLI entry point with command routing |
| `tools/ApiCapture/CaptureContext.cs` | Shared OAuth + HttpClient + recording setup |
| `tools/ApiCapture/Recording/RecordingContext.cs` | Moved from test project |
| `tools/ApiCapture/Recording/RecordingDelegatingHandler.cs` | Moved from test project |
| `tools/ApiCapture/Modules/SessionCapture.cs` | Session endpoint capture |
| `tools/ApiCapture/Modules/AccountsCapture.cs` | Accounts endpoint capture |
| `tools/ApiCapture/Modules/PortfolioCapture.cs` | Portfolio endpoint capture |
| `tools/ApiCapture/Modules/ContractsCapture.cs` | Contracts endpoint capture |
| `tools/ApiCapture/Modules/MarketDataCapture.cs` | Market data endpoint capture |
| `tools/ApiCapture/Modules/OrdersCapture.cs` | Orders endpoint capture |
| `tools/ApiCapture/Modules/AlertsCapture.cs` | Alerts endpoint capture |
| `tools/ApiCapture/Modules/WatchlistsCapture.cs` | Watchlists endpoint capture |
| `tools/ApiCapture/Modules/FyiCapture.cs` | FYI/Notifications endpoint capture |
| `tools/ApiCapture/Modules/AllocationCapture.cs` | Allocation endpoint capture (FA only) |
| `recordings/` | Committed recording files (gitignored account-specific data is sanitized) |
| `tools/generate_dtos.py` | DTO generation from recordings |
| `tools/generate_wiremock_fixtures.py` | WireMock fixture generation |
| `tests/IbkrConduit.Tests.Integration/Fixtures/` | Generated WireMock fixture files |

### Modified Files
| Path | Change |
|---|---|
| `tests/IbkrConduit.Tests.Integration/Recording/` | Removed (M0) — recording moves to capture tool |
| `tests/IbkrConduit.Tests.Integration/E2E/E2eScenarioBase.cs` | Remove recording context and filter registration (M0) |
| `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` | Regenerated from recordings (M6) |
| `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs` | Regenerated from recordings (M6) |
| `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs` | Regenerated from recordings (M6) |
| All other `*Models.cs` files | Regenerated from recordings (M6) |
| All integration test files | Switch to fixture files (M7) |
| `.gitignore` | Remove `Recordings/` exclusion, add any new patterns |

## Scope Boundaries

### In Scope
- Capture raw responses from all 87 endpoints
- Auto-generate DTOs from captured data
- Auto-generate WireMock fixtures from captured data
- Update integration tests to use fixture files
- Document paper account prerequisites per module
- Breaking changes to public DTOs are acceptable

### Out of Scope
- WebSocket streaming capture (different protocol, existing tests are adequate)
- Flex Web Service capture (XML-based, separate infrastructure, already working)
- Performance/load testing
- Adding new endpoints not already in the Refit interfaces
- FA allocation capture (requires FA account we don't have)
