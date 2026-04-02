# IBKR API Documentation vs. Actual Recorded Responses

**Date:** 2026-04-02
**Method:** Manual comparison of documented response fields (`docs/ibkr_api.md`) against actual WireMock-recorded API responses from E2E tests.

---

## Table of Contents

1. [GET /iserver/account/orders (Live Orders)](#1-get-iserveraccountorders-live-orders)
2. [GET /iserver/account/order/status/{orderId}](#2-get-iserveraccountorderstatusorderid)
3. [GET /portfolio/{accountId}/positions/{page}](#3-get-portfolioaccountidpositionspage)
4. [GET /portfolio/accounts](#4-get-portfolioaccounts)
5. [GET /iserver/secdef/search](#5-get-iserversecdefsearch)
6. [GET /iserver/contract/{conid}/info](#6-get-iservercontractconidinfo)
7. [POST /iserver/account/{accountId}/orders (Place Order)](#7-post-iserveraccountaccountidorders-place-order)
8. [GET /iserver/marketdata/snapshot](#8-get-iservermarketdatasnapshot)
9. [POST /tickle](#9-post-tickle)
10. [POST /iserver/auth/ssodh/init](#10-post-iserverauthssodhinit)

**Discrepancy Legend:**
- **UNDOCUMENTED** -- field present in recording but not described in docs
- **WRONG_TYPE** -- docs declare one type, actual response uses a different type
- **MISSING** -- docs describe the field but it does not appear in recording
- **WRONG_NAME** -- docs use a different field name than the actual response

---

## 1. GET /iserver/account/orders (Live Orders)

**Doc location:** Line 5054
**Recording:** `Scenario04_OrderLifecycle/008-GET-iserver-account-orders.json`

The recording returned `{"orders":[],"snapshot":false}` (empty orders array). Field analysis is based on the documented response schema and the order status recording (which shares the same order object shape).

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| orders | Yes | Array | Yes | Array | `[]` | -- |
| snapshot | Yes | bool | Yes | bool | `false` | -- |
| acct | Yes | String | N/A (empty) | -- | -- | See note 1 |
| conidex | Yes | String | N/A | -- | -- | -- |
| conid | Yes | int | N/A | -- | -- | -- |
| orderId | Yes | int | N/A | -- | -- | -- |
| cashCcy | Yes | String | N/A | -- | -- | -- |
| sizeAndFills | Yes | String | N/A | -- | -- | -- |
| orderDesc | Yes | String | N/A | -- | -- | -- |
| description1 | Yes | String | N/A | -- | -- | -- |
| ticker | Yes | String | N/A | -- | -- | -- |
| secType | Yes | String | N/A | -- | -- | -- |
| listingExchange | Yes | String | N/A | -- | -- | -- |
| remainingQuantity | Yes | float | N/A | -- | -- | -- |
| filledQuantity | Yes | float | N/A | -- | -- | -- |
| companyName | Yes | String | N/A | -- | -- | -- |
| status | Yes | String | N/A | -- | -- | -- |
| order_ccp_status | Yes | String | N/A | -- | -- | -- |
| origOrderType | Yes | String | N/A | -- | -- | -- |
| supportsTaxOpt | Yes | String | N/A | -- | -- | -- |
| lastExecutionTime | Yes | String | N/A | -- | -- | -- |
| orderType | Yes | String | N/A | -- | -- | -- |
| bgColor | Yes | String | N/A | -- | -- | -- |
| fgColor | Yes | String | N/A | -- | -- | -- |
| order_ref | Yes | String | N/A | -- | -- | -- |
| timeInForce | Yes | String | N/A | -- | -- | -- |
| lastExecutionTime_r | Yes | int | N/A | -- | -- | -- |
| side | Yes | String | N/A | -- | -- | -- |
| avgPrice | Yes | String | N/A | -- | -- | -- |
| account | No (only in example) | -- | N/A | -- | In example JSON | **UNDOCUMENTED** (in example but not in field list) |
| totalSize | No (only in example) | -- | N/A | -- | In example JSON | **UNDOCUMENTED** (in example but not in field list) |

**Note 1:** The orders array was empty in this recording. Cross-referencing with the order status endpoint recording (which returns individual order objects with snake_case fields), it is very likely that the live orders response uses **camelCase** field names while order status uses **snake_case** -- the docs do not call this out clearly.

---

## 2. GET /iserver/account/order/status/{orderId}

**Doc location:** Line 5212
**Recording:** `Scenario04_OrderLifecycle/007-GET-iserver-account-order-status-1020484121.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| sub_type | Yes | null | Yes | null | `null` | -- |
| request_id | Yes | String | Yes | String | `"4852491"` | -- |
| order_id | Yes | int | Yes | int | `1020484121` | -- |
| conidex | Yes | String | Yes | String | `"756733"` | -- |
| conid | Yes | int | Yes | int | `756733` | -- |
| symbol | Yes | String | Yes | String | `"SPY"` | -- |
| side | Yes | String | Yes | String | `"B"` | -- |
| contract_description_1 | Yes | String | Yes | String | `"SPY"` | -- |
| listing_exchange | Yes | String | Yes | String | `"ARCA"` | -- |
| option_acct | Yes | String | Yes | String | `"c"` | -- |
| company_name | Yes | String | Yes | String | `"SS SPDR S&P 500 ETF TRUST-US"` | -- |
| size | Yes | String | Yes | String | `"1.0"` | -- |
| total_size | Yes | String | Yes | String | `"1.0"` | -- |
| currency | Yes | String | Yes | String | `"USD"` | -- |
| account | Yes | String | Yes | String | `"DUO873728"` | -- |
| order_type | Yes | String | Yes | String | `"LIMIT"` | -- |
| cum_fill | Yes | String | Yes | String | `"0.0"` | -- |
| order_status | Yes | String | Yes | String | `"PreSubmitted"` | -- |
| order_ccp_status | Yes | String | Yes | String | `"0"` | -- |
| order_status_description | Yes | String | Yes | String | `"Order Submitted"` | -- |
| tif | Yes | String | Yes | String | `"GTC"` | -- |
| fg_color | Yes | String | Yes | String | `"#FFFFFF"` | -- |
| bg_color | Yes | String | Yes | String | `"#0000CC"` | -- |
| order_not_editable | Yes | bool | Yes | bool | `false` | -- |
| editable_fields | Yes | null | Yes | String | `"\u001e"` | **WRONG_TYPE** -- docs say null, actual is String |
| cannot_cancel_order | Yes | bool | Yes | bool | `false` | -- |
| deactivate_order | Yes | bool | Yes | bool | `false` | -- |
| sec_type | Yes | String | Yes | String | `"STK"` | -- |
| available_chart_periods | Yes | String | Yes | String | `"#R\|1"` | -- |
| order_description | Yes | String | Yes | String | `"Buy 1 Limit 1.00, GTC"` | -- |
| order_description_with_contract | Yes | String | Yes | String | `"Buy 1 SPY Limit 1.00, GTC"` | -- |
| alert_active | Yes | int | Yes | int | `1` | -- |
| child_order_type | Yes | String | Yes | String | `"3"` | -- |
| order_clearing_account | Yes | String | Yes | String | `"DUO873728"` | -- |
| size_and_fills | Yes | String | Yes | String | `"0/1"` | -- |
| exit_strategy_display_price | Yes | String | Yes | String | `"1.00"` | -- |
| exit_strategy_chart_description | Yes | String | Yes | String | `"Buy 1 Limit 1.00, GTC"` | -- |
| exit_strategy_tool_availability | No | -- | Yes | String | `"1"` | **UNDOCUMENTED** |
| allowed_duplicate_opposite | No | -- | Yes | bool | `true` | **UNDOCUMENTED** |
| order_time | No | -- | Yes | String | `"260401231429"` | **UNDOCUMENTED** |
| server_id | No | -- | Yes | String | `"476341"` | **UNDOCUMENTED** |
| limit_price | No | -- | Yes | String | `"1.00"` | **UNDOCUMENTED** |
| outside_rth | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| all_or_none | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| use_price_mgmt_algo | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| clearing_id | No | -- | Yes | String | `"IB"` | **UNDOCUMENTED** |
| clearing_name | No | -- | Yes | String | `"IB"` | **UNDOCUMENTED** |
| average_price | Docs example only | -- | No | -- | -- | **MISSING** in this recording (appears in filled orders per docs example as `"average_price"`) |

**Notes:**
- The docs truncate the field description for `exit_strategy_chart_description` mid-sentence (line 5357), then jump directly into the example JSON without closing the field list. This makes it ambiguous whether `exit_strategy_tool_availability`, `allowed_duplicate_opposite`, and `order_time` were intended to be documented.
- `editable_fields` is documented as `null` type but the actual response returns a String containing a control character (`\u001e`).
- `server_id` is present in the recording but not in the documented field list (though it is in the example JSON for the auth/status endpoint).
- `limit_price` is a critical field for limit orders but is completely undocumented.
- `outside_rth`, `all_or_none`, `use_price_mgmt_algo` are useful order attribute fields that are undocumented.

---

## 3. GET /portfolio/{accountId}/positions/{page}

**Doc location:** Line 7330
**Recording:** `Scenario05_PortfolioDeepDive/008-GET-portfolio-DUO873728-positions-0.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| acctId | Yes | String | Yes | String | `"DUO873728"` | -- |
| conid | Yes | int | Yes | int | `756733` | -- |
| contractDesc | Yes | String | Yes | String | `"SPY"` | -- |
| position | Yes | float | Yes | float | `22.0` | -- |
| mktPrice | Yes | float | Yes | float | `654.94` | -- |
| mktValue | Yes | float | Yes | float | `14408.68` | -- |
| avgCost | Yes | float | Yes | float | `654.19` | -- |
| avgPrice | Yes | float | Yes | float | `654.19` | -- |
| realizedPnl | Yes | float | Yes | float | `0.0` | -- |
| unrealizedPnl | Yes | float | Yes | float | `16.51` | -- |
| exchs | Yes | null | Yes | null | `null` | -- |
| currency | Yes | String | Yes | String | `"USD"` | -- |
| time | Yes | int | Yes | int | `34` | -- |
| chineseName | Yes | String | Yes | String | HTML entities | -- |
| allExchanges | Yes | String | Yes | String | Long comma-separated list | -- |
| listingExchange | Yes | String | Yes | String | `"ARCA"` | -- |
| countryCode | Yes | String | Yes | String | `"US"` | -- |
| name | Yes | String | Yes | String | `"SS SPDR S&P 500 ETF TRUST-US"` | -- |
| assetClass | Yes | String | Yes | String | `"STK"` | -- |
| expiry | Yes | String | Yes | null | `null` | -- |
| lastTradingDay | Yes | String | Yes | null | `null` | -- |
| group | Yes | String | Yes | null | `null` | -- |
| putOrCall | Yes | String | Yes | null | `null` | -- |
| sector | Yes | String | Yes | null | `null` | -- |
| sectorGroup | Yes | String | Yes | null | `null` | -- |
| strike | Yes | String | Yes | String | `"0"` | -- |
| ticker | Yes | String | Yes | String | `"SPY"` | -- |
| undConid | Yes | int | Yes | int | `0` | -- |
| multiplier | Yes | float | Yes | float | `0.0` | **WRONG_TYPE** -- docs example shows `null`, recording shows `0.0` |
| type | Yes | String | Yes | String | `"ETF"` | -- |
| hasOptions | Yes | bool | Yes | bool | `true` | -- |
| fullName | Yes | String | Yes | String | `"SPY"` | -- |
| isUS | Yes | bool | Yes | bool | `true` | -- |
| incrementRules | Yes | Array | Yes | Array | `[{"lowerEdge":0.0,"increment":0.01}]` | -- |
| displayRule | Yes | object | Yes | object | See below | -- |
| exerciseStyle | No | -- | Yes | null | `null` | **UNDOCUMENTED** |
| conExchMap | No | -- | Yes | Array | `[]` | **UNDOCUMENTED** |
| model | No | -- | Yes | String | `""` | **UNDOCUMENTED** |
| isEventContract | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| pageSize | No | -- | Yes | int | `100` | **UNDOCUMENTED** |
| baseMktValue | No | -- | Yes | float | `14408.68` | **UNDOCUMENTED** |
| baseMktPrice | No | -- | Yes | float | `654.94` | **UNDOCUMENTED** |
| baseAvgCost | No | -- | Yes | float | `654.19` | **UNDOCUMENTED** |
| baseAvgPrice | No | -- | Yes | float | `654.19` | **UNDOCUMENTED** |
| baseRealizedPnl | No | -- | Yes | float | `0.0` | **UNDOCUMENTED** |
| baseUnrealizedPnl | No | -- | Yes | float | `16.51` | **UNDOCUMENTED** |

**Notes:**
- The `base*` fields (`baseMktValue`, `baseMktPrice`, `baseAvgCost`, `baseAvgPrice`, `baseRealizedPnl`, `baseUnrealizedPnl`) are entirely undocumented. These appear to represent values in the account's base currency, which is critical for multi-currency accounts.
- `exerciseStyle` is relevant for options but undocumented.
- `conExchMap` and `model` appear in the recording's example JSON at line 7521-7524 but are NOT in the field description list.
- `pageSize` is undocumented -- it indicates the pagination size (100).
- `isEventContract` is undocumented -- indicates whether the position is an event/forecast contract.
- `multiplier` is documented as `float` but the doc example at line 7518 shows `null`, while the recording shows `0.0`. The type varies by instrument.

---

## 4. GET /portfolio/accounts

**Doc location:** Line 6396
**Recording:** `Scenario05_PortfolioDeepDive/003-GET-portfolio-accounts.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| id | Yes | String | Yes | String | `"DUO873728"` | -- |
| accountId | Yes | String | Yes | String | `"DUO873728"` | -- |
| accountVan | Yes | String | Yes | String | `"DUO873728"` | -- |
| accountTitle | Yes | String | Yes | String | `"Robert C Quillen"` | -- |
| displayName | Yes | String | Yes | String | `"Robert C Quillen"` | -- |
| accountAlias | Yes | String | Yes | null | `null` | -- |
| accountStatus | Yes | int | Yes | int | `1769922000000` | -- |
| currency | Yes | String | Yes | String | `"USD"` | -- |
| type | Yes | String | Yes | String | `"DEMO"` | -- |
| tradingType | Yes | String | Yes | String | `"STKNOPT"` | -- |
| businessType | Yes | String | Yes | String | `"INDEPENDENT"` | -- |
| ibEntity | Yes | String | Yes | String | `"IBLLC-US"` | -- |
| faclient | Yes (as faClient) | bool | Yes | bool | `false` | **WRONG_NAME** -- docs say `faClient`, actual is `faclient` |
| clearingStatus | Yes | String | Yes | String | `"O"` | -- |
| covestor | Yes | bool | Yes | bool | `false` | -- |
| noClientTrading | Yes | bool | Yes | bool | `false` | -- |
| trackVirtualFXPortfolio | Yes | bool | Yes | bool | `false` | -- |
| parent | Yes | object | Yes | object | See sub-fields | -- |
| parent.mmc | Yes | Array | Yes | Array | `[]` | -- |
| parent.accountId | Yes | String | Yes | String | `""` | -- |
| parent.isMParent | Yes | bool | Yes | bool | `false` | -- |
| parent.isMChild | Yes | bool | Yes | bool | `false` | -- |
| parent.isMultiplex | Yes | bool | Yes | bool | `false` | -- |
| desc | Yes | String | Yes | String | `"DUO873728"` | -- |
| PrepaidCrypto-Z | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| PrepaidCrypto-P | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| brokerageAccess | No | -- | Yes | bool | `true` | **UNDOCUMENTED** |
| category | No | -- | Yes | String | `""` | **UNDOCUMENTED** |
| acctCustType | No | -- | Yes | String | `"INDIVIDUAL"` | **UNDOCUMENTED** |

**Notes:**
- `faClient` vs `faclient`: The docs describe the field as `faClient` (camelCase) but both the example JSON in the docs (line 6510) and the actual recording use `faclient` (all lowercase). The doc field description is misleading.
- `PrepaidCrypto-Z` and `PrepaidCrypto-P` are unusual field names with hyphens. Completely undocumented.
- `brokerageAccess` is an important field indicating whether the account has brokerage access -- undocumented.
- `acctCustType` (e.g., `"INDIVIDUAL"`) is undocumented.
- `category` is undocumented.

---

## 5. GET /iserver/secdef/search

**Doc location:** Line 1994
**Recording:** `Scenario02_ContractResearch/003-GET-iserver-secdef-search.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| conid | Yes | String | Yes | String | `"265598"` | -- |
| companyHeader | Yes | String | Yes | String | `"APPLE INC - NASDAQ"` | -- |
| companyName | Yes | String | Yes | String | `"APPLE INC"` | -- |
| symbol | Yes | String | Yes | String | `"AAPL"` | -- |
| description | Yes | String | Yes | String | `"NASDAQ"` | -- |
| restricted | Yes | bool | Yes | null | `null` | **WRONG_TYPE** -- docs say bool, actual is null |
| sections | Yes | Array | Yes | Array | See sub-fields | -- |
| sections[].secType | Yes | String | Yes | String | `"STK"` | -- |
| sections[].months | Yes | String | Yes | String | `"APR26;MAY26;..."` | -- |
| sections[].exchange | Yes | String | Yes | String | `"SMART;AMEX;..."` | -- |
| sections[].conid | No | -- | Yes | String | `"120549942"` | **UNDOCUMENTED** (on CFD section) |
| sections[].symbol | Yes | String | No | -- | -- | **MISSING** -- docs mention it but not in recording |
| issuers | Yes (bonds) | Array | Yes | Array | Bond-specific | -- |
| issuers[].id | Yes | String | Yes | String | `"e1432232"` | -- |
| issuers[].name | Yes | String | Yes | String | `"Apple Inc"` | -- |
| bondid | Yes | int | Yes | int | `4` | -- |

**Notes:**
- `restricted` is documented as `bool` but consistently returns `null` in all recording entries. It may only return a bool when the contract is actually restricted.
- The `sections` sub-objects can have a `conid` field (seen on CFD entries) that is not documented.
- The `sections[].symbol` field documented does not appear in any section in the recording.
- The doc example at line 2114 shows `"secType": "STK"` as a top-level field on the result object, but recordings do not have `secType` at the top level -- it only exists inside `sections[]`.

---

## 6. GET /iserver/contract/{conid}/info

**Doc location:** Line 1298
**Recording:** `Scenario02_ContractResearch/004-GET-iserver-contract-265598-info.json`

The docs use camelCase field names in the description but the actual response (and the doc's own example JSON) uses snake_case. This is a major documentation inconsistency.

| Field | In Docs? | Doc Name | In Recording? | Actual Name | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|-------------|--------------|-------------|
| conid | Yes | conid | Yes | con_id | int | `265598` | **WRONG_NAME** -- docs say `conid`, actual is `con_id` |
| ticker | Yes | ticker | No | -- | -- | -- | **WRONG_NAME** -- actual field is `symbol` |
| secType | Yes | secType | Yes | instrument_type | String | `"STK"` | **WRONG_NAME** -- docs say `secType`, actual is `instrument_type` |
| listingExchange | Yes | listingExchange | No | -- | -- | -- | **MISSING** -- not present in recording |
| exchange | Yes | exchange | Yes | exchange | String | `"SMART"` | -- |
| companyName | Yes | companyName | Yes | company_name | String | `"APPLE INC"` | **WRONG_NAME** -- docs say `companyName`, actual is `company_name` |
| currency | Yes | currency | Yes | currency | String | `"USD"` | -- |
| validExchanges | Yes | validExchanges | Yes | valid_exchanges | String | `"SMART,AMEX,..."` | **WRONG_NAME** -- docs say `validExchanges`, actual is `valid_exchanges` |
| priceRendering | Yes | priceRendering | No | -- | -- | -- | **MISSING** |
| maturityDate | Yes | maturityDate | Yes | maturity_date | null | `null` | **WRONG_NAME** -- docs say `maturityDate`, actual is `maturity_date` |
| right | Yes | right | No | -- | -- | -- | **MISSING** |
| strike | Yes | strike | No | -- | -- | -- | **MISSING** (for stocks) |
| cfi_code | No | -- | Yes | cfi_code | String | `""` | **UNDOCUMENTED** |
| symbol | No | -- | Yes | symbol | String | `"AAPL"` | **UNDOCUMENTED** (docs list `ticker` instead) |
| cusip | No | -- | Yes | cusip | null | `null` | **UNDOCUMENTED** |
| expiry_full | No | -- | Yes | expiry_full | null | `null` | **UNDOCUMENTED** |
| industry | No | -- | Yes | industry | String | `"Computers"` | **UNDOCUMENTED** |
| has_related_contracts | No | -- | Yes | has_related_contracts | bool | `true` | **UNDOCUMENTED** |
| trading_class | No | -- | Yes | trading_class | String | `"NMS"` | **UNDOCUMENTED** |
| allow_sell_long | No | -- | Yes | allow_sell_long | bool | `false` | **UNDOCUMENTED** |
| is_zero_commission_security | No | -- | Yes | is_zero_commission_security | bool | `false` | **UNDOCUMENTED** |
| local_symbol | No | -- | Yes | local_symbol | String | `"AAPL"` | **UNDOCUMENTED** |
| contract_clarification_type | No | -- | Yes | contract_clarification_type | null | `null` | **UNDOCUMENTED** |
| classifier | No | -- | Yes | classifier | null | `null` | **UNDOCUMENTED** |
| text | No | -- | Yes | text | null | `null` | **UNDOCUMENTED** |
| underlying_con_id | No | -- | Yes | underlying_con_id | int | `0` | **UNDOCUMENTED** |
| r_t_h | No | -- | Yes | r_t_h | bool | `true` | **UNDOCUMENTED** |
| multiplier | No | -- | Yes | multiplier | null | `null` | **UNDOCUMENTED** |
| underlying_issuer | No | -- | Yes | underlying_issuer | null | `null` | **UNDOCUMENTED** |
| contract_month | No | -- | Yes | contract_month | null | `null` | **UNDOCUMENTED** |
| smart_available | No | -- | Yes | smart_available | bool | `true` | **UNDOCUMENTED** |
| category | No | -- | Yes | category | String | `"Computers"` | **UNDOCUMENTED** |

**Notes:**
- This endpoint has the worst documentation quality of all endpoints examined. The docs describe 11 fields; the actual response has 28 fields.
- The field naming convention is completely wrong in the docs: docs use camelCase (`companyName`, `validExchanges`) but the actual API uses snake_case (`company_name`, `valid_exchanges`). The docs' own example JSON (line 1359-1386) uses snake_case, contradicting the field descriptions above it.
- The docs list `ticker` as a field but the actual field is `symbol`.
- The docs list `conid` but the actual field is `con_id`.
- The docs list `secType` but the actual field is `instrument_type`.
- 17 fields in the recording are completely undocumented, including important ones like `industry`, `category`, `trading_class`, `local_symbol`, `smart_available`, and `r_t_h` (regular trading hours).

---

## 7. POST /iserver/account/{accountId}/orders (Place Order)

**Doc location:** Line 5556
**Recording:** `Scenario04_OrderLifecycle/006-POST-iserver-account-DUO873728-orders.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| order_id | Yes | String | Yes | String | `"22239254"` | -- |
| order_status | Yes | String | Yes | String | `"PreSubmitted"` | -- |
| encrypt_message | Yes | String | Yes | String | `"1"` | -- |

**Notes:**
- The place order success response is minimal and matches the documentation well.
- The docs show `order_status: "Submitted"` in the example but the actual recording returns `"PreSubmitted"`. This is expected behavior (different statuses depending on order routing) but the example could be misleading.
- The what-if response (`POST /iserver/account/{accountId}/orders/whatif`) has a substantially richer response structure not fully analyzed here, including `amount`, `equity`, `initial`, `maintenance`, `position`, `warn`, `error`, `warns`, `errors`, `accruedInterest`, `action`, `order_id`, and `cqe` fields.

---

## 8. GET /iserver/marketdata/snapshot

**Doc location:** Line 4429
**Recording:** `Scenario03_MarketDataScanners/005-GET-iserver-marketdata-snapshot.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| conidEx | Yes | String | Yes | String | `"756733"` | -- |
| conid | Yes | int | Yes | int | `756733` | -- |
| server_id | Yes | String | Yes | String | `"q0"` | -- |
| _updated | Yes | int | Yes | int | `1775086883187` | -- |
| 6119 | Yes | String | Yes | String | `"q0"` | -- |
| 6509 | Yes | String | Yes | String | `"DPB"` | -- |
| 84 | Indirectly (via fields table) | String | Yes | String | `"655.05"` | -- |
| 31 | Indirectly (via fields table) | String | Yes | String | `"655.05"` | -- |

**Notes:**
- The market data snapshot response matches documentation reasonably well. The field IDs (31, 84, etc.) are documented in the Market Data Fields table starting at line 4641.
- The first snapshot request (preflight) returns only `conidEx` and `conid` with no market data -- this behavior is documented but the response shape difference is not explicitly shown.
- The `_updated` field uses an underscore prefix, which is unusual and could cause issues with some JSON deserializers. This is documented correctly.
- Field 6509 value `"DPB"` should be interpreted per the Market Data Availability section (line 4620). `D`=Delayed, `P`=Snapshot, `B`=Book (OTC/Pink Sheet).

---

## 9. POST /tickle

**Doc location:** Line 9765
**Recording:** `Scenario10_WebSocketStreaming/005-POST-tickle.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| session | Yes | String | Yes | String | `"e98d648092a5..."` | -- |
| ssoExpires | Yes | int | No | -- | -- | **MISSING** |
| collission | Yes | bool | No | -- | -- | **MISSING** |
| userId | Yes | int | No | -- | -- | **MISSING** |
| hmds | Yes | object | Yes | object | `{"error":"no bridge"}` | -- |
| iserver | Yes | object | Yes | object | See sub-fields | -- |
| iserver.authStatus | Indirectly | object | Yes | object | See sub-fields | -- |
| iserver.authStatus.authenticated | Yes (via auth/status) | bool | Yes | bool | `true` | -- |
| iserver.authStatus.competing | Yes (via auth/status) | bool | Yes | bool | `false` | -- |
| iserver.authStatus.connected | Yes (via auth/status) | bool | Yes | bool | `true` | -- |
| iserver.authStatus.message | Yes (via auth/status) | String | Yes | String | `""` | -- |
| iserver.authStatus.MAC | Yes (via auth/status) | String | Yes | String | `"06:05:9B:DE:2D:8B"` | -- |
| iserver.authStatus.serverInfo | Yes (via auth/status) | object | Yes | object | See sub-fields | -- |
| iserver.authStatus.hardware_info | No | -- | Yes | String | `"d12054d0\|06:05:9B..."` | **UNDOCUMENTED** |
| iserver.authStatus.established | No | -- | No (but in ssodh) | -- | -- | See note |

**Notes:**
- `ssoExpires`, `collission` (note: the docs misspell "collision"), and `userId` are documented but **not present** in the recording. The cause is unknown -- it could be an OAuth vs gateway difference, a paper vs live account difference, a version change, or the docs may simply be wrong. We only have OAuth paper-account recordings to compare against.
- The `iserver.authStatus` sub-object includes `hardware_info` which is not documented in the tickle response (though it is documented in the `/iserver/auth/status` response at line 9625).
- The `iserver.authStatus` includes `established` in the ssodh/init recording but not here -- field presence varies by endpoint.

---

## 10. POST /iserver/auth/ssodh/init

**Doc location:** Line 9660
**Recording:** `Scenario01_AccountDiscovery/002-POST-iserver-auth-ssodh-init.json`

| Field | In Docs? | Doc Type | In Recording? | Actual Type | Sample Value | Discrepancy |
|-------|----------|----------|---------------|-------------|--------------|-------------|
| authenticated | Yes | bool | Yes | bool | `true` | -- |
| competing | Yes | bool | Yes | bool | `false` | -- |
| connected | Yes | bool | Yes | bool | `true` | -- |
| message | Yes | String | No | -- | -- | **MISSING** |
| MAC | Yes | String | No | -- | -- | **MISSING** |
| serverInfo | Yes | Object | No | -- | -- | **MISSING** |
| passed | No | -- | Yes | bool | `true` | **UNDOCUMENTED** |
| established | No | -- | Yes | bool | `false` | **UNDOCUMENTED** |
| hardware_info | No | -- | Yes | String | `"d12054d0\|06:05:9B:DE:2D:8B"` | **UNDOCUMENTED** |

**Notes:**
- The actual response is **significantly different** from the documented response. The docs show a rich object with `message`, `MAC`, and `serverInfo`, but our recording returns a simpler object with `passed`, `established`, and `hardware_info` instead.
- **Hypothesis (unconfirmed):** This may be a different response shape for OAuth-authenticated sessions vs. gateway sessions, with the documentation only describing the gateway response. However, we only have OAuth paper-account recordings -- the difference could also be due to API version changes, paper vs live accounts, or the docs simply being outdated. We cannot confirm this without a gateway-mode recording to compare against.
- `passed` is an important field (indicates whether the init request passed) that is completely undocumented.
- `established` indicates whether a brokerage session was already established -- also undocumented.
- The `hardware_info` field format is `"hash|MAC_address"` -- undocumented.

---

## Summary of Findings

### Discrepancy Counts by Endpoint

| Endpoint | UNDOCUMENTED | WRONG_TYPE | MISSING | WRONG_NAME | Total |
|----------|-------------|------------|---------|------------|-------|
| GET /iserver/account/orders | 2 | 0 | 0 | 0 | 2 |
| GET /iserver/account/order/status/{orderId} | 9 | 1 | 0 | 0 | 10 |
| GET /portfolio/{accountId}/positions/{page} | 8 | 1 | 0 | 0 | 9 |
| GET /portfolio/accounts | 5 | 0 | 0 | 1 | 6 |
| GET /iserver/secdef/search | 1 | 1 | 1 | 0 | 3 |
| GET /iserver/contract/{conid}/info | 17 | 0 | 3 | 5 | 25 |
| POST /iserver/account/{accountId}/orders | 0 | 0 | 0 | 0 | 0 |
| GET /iserver/marketdata/snapshot | 0 | 0 | 0 | 0 | 0 |
| POST /tickle | 1 | 0 | 3 | 0 | 4 |
| POST /iserver/auth/ssodh/init | 3 | 0 | 3 | 0 | 6 |
| **TOTAL** | **46** | **3** | **10** | **6** | **65** |

### Critical Findings

1. **GET /iserver/contract/{conid}/info is severely misdocumented.** The field description section uses camelCase names while the actual API returns snake_case. 17 of 28 actual fields are undocumented. The doc descriptions list `conid`, `ticker`, `secType` but the actual fields are `con_id`, `symbol`, `instrument_type`.

2. **POST /iserver/auth/ssodh/init returns a different response than documented.** The documented response (`message`, `MAC`, `serverInfo`) does not match our recordings (`passed`, `established`, `hardware_info`). The cause is unknown -- could be OAuth vs gateway, API version changes, or outdated docs.

3. **POST /tickle omits documented fields.** `ssoExpires`, `collission`, and `userId` are documented but not present in our recordings. Cause unknown (see ssodh/init note above).

4. **Positions endpoint has 8 undocumented fields**, including the critically important `base*` fields for multi-currency support.

5. **Order status has 9 undocumented fields**, including `limit_price`, `outside_rth`, and `server_id`.

6. **Portfolio accounts has undocumented crypto and brokerage access fields** (`PrepaidCrypto-Z`, `PrepaidCrypto-P`, `brokerageAccess`).

### Patterns

- **IBKR returns more fields than documented** -- this is the dominant pattern (46 of 65 discrepancies). The docs are a minimal subset of the actual response.
- **Naming convention inconsistency** -- some endpoints use snake_case (order/status, contract/info), some use camelCase (portfolio/accounts, positions). The docs sometimes describe one convention while the API uses another.
- **Session endpoint response divergence (cause unknown)** -- session-related endpoints (ssodh/init, tickle) return different shapes than documented. This could be OAuth vs gateway, API version changes, paper vs live accounts, or outdated docs. We only have OAuth paper-account recordings to compare.
- **Doc examples are more accurate than field descriptions** -- in several cases (contract/info, portfolio/accounts), the JSON examples in the docs use the correct field names while the field description section uses wrong names.
