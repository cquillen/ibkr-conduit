# Contracts API Completion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 10 missing contract/security definition endpoints to the IbkrConduit library, completing the Contracts API surface.

**Architecture:** All new endpoints follow the existing pass-through pattern: Refit interface -> immutable response models -> IContractOperations interface -> ContractOperations (pass-through with activity tracing). Response models use positional records with `[ExcludeFromCodeCoverage]` and `[JsonExtensionData]` for forward compatibility.

**Tech Stack:** .NET 10 / .NET 8 (multi-target), xUnit v3, Shouldly, WireMock.Net, Refit

---

## Dependency Graph

```
Task 1 (Refit methods + response models)
         │
         ▼
Task 2 (IContractOperations + ContractOperations)
         │
         ├──────────────┐
         ▼              ▼
Task 3 (unit tests)   Task 4 (integration tests)
```

---

## Task 1 — Refit Methods and Response Models

**Branch:** `feat/contracts-api-completion` (single branch for all tasks — they are tightly coupled additions)

### TDD: Write model deserialization tests first (Red), then add models (Green)

- [ ] Add unit tests for new model deserialization in `tests/IbkrConduit.Tests.Unit/Contracts/ContractApiModelTests.cs`
- [ ] Add 10 Refit methods to `IIbkrContractApi`:
  - `GetSecurityDefinitionInfoAsync` — GET /iserver/secdef/info
  - `GetOptionStrikesAsync` — GET /iserver/secdef/strikes
  - `GetTradingRulesAsync` — POST /iserver/contract/rules
  - `GetSecurityDefinitionsByConidAsync` — GET /trsrv/secdef
  - `GetAllConidsByExchangeAsync` — GET /trsrv/all-conids
  - `GetFuturesBySymbolAsync` — GET /trsrv/futures
  - `GetStocksBySymbolAsync` — GET /trsrv/stocks
  - `GetTradingScheduleAsync` — GET /trsrv/secdef/schedule
  - `GetCurrencyPairsAsync` — GET /iserver/currency/pairs
  - `GetExchangeRateAsync` — GET /iserver/exchangerate
- [ ] Add response models to `IIbkrContractApiModels.cs`:
  - `SecurityDefinitionInfo` — conid, symbol, description, exchange, etc.
  - `OptionStrikes` — call and put arrays of decimals
  - `TradingRulesRequest` — body record for POST /iserver/contract/rules
  - `TradingRules` — order types, TIF options, etc. (uses JsonElement for loosely-typed sections)
  - `SecurityDefinitionResponse` — wrapper with secdef array
  - `ExchangeConid` — ticker, conid, exchange
  - `FuturesResponse` — Dictionary keyed by symbol
  - `FutureContract` — symbol, conid, expiry, etc.
  - `StocksResponse` — Dictionary keyed by symbol
  - `StockContract` — name, conid, exchange, etc.
  - `TradingSchedule` — trading times
  - `CurrencyPairsResponse` — Dictionary keyed by currency
  - `CurrencyPair` — symbol, conid, etc.
  - `ExchangeRate` — rate value

### Files Modified

- `src/IbkrConduit/Contracts/IIbkrContractApi.cs`
- `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs`
- `tests/IbkrConduit.Tests.Unit/Contracts/ContractApiModelTests.cs` (new)

---

## Task 2 — Operations Interface and Implementation

- [ ] Add 10 methods to `IContractOperations`
- [ ] Add 10 pass-through implementations to `ContractOperations` with activity tracing

### Files Modified

- `src/IbkrConduit/Client/IContractOperations.cs`
- `src/IbkrConduit/Client/ContractOperations.cs`

---

## Task 3 — Unit Tests for Model Deserialization

- [ ] Deserialization tests for all new response models (SecurityDefinitionInfo, OptionStrikes, TradingRules, etc.)
- [ ] Verify JsonNumberHandling works for string-encoded numeric fields
- [ ] Verify JsonExtensionData captures unknown properties

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Contracts/ContractApiModelTests.cs`

---

## Task 4 — WireMock Integration Tests

- [ ] Add WireMock integration tests for key endpoints in `tests/IbkrConduit.Tests.Integration/Contracts/ContractEndpointTests.cs` (new)
- [ ] Test endpoints: secdef/info, strikes, futures, stocks, currency pairs, exchange rate
- [ ] Verify Refit wiring (correct path, query params, deserialization)

### Files Modified

- `tests/IbkrConduit.Tests.Integration/Contracts/ContractEndpointTests.cs` (new)

---

## Verification

- [ ] `dotnet build --configuration Release` — zero warnings
- [ ] `dotnet test --configuration Release` — all pass
- [ ] `dotnet format --verify-no-changes` — clean
