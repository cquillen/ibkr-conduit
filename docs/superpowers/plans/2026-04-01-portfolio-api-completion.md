# Portfolio API Completion

**Date:** 2026-04-01
**Branch:** `feat/portfolio-api-completion`
**Status:** In Progress

## Goal

Add the 7 remaining portfolio-related endpoints to reach full portfolio endpoint coverage.

## Endpoints

| # | Endpoint | Method | Description |
|---|----------|--------|-------------|
| 1 | `/portfolio/allocation` | POST | Consolidated allocation across multiple accounts |
| 2 | `/portfolio/{accountId}/combo/positions` | GET | Combination (spread) positions |
| 3 | `/portfolio2/{accountId}/positions` | GET | Real-time positions (bypasses cache) |
| 4 | `/portfolio/subaccounts` | GET | Sub-accounts for FA/IBroker |
| 5 | `/portfolio/subaccounts2` | GET | Sub-accounts paginated for FA/IBroker |
| 6 | `/pa/allperiods` | POST | Performance across all time periods |
| 7 | `/iserver/account/pnl/partitioned` | GET | P&L partitioned by account/model |

## Implementation Pattern

Same pattern as contracts API completion:

1. Add response/request models to `IIbkrPortfolioApiModels.cs`
2. Add Refit methods to `IIbkrPortfolioApi.cs`
3. Add operation methods to `IPortfolioOperations.cs`
4. Add pass-through implementations to `PortfolioOperations.cs`
5. Add model deserialization unit tests to `PortfolioApiTests.cs`
6. Add WireMock integration tests to `PortfolioAccountsTests.cs`
7. Update FakePortfolioApi in `PortfolioOperationsTests.cs`
8. Add delegation unit tests to `PortfolioOperationsTests.cs`

## New Models

### Request Records
- `ConsolidatedAllocationRequest(AccountIds)` -- body for POST /portfolio/allocation
- `AllPeriodsRequest(AccountIds)` -- body for POST /pa/allperiods

### Response Records
- `ComboPosition(Name, Description, Legs, Positions)` -- combo position wrapper
- `ComboLeg(Conid, Ratio)` -- leg within a combo
- `SubAccount(Id, AccountTitle, AccountType)` -- sub-account (FA/IBroker)
- `AllPeriodsPerformance(CurrencyType, Rc)` -- all-periods performance data
- `PartitionedPnl(Upnl)` -- partitioned P&L response
- `PnlEntry(RowType, Dpl, Nl, Upl, El, Mv)` -- individual P&L entry

All response records use `[ExcludeFromCodeCoverage]` and `[JsonExtensionData]`.

## Tasks

### Task 1: Models and Refit Interface
- Add all request/response records
- Add 7 Refit methods to `IIbkrPortfolioApi`

### Task 2: Operations Interface and Implementation
- Add 7 methods to `IPortfolioOperations`
- Add pass-through implementations to `PortfolioOperations`

### Task 3: Unit Tests
- Model deserialization tests for each new response type
- Delegation tests via FakePortfolioApi

### Task 4: Integration Tests
- WireMock tests for each endpoint

### Task 5: Documentation
- Update `docs/endpoint-coverage-audit.md` to mark 7 endpoints as done

## Verification

```bash
dotnet build --configuration Release    # zero warnings
dotnet test --configuration Release     # all pass
dotnet format --verify-no-changes       # clean
```
