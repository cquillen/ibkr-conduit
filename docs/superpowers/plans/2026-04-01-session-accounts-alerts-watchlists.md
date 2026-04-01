# Session, Accounts, Alerts, and Watchlists API Completion

**Date:** 2026-04-01
**Branch:** `feat/session-accounts-alerts-watchlists`
**Status:** In Progress

## Goal

Add 17 missing endpoints across 4 categories: Session/Auth (4), Accounts (5), Alerts (4), and Watchlists (4).

## Category 1: Session/Auth (4 endpoints)

Added to existing `IIbkrSessionApi` -- no new facade surface needed. These are internal session endpoints for `SessionManager` and advanced consumers.

| # | Endpoint | Method | Description |
|---|----------|--------|-------------|
| 1 | `/iserver/questions/suppress/reset` | POST | Reset all suppressed messages |
| 2 | `/iserver/auth/status` | GET | Authentication status |
| 3 | `/iserver/reauthenticate` | POST | Reauthenticate (deprecated) |
| 4 | `/sso/validate` | GET | Validate SSO session |

### New Models
- `SuppressResetResponse(Status)` -- response from suppress reset
- `AuthStatusResponse(Authenticated, Competing, Connected, Fail, Message, Prompts)` -- auth status
- `ReauthenticateResponse(Message)` -- reauthenticate response
- `SsoValidateResponse(UserId, Expire, Result, AuthTime)` -- SSO validation

## Category 2: Accounts (5 endpoints)

New Refit interface `IIbkrAccountApi`, operations `IAccountOperations`, implementation `AccountOperations`.

| # | Endpoint | Method | Description |
|---|----------|--------|-------------|
| 1 | `GET /iserver/accounts` | GET | List brokerage accounts (iserver) |
| 2 | `POST /iserver/account` | POST | Switch active account |
| 3 | `POST /iserver/dynaccount` | POST | Set dynamic account |
| 4 | `GET /iserver/account/search/{pattern}` | GET | Search accounts by pattern |
| 5 | `GET /iserver/account/{accountId}` | GET | Get account info |

### New Models
- `IserverAccountsResponse(Accounts, SelectedAccount, ServerInfo, Aliases)` -- accounts list
- `SwitchAccountRequest(AcctId)` -- switch account body
- `SwitchAccountResponse(Set, SelectedAccount)` -- switch account result
- `DynAccountRequest(AcctId)` -- dynamic account body
- `DynAccountResponse(Set, SelectedAccount)` -- dynamic account result
- `AccountSearchResult(AccountId, AccountTitle, AccountType)` -- search result
- `IserverAccountInfo(AccountId, AccountTitle, AccountType)` -- account info

## Category 3: Alerts (4 endpoints)

New Refit interface `IIbkrAlertApi`, operations `IAlertOperations`, implementation `AlertOperations`.

| # | Endpoint | Method | Description |
|---|----------|--------|-------------|
| 1 | `POST /iserver/account/{accountId}/alert` | POST | Create or modify alert |
| 2 | `GET /iserver/account/mta` | GET | List all alerts |
| 3 | `GET /iserver/account/alert/{alertId}` | GET | Get alert details |
| 4 | `DELETE /iserver/account/{accountId}/alert/{alertId}` | DELETE | Delete alert |

### New Models
- `CreateAlertRequest(AccountId, OrderId, AlertName, AlertMessage, AlertRepeatable, OutsideRth, Conditions)` -- alert creation
- `AlertCondition(Type, Conidex, Operator, TriggerMethod, Value)` -- alert condition
- `CreateAlertResponse(RequestId, OrderId, OrderStatus, Text)` -- creation result
- `AlertSummary(AccountId, OrderId, AlertName, AlertActive, OrderStatus)` -- alert list item
- `AlertDetail(AccountId, OrderId, AlertName, AlertMessage, AlertActive, AlertRepeatable, Conditions)` -- alert detail
- `DeleteAlertResponse(RequestId, OrderId, Msg, Text)` -- delete result

## Category 4: Watchlists (4 endpoints)

New Refit interface `IIbkrWatchlistApi`, operations `IWatchlistOperations`, implementation `WatchlistOperations`.

| # | Endpoint | Method | Description |
|---|----------|--------|-------------|
| 1 | `POST /iserver/watchlist` | POST | Create watchlist |
| 2 | `GET /iserver/watchlists` | GET | List all watchlists |
| 3 | `GET /iserver/watchlist` | GET | Get watchlist by ID |
| 4 | `DELETE /iserver/watchlist` | DELETE | Delete watchlist |

### New Models
- `CreateWatchlistRequest(Id, Rows)` -- watchlist creation
- `WatchlistRow(C, H)` -- watchlist row (conid + header)
- `CreateWatchlistResponse(Id)` -- creation result
- `WatchlistSummary(Id, Name, Modified, Instruments)` -- watchlist summary
- `WatchlistDetail(Id, Name, Rows)` -- watchlist with rows
- `WatchlistDetailRow(C, H, Sym)` -- row in detail response
- `DeleteWatchlistResponse(Deleted, Id)` -- delete result

## Implementation Pattern

For each new category (Accounts, Alerts, Watchlists):
1. Create `src/IbkrConduit/{Category}/IIbkr{Category}Api.cs` -- Refit interface
2. Create `src/IbkrConduit/{Category}/IIbkr{Category}ApiModels.cs` -- models
3. Create `src/IbkrConduit/Client/I{Category}Operations.cs` -- operations interface
4. Create `src/IbkrConduit/Client/{Category}Operations.cs` -- pass-through with Activity spans
5. Add `I{Category}Operations` to `IIbkrClient` and `IbkrClient`
6. Register in `ServiceCollectionExtensions`
7. Add model deserialization unit tests
8. Add WireMock integration tests

For Session/Auth: add to existing `IIbkrSessionApi` and `IIbkrSessionApiModels`.

## Tasks

### Task 1: Session/Auth Endpoints
- Add 4 Refit methods to `IIbkrSessionApi`
- Add 4 response models to `IIbkrSessionApiModels.cs`
- Add model deserialization unit tests
- Add WireMock integration tests

### Task 2: Accounts Endpoints
- Create `IIbkrAccountApi`, `IIbkrAccountApiModels`
- Create `IAccountOperations`, `AccountOperations`
- Wire into `IIbkrClient`, `IbkrClient`, DI
- Add unit and integration tests

### Task 3: Alerts Endpoints
- Create `IIbkrAlertApi`, `IIbkrAlertApiModels`
- Create `IAlertOperations`, `AlertOperations`
- Wire into `IIbkrClient`, `IbkrClient`, DI
- Add unit and integration tests

### Task 4: Watchlists Endpoints
- Create `IIbkrWatchlistApi`, `IIbkrWatchlistApiModels`
- Create `IWatchlistOperations`, `WatchlistOperations`
- Wire into `IIbkrClient`, `IbkrClient`, DI
- Add unit and integration tests

### Task 5: Audit and Cleanup
- Update `docs/endpoint-coverage-audit.md` to mark all 17 as done
- Run full build, test, format check

## Verification

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```
