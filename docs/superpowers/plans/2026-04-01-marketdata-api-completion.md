# Market Data API Completion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 6 missing market data endpoints to the IbkrConduit library, completing the Market Data API surface.

**Architecture:** All new endpoints follow the existing pass-through pattern: Refit interface -> immutable response models -> IMarketDataOperations interface -> MarketDataOperations implementation. Response models use positional records with `[ExcludeFromCodeCoverage]` and `[JsonExtensionData]` for forward compatibility. The regulatory snapshot endpoint includes a warning log since it costs $0.01 per request.

**Tech Stack:** .NET 10 / .NET 8 (multi-target), xUnit v3, Shouldly, WireMock.Net, Refit

---

## Dependency Graph

```
Task 1 (Refit methods + response models)
         |
         v
Task 2 (IMarketDataOperations + MarketDataOperations)
         |
         +---------------+
         v               v
Task 3 (unit tests)   Task 4 (integration tests)
```

---

## Task 1 — Refit Methods and Response Models

**Branch:** `feat/marketdata-api-completion`

### TDD: Write model deserialization tests first (Red), then add models (Green)

- [ ] Add unit tests for new model deserialization in `tests/IbkrConduit.Tests.Unit/MarketData/MarketDataApiModelTests.cs`
- [ ] Add 6 Refit methods to `IIbkrMarketDataApi`:
  - `GetRegulatorySnapshotAsync` — GET /md/regsnapshot?conid={conid}
  - `UnsubscribeAsync` — POST /iserver/marketdata/unsubscribe
  - `UnsubscribeAllAsync` — GET /iserver/marketdata/unsubscribeall
  - `RunScannerAsync` — POST /iserver/scanner/run
  - `GetScannerParametersAsync` — GET /iserver/scanner/params
  - `RunHmdsScannerAsync` — POST /hmds/scanner
- [ ] Add response/request models to `IIbkrMarketDataApiModels.cs`:
  - `UnsubscribeRequest` — conid field for unsubscribe POST body
  - `UnsubscribeResponse` — success bool
  - `UnsubscribeAllResponse` — unsubscribed bool
  - `ScannerRequest` — instrument, type, location, filter array
  - `ScannerFilter` — code, value
  - `ScannerResponse` — contracts array, scan_data_column_name
  - `ScannerContract` — server_id, symbol, conidex, con_id, company_name, listing_exchange, sec_type
  - `HmdsScannerRequest` — instrument, locations, scanCode, secType, maxItems, filters
  - `HmdsScannerResponse` — total, size, offset, Contracts wrapper
  - `HmdsScannerContract` — inScanTime, contractID

### Regulatory Snapshot

The regulatory snapshot returns the same shape as the regular snapshot (`MarketDataSnapshotRaw`), so we reuse that type. The `GetRegulatorySnapshotAsync` Refit method takes a `conid` query parameter and returns `MarketDataSnapshotRaw`.

### Scanner Parameters

The `/iserver/scanner/params` response is large and schema-variable. We return it as `ScannerParameters` — a record with typed top-level lists (`scan_type_list`, `instrument_list`, `filter_list`, `location_tree`) using nested records plus `[JsonExtensionData]`.

---

## Task 2 — Operations Interface and Implementation

- [ ] Add 6 methods to `IMarketDataOperations`:
  - `GetRegulatorySnapshotAsync(int conid, CancellationToken)` — returns `MarketDataSnapshot`
  - `UnsubscribeAsync(int conid, CancellationToken)` — returns `UnsubscribeResponse`
  - `UnsubscribeAllAsync(CancellationToken)` — returns `UnsubscribeAllResponse`
  - `RunScannerAsync(ScannerRequest, CancellationToken)` — returns `ScannerResponse`
  - `GetScannerParametersAsync(CancellationToken)` — returns `ScannerParameters`
  - `RunHmdsScannerAsync(HmdsScannerRequest, CancellationToken)` — returns `HmdsScannerResponse`
- [ ] Implement in `MarketDataOperations`:
  - Most are pass-throughs
  - `GetRegulatorySnapshotAsync` logs a Warning before delegating, then maps through `MapSnapshot`
  - Add `[LoggerMessage]` for the regulatory snapshot warning

---

## Task 3 — Unit Tests

- [ ] `tests/IbkrConduit.Tests.Unit/MarketData/MarketDataApiModelTests.cs` — deserialization tests for all new models
- [ ] `tests/IbkrConduit.Tests.Unit/MarketData/MarketDataOperationsTests.cs` — operations delegation tests with fake API

---

## Task 4 — Integration Tests

- [ ] `tests/IbkrConduit.Tests.Integration/MarketData/MarketDataApiTests.cs` — WireMock tests for all 6 endpoints
  - Regulatory snapshot: verify deserialization of snapshot with bid/ask/last fields
  - Unsubscribe: verify success response
  - Unsubscribe all: verify unsubscribed response
  - Scanner run: verify contracts array deserialization
  - Scanner params: verify structured response
  - HMDS scanner: verify response with contracts wrapper

---

## Task 5 — Audit Update

- [ ] Update `docs/endpoint-coverage-audit.md` to mark 6 endpoints as implemented

---

## Acceptance Criteria

- `dotnet build --configuration Release` — zero warnings
- `dotnet test --configuration Release` — all tests pass
- `dotnet format --verify-no-changes` — clean formatting
- All 6 endpoints exercised by both unit and integration tests
