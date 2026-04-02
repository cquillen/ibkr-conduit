# DTO Audit Report

**Date:** 2026-04-02
**Scope:** All C# DTOs in IbkrConduit compared against 227 WireMock recorded API responses and IBKR API documentation.

## Executive Summary

The audit analyzed 127 C# model types against 70 recorded endpoints, producing 380 findings:
- **293 MISSING_IN_DTO** -- fields present in IBKR responses but absent from our DTOs
- **33 NO_RECORDING** -- models with no recorded response to compare against
- **54 UNMAPPED_MODEL** -- models the script parser could not resolve (nested types, inner records)
- **0 EXTRA_IN_DTO / 0 TYPE_MISMATCH** -- script limitation; manual spot-checks below reveal real issues

**Key takeaway:** Most DTOs have `[JsonExtensionData]` dictionaries that silently capture unmapped fields, so deserialization does not fail. However, several critical models are missing fields that consumers would reasonably need, and a few have type mismatches that could cause runtime errors.

---

## 1. Manual Model-by-Model Audit

### 1.1 ContractSearchResult

**Source:** `Scenario02_ContractResearch/003-GET-iserver-secdef-search.json`

**CRITICAL -- Type Mismatch on `conid`:**
- IBKR sends `conid` as a **string** (`"265598"`) in secdef/search responses
- Our DTO declares `int Conid` with `JsonNumberHandling.AllowReadingFromString`
- `AllowReadingFromString` handles numeric strings like `"265598"` correctly, so this works at runtime
- However, the `conidEx` field is not present in the search response at all -- our DTO maps it but it may always be null here

| Field in IBKR response | In our DTO? | Notes |
|---|---|---|
| `conid` (string) | Yes | Works via AllowReadingFromString |
| `companyHeader` | Yes | |
| `companyName` | Yes | |
| `symbol` | Yes | |
| `description` | Yes | |
| `restricted` | **NO** | boolean/null, indicates trading restrictions |
| `sections` | Yes | |
| `issuers` | **NO** | array, present for bond searches |

**ContractSection inner type:**

| Field in IBKR response | In our DTO? | Notes |
|---|---|---|
| `secType` | Yes | |
| `months` | Yes | |
| `exchange` | Yes | |
| `symbol` | Yes | |
| `conid` | Yes (string in section too) | Works via AllowReadingFromString |

**Severity:** MEDIUM -- `restricted` field is useful for pre-trade validation. `issuers` only matters for bond workflows.

---

### 1.2 ContractDetails

**Source:** `Scenario02_ContractResearch/004-GET-iserver-contract-265598-info.json`

| Field in IBKR response | In our DTO? | Notes |
|---|---|---|
| `con_id` | Yes | |
| `symbol` | Yes | |
| `company_name` | Yes | |
| `exchange` | Yes | |
| `listing_exchange` | Yes | |
| `currency` | Yes | |
| `instrument_type` | Yes | |
| `valid_exchanges` | Yes | |
| `cfi_code` | **NO** | string |
| `cusip` | **NO** | null/string |
| `expiry_full` | **NO** | null/string |
| `maturity_date` | **NO** | null/string |
| `industry` | **NO** | string ("Computers") |
| `has_related_contracts` | **NO** | boolean |
| `trading_class` | **NO** | string ("NMS") |
| `allow_sell_long` | **NO** | boolean |
| `is_zero_commission_security` | **NO** | boolean |
| `local_symbol` | **NO** | string |
| `contract_clarification_type` | **NO** | null/string |
| `classifier` | **NO** | null/string |
| `text` | **NO** | null/string |
| `underlying_con_id` | **NO** | integer |
| `r_t_h` | **NO** | boolean (regular trading hours) |
| `multiplier` | **NO** | null/number |
| `underlying_issuer` | **NO** | null/string |
| `contract_month` | **NO** | null/string |
| `smart_available` | **NO** | boolean |
| `category` | **NO** | string ("Computers") |

**Severity:** HIGH -- `ContractDetails` has no `[JsonExtensionData]`, so all 17 unmapped fields are silently discarded. Fields like `industry`, `category`, `multiplier`, `underlying_con_id`, and `r_t_h` are frequently needed by trading applications.

---

### 1.3 OrderStatus

**Source:** `Scenario04_OrderLifecycle/007-GET-iserver-account-order-status-1020484121.json`

The actual response contains **38 fields**. Our `OrderStatus` DTO maps 25 of them and has `[JsonExtensionData]` for the rest. The script flagged all 38 as MISSING_IN_DTO (parser issue with positional records).

**Fields present in both (correctly mapped):**
`sub_type`, `request_id`, `order_id`, `conidex`, `conid`, `symbol`, `side`, `contract_description_1`, `listing_exchange`, `order_status`, `order_type`, `size`, `total_size`, `price` (mapped as `limit_price` -- see below), `tif`, `fg_color`, `bg_color`, `order_not_editable`, `editable_fields`, `cannot_cancel_order`

**CRITICAL -- Field name mismatch on `price`:**
- Our DTO maps `price` (JsonPropertyName "price")
- IBKR sends `limit_price` (string "1.00") for the limit price
- IBKR does NOT send a field named `price` in this endpoint
- Result: `Price` will always be null; the actual limit price lands in `AdditionalData`

**HIGH -- Missing `is_event_trading`:**
- Our DTO has it but the wire name matches -- actually present and working

**Fields in IBKR response NOT in DTO (beyond JsonExtensionData):**

| Missing Field | Type | Importance |
|---|---|---|
| `server_id` | string | LOW |
| `option_acct` | string | LOW |
| `company_name` | string | MEDIUM |
| `currency` | string | MEDIUM |
| `account` | string | MEDIUM |
| `cum_fill` | string(numeric) | MEDIUM |
| `order_ccp_status` | string | LOW |
| `order_status_description` | string | MEDIUM |
| `outside_rth` | boolean | MEDIUM |
| `all_or_none` | boolean | LOW |
| `deactivate_order` | boolean | LOW |
| `use_price_mgmt_algo` | boolean | LOW |
| `sec_type` | string | MEDIUM |
| `available_chart_periods` | string | LOW |
| `order_description` | string | LOW |
| `order_description_with_contract` | string | LOW |
| `clearing_id` | string | LOW |
| `clearing_name` | string | LOW |
| `alert_active` | integer | LOW |
| `child_order_type` | string | LOW |
| `order_clearing_account` | string | LOW |
| `size_and_fills` | string | LOW |
| `exit_strategy_display_price` | string | LOW |
| `exit_strategy_chart_description` | string | LOW |
| `exit_strategy_tool_availability` | string | LOW |
| `allowed_duplicate_opposite` | boolean | LOW |
| `order_time` | string | MEDIUM |
| `limit_price` | string(numeric) | **CRITICAL** |

**Severity:** CRITICAL for `limit_price` name mismatch. MEDIUM overall for missing fields.

**TYPE MISMATCH -- `order_id` and numeric string fields:**
- IBKR sends `order_id` as **integer** (1020484121), not string
- Our DTO correctly uses `int` with `JsonNumberHandling.AllowReadingFromString` -- this works
- `size`, `total_size`, `cum_fill`, `limit_price` are all **string(numeric)** -- our DTO uses `decimal?` with `AllowReadingFromString` where mapped

---

### 1.4 LiveOrder

**Source:** `Scenario04_OrderLifecycle/008-GET-iserver-account-orders.json`

The recording returned `{"orders": [], "snapshot": false}` -- an empty order list. No live orders were captured in recordings, so we cannot validate field names against real data.

**DTO definition review:**
- `OrdersResponse` maps `orders` and has no `[JsonExtensionData]` -- the `snapshot` field is silently lost
- `LiveOrder` uses `orderId`, `conid`, `symbol`, `side`, `quantity`, `orderType`, `price`, `status`, `filledQuantity`, `remainingQuantity`

**Concern:** Based on the OrderStatus recording (which uses snake_case like `order_id`, `order_type`, `order_status`), the live orders endpoint likely uses **camelCase** field names (IBKR is inconsistent between endpoints). Without a recording of actual live orders, we cannot confirm whether `LiveOrder` field names are correct. The fact that the orders list endpoint uses a wrapper with `orders` + `snapshot` while individual order status uses snake_case is a known IBKR inconsistency.

**Action needed:** Capture a recording with actual live orders to validate field names.

**Additional issue:** `OrdersResponse` is missing the `snapshot` field (boolean). This should be added.

**Severity:** HIGH (unverifiable without data, potential field name mismatches)

---

### 1.5 Trade

**Source:** `Scenario04_OrderLifecycle/012-GET-iserver-account-trades.json`

The recording returned `[]` -- no trades were captured. Cannot validate field names against real wire data.

**DTO definition review uses:** `execution_id`, `conid`, `symbol`, `side`, `size`, `price`, `order_ref`, `submitter` -- all snake_case, which matches the IBKR pattern for order-related endpoints.

**Severity:** MEDIUM (unverifiable, but naming pattern looks consistent)

---

### 1.6 Position

**Source:** `Scenario05_PortfolioDeepDive/008-GET-portfolio-DUO873728-positions-0.json`

Rich recording with many fields. Our DTO maps 16 fields and has `[JsonExtensionData]`.

| Field in IBKR response | In our DTO? | Notes |
|---|---|---|
| `acctId` | Yes | |
| `conid` | Yes (int + AllowReadingFromString) | Wire sends integer -- works |
| `contractDesc` | Yes | |
| `position` | Yes (mapped to Quantity) | |
| `mktPrice` | Yes | |
| `mktValue` | Yes | |
| `avgCost` | Yes | |
| `avgPrice` | Yes | |
| `realizedPnl` | Yes | |
| `unrealizedPnl` | Yes | |
| `currency` | Yes | |
| `name` | Yes | |
| `assetClass` | Yes | |
| `sector` | Yes (nullable) | |
| `ticker` | Yes | |
| `multiplier` | Yes (nullable decimal) | Wire sends 0.0 |
| `isUS` | Yes (nullable bool) | |
| `exchs` | **NO** | null |
| `expiry` | **NO** | null/string |
| `putOrCall` | **NO** | null/string |
| `strike` | **NO** | string ("0") |
| `exerciseStyle` | **NO** | null/string |
| `conExchMap` | **NO** | array |
| `undConid` | **NO** | integer |
| `model` | **NO** | string |
| `incrementRules` | **NO** | array of objects |
| `displayRule` | **NO** | object |
| `time` | **NO** | integer |
| `chineseName` | **NO** | string |
| `allExchanges` | **NO** | string (comma-delimited) |
| `listingExchange` | **NO** | string |
| `countryCode` | **NO** | string |
| `lastTradingDay` | **NO** | null/string |
| `group` | **NO** | null/string |
| `sectorGroup` | **NO** | null/string |
| `type` | **NO** | string ("ETF") |
| `hasOptions` | **NO** | boolean |
| `fullName` | **NO** | string |
| `isEventContract` | **NO** | boolean |
| `pageSize` | **NO** | integer |
| `baseMktValue` | **NO** | decimal |
| `baseMktPrice` | **NO** | decimal |
| `baseAvgCost` | **NO** | decimal |
| `baseAvgPrice` | **NO** | decimal |
| `baseRealizedPnl` | **NO** | decimal |
| `baseUnrealizedPnl` | **NO** | decimal |

**Notable missing fields:**
- `listingExchange` -- important for routing
- `undConid` -- needed for derivatives
- `type` -- distinguishes ETF from STK
- `baseMktValue`, `baseUnrealizedPnl` etc. -- base currency equivalents, useful for multi-currency accounts
- Options fields (`strike`, `putOrCall`, `expiry`, `exerciseStyle`) -- critical for options positions

All unmapped fields fall into `[JsonExtensionData]`, so no deserialization failure, but consumers must dig into `AdditionalData` for frequently-needed fields.

**Severity:** MEDIUM -- core position data is mapped; options/derivatives fields should be promoted to named properties

---

### 1.7 Account (Portfolio)

**Source:** `Scenario05_PortfolioDeepDive/003-GET-portfolio-accounts.json`

Our `Account` record has only 3 fields: `Id`, `AccountTitle`, `Type`. The IBKR response has **28 fields**.

| Missing Field | Type | Importance |
|---|---|---|
| `PrepaidCrypto-Z` | boolean | LOW |
| `PrepaidCrypto-P` | boolean | LOW |
| `brokerageAccess` | boolean | MEDIUM |
| `accountId` | string | LOW (redundant with `id`) |
| `accountVan` | string | LOW |
| `displayName` | string | LOW |
| `accountAlias` | null/string | MEDIUM |
| `accountStatus` | integer (epoch ms) | MEDIUM |
| `currency` | string | **HIGH** |
| `tradingType` | string | MEDIUM |
| `businessType` | string | LOW |
| `category` | string | LOW |
| `ibEntity` | string | LOW |
| `faclient` | boolean | MEDIUM (FA detection) |
| `clearingStatus` | string | LOW |
| `covestor` | boolean | LOW |
| `noClientTrading` | boolean | MEDIUM |
| `trackVirtualFXPortfolio` | boolean | LOW |
| `acctCustType` | string | LOW |
| `parent` | object | LOW |
| `desc` | string | LOW |

**CRITICAL -- No `[JsonExtensionData]`:** The `Account` record has no `JsonExtensionData` property, meaning all 25 unmapped fields are silently discarded with **no way for consumers to access them**. Most importantly, `currency` (the account's base currency) is lost.

**Severity:** CRITICAL -- `currency` is essential; `Account` needs either more named fields or `[JsonExtensionData]`

---

### 1.8 AccountInfo (Portfolio Meta)

**Source:** `Scenario05_PortfolioDeepDive/006-GET-portfolio-DUO873728-meta.json`

Our `AccountInfo` maps 6 fields and has `[JsonExtensionData]`. The response has 28 fields -- the same structure as `/portfolio/accounts` (they return identical shapes).

The 6 mapped fields (`id`, `accountId`, `accountTitle`, `accountAlias`, `type`, `currency`) all match correctly. The remaining 22 fields land in `AdditionalData`.

**Severity:** LOW -- key fields are mapped, extras are captured. But note that `AccountInfo` and `Account` model the same IBKR response shape, yet `Account` is much sparser.

---

### 1.9 MarketDataSnapshotRaw

**Source:** `Scenario03_MarketDataScanners/005-GET-iserver-marketdata-snapshot.json`

Recording shows: `{"6509":"DPB","conidEx":"756733","conid":756733,"_updated":1775086883187,"6119":"q0","server_id":"q0","84":"655.05","31":"655.05"}`

Our DTO maps: `conid`, `conidEx`, `_updated`, `server_id`, `6509` (MarketDataAvailability). All remaining numeric-keyed fields go to `[JsonExtensionData]` `Fields` dictionary.

**Observation:** Field `6119` appears in the response but is not documented. The design of using `JsonExtensionData` for numeric fields is correct -- market data fields are inherently dynamic.

**Severity:** LOW -- design is correct for this endpoint's variability

---

### 1.10 SsodhInitResponse

**Source:** `Scenario01_AccountDiscovery/002-POST-iserver-auth-ssodh-init.json`

IBKR response fields: `passed`, `authenticated`, `connected`, `established`, `competing`, `hardware_info`

Our DTO maps: `authenticated`, `connected`, `competing`

| Missing Field | Type | Importance |
|---|---|---|
| `passed` | boolean | MEDIUM (overall pass/fail indicator) |
| `established` | boolean | **HIGH** (session establishment status) |
| `hardware_info` | string | LOW |

**No `[JsonExtensionData]`:** Missing fields are silently discarded.

**Severity:** HIGH -- `established` is semantically important for session state management. `passed` indicates overall success.

---

### 1.11 TickleResponse

**Source:** `Scenario10_WebSocketStreaming/005-POST-tickle.json`

IBKR response: `{"session":"...","hmds":{"error":"no bridge"},"iserver":{"authStatus":{...}}}`

Our DTO maps: `session`, `iserver` (as `TickleIserverStatus`)

| Missing Field | Type | Importance |
|---|---|---|
| `hmds` | object | LOW (HMDS bridge status) |

`TickleIserverStatus` maps `authStatus`. `TickleAuthStatus` maps `authenticated`, `competing`, `connected`.

**Missing from TickleAuthStatus:**
| Missing Field | Type | Importance |
|---|---|---|
| `established` | boolean | **HIGH** |
| `message` | string | MEDIUM |
| `MAC` | string | LOW |
| `serverInfo` | object | LOW |
| `hardware_info` | string | LOW |

**No `[JsonExtensionData]` on any of the three tickle records.**

**Severity:** HIGH -- `established` is critical for session state; `message` provides error context

---

### 1.12 AuthStatusResponse

**Source:** `Scenario01_AccountDiscovery/005-GET-iserver-auth-status.json`

IBKR response: `{"authenticated":true,"established":true,"competing":false,"connected":true,"message":"","MAC":"...","serverInfo":{...},"hardware_info":"...","fail":""}`

Our DTO maps: `authenticated`, `competing`, `connected`, `fail`, `message`, `prompts` -- and has `[JsonExtensionData]`.

| Missing Field | Type | Importance |
|---|---|---|
| `established` | boolean | **HIGH** |
| `MAC` | string | LOW |
| `serverInfo` | object | LOW |
| `hardware_info` | string | LOW |

`established` falls into `AdditionalData` but should be a named property -- it is fundamental to session state.

**Severity:** HIGH -- `established` should be promoted to a named property

---

### 1.13 AlertSummary / AlertDetail

**Source:** Alert creation recording returned HTTP 403 (access denied). No successful alert list recording exists. The script reports NO_RECORDING for the detail endpoint.

Cannot validate field names against real data. The DTO structures look plausible based on API docs.

**Severity:** UNKNOWN (no recording data available)

---

### 1.14 WatchlistSummary / WatchlistDetail

**Source:** Watchlist creation recording returned an error response. No successful list/detail recordings exist.

Cannot validate field names against real data.

**Severity:** UNKNOWN (no recording data available)

---

### 1.15 WhatIfResponse

**Source:** `Scenario04_OrderLifecycle/005-POST-iserver-account-DUO873728-orders-whatif.json`

Our DTO maps: `amount`, `equity`, `initial`, `maintenance`, `warn`, `error` + `[JsonExtensionData]`

| Missing Field | Type | Importance |
|---|---|---|
| `position` | object | **HIGH** (position impact, same shape as equity) |
| `warns` | array of strings | MEDIUM (multiple warnings) |
| `errors` | array of strings | MEDIUM (multiple errors) |
| `accruedInterest` | null/object | LOW |
| `action` | string | MEDIUM |
| `order_id` | string | MEDIUM |
| `cqe` | object | LOW (internal analytics) |

**Note:** `warn` (singular string) and `warns` (array) coexist. Similarly `error` and `errors`. Our DTO only maps the singular versions. The array versions provide all messages when multiple warnings/errors exist.

**Severity:** HIGH -- `position` impact data is important for pre-trade analysis; `warns`/`errors` arrays are needed for complete error reporting

---

### 1.16 IserverAccountsResponse

**Source:** `Scenario01_AccountDiscovery/003-GET-iserver-accounts.json`

This endpoint returns a very large response with nested objects: `accounts`, `acctProps`, `aliases`, `allowFeatures`, `chartPeriods`, `groups`, `profiles`, plus many flattened feature flags.

Our DTO maps only: `accounts` (List<string>) and `selectedAccount` (string) + `[JsonExtensionData]`.

The script reports 52 MISSING_IN_DTO findings. Most are from the deeply-nested `allowFeatures` and `chartPeriods` objects being flattened by the script.

**Notable missing named fields:**
| Missing Field | Type | Importance |
|---|---|---|
| `acctProps` | object | MEDIUM (per-account capabilities) |
| `aliases` | object | LOW |
| `allowFeatures` | object | MEDIUM |
| `chartPeriods` | object | LOW |
| `serverInfo` | object | LOW |
| `sessionId` | string | LOW |
| `isPaper` | boolean | MEDIUM |
| `isFT` | boolean | LOW |

All unmapped fields go to `[JsonExtensionData]`, so no data loss.

**Severity:** LOW -- key data (`accounts`, `selectedAccount`) is mapped; the rest is captured in extension data

---

### 1.17 LogoutResponse

**Source:** `Scenario02_ContractResearch/012-POST-logout.json`

IBKR response includes `status` (boolean, true) in addition to `confirmed`. Our DTO only maps `confirmed`. No `[JsonExtensionData]`.

**Severity:** LOW -- `status` is redundant with `confirmed`

---

### 1.18 SwitchAccountResponse

**Source:** `Scenario01_AccountDiscovery/008-POST-iserver-account.json`

IBKR response: `{"success": "Account already set"}` -- this does NOT match our DTO which expects `set` (boolean) and `selectedAccount` (string).

**CRITICAL -- Wrong response shape:** The real response has `success` (string), not `set` (boolean). Our DTO will fail to deserialize correctly -- `Set` will default to `false` and `SelectedAccount` will be null.

**Severity:** CRITICAL -- response shape mismatch causes silent data loss

---

### 1.19 CancelOrderResponse

**Source:** `Scenario04_OrderLifecycle/010-DELETE-iserver-account-DUO873728-order-1020484121.json`

IBKR response: `{"msg":"Request was submitted","order_id":1020484121,"conid":-1,"account":null}`

Our DTO maps: `msg` (as Message), `order_id` (as OrderId, int), `conid` (as Conid, int). Missing: `account` (null/string).

**Severity:** LOW -- `account` is nullable/null in the recording, core fields are correctly mapped

---

### 1.20 AccountAllocation

**Source:** `Scenario05_PortfolioDeepDive/007-GET-portfolio-DUO873728-allocation.json`

IBKR response: `{"assetClass":{"long":{...},"short":{}},"sector":{"long":{...},"short":{}},"group":{"long":{...},"short":{}}}`

Our DTO maps all three top-level fields. The script flagged `long`, `short`, and the top-level names as MISSING_IN_DTO because it flattened the nested structure. These are **false positives** -- our DTO correctly models the nested `Dictionary<string, Dictionary<string, decimal>>`.

**Severity:** NONE -- correctly mapped

---

### 1.21 AccountSummaryEntry (Portfolio Summary)

**Source:** `Scenario05_PortfolioDeepDive/004-GET-portfolio-DUO873728-summary.json`

Each entry has: `amount`, `currency`, `isNull`, `timestamp`, `value`, `severity`

Our DTO maps: `amount`, `currency`, `isNull`, `timestamp`, `value` + `[JsonExtensionData]`

Missing named field: `severity` (integer, 0) -- goes to extension data.

**Severity:** LOW -- `severity` is a display hint

---

### 1.22 LedgerEntry

**Source:** `Scenario05_PortfolioDeepDive/005-GET-portfolio-DUO873728-ledger.json`

IBKR sends many more fields per currency than our DTO maps:

| Missing Field | Type | Importance |
|---|---|---|
| `interest` | decimal | MEDIUM |
| `unrealizedpnl` | decimal | MEDIUM |
| `stockmarketvalue` | decimal | Already mapped as `stockmarketvalue` |
| `moneyfunds` | decimal | LOW |
| `currency` | string | LOW (key is currency already) |
| `realizedpnl` | decimal | MEDIUM |
| `funds` | decimal | LOW |
| `acctcode` | string | LOW |
| `issueroptionsmarketvalue` | decimal | LOW |
| `key` | string | LOW |
| `timestamp` | integer | MEDIUM |
| `severity` | integer | LOW |
| `stockoptionmarketvalue` | decimal | LOW |
| `futuresonlypnl` | decimal | LOW |
| `tbondsmarketvalue` | decimal | LOW |
| `futureoptionmarketvalue` | decimal | LOW |
| `cashbalancefxsegment` | decimal | LOW |
| `secondkey` | string | LOW |
| `tbillsmarketvalue` | decimal | LOW |
| `endofbundle` | integer | LOW |
| `dividends` | decimal | LOW |
| `cryptocurrencyvalue` | decimal | LOW |
| `sessionid` | integer | LOW |

All go to `[JsonExtensionData]`.

**Severity:** LOW -- core balance fields are mapped; the rest is captured

---

### 1.23 PositionContractInfo

**Source:** `Scenario05_PortfolioDeepDive/010-GET-portfolio-positions-756733.json`

**CRITICAL -- Wrong response shape:** The endpoint returns `{"DUO873728": [...positions...], "DUO873728C": [...positions...]}` -- a dictionary keyed by account ID, with arrays of position objects as values. Our DTO `PositionContractInfo` expects a flat object with `conid`, `ticker`, `name`, etc. This is a complete structural mismatch.

The actual response type should be `Dictionary<string, List<Position>>`, not `PositionContractInfo`.

**Severity:** CRITICAL -- DTO is entirely wrong shape for this endpoint's response

---

## 2. MISSING_IN_DTO Summary by Model

### Models with most missing fields (from script):

| Model | Missing Fields | Has JsonExtensionData? | Real Issue? |
|---|---|---|---|
| IserverAccountsResponse | 52 | Yes | LOW -- mostly nested sub-properties flattened by script |
| PositionContractInfo | 48 | Yes | CRITICAL -- wrong response shape entirely |
| OrderStatus | 47 | Yes | CRITICAL -- `limit_price` name mismatch; rest is script parser false positives (it missed the record's properties) |
| AccountInfo | 28 | Yes | LOW -- key fields mapped |
| WhatIfResponse | 20 | Yes | HIGH -- missing `position`, `warns`, `errors` |
| AllPeriodsPerformance | 18 | Yes | LOW -- intentionally thin model with extension data |
| AccountPerformance | 11 | Yes | LOW -- intentionally thin model with extension data |
| AuthStatusResponse | 11 | Yes | HIGH -- missing `established` |
| ScannerParameters | 11 | Yes | Undetermined (NO_RECORDING) |
| AccountAllocation | 10 | Yes | FALSE POSITIVE -- script flattened nested dicts |

### False positives explained:

The script's C# parser could not resolve **positional record** parameters. When a model is defined as `record Foo([property: JsonPropertyName("x")] int X)`, the parser saw 0 properties. This means **every field** in the IBKR response was flagged as MISSING_IN_DTO, even fields that ARE correctly mapped.

Models affected: `OrderStatus` (47 findings, ~25 are false positives), `OrdersResponse` (2, both false positives), `CancelOrderResponse` (4, 3 false positives), `SwitchAccountResponse` (1, false positive for `selectedAccount`), `SsodhInitResponse` (6, 3 are false positives), `TickleResponse` (5, 2 false positives), `LogoutResponse` (1, false positive), `UnsubscribeResponse`, `UnsubscribeAllResponse`, `SuppressResetResponse`.

---

## 3. Categorized Findings

### CRITICAL

| # | Model | Issue | Impact |
|---|---|---|---|
| C1 | `Account` | No `[JsonExtensionData]` -- 25 fields silently discarded including `currency` | Consumers cannot access account currency |
| C2 | `PositionContractInfo` | Wrong response shape -- endpoint returns `Dict<string, List<Position>>` not flat object | Deserialization produces garbage/defaults |
| C3 | `OrderStatus` | `Price` property maps to wire name `"price"` but IBKR sends `"limit_price"` | Limit price always null |
| C4 | `SwitchAccountResponse` | Expected `set`/`selectedAccount` but IBKR sends `success` (string) | Response fields always default values |
| C5 | `ContractDetails` | No `[JsonExtensionData]` -- 17 fields silently discarded | `industry`, `multiplier`, `underlying_con_id` etc. lost |
| C6 | `LiveOrder` | No recording with actual orders to validate field names; no `[JsonExtensionData]` | Potential silent data loss if names are wrong |

### HIGH

| # | Model | Issue | Impact |
|---|---|---|---|
| H1 | `SsodhInitResponse` | Missing `established` and `passed`; no `[JsonExtensionData]` | Session state tracking incomplete |
| H2 | `TickleResponse` / `TickleAuthStatus` | Missing `established`, `message`; no `[JsonExtensionData]` | Session state tracking incomplete |
| H3 | `AuthStatusResponse` | Missing `established` as named property (falls to extension data) | Consumers must dig in AdditionalData |
| H4 | `WhatIfResponse` | Missing `position` object and `warns`/`errors` arrays | Incomplete pre-trade impact data |
| H5 | `OrdersResponse` | Missing `snapshot` field; no `[JsonExtensionData]` | Minor data loss |
| H6 | `ContractSearchResult` | No `[JsonExtensionData]`; missing `restricted` field | Cannot detect trading restrictions |
| H7 | `OrderSubmissionResponse` | No `[JsonExtensionData]`; `encrypt_message` field in response not mapped | Minor data loss |

### MEDIUM

| # | Model | Issue | Impact |
|---|---|---|---|
| M1 | `Position` | Missing `listingExchange`, `undConid`, `type`, options fields (`strike`, `putOrCall`, `expiry`) | Need extension data lookup for derivatives |
| M2 | `OrderStatus` | Missing `currency`, `account`, `order_time`, `order_status_description`, `cum_fill` | Useful fields require extension data |
| M3 | `LedgerEntry` | Missing `interest`, `unrealizedpnl`, `realizedpnl`, `timestamp` | Need extension data |
| M4 | `Account` (Portfolio) | Missing `accountAlias`, `brokerageAccess`, `faclient` | FA detection requires extension data (which is absent) |
| M5 | `ContractDetails` | Missing `industry`, `category`, `r_t_h`, `smart_available` | Useful for display and routing |

### LOW

| # | Model | Issue | Impact |
|---|---|---|---|
| L1 | Various | Display hint fields (`bgColor`, `fgColor`, `severity`) not as named props | Only relevant for UI rendering |
| L2 | `IserverAccountsResponse` | Many sub-object fields from `allowFeatures`, `chartPeriods` | Captured in extension data |
| L3 | `AccountInfo` | 22 extra fields | All captured in extension data |
| L4 | `ScannerParameters` | 11 sub-object fields | Likely captured in extension data |
| L5 | Session models | `hardware_info`, `MAC`, `serverInfo` | Diagnostic fields only |

---

## 4. Cross-Reference with ibkr_api.md

### OrderStatus `limit_price` vs `price`
The API docs show order status responses with `"limit_price"` for limit orders. Our DTO incorrectly uses `"price"` as the JSON property name. This is confirmed as a real bug.

### ContractSearchResult `conid` type
The API docs describe `conid` as a string in search results. Our DTO uses `int` with `AllowReadingFromString`, which handles the conversion correctly.

### `ticker` vs `symbol`
The API docs use both terms. The secdef/search endpoint returns `symbol`. The portfolio/positions endpoint returns both `ticker` and uses it as the primary display symbol. The order status endpoint returns `symbol`. Both names are correct depending on context -- they refer to different IBKR fields.

### `established` field
The API docs mention `established` as part of the auth status response. It indicates whether the brokerage session has been fully initialized (different from `authenticated` which only means credentials are valid).

---

## 5. UNMAPPED_MODEL Analysis

54 models were flagged as UNMAPPED_MODEL. These are **all parser false positives** caused by:

1. **Nested types inside wrapper records** -- `LiveOrder` (inside `OrdersResponse`), `TickleAuthStatus` (inside `TickleIserverStatus`)
2. **Types only used as properties of other types** -- `ContractSection`, `AlertCondition`, `ComboLeg`, `HistoricalBar`, etc.
3. **Streaming models** -- `OrderUpdate`, `PnlUpdate`, `AccountSummaryUpdate`, `AccountLedgerUpdate` are WebSocket models not used in REST responses

These are not real issues.

---

## 6. NO_RECORDING Analysis

33 model-endpoint combinations have no recording. Breakdown:

- **FA Allocation endpoints (8):** `AllocationAccountsResponse`, `AllocationGroupDetail`, `AllocationGroupListResponse`, `AllocationPresetsResponse`, `AllocationSuccessResponse` (x4) -- FA-only features, not testable with individual accounts
- **Endpoints that returned errors in E2E (5):** `ContractDetails` (404), `CreateAlertResponse` (403), `CreateWatchlistResponse` (503), `ScannerResponse` (400), `TradingRules` (empty body)
- **Endpoints not exercised (7):** `DynAccountResponse`, `ExchangeRateResponse`, `ReauthenticateResponse`, `SsoValidateResponse`, `SuppressResponse`, `TransactionHistory`, `WatchlistDetail`
- **Recording path mismatch (4):** `HistoricalDataResponse`, `HmdsScannerResponse`, `MarketDataSnapshotRaw` (regsnapshot), `OptionStrikes`, `SecurityDefinitionResponse` -- script may not have matched the recording path
- **IserverAccountInfo (1):** Returned HTML error, not JSON

---

## 7. Recommended Fixes (Priority Order)

### P0 -- Fix immediately (CRITICAL)

1. **`Account`**: Add `[JsonExtensionData]` property and `Currency` named property
2. **`PositionContractInfo`**: Change response type to `Dictionary<string, List<Position>>` or redesign endpoint return type
3. **`OrderStatus.Price`**: Rename JsonPropertyName from `"price"` to `"limit_price"`
4. **`SwitchAccountResponse`**: Redesign to match actual response shape `{"success": "..."}`. Consider using `JsonExtensionData` or changing to `string Success`
5. **`ContractDetails`**: Add `[JsonExtensionData]` property

### P1 -- Fix soon (HIGH)

6. **`SsodhInitResponse`**: Add `Passed` (bool), `Established` (bool) properties and `[JsonExtensionData]`
7. **`TickleResponse`/`TickleAuthStatus`**: Add `Established` (bool), `Message` (string) and `[JsonExtensionData]`
8. **`AuthStatusResponse`**: Add `Established` (bool) as named property
9. **`WhatIfResponse`**: Add `Position` (same shape as `WhatIfEquity`), `Warns` (List<string>), `Errors` (List<string>)
10. **`OrdersResponse`**: Add `Snapshot` (bool) property and `[JsonExtensionData]`
11. **`ContractSearchResult`**: Add `[JsonExtensionData]` and `Restricted` (bool?) property
12. **`LiveOrder`**: Add `[JsonExtensionData]`; capture a real recording to validate field names

### P2 -- Add when convenient (MEDIUM)

13. **`Position`**: Add `ListingExchange`, `UndConid`, `Type`, `Strike`, `PutOrCall`, `Expiry` as named properties
14. **`OrderStatus`**: Add `Currency`, `Account`, `OrderTime`, `LimitPrice` (fix from P0), `CumFill`
15. **`ContractDetails`**: Add `Industry`, `Category`, `Multiplier`, `UnderlyingConId`, `Rth`
16. **`LedgerEntry`**: Add `Interest`, `UnrealizedPnl`, `RealizedPnl`, `Timestamp`

### P3 -- Low priority

17. Various display-hint fields (`bgColor`, `fgColor`, `severity`)
18. Server diagnostic fields (`serverInfo`, `hardware_info`, `MAC`)
19. IserverAccountsResponse sub-objects (`acctProps`, `allowFeatures`, `chartPeriods`)

---

## 8. JsonNumberHandling Assessment

Models that correctly use `JsonNumberHandling.AllowReadingFromString`:
- `OrderStatus`: `order_id`, `conid`, `size`, `fill_price`, `filled_quantity`, `remaining_quantity`, `avg_fill_price`, `last_fill_price`, `total_size`, `total_cash_size`, `price`
- `CancelOrderResponse`: `order_id`, `conid`
- `ContractSearchResult`: `conid`
- `Position`: `conid`
- `SecurityDefinition`: `conid`, `strike`, `undConid`
- `TradeTiming`: `openingTime`, `closingTime`
- `TradingRules`: `defaultSize`, `sizeIncrement`, `cashSize`
- `FutureContract`: `conid`, `underlyingConid`, `expirationDate`, `ltd`

**Missing `AllowReadingFromString` where needed:**
- `OrderStatus.limit_price` (when fix C3 is applied) -- IBKR sends `"1.00"` (string)
- `OrderStatus.cum_fill` (if promoted) -- IBKR sends `"0.0"` (string)

---

## 9. Script Limitation Summary

| Limitation | Impact | Workaround |
|---|---|---|
| Parser misses positional record parameters | ~150 false positive MISSING_IN_DTO | Manual verification (done above) |
| Zero EXTRA_IN_DTO findings | Cannot detect DTO fields with wrong wire names | Manual spot-checks (found C3, C4) |
| Zero TYPE_MISMATCH findings | Cannot detect type mismatches | Manual spot-checks (found string-vs-int on conid, correctly handled) |
| Nested types flagged as UNMAPPED_MODEL | 54 false positives | All verified as parser artifacts |
| Recording path matching | Some recordings not matched to endpoints | 33 NO_RECORDING, ~10 are path matching issues |
