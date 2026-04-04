# IBKR Client Portal Web API

## Table of Contents

- [Accounts](#accounts)
  - [Account Profit and Loss](#account-profit-and-loss)
  - [Search Dynamic Account](#search-dynamic-account)
  - [Set Dynamic Account](#set-dynamic-account)
  - [Signatures and Owners](#signatures-and-owners)
  - [Switch Account](#switch-account)
  - [Receive Brokerage Accounts](#receive-brokerage-accounts)
- [Alerts](#alerts)
  - [Get a list of available alerts](#get-a-list-of-available-alerts)
  - [Get details of a specific alert](#get-details-of-a-specific-alert)
  - [Create or Modify Alert](#create-or-modify-alert)
  - [Get MTA Alert](#get-mta-alert)
  - [Activate or deactivate an alert](#activate-or-deactivate-an-alert)
  - [Delete an alert](#delete-an-alert)
- [Contract](#contract)
  - [Search the security definition by Contract ID](#search-the-security-definition-by-contract-id)
  - [All Conids by Exchange](#all-conids-by-exchange)
  - [Contract information by Contract ID](#contract-information-by-contract-id)
  - [Currency Pairs](#currency-pairs)
  - [Currency Exchange Rate](#currency-exchange-rate)
  - [Find all Info and Rules for a given contract](#find-all-info-and-rules-for-a-given-contract)
  - [Search Algo Params by Contract ID](#search-algo-params-by-contract-id)
  - [Search Bond Filter Information](#search-bond-filter-information)
  - [Search Contract by Symbol](#search-contract-by-symbol)
  - [Search Contract Rules](#search-contract-rules)
  - [Search SecDef information by conid](#search-secdef-information-by-conid)
  - [Search Strikes by Underlying Contract ID](#search-strikes-by-underlying-contract-id)
  - [Security Future by Symbol](#security-future-by-symbol)
  - [Security Stocks by Symbol](#security-stocks-by-symbol)
  - [Trading Schedule by Symbol](#trading-schedule-by-symbol)
  - [Trading Schedule (NEW)](#trading-schedule-new)
- [Event Contracts](#event-contracts)
  - [Categorization](#categorization)
  - [Markets and Strikes](#markets-and-strikes)
  - [Contract Rules](#contract-rules)
  - [Contract Details](#contract-details)
  - [Trading Schedule](#trading-schedule)
  - [Order Submission](#order-submission)
  - [Executions and Netting](#executions-and-netting)
- [FA Allocation Management](#fa-allocation-management)
  - [Allocatable Sub-Accounts](#allocatable-sub-accounts)
  - [Retrieve Single Allocation Group](#retrieve-single-allocation-group)
  - [List All Allocation Groups](#list-all-allocation-groups)
  - [Add Allocation Group](#add-allocation-group)
  - [Modify Allocation Group](#modify-allocation-group)
  - [Delete Allocation Group](#delete-allocation-group)
  - [Retrieve Allocation Presets](#retrieve-allocation-presets)
  - [Set Allocation Presets](#set-allocation-presets)
  - [Allocation Method Codes](#allocation-method-codes)
  - [Allocation Preset Combinations](#allocation-preset-combinations)
- [FYIs and Notifications](#fyis-and-notifications)
  - [Unread Bulletins](#unread-bulletins)
  - [Get a List of Subscriptions](#get-a-list-of-subscriptions)
  - [Enable/Disable Specified Subscription](#enabledisable-specified-subscription)
  - [FYI Typecodes](#fyi-typecodes)
  - [Get disclaimer for a certain kind of FYI](#get-disclaimer-for-a-certain-kind-of-fyi)
  - [Mark Disclaimer Read](#mark-disclaimer-read)
  - [Get Delivery Options](#get-delivery-options)
  - [Enable/Disable Device Option](#enabledisable-device-option)
  - [Delete a Device](#delete-a-device)
  - [Enable/Disable Email Option](#enabledisable-email-option)
  - [Get a list of notifications](#get-a-list-of-notifications)
  - [Mark Notification Read](#mark-notification-read)
- [Market Data](#market-data)
  - [Live Market Data Snapshot](#live-market-data-snapshot)
  - [Market Data Update Frequency](#market-data-update-frequency)
  - [Regulatory Snapshot](#regulatory-snapshot)
  - [Market Data Availability](#market-data-availability)
  - [Market Data Fields](#market-data-fields)
  - [Unavailable Historical Data](#unavailable-historical-data)
  - [Historical Market Data](#historical-market-data)
  - [HMDS Period and Bar Size](#hmds-period-and-bar-size)
  - [Unsubscribe (Single)](#unsubscribe-single)
  - [Unsubscribe (All)](#unsubscribe-all)
- [Order Monitoring](#order-monitoring)
  - [Live Orders](#live-orders)
  - [Order Status](#order-status)
  - [Order Status Values](#order-status-values)
  - [Trades](#trades)
- [Orders](#orders)
  - [Place Order](#place-order)
  - [Cash Quantity Orders in the Web API](#cash-quantity-orders-in-the-web-api)
  - [Place Order Reply Confirmation](#place-order-reply-confirmation)
  - [Respond to a Server Prompt](#respond-to-a-server-prompt)
  - [Preview Order / WhatIf Order](#preview-order--whatif-order)
  - [Overnight Order Submission](#overnight-order-submission)
  - [Combo / Spread Orders](#combo--spread-orders)
  - [Bracket Orders and OCA Groups](#bracket-orders-and-oca-groups)
  - [Cancel Order](#cancel-order)
  - [Modify Order](#modify-order)
  - [Suppress Messages](#suppress-messages)
  - [Suppressible MessageIds](#suppressible-messageids)
  - [Reset Suppressed Messages](#reset-suppressed-messages)
- [Portfolio](#portfolio)
  - [Portfolio Accounts](#portfolio-accounts)
  - [Portfolio Subaccounts](#portfolio-subaccounts)
  - [Portfolio Subaccounts (Large Account Structures)](#portfolio-subaccounts-large-account-structures)
  - [Specific Account's Portfolio Information](#specific-accounts-portfolio-information)
  - [PortfolioAccount Schema](#portfolioaccount-schema)
  - [Portfolio Allocation (Single)](#portfolio-allocation-single)
  - [Combination Positions](#combination-positions)
  - [Portfolio Allocation (All)](#portfolio-allocation-all)
  - [Positions](#positions)
  - [Positions (NEW)](#positions-new)
  - [Positions by Conid](#positions-by-conid)
  - [Invalidate Backend Portfolio Cache](#invalidate-backend-portfolio-cache)
  - [Portfolio Summary](#portfolio-summary)
  - [Portfolio Ledger](#portfolio-ledger)
  - [Position and Contract Info](#position-and-contract-info)
- [Portfolio Analyst](#portfolio-analyst)
  - [All Periods](#all-periods)
  - [Account Performance](#account-performance)
  - [Transaction History](#transaction-history)
- [Scanner](#scanner)
  - [Iserver Scanner Parameters](#iserver-scanner-parameters)
  - [Iserver Market Scanner](#iserver-market-scanner)
  - [HMDS Market Scanner](#hmds-market-scanner)
- [Session](#session)
  - [Authentication Status](#authentication-status)
  - [Initialize Brokerage Session](#initialize-brokerage-session)
  - [Logout of the current session](#logout-of-the-current-session)
  - [Ping the server](#ping-the-server)
  - [Re-authenticate the Brokerage Session (Deprecated)](#re-authenticate-the-brokerage-session-deprecated)
  - [Validate SSO](#validate-sso)
- [Watchlists](#watchlists)
  - [Create a Watchlist](#create-a-watchlist)
  - [Get All Watchlists](#get-all-watchlists)
  - [Get Watchlist Information](#get-watchlist-information)
  - [Delete a Watchlist](#delete-a-watchlist)

## Accounts

Endpoints for managing and retrieving information about your Interactive Brokers accounts.

---

[↑ Back to Table of Contents](#table-of-contents)


### Account Profit and Loss

Returns an object containing PnL for the selected account and its models (if any).

- **Method:** `GET`
- **URL:** `/iserver/account/pnl/partitioned`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| upnl | object | Updated PnL. Holds a JSON object of key-value paired account PnL details. Keys are formatted as `{accountId}.Core` |

**`{accountId}.Core` object:**

| Field | Type | Description |
|-------|------|-------------|
| rowType | int | Returns the positional value of the returned account. Always returns 1 for individual accounts |
| dpl | float | Daily PnL for the specified account profile |
| nl | float | Net Liquidity for the specified account profile |
| upl | float | Unrealized PnL for the specified account profile |
| el | float | Excess Liquidity for the specified account profile |
| mv | float | Margin value for the specified account profile |

#### Example Response

```json
{
  "upnl": {
    "U1234567.Core": {
      "rowType": 1,
      "dpl": 15.7,
      "nl": 10000.0,
      "upl": 607.0,
      "el": 10000.0,
      "mv": 0.0
    }
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Dynamic Account

Returns a list of accounts matching a query pattern set in the request. Broker accounts configured with the DYNACCT property will not receive account information at login. Instead, they must dynamically query then set their account number.

> **Important:** This will not function for individual or financial advisor accounts. This will only be functional for IBrokers with the DYNACCT property approved. Customers without the DYNACCT property will receive a 503 error.

- **Method:** `GET`
- **URL:** `/iserver/account/search/{searchPattern}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| searchPattern | path | string | yes | The pattern used to describe credentials to search for. Valid Format: "DU" to query all paper accounts |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| matchedAccounts | array\<MatchedAccount\> | Contains a series of objects that pertain to the account information requested. See nested table below |
| pattern | string | Displays the searchPattern used for the request |

**`MatchedAccount` object:**

| Field | Type | Description |
|-------|------|-------------|
| accountId | string | Returns a matching account ID that corresponds to the matching value |
| alias | string | Returns the corresponding alias or alternative name for the specific account ID. May be a duplicate of the accountId value in most cases |
| allocationId | string | Returns the allocation identifier used internally for the account |

#### Example Response

```json
{
  "matchedAccounts": [
    {
      "accountId": "U1234567",
      "alias": "U1234567",
      "allocationId": "1"
    }
  ],
  "pattern": "U123"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Set Dynamic Account

Set the active dynamic account. Values retrieved from Search Dynamic Account. Broker accounts configured with the DYNACCT property will not receive account information at login. Instead, they must dynamically query then set their account number.

> **Important:** This will not function for individual or financial advisor accounts. This will only be functional for IBrokers with the DYNACCT property approved. Customers without the DYNACCT property will receive a 503 error.

- **Method:** `POST`
- **URL:** `/iserver/dynaccount`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctId | string | yes | The account ID that should be set for future requests |

#### Example Request

```json
{
  "acctId": "U1234567"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| set | bool | Confirms if the account change was fully set |
| acctId | string | The account ID that was set for future use |

#### Example Response

```json
{
  "set": true,
  "acctId": "U1234567"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Signatures and Owners

Receive a list of all applicant names on the account and for which account and entity is represented.

- **Method:** `GET`
- **URL:** `/acesws/{accountId}/signatures-and-owners`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Pass the account identifier to receive information for. Valid Structure: "U1234567" |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| accountId | string | Specified account identifier in the request |
| users | array\<User\> | Returns all usernames and their information affiliated with the account. See nested table below |
| applicant | object | Provides information about the individual listed for the account. See nested table below |

**`User` object:**

| Field | Type | Description |
|-------|------|-------------|
| roleId | string | Returns the role of the username as it relates to the account |
| hasRightCodeInd | bool | Internal use only |
| username | string | Returns the username for the particular user under the account |
| entity | object | Provides information about the particular entity. See nested table below |

**`entity` object:**

| Field | Type | Description |
|-------|------|-------------|
| firstName | string | Returns the first name of the user |
| lastName | string | Returns the last name of the user |
| entityType | string | Returns the type of entity assigned to the user. One of: `INDIVIDUAL`, `Joint`, `ORG` |
| entityName | string | Returns the full entity's name, concatenating the first and last name fields |

**`applicant` object:**

| Field | Type | Description |
|-------|------|-------------|
| signatures | array\<string\> | Returns all names attached to the account |

#### Example Response

```json
{
  "accountId": "U1234567",
  "users": [
    {
      "roleId": "OWNER",
      "hasRightCodeInd": true,
      "userName": "user1234",
      "entity": {
        "firstName": "John",
        "lastName": "Smith",
        "entityType": "INDIVIDUAL",
        "entityName": "John Smith"
      }
    },
    {
      "roleId": "Trustee",
      "hasRightCodeInd": false,
      "userName": "user5678",
      "entity": {
        "firstName": "Jane",
        "lastName": "Doe",
        "entityType": "INDIVIDUAL",
        "entityName": "Jane Doe"
      }
    }
  ],
  "applicant": {
    "signatures": [
      "John Smith",
      "Jane Doe"
    ]
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Switch Account

Switch the active account for how you request data. Only available for financial advisors and multi-account structures.

- **Method:** `POST`
- **URL:** `/iserver/account`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctId | string | yes | Identifier for the unique account to retrieve information from. Value Format: "DU1234567" |

#### Example Request

```json
{
  "acctId": "U1234567"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| set | bool | Confirms that the account change was set |
| acctId | string | Confirms the account switched to |

#### Example Response

```json
{
  "set": true,
  "acctId": "U1234567"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Receive Brokerage Accounts

Returns a list of accounts the user has trading access to, their respective aliases and the currently selected account. Note this endpoint must be called before modifying an order or querying open orders.

- **Method:** `GET`
- **URL:** `/iserver/accounts`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| accounts | array\<string\> | Returns an array of all accessible account IDs |
| acctProps | object | Returns a JSON object keyed by account ID, each containing account properties. See nested table below |
| aliases | object | Returns any available aliases for accounts, keyed by account ID |
| allowFeatures | object | JSON of allowed features for the account. See nested table below |
| chartPeriods | object | Returns available trading times for all available security types, keyed by asset type |
| groups | array | Returns an array of affiliated groups |
| profiles | array | Returns an array of affiliated profiles |
| selectedAccount | string | Returns currently selected account. See Switch Account for more details |
| serverInfo | object | Returns information about the IBKR session. See nested table below |
| sessionId | string | Returns current session ID |
| isFT | bool | Returns fractional trading access |
| isPaper | bool | Returns account type status |

**`acctProps.{accountId}` object:**

| Field | Type | Description |
|-------|------|-------------|
| hasChildAccounts | bool | Returns whether or not child accounts exist for the account |
| supportsCashQty | bool | Returns whether or not the account can use Cash Quantity for trading |
| noFXConv | bool | Returns whether FX conversion is disabled |
| isProp | bool | Returns whether the account is a proprietary account |
| supportsFractions | bool | Returns whether or not the account can submit fractional share orders |
| allowCustomerTime | bool | Returns whether or not the account must submit "manualOrderTime" with orders. If true, manualOrderTime must be included. If false, manualOrderTime cannot be included |

**`allowFeatures` object:**

| Field | Type | Description |
|-------|------|-------------|
| showGFIS | bool | Returns if the account can access market data |
| showEUCostReport | bool | Returns if the account can view the EU Cost Report |
| allowEventContract | bool | Returns if the account can use event contracts |
| allowFXConv | bool | Returns if the account can convert currencies |
| allowFinancialLens | bool | Returns if the account can access the financial lens |
| allowMTA | bool | Returns if the account can use mobile trading alerts |
| allowTypeAhead | bool | Returns if the account can use Type-Ahead support for Client Portal |
| allowEventTrading | bool | Returns if the account can use Event Trader |
| snapshotRefreshTimeout | int | Returns the snapshot refresh timeout window for new data |
| liteUser | bool | Returns if the account is an IBKR Lite user |
| showWebNews | bool | Returns if the account can use News feeds via the web |
| research | bool | Returns if the account has research access |
| debugPnl | bool | Returns if the account can use the debugPnl endpoint |
| showTaxOpt | bool | Returns if the account can use the Tax Optimizer tool |
| showImpactDashboard | bool | Returns if the account can view the Impact Dashboard |
| allowDynAccount | bool | Returns if the account can use dynamic account changes |
| allowCrypto | bool | Returns if the account can trade crypto currencies |
| allowedAssetTypes | string | Returns a comma-separated list of asset types the account can trade |

**`serverInfo` object:**

| Field | Type | Description |
|-------|------|-------------|
| serverName | string | Returns the server name |
| serverVersion | string | Returns the server build version |

#### Example Response

```json
{
  "accounts": [
    "U1234567"
  ],
  "acctProps": {
    "U1234567": {
      "hasChildAccounts": false,
      "supportsCashQty": true,
      "noFXConv": false,
      "isProp": false,
      "supportsFractions": true,
      "allowCustomerTime": false
    }
  },
  "aliases": {
    "U1234567": "U1234567"
  },
  "allowFeatures": {
    "showGFIS": true,
    "showEUCostReport": false,
    "allowEventContract": true,
    "allowFXConv": true,
    "allowFinancialLens": false,
    "allowMTA": true,
    "allowTypeAhead": true,
    "allowEventTrading": true,
    "snapshotRefreshTimeout": 30,
    "liteUser": false,
    "showWebNews": true,
    "research": true,
    "debugPnl": true,
    "showTaxOpt": true,
    "showImpactDashboard": true,
    "allowDynAccount": false,
    "allowCrypto": false,
    "allowedAssetTypes": "STK,CRYPTO"
  },
  "chartPeriods": {
    "STK": ["*"],
    "CRYPTO": ["*"]
  },
  "groups": [],
  "profiles": [],
  "selectedAccount": "U1234567",
  "serverInfo": {
    "serverName": "JifN17091",
    "serverVersion": "Build 10.25.0p, Dec 5, 2023 5:48:12 PM"
  },
  "sessionId": "1234a5b.12345678",
  "isFT": false,
  "isPaper": false
}
```

## Alerts

Alerts allow users to set up notifications to pop-up in their Trader Workstation, Interactive Brokers app, or via email in the event of a particular event.

---

[↑ Back to Table of Contents](#table-of-contents)


### Get a list of available alerts

Retrieve a list of all alerts attached to the provided account.

- **Method:** `GET`
- **URL:** `/iserver/account/{accountId}/alerts`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Identifier for the unique account to retrieve information from. Value Format: "DU1234567" |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| order_id | int | The searchable order ID |
| account | string | The account the alert was attributed to |
| alert_name | string | The requested name for the alert |
| alert_active | int | Determines if the alert is active or not |
| order_time | string | UTC-formatted time of the alert's creation |
| alert_triggered | bool | Confirms if the order is triggered or not |
| alert_repeatable | int | Confirms if the alert is enabled to repeat |

#### Example Response

```json
[
  {
    "order_id": 9876543210,
    "account": "U1234567",
    "alert_name": "AAPL Price",
    "alert_active": 1,
    "order_time": "20231211-18:55:35",
    "alert_triggered": false,
    "alert_repeatable": 0
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get details of a specific alert

Request details of a specific alert by providing the assigned order ID.

- **Method:** `GET`
- **URL:** `/iserver/account/alert/{order_id}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| order_id | path | int | yes | Alert ID returned from the original alert creation, or from the list of available alerts |
| type | query | string | yes | Must always pass "Q" |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| account | string | Requestor's account ID |
| order_id | int | Alert's tracking ID. Can be used for modifying or deleting alerts |
| alertName | string | Human readable name of the alert |
| tif | string | Time in Force effective for the Alert |
| expire_time | string | Returns the UTC formatted date used in GTD orders |
| alert_active | int | Returns if the alert is active or disabled |
| alert_repeatable | int | Returns if the alert can be sent more than once |
| alert_email | string | Returns the designated email address for sendMessage functionality |
| alert_send_message | int | Returns whether or not the alert will send an email |
| alert_message | string | Returns the body content of what your alert will report once triggered |
| alert_show_popup | int | Returns whether or not the alert will trigger TWS Pop-up messages |
| alert_play_audio | int | Returns whether or not the alert will play audio |
| order_status | string | Always returns "Presubmitted" |
| alert_triggered | int | Returns whether or not the alert was triggered yet |
| fg_color | string | Always returns "#FFFFFF". Can be ignored |
| bg_color | string | Always returns "#000000". Can be ignored |
| order_not_editable | bool | Returns if the order can be edited |
| itws_orders_only | int | Returns whether or not the alert will trigger mobile notifications |
| alert_mta_currency | string | Returns currency set for MTA alerts. Only valid for alert type 8 & 9 |
| alert_mta_defaults | string | Returns current MTA default values |
| tool_id | int | Tracking ID for MTA alerts only. Returns null for standard alerts |
| time_zone | string | Returned for time-specific conditions |
| alert_default_type | int | Returns default type set for alerts. Configured in Client Portal |
| condition_size | int | Returns the total number of conditions in the alert |
| condition_outside_rth | int | Returns whether or not the alert will trigger outside of regular trading hours |
| conditions | array\<Condition\> | Returns all conditions. See nested table below |

**`Condition` object:**

| Field | Type | Description |
|-------|------|-------------|
| condition_type | int | Returns the type of condition set |
| conidex | string | Returns full conidex in the format "conid@exchange" |
| contract_description_1 | string | Includes relevant descriptions (if applicable) |
| condition_operator | string | Returns condition set for alert |
| condition_trigger_method | int | Returns triggerMethod value set |
| condition_value | string | Returns value set |
| condition_logic_bind | string | Returns logic_bind value set |
| condition_time_zone | string | Returns timeZone value set |

#### Example Response

```json
{
  "account": "U1234567",
  "order_id": 9876543210,
  "alert_name": "AAPL Price",
  "tif": "GTD",
  "expire_time": "20231231-12:00:00",
  "alert_active": 1,
  "alert_repeatable": 0,
  "alert_email": null,
  "alert_send_message": 0,
  "alert_message": "MTA TEST!",
  "alert_show_popup": 0,
  "alert_play_audio": null,
  "order_status": "Submitted",
  "alert_triggered": false,
  "fg_color": "#FFFFFF",
  "bg_color": "#0000CC",
  "order_not_editable": false,
  "itws_orders_only": 0,
  "alert_mta_currency": null,
  "alert_mta_defaults": null,
  "tool_id": null,
  "time_zone": null,
  "alert_default_type": null,
  "condition_size": 1,
  "condition_outside_rth": 0,
  "conditions": [
    {
      "condition_type": 1,
      "conidex": "265598@SMART",
      "contract_description_1": "AAPL",
      "condition_operator": "<=",
      "condition_trigger_method": "0",
      "condition_value": "183.34",
      "condition_logic_bind": "n",
      "condition_time_zone": null
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Create or Modify Alert

Endpoint used to create a new alert, or modify an existing alert.

- **Method:** `POST`
- **URL:** `/iserver/account/{accountId}/alert`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Identifier for the unique account to retrieve information from. Value Format: "DU1234567" |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| alertName | string | yes | Used as a human-readable identifier for your created alert. Format: "Alert Name" |
| alertMessage | string | yes | The body content of what your alert will report once triggered. Format: "MESSAGE TEXT" |
| alertRepeatable | int | yes | Boolean number (0, 1) signifies if an alert can be triggered more than once. A value of 1 is required for MTA alerts |
| email | string | conditional | Email address you want to send email alerts to. Required if sendMessage == 1 |
| expireTime | string | conditional | Used with a tif of "GTD" only. Signifies time when the alert should terminate if no alert is triggered. Required if tif == "GTD". Format: "YYYYMMDD-HH:mm:ss" |
| iTWSOrdersOnly | int | no | Boolean number (0, 1) to allow alerts to trigger alerts through the mobile app |
| outsideRth | int | yes | Boolean number (0, 1) to allow the alert to trigger outside of regular trading hours |
| sendMessage | int | no | Boolean number (0, 1) to allow alerts to trigger email messages |
| showPopup | int | no | Boolean number (0, 1) to allow alerts to trigger TWS Pop-up messages |
| tif | string | yes | Time in Force duration of alert. One of: `GTC`, `GTD` |
| conditions | array\<AlertCondition\> | yes | Container for all conditions applied for an alert to trigger |

**`AlertCondition` object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| conidex | string | yes | Concatenation of conid and exchange. Format: "conid@exchange" (e.g., "265598@SMART") |
| logicBind | string | yes | Describes how multiple conditions should behave together. One of: `a` (AND), `o` (OR), `n` (END) |
| operator | string | yes | Indicates whether the trigger should be above or below the given value (e.g., ">=", "<=") |
| timeZone | string | conditional | Only needed for some MTA alert conditions. Required for MTA alerts. Format: "US/Eastern" |
| triggerMethod | string | yes | Pass the string representation of zero, "0" |
| type | int | yes | Designate what condition type to use. One of: `1` (Price), `3` (Time), `4` (Margin), `5` (Trade), `6` (Volume), `7` (MTA market), `8` (MTA Position), `9` (MTA Account Daily PnL) |
| value | string | yes | Trigger value based on type. Allows a default value of "*". Format: "195.00" or "YYYYMMDD-HH:mm:ss" |

#### Example Request

```json
{
  "alertMessage": "AAPL Price Drop!",
  "alertName": "AAPL_Price",
  "expireTime": "20270101-12:00:00",
  "alertRepeatable": 0,
  "outsideRth": 0,
  "sendMessage": 1,
  "email": "user@domain.net",
  "iTWSOrdersOnly": 0,
  "showPopup": 0,
  "tif": "GTD",
  "conditions": [
    {
      "conidex": "265598@SMART",
      "logicBind": "n",
      "operator": "<=",
      "triggerMethod": "0",
      "type": 1,
      "value": "183.34"
    }
  ]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| request_id | integer | Always returns null |
| order_id | integer | Signifies tracking ID for given alert |
| success | boolean | Displays result status of alert request |
| text | string | Response message to clarify success status reason |
| order_status | string | Returns null |
| warning_message | string | Returns null |

#### Example Response

```json
{
  "request_id": null,
  "order_id": 9876543210,
  "success": true,
  "text": "Submitted",
  "order_status": null,
  "warning_message": null
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get MTA Alert

Retrieve information about your MTA alert.

Each login user only has one mobile trading assistant (MTA) alert with its own unique tool id that cannot be changed. MTA alerts can not be created or deleted, only modified. When modified a new order ID is generated.

- **Method:** `GET`
- **URL:** `/iserver/account/mta`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| account | string | Requestor's account ID |
| order_id | int | Alert's tracking ID. Can be used for modifying or deleting alerts |
| alertName | string | Human readable name of the alert |
| tif | string | Time in Force effective for the Alert |
| expire_time | string | Returns the UTC formatted date used in GTD orders |
| alert_active | int | Returns if the alert is active or disabled |
| alert_repeatable | int | Returns if the alert can be sent more than once |
| alert_email | string | Returns the designated email address for sendMessage functionality |
| alert_send_message | int | Returns whether or not the alert will send an email |
| alert_message | string | Returns the body content of what your alert will report once triggered |
| alert_show_popup | int | Returns whether or not the alert will trigger TWS Pop-up messages |
| alert_play_audio | int | Returns whether or not the alert will play audio |
| order_status | string | Always returns "Presubmitted" |
| alert_triggered | int | Returns whether or not the alert was triggered yet |
| fg_color | string | Always returns "#FFFFFF". Can be ignored |
| bg_color | string | Always returns "#000000". Can be ignored |
| order_not_editable | bool | Returns if the order can be edited |
| itws_orders_only | int | Returns whether or not the alert will trigger mobile notifications |
| alert_mta_currency | string | Returns currency set for MTA alerts. Only valid for alert type 8 & 9 |
| alert_mta_defaults | string | Returns current MTA default values |
| tool_id | int | Tracking ID for MTA alerts only. Returns null for standard alerts |
| time_zone | string | Returned for time-specific conditions |
| alert_default_type | int | Returns default type set for alerts. Configured in Client Portal |
| condition_size | int | Returns the total number of conditions in the alert |
| condition_outside_rth | int | Returns whether or not the alert will trigger outside of regular trading hours |
| conditions | array\<Condition\> | Returns all conditions. See nested table below |

**`Condition` object:**

| Field | Type | Description |
|-------|------|-------------|
| condition_type | int | Returns the type of condition set |
| conidex | string | Returns full conidex in the format "conid@exchange" |
| contract_description_1 | string | Includes relevant descriptions (if applicable) |
| condition_operator | string | Returns condition set for alert |
| condition_trigger_method | int | Returns triggerMethod value set |
| condition_value | string | Returns value set |
| condition_logic_bind | string | Returns logic_bind value set |
| condition_time_zone | string | Returns timeZone value set |

#### Example Response

```json
{
  "account": "U1234567",
  "order_id": 9998887776,
  "alert_name": null,
  "tif": "GTC",
  "expire_time": null,
  "alert_active": 1,
  "alert_repeatable": 1,
  "alert_email": null,
  "alert_send_message": 1,
  "alert_message": null,
  "alert_show_popup": 0,
  "alert_play_audio": null,
  "order_status": "Inactive",
  "alert_triggered": false,
  "fg_color": "#000000",
  "bg_color": "#AFAFAF",
  "order_not_editable": false,
  "itws_orders_only": 0,
  "alert_mta_currency": "USD",
  "alert_mta_defaults": "9:STATE=1,MIN=-43115000,MAX=43115000,STEP=500,DEF_MIN=-4311500,DEF_MAX=4311500|...",
  "tool_id": 55834574848,
  "time_zone": "GMT (GMT),GMT (Africa/Abidjan),...",
  "alert_default_type": null,
  "condition_size": 0,
  "condition_outside_rth": 0,
  "conditions": []
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Activate or deactivate an alert

Activate or Deactivate existing alerts created for this account. This does not delete alerts, but disables notifications until reactivated.

- **Method:** `POST`
- **URL:** `/iserver/account/{accountId}/alert/activate`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Identifier for the unique account to retrieve information from. Value Format: "DU1234567" |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| alertId | int | yes | The alertId, or order_id, received from order creation or the list of alerts |
| alertActive | int | yes | Set whether or not the alert should be active (1) or inactive (0) |

#### Example Request

```json
{
  "alertId": 9876543210,
  "alertActive": 1
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| request_id | int | Returns null |
| order_id | int | Returns requested alertId or order_id |
| success | bool | Returns true if successful |
| text | string | Adds additional information for "success" status |
| failure_list | string | If "success" returns false, will list failed order IDs |

#### Example Response

```json
{
  "request_id": null,
  "order_id": 9876543210,
  "success": true,
  "text": "Request was submitted",
  "failure_list": null
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Delete an alert

Permanently delete an existing alert. If alertId is 0, it will delete all alerts. If you delete an MTA alert, it will reset to the default state.

- **Method:** `DELETE`
- **URL:** `/iserver/account/{accountId}/alert/{alertId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Identifier for the unique account to retrieve information from. Value Format: "DU1234567" |
| alertId | path | int | yes | order_id returned from the original alert creation, or from the list of available alerts. Pass 0 to delete all alerts |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| request_id | int | Returns null |
| order_id | int | Returns requested alertId or order_id |
| success | bool | Returns true if successful |
| text | string | Adds additional information for "success" status |
| failure_list | string | If "success" returns false, will list failed order IDs |

#### Example Response

```json
{
  "request_id": null,
  "order_id": 9876543210,
  "success": true,
  "text": "Request was submitted",
  "failure_list": null
}
```

## Contract

Endpoints for searching, retrieving, and inspecting contract definitions including stocks, futures, options, warrants, bonds, and currency pairs. Provides security definitions, trading rules, algo parameters, strike prices, and trading schedules.

---

[↑ Back to Table of Contents](#table-of-contents)


### Search the security definition by Contract ID

Returns a list of security definitions for the given conids.

- **Method:** `GET`
- **URL:** `/trsrv/secdef`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conids | query | string | yes | A comma separated series of contract IDs. Value Format: "265598" or "265598,8314" |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| secdef | array\<SecDef\> | Returns the contents of the request within the array |

**`SecDef` object:**

| Field | Type | Description |
|-------|------|-------------|
| conid | int | Returns the contract ID |
| currency | string | Returns the traded currency for the contract |
| time | int | Returns amount of time in ms to generate the data |
| chineseName | string | Returns the Chinese characters for the symbol |
| allExchanges | string | Returns a comma-separated series of exchanges the given symbol can trade on |
| listingExchange | string | Returns the primary or listing exchange the contract is hosted on |
| countryCode | string | Returns the country code the contract is traded on |
| name | string | Returns the company name |
| assetClass | string | Returns the asset class or security type of the contract |
| expiry | string | Returns the expiry of the contract. Returns null for non-expiry instruments |
| lastTradingDay | string | Returns the last trading day of the contract |
| group | string | Returns the group or industry the contract is affiliated with |
| putOrCall | string | Returns if the contract is a Put or Call option |
| sector | string | Returns the contract's sector |
| sectorGroup | string | Returns the sector's group |
| strike | string | Returns the strike of the contract |
| ticker | string | Returns the ticker symbol of the traded contract |
| undConid | int | Returns the contract's underlyer |
| multiplier | float | Returns the contract multiplier |
| type | string | Returns stock type |
| hasOptions | bool | Returns if contract has tradable options contracts |
| fullName | string | Returns symbol name for requested contract |
| isUS | bool | Returns if the contract is US based or not |
| incrementRules | array\<IncrementRule\> | Returns rules regarding incrementation for order placement. See nested table below |
| displayRule | object | Returns display rules for the contract. See nested table below |
| isEventContract | bool | Returns if the contract is an event contract or not |
| pageSize | int | Returns the content size of the request |

**`IncrementRule` object:**

| Field | Type | Description |
|-------|------|-------------|
| lowerEdge | float | Lower edge of the price range for this increment |
| increment | float | Price increment for orders within this range |

**`displayRule` object:**

| Field | Type | Description |
|-------|------|-------------|
| magnification | int | Magnification factor for display |
| displayRuleStep | array\<DisplayRuleStep\> | Display rule steps. See nested table below |

**`DisplayRuleStep` object:**

| Field | Type | Description |
|-------|------|-------------|
| decimalDigits | int | Number of decimal digits to display |
| lowerEdge | float | Lower edge of the price range for this display rule |
| wholeDigits | int | Number of whole digits to display |

#### Example Response

```json
{
  "secdef": [
    {
      "conid": 265598,
      "currency": "USD",
      "time": 43,
      "chineseName": "苹果公司",
      "allExchanges": "AMEX,NYSE,CBOE,...",
      "listingExchange": "NASDAQ",
      "countryCode": "US",
      "name": "APPLE INC",
      "assetClass": "STK",
      "expiry": null,
      "lastTradingDay": null,
      "group": "Computers",
      "putOrCall": null,
      "sector": "Technology",
      "sectorGroup": "Computers",
      "strike": "0",
      "ticker": "AAPL",
      "undConid": 0,
      "multiplier": 0.0,
      "type": "COMMON",
      "hasOptions": true,
      "fullName": "AAPL",
      "isUS": true,
      "incrementRules": [
        {
          "lowerEdge": 0.0,
          "increment": 0.01
        }
      ],
      "displayRule": {
        "magnification": 0,
        "displayRuleStep": [
          {
            "decimalDigits": 2,
            "lowerEdge": 0.0,
            "wholeDigits": 4
          }
        ]
      },
      "isEventContract": false,
      "pageSize": 100
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### All Conids by Exchange

Send out a request to retrieve all contracts made available on a requested exchange. This returns all contracts that are tradable on the exchange, even those that are not using the exchange as their primary listing.

> **Note:** This is only available for Stock contracts.

- **Method:** `GET`
- **URL:** `/trsrv/all-conids`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| exchange | query | string | yes | Specify a single exchange to receive conids for |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| ticker | string | Returns the ticker symbol of the contract |
| conid | int | Returns the contract identifier of the returned contract |
| exchange | string | Returns the exchange of the returned contract |

#### Example Response

```json
[
  {
    "ticker": "BMO",
    "conid": 5094,
    "exchange": "NYSE"
  },
  {
    "ticker": "ZKH",
    "conid": 671347171,
    "exchange": "NYSE"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Contract information by Contract ID

Requests full contract details for the given conid.

- **Method:** `GET`
- **URL:** `/iserver/contract/{conid}/info`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | path | string | yes | Contract ID for the desired contract information |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| cfi_code | string | CFI code of the contract |
| symbol | string | Ticker symbol of the contract |
| cusip | string | CUSIP identifier of the contract |
| expiry_full | string | Full expiry date of the contract |
| con_id | int | Contract ID of the requested contract |
| maturity_date | string | Maturity, or expiration date, of the requested contract |
| industry | string | Industry classification of the contract |
| instrument_type | string | Security type of the requested contract |
| trading_class | string | Trading class of the contract |
| valid_exchanges | string | Comma-separated list of all valid exchanges for the contract |
| allow_sell_long | bool | Whether sell long is allowed |
| is_zero_commission_security | bool | Whether the security has zero commission |
| local_symbol | string | Local symbol of the contract |
| contract_clarification_type | string | Contract clarification type |
| classifier | string | Classifier for the contract |
| currency | string | National currency of the requested contract |
| text | string | Additional text information |
| underlying_con_id | int | Contract ID of the underlying instrument |
| r_t_h | bool | Whether regular trading hours apply |
| multiplier | string | Contract multiplier |
| underlying_issuer | string | Issuer of the underlying instrument |
| contract_month | string | Contract month |
| company_name | string | Company name of the requested contract |
| smart_available | bool | Whether SMART routing is available |
| exchange | string | Traded exchange of the requested contract |
| category | string | Category classification of the contract |

#### Example Response

```json
{
  "cfi_code": "",
  "symbol": "AAPL",
  "cusip": null,
  "expiry_full": null,
  "con_id": 265598,
  "maturity_date": null,
  "industry": "Computers",
  "instrument_type": "STK",
  "trading_class": "NMS",
  "valid_exchanges": "SMART,AMEX,NYSE,CBOE,...",
  "allow_sell_long": false,
  "is_zero_commission_security": false,
  "local_symbol": "AAPL",
  "contract_clarification_type": null,
  "classifier": null,
  "currency": "USD",
  "text": null,
  "underlying_con_id": 0,
  "r_t_h": true,
  "multiplier": null,
  "underlying_issuer": null,
  "contract_month": null,
  "company_name": "APPLE INC",
  "smart_available": true,
  "exchange": "SMART",
  "category": "Computers"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Currency Pairs

Obtains available currency pairs corresponding to the given target currency.

- **Method:** `GET`
- **URL:** `/iserver/currency/pairs`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| currency | query | string | yes | Specify the target currency you would like to receive official pairs of. Valid Structure: "USD" |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| {currency} | array\<CurrencyPair\> | Dynamic key matching the requested currency. Contains available pairs. See nested table below |

**`CurrencyPair` object:**

| Field | Type | Description |
|-------|------|-------------|
| symbol | string | The official symbol of the given currency pair |
| conid | int | The official contract identifier of the given currency pair |
| ccyPair | string | Returns the counterpart currency |

#### Example Response

```json
{
  "USD": [
    {
      "symbol": "USD.SGD",
      "conid": 37928772,
      "ccyPair": "SGD"
    },
    {
      "symbol": "USD.RUB",
      "conid": 28454968,
      "ccyPair": "RUB"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Currency Exchange Rate

Obtains the exchange rates of the currency pair.

- **Method:** `GET`
- **URL:** `/iserver/exchangerate`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| source | query | string | yes | Specify the base currency to request data for. Valid Structure: "AUD" |
| target | query | string | yes | Specify the quote currency to request data for. Valid Structure: "USD" |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| rate | float | Returns the exchange rate for the currency pair |

#### Example Response

```json
{
  "rate": 0.67005002
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Find all Info and Rules for a given contract

Returns both contract info and rules from a single endpoint. For only contract rules, use the endpoint /iserver/contract/rules. For only contract info, use the endpoint /iserver/contract/{conid}/info.

- **Method:** `GET`
- **URL:** `/iserver/contract/{conid}/info-and-rules`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | path | string | yes | Contract identifier for the given contract |
| isBuy | query | bool | no | Indicates whether you are searching for Buy or Sell order rules. Set to true for Buy Orders, set to false for Sell Orders (assumed optional) |

#### Response Body

The response includes the same contract info fields as the Contract information by Contract ID endpoint, plus an additional `rules` object.

| Field | Type | Description |
|-------|------|-------------|
| cfi_code | string | Classification of Financial Instrument codes |
| symbol | string | Underlying symbol |
| cusip | string | Returns the CUSIP for the given instrument. Only used in BOND trading |
| expiry_full | string | Returns the expiration month of the contract. Format: "YYYYMM" |
| con_id | int | Indicates the contract identifier of the given contract |
| maturity_date | string | Indicates the final maturity date of the given contract. Format: "YYYYMMDD" |
| industry | string | Specific group of companies or businesses |
| instrument_type | string | Asset class of the instrument |
| trading_class | string | Designated trading class of the contract |
| valid_exchanges | string | Comma separated list of supported exchanges or trading venues |
| allow_sell_long | bool | Allowed to sell shares you own |
| is_zero_commission_security | bool | Indicates if the contract supports zero commission trading |
| local_symbol | string | Contract's symbol from primary exchange. For options it is the OCC symbol |
| contract_clarification_type | string | Contract clarification type |
| classifier | string | Classifier for the contract |
| currency | string | Base currency contract is traded in |
| text | string | Indicates the display name of the contract, as shown with Client Portal |
| underlying_con_id | int | Underlying contract identifier for the requested contract |
| r_t_h | bool | Indicates if the contract can be traded outside regular trading hours or not |
| multiplier | string | Indicates the multiplier of the contract |
| underlying_issuer | string | Indicates the issuer of the underlying |
| contract_month | string | Indicates the year and month the contract expires. Format: "YYYYMM" |
| company_name | string | Indicates the name of the company or index |
| smart_available | bool | Indicates if the contract can be smart routed or not |
| exchange | string | Indicates the primary exchange for which the contract can be traded |
| category | string | Indicates the industry category of the instrument |
| rules | object | Contract trading rules. See nested table below |

**`rules` object:**

| Field | Type | Description |
|-------|------|-------------|
| algoEligible | bool | Whether the contract supports algo orders |
| overnightEligible | bool | Whether the contract supports overnight orders |
| costReport | bool | Whether a cost report is available |
| canTradeAcctIds | array\<string\> | Account IDs that can trade this contract |
| error | string | Error message, if any |
| orderTypes | array\<string\> | Supported order types for this contract |
| ibAlgoTypes | array\<string\> | Supported IB algo order types |
| fraqTypes | array\<string\> | Supported fractional order types |
| forceOrderPreview | bool | Whether order preview is forced |
| cqtTypes | array\<string\> | Supported cash quantity order types |
| orderDefaults | object | Default order values keyed by order type |
| orderTypesOutside | array\<string\> | Order types available outside regular trading hours |
| defaultSize | int | Default order size |
| cashSize | float | Default cash size |
| sizeIncrement | int | Size increment for orders |
| tifTypes | array\<string\> | Supported time-in-force types with their applicable order types |
| tifDefaults | object | Default time-in-force settings |
| limitPrice | float | Current limit price |
| stopprice | float | Current stop price |
| orderOrigination | string | Order origination |
| preview | bool | Whether preview is enabled |
| displaySize | int | Display size for iceberg orders |
| fraqInt | int | Fractional interval |
| cashCcy | string | Cash currency |
| cashQtyIncr | int | Cash quantity increment |
| priceMagnifier | int | Price magnifier |
| negativeCapable | bool | Whether negative prices are supported |
| incrementType | int | Increment type |
| incrementRules | array\<IncrementRule\> | Price increment rules |
| hasSecondary | bool | Whether secondary orders are supported |
| increment | float | Price increment |
| incrementDigits | int | Number of increment digits |

#### Example Response

```json
{
  "cfi_code": "",
  "symbol": "AAPL",
  "cusip": null,
  "expiry_full": null,
  "con_id": 265598,
  "maturity_date": null,
  "industry": "Computers",
  "instrument_type": "STK",
  "trading_class": "NMS",
  "valid_exchanges": "SMART,AMEX,NYSE,CBOE,...",
  "allow_sell_long": false,
  "is_zero_commission_security": false,
  "local_symbol": "AAPL",
  "currency": "USD",
  "company_name": "APPLE INC",
  "smart_available": true,
  "exchange": "SMART",
  "category": "Computers",
  "rules": {
    "algoEligible": true,
    "overnightEligible": true,
    "costReport": false,
    "canTradeAcctIds": ["U1234567"],
    "error": null,
    "orderTypes": ["limit", "midprice", "market", "stop", "stop_limit", "mit", "lit", "trailing_stop", "trailing_stop_limit", "relative", "marketonclose", "limitonclose"],
    "ibAlgoTypes": ["limit", "stop_limit", "lit", "trailing_stop_limit", "relative", "marketonclose", "limitonclose"],
    "fraqTypes": ["limit", "market", "stop", "stop_limit", "mit", "lit", "trailing_stop", "trailing_stop_limit"],
    "forceOrderPreview": false,
    "cqtTypes": ["limit", "market", "stop", "stop_limit", "mit", "lit", "trailing_stop", "trailing_stop_limit"],
    "orderDefaults": {
      "LMT": { "LP": "197.93" }
    },
    "orderTypesOutside": ["limit", "stop_limit", "lit", "trailing_stop_limit", "relative"],
    "defaultSize": 100,
    "cashSize": 0.0,
    "sizeIncrement": 100,
    "tifTypes": ["IOC/MARKET,LIMIT,...", "GTC/o,a", "OPG/LIMIT,MARKET,a", "GTD/o,a", "DAY/o,a"],
    "tifDefaults": { "TIF": "DAY", "SIZE": "100.00" },
    "limitPrice": 197.93,
    "stopprice": 197.93,
    "preview": true,
    "fraqInt": 4,
    "cashCcy": "USD",
    "cashQtyIncr": 500,
    "negativeCapable": false,
    "incrementType": 1,
    "incrementRules": [{ "lowerEdge": 0.0, "increment": 0.01 }],
    "hasSecondary": true,
    "increment": 0.01,
    "incrementDigits": 2
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Algo Params by Contract ID

Returns supported IB Algos for contract. A pre-flight request must be submitted before retrieving information.

- **Method:** `GET`
- **URL:** `/iserver/contract/{conid}/algos`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | path | string | yes | Contract identifier for the requested contract of interest |
| algos | query | string | no | List of algo ids delimited by ";" to filter by. Max of 8 algo ids can be specified. Case sensitive to algo id |
| addDescription | query | string | no | Whether or not to add algo descriptions to response. Set to "1" for yes, "0" for no |
| addParams | query | string | no | Whether or not to show algo parameters. Set to "1" for yes, "0" for no |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| algos | array\<Algo\> | Contains all relevant algos for the contract. See nested table below |

**`Algo` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | Common name of the algo |
| id | string | Algo identifier used for requests |
| parameters | array\<AlgoParam\> | All parameters relevant to the given algo. Only returned if addParams=1. See nested table below |

**`AlgoParam` object:**

| Field | Type | Description |
|-------|------|-------------|
| guiRank | int | Positional ranking for the algo. Used for Client Portal |
| defaultValue | varies | Default parameter value |
| name | string | Parameter name |
| id | string | Parameter identifier for the algo |
| description | string | Description of the parameter |
| legalStrings | array\<string\> | Allowed values for the parameter |
| required | string | States whether the parameter is required. Returns a string representation of a boolean |
| valueClassName | string | Returns the variable type of the parameter |
| minValue | float | Minimum allowed value |
| maxValue | float | Maximum allowed value |
| enabledConditions | array\<string\> | Conditions that control when this parameter is enabled |

#### Example Response

```json
{
  "algos": [
    {
      "name": "Adaptive",
      "id": "Adaptive",
      "parameters": [
        {
          "guiRank": 1,
          "defaultValue": "Normal",
          "name": "Adaptive order priority/urgency",
          "id": "adaptivePriority",
          "legalStrings": ["Urgent", "Normal", "Patient"],
          "required": "true",
          "valueClassName": "String"
        }
      ]
    },
    {
      "name": "VWAP",
      "id": "Vwap",
      "parameters": [
        {
          "guiRank": 5,
          "defaultValue": false,
          "name": "Attempt to never take liquidity",
          "id": "noTakeLiq",
          "valueClassName": "Boolean"
        },
        {
          "guiRank": 1,
          "minValue": 0.01,
          "maxValue": 50,
          "name": "Max Percentage",
          "description": "From 0.01 to 50.0",
          "id": "maxPctVol",
          "valueClassName": "Double"
        }
      ]
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Bond Filter Information

Request a list of filters relating to a given Bond issuerID. The issuerId is retrieved from /iserver/secdef/search and can be used in /iserver/secdef/info for retrieving conIds.

- **Method:** `GET`
- **URL:** `/iserver/secdef/bond-filters`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| symbol | query | string | yes | This should always be set to "BOND" |
| issuerId | query | string | yes | Specifies the issuerId value used to designate the bond issuer type |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| bondFilters | array\<BondFilter\> | Contains all filters pertaining to the given issuerId. See nested table below |

**`BondFilter` object:**

| Field | Type | Description |
|-------|------|-------------|
| displayText | string | An identifier used to document returned options/values. This can be thought of as a key value |
| columnId | int | Used for user interfaces. Internal use only |
| options | array\<BondFilterOption\> | Contains all objects with values corresponding to the parent displayText key. See nested table below |

**`BondFilterOption` object:**

| Field | Type | Description |
|-------|------|-------------|
| text | string | A text value indicating the standardized value format such as plaintext dates, rather than solely numerical values |
| value | string | Returns value directly correlating to the displayText key. This may include exchange, maturity date, issue date, coupon, or currency |

#### Example Response

```json
{
  "bondFilters": [
    {
      "displayText": "Exchange",
      "columnId": 0,
      "options": [{ "value": "SMART" }]
    },
    {
      "displayText": "Maturity Date",
      "columnId": 27,
      "options": [{ "text": "Jan 2025", "value": "202501" }]
    },
    {
      "displayText": "Issue Date",
      "columnId": 28,
      "options": [{ "text": "Sep 18 2014", "value": "20140918" }]
    },
    {
      "displayText": "Coupon",
      "columnId": 25,
      "options": [{ "value": "1.301" }]
    },
    {
      "displayText": "Currency",
      "columnId": 5,
      "options": [{ "value": "EUR" }]
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Contract by Symbol

Search by underlying symbol or company name. Relays back what derivative contract(s) it has. This endpoint must be called before using /secdef/info. For bonds, enter the family type in the symbol field to receive the issuerID used in the /iserver/secdef/info endpoint.

- **Method:** `GET`
- **URL:** `/iserver/secdef/search`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| symbol | query | string | yes | Underlying symbol of interest. May also pass company name if name is set to true, or bond issuer type to retrieve bonds |
| name | query | bool | no | Determines if symbol reflects company name or ticker symbol. If true, only receives limited response: conid, companyName, companyHeader and symbol. The inclusion of the name field will prohibit the /iserver/secdef/strikes endpoint from returning data |
| secType | query | string | no | Declares underlying security type. One of: `STK`, `IND`, `BOND` |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| conid | string | Conid of the given contract |
| companyHeader | string | Extended company name and primary exchange |
| companyName | string | Name of the company |
| symbol | string | Company ticker symbol |
| description | string | Primary exchange of the contract |
| restricted | bool | Returns if the contract is available for trading |
| secType | string | Given contract's security type |
| sections | array\<Section\> | Derivative sections available. See nested table below |
| issuers | array\<Issuer\> | (Bonds only) Array of objects containing the id and name for each bond issuer. See nested table below |

**`Section` object:**

| Field | Type | Description |
|-------|------|-------------|
| secType | string | Security type of the derivative |
| months | string | Semicolon-separated list of available expiration months. Format: "JANYY;FEBYY;MARYY" |
| symbol | string | Symbol of the instrument |
| exchange | string | Semicolon-separated list of exchanges. Format: "EXCH;EXCH;EXCH" |

**`Issuer` object (Bonds only):**

| Field | Type | Description |
|-------|------|-------------|
| id | string | Issuer ID for the given contract |
| name | string | Name of the issuer |

#### Example Response

```json
[
  {
    "conid": "43645865",
    "companyHeader": "IBKR INTERACTIVE BROKERS GRO-CL A (NASDAQ)",
    "companyName": "INTERACTIVE BROKERS GRO-CL A (NASDAQ)",
    "symbol": "IBKR",
    "description": null,
    "restricted": null,
    "sections": [],
    "secType": "STK"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Contract Rules

Returns trading related rules for a specific contract and side.

- **Method:** `POST`
- **URL:** `/iserver/contract/rules`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| conid | int | yes | Contract identifier for the interested contract |
| exchange | string | no | Designate the exchange you wish to receive information for in relation to the contract |
| isBuy | bool | no | Side of the market rules apply to. Set to true for Buy Orders, set to false for Sell Orders. Defaults to true |
| modifyOrder | bool | no | Used to find trading rules related to an existing order |
| orderId | int | conditional | Specify the order identifier used for tracking a given order. Required if modifyOrder is true |

#### Example Request

```json
{
  "conid": 265598,
  "exchange": "SMART",
  "isBuy": true,
  "modifyOrder": true,
  "orderId": 1234567890
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| algoEligible | bool | Indicates if the contract can trade algos or not |
| overnightEligible | bool | Indicates if outside RTH trading is permitted for the instrument |
| costReport | bool | Indicates whether or not a cost report has been requested (Client Portal only) |
| canTradeAcctIds | array\<string\> | Indicates permitted account IDs that may trade the contract |
| error | string | If rules information can not be received for any reason, it will be expressed here |
| orderTypes | array\<string\> | Indicates permitted order types for use with standard quantity trading |
| ibAlgoTypes | array\<string\> | Indicates permitted algo types for use with the given contract |
| fraqTypes | array\<string\> | Indicates permitted order types for use with fractional trading |
| forceOrderPreview | bool | Indicates if the order preview is forced upon the user before submission |
| cqtTypes | array\<string\> | Indicates accepted order types for use with cash quantity |
| orderDefaults | object | Indicates default order type for the given security type |
| orderTypesOutside | array\<string\> | Indicates permitted order types for use outside of regular trading hours |
| defaultSize | int | Default total quantity value for orders |
| cashSize | float | Default cash value quantity |
| sizeIncrement | int | Indicates quantity increase for the contract |
| tifTypes | array\<string\> | Indicates allowed TIF types supported for the contract |
| tifDefaults | object | Object containing details about your TIF value defaults |
| limitPrice | float | Default limit price for the given contract |
| stopprice | float | Default stop price for the given contract |
| orderOrigination | string | Order origin designation for US securities options and Options Clearing Corporation |
| preview | bool | Indicates if the order preview is required (for Client Portal only) |
| displaySize | int | Display size for iceberg orders |
| fraqInt | int | Indicates decimal places for fractional order size |
| cashCcy | string | Indicates base currency for the instrument |
| cashQtyIncr | int | Indicates cash quantity increment rules |
| priceMagnifier | int | Signifies if a contract is not trading in the standard cash denomination. Null for standard instruments |
| negativeCapable | bool | Indicates if the value of the contract can be negative (true) or if it is always positive (false) |
| incrementType | int | Indicates the type of increment style |
| incrementRules | array\<IncrementRule\> | Indicates increment rule values including lowerEdge and increment value |
| hasSecondary | bool | Whether secondary orders are supported |
| modTypes | array\<string\> | Lists the available order types supported when modifying the order |
| increment | float | Minimum increment values for prices |
| incrementDigits | int | Number of decimal places to indicate the increment value |

#### Example Response

```json
{
  "algoEligible": true,
  "overnightEligible": true,
  "costReport": false,
  "canTradeAcctIds": ["U1234567"],
  "error": null,
  "orderTypes": ["limit", "midprice", "market", "stop", "stop_limit", "mit", "lit", "trailing_stop", "trailing_stop_limit", "relative", "marketonclose", "limitonclose"],
  "ibAlgoTypes": ["limit", "stop_limit", "lit", "trailing_stop_limit", "relative", "marketonclose", "limitonclose"],
  "fraqTypes": [],
  "forceOrderPreview": false,
  "cqtTypes": ["limit", "market", "stop", "stop_limit", "mit", "lit", "trailing_stop", "trailing_stop_limit"],
  "orderDefaults": { "LMT": { "LP": "549000.00" } },
  "orderTypesOutside": ["limit", "stop_limit", "lit", "trailing_stop_limit", "relative"],
  "defaultSize": 100,
  "cashSize": 0.0,
  "sizeIncrement": 1,
  "tifTypes": ["IOC/MARKET,LIMIT,...", "GTC/o,a", "OPG/LIMIT,MARKET,a", "GTD/o,a", "DAY/o,a"],
  "tifDefaults": { "TIF": "DAY", "SIZE": "100.00" },
  "limitPrice": 549000.0,
  "stopprice": 549000.0,
  "preview": true,
  "fraqInt": 0,
  "cashCcy": "USD",
  "cashQtyIncr": 500,
  "negativeCapable": false,
  "incrementType": 1,
  "incrementRules": [{ "lowerEdge": 0.0, "increment": 0.01 }],
  "hasSecondary": true,
  "increment": 0.01,
  "incrementDigits": 2
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search SecDef information by conid

Provides Contract Details of Futures, Options, Warrants, Cash and CFDs based on conid. For all instruments, /iserver/secdef/search must be called first. For derivatives such as Options, Warrants, and Futures Options, you will need to query /iserver/secdef/strikes as well.

- **Method:** `GET`
- **URL:** `/iserver/secdef/info`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | string | yes | Contract identifier of the underlying. May also pass the final derivative conid directly |
| sectype | query | string | yes | Security type of the requested contract of interest |
| month | query | string | conditional | Expiration month for the given derivative. Required for derivatives |
| exchange | query | string | no | Designate the exchange you wish to receive information for in relation to the contract |
| strike | query | string | conditional | Set the strike price for the requested contract details. Required for Options and Futures Options |
| right | query | string | conditional | Set the right for the given contract. One of: `C` (Call), `P` (Put). Required for Options |
| issuerId | query | string | conditional | Set the issuerId for the given bond issuer type. Required for Bonds. Example: "e1234567" |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| conid | int | Contract Identifier of the given contract |
| symbol | string | Ticker symbol for the given contract |
| secType | string | Security type for the given contract |
| exchange | string | Exchange requesting data for |
| listingExchange | string | Primary listing exchange for the given contract |
| right | string | Right (P or C) for the given contract |
| strike | float | Returns the given strike value for the given contract |
| currency | string | Traded currency allowed for the given contract |
| cusip | string | CUSIP identifier |
| coupon | string | Coupon information for the contract |
| desc1 | string | Primary description of the contract |
| desc2 | string | Secondary description of the contract |
| maturityDate | string | Date of expiration for the given contract. Format: "YYYYMMDD" |
| multiplier | string | Contract multiplier |
| tradingClass | string | Trading class of the contract |
| validExchanges | string | Comma-separated list of all valid exchanges the contract can be traded on |

#### Example Response

```json
[
  {
    "conid": 667629330,
    "symbol": "AAPL",
    "secType": "OPT",
    "exchange": "SMART",
    "listingExchange": null,
    "right": "P",
    "strike": 195.0,
    "currency": "USD",
    "cusip": null,
    "coupon": "No Coupon",
    "desc1": "AAPL",
    "desc2": "JAN 05 '24 195 Put",
    "maturityDate": "20240105",
    "multiplier": "100",
    "tradingClass": "AAPL",
    "validExchanges": "SMART,AMEX,CBOE,PHLX,PSE,ISE,BOX,BATS,NASDAQOM,..."
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Search Strikes by Underlying Contract ID

Query to receive a list of potential strikes supported for a given underlying. This endpoint will always return empty arrays unless /iserver/secdef/search is called for the same underlying symbol beforehand. The inclusion of the name field with /iserver/secdef/search will prohibit the strikes endpoint from returning data.

- **Method:** `GET`
- **URL:** `/iserver/secdef/strikes`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | string | yes | Contract Identifier number for the underlying |
| sectype | query | string | yes | Security type of the derivatives you are looking for. One of: `OPT`, `WAR` |
| month | query | string | yes | Expiration month and year for the given underlying. Format: "{3 char month}{2 char year}" (e.g., "AUG23") |
| exchange | query | string | no | Exchange from which derivatives should be retrieved from. Default value is set to SMART |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| call | array\<float\> | Array of potential call strikes for the instrument |
| put | array\<float\> | Array of potential put strikes for the instrument |

#### Example Response

```json
{
  "call": [185.0, 190.0, 195.0, 200.0],
  "put": [185.0, 190.0, 195.0, 200.0]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Security Future by Symbol

Returns a list of non-expired future contracts for given symbol(s).

- **Method:** `GET`
- **URL:** `/trsrv/futures`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| symbols | query | string | yes | Indicate the symbol(s) of the underlier you are trying to retrieve futures on. Accepts comma delimited string of symbols |

#### Response Body

Response is an object with dynamic keys matching the requested symbols. Each key contains an array of future contract objects.

**`FutureContract` object:**

| Field | Type | Description |
|-------|------|-------------|
| symbol | string | The requested symbol value |
| conid | int | Contract identifier for the specific symbol |
| underlyingConid | int | Contract identifier for the future's underlying contract |
| expirationDate | int | Expiration date of the specific future contract |
| ltd | int | Last trade date of the future contract |
| shortFuturesCutOff | int | Represents the final day for contract rollover for shorted futures |
| longFuturesCutOff | int | Represents the final day for contract rollover for long futures |

#### Example Response

```json
{
  "ES": [
    {
      "symbol": "ES",
      "conid": 495512552,
      "underlyingConid": 11004968,
      "expirationDate": 20231215,
      "ltd": 20231214,
      "shortFuturesCutOff": 20231214,
      "longFuturesCutOff": 20231214
    }
  ],
  "MES": [
    {
      "symbol": "MES",
      "conid": 586139726,
      "underlyingConid": 362673777,
      "expirationDate": 20231215,
      "ltd": 20231215,
      "shortFuturesCutOff": 20231215,
      "longFuturesCutOff": 20231215
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Security Stocks by Symbol

Returns an object containing all stock contracts for given symbol(s).

- **Method:** `GET`
- **URL:** `/trsrv/stocks`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| symbols | query | string | yes | Comma-separated list of stock symbols. Symbols must contain only capitalized letters |

#### Response Body

Response is an object with dynamic keys matching the requested symbols. Each key contains an array of stock match objects.

**`StockMatch` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | Full company name for the given contract |
| chineseName | string | Chinese name for the given company |
| assetClass | string | Asset class for the given company |
| contracts | array\<StockContract\> | A series of contracts pertaining to the same company. Typically differentiated based on currency of the primary exchange. See nested table below |

**`StockContract` object:**

| Field | Type | Description |
|-------|------|-------------|
| conid | int | Contract ID for the specific contract |
| exchange | string | Primary exchange for the given contract |
| isUS | bool | States whether the contract is hosted in the United States or not |

#### Example Response

```json
{
  "AAPL": [
    {
      "name": "APPLE INC",
      "chineseName": "苹果公司",
      "assetClass": "STK",
      "contracts": [
        {
          "conid": 265598,
          "exchange": "NASDAQ",
          "isUS": true
        },
        {
          "conid": 38708077,
          "exchange": "MEXI",
          "isUS": false
        }
      ]
    },
    {
      "name": "LS 1X AAPL",
      "chineseName": null,
      "assetClass": "STK",
      "contracts": [
        {
          "conid": 493546048,
          "exchange": "LSEETF",
          "isUS": false
        }
      ]
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Trading Schedule by Symbol

Returns the trading schedule up to a month for the requested contract.

- **Method:** `GET`
- **URL:** `/trsrv/secdef/schedule`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| assetClass | query | string | yes | Specify the security type of the given contract. One of: `STK`, `OPT`, `FUT`, `CFD`, `WAR`, `SWP`, `FND`, `BND`, `ICS` |
| conid | query | string | yes | Provide the contract identifier to retrieve the trading schedule for |
| symbol | query | string | yes | Specify the symbol for your contract |
| exchange | query | string | no | Specify the primary exchange of your contract |
| exchangeFilter | query | string | no | Specify exchange you want to retrieve data from |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| id | string | Exchange parameter id |
| tradeVenueId | string | Reference on a trade venue of given exchange parameter |
| timezone | string | Timezone of the exchange |
| schedules | array\<Schedule\> | Always contains at least one tradingTime and zero or more session tags. See nested table below |

**`Schedule` object:**

| Field | Type | Description |
|-------|------|-------------|
| clearingCycleEndTime | string | End of clearing cycle |
| tradingScheduleDate | string | Date of the clearing schedule. 20000101 = any Sat, 20000102 = any Sun, ... 20000107 = any Fri. Any other date stands for itself |
| sessions | array\<Session\> | Liquid hours sessions. See nested table below |
| tradingtimes | array\<TradingTime\> | Full trading day times. See nested table below |

**`Session` object:**

| Field | Type | Description |
|-------|------|-------------|
| openingTime | string | Opening date time of the session |
| closingTime | string | Closing date time of the session |
| prop | string | If the whole trading day is considered LIQUID then the value "LIQUID" is returned |

**`TradingTime` object:**

| Field | Type | Description |
|-------|------|-------------|
| openingTime | string | Opening time of the trading day |
| closingTime | string | Closing time of the trading day |
| cancelDayOrders | string | Cancel time for day orders |

#### Example Response

```json
[
  {
    "id": "p102082",
    "tradeVenueId": "v13133",
    "timezone": "America/New_York",
    "schedules": [
      {
        "clearingCycleEndTime": "2000",
        "tradingScheduleDate": "20000103",
        "sessions": [
          {
            "openingTime": "0930",
            "closingTime": "1600",
            "prop": "LIQUID"
          }
        ],
        "tradingtimes": [
          {
            "openingTime": "0400",
            "closingTime": "2000",
            "cancelDayOrders": "Y"
          }
        ]
      }
    ]
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Trading Schedule (NEW)

Returns the trading schedule for the 6 total days surrounding the current trading day. Non-Trading days, such as holidays, will not be returned.

- **Method:** `GET`
- **URL:** `/contract/trading-schedule`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | string | yes | Provide the contract identifier to retrieve the trading schedule for |
| exchange | query | string | no | Accepts the exchange to retrieve data from. Primary exchange is assumed by default |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| exchange_time_zone | string | Returns the time zone the exchange trades in |
| schedules | object | A schedule object with dynamic date keys (format: "YYYYMMDD"). Each date contains trading hours. See nested tables below |

**`{date}` object:**

| Field | Type | Description |
|-------|------|-------------|
| extended_hours | array\<ExtendedHours\> | Total extended trading hours for the session. See nested table below |
| liquid_hours | array\<LiquidHours\> | Available trading hours for the regular session. See nested table below |

**`ExtendedHours` object:**

| Field | Type | Description |
|-------|------|-------------|
| cancel_daily_orders | bool | Determines if DAY orders are canceled after closing time |
| closing | int | Epoch timestamp of the exchange's close |
| opening | int | Epoch timestamp of the exchange's open |

**`LiquidHours` object:**

| Field | Type | Description |
|-------|------|-------------|
| closing | int | Epoch timestamp of the exchange's close |
| opening | int | Epoch timestamp of the exchange's open |

#### Example Response

```json
{
  "exchange_time_zone": "US/Central",
  "schedules": {
    "20251218": {
      "extended_hours": [
        {
          "cancel_daily_orders": true,
          "closing": 1766095200,
          "opening": 1766012400
        }
      ],
      "liquid_hours": [
        {
          "closing": 1766095200,
          "opening": 1766068200
        }
      ]
    },
    "20251219": {
      "extended_hours": [
        {
          "cancel_daily_orders": true,
          "closing": 1766181600,
          "opening": 1766098800
        }
      ],
      "liquid_hours": [
        {
          "closing": 1766181600,
          "opening": 1766154600
        }
      ]
    }
  }
}
```

## Event Contracts

Interactive Brokers models Event Contract instruments on options (for ForecastEx products) and futures options (for CME Group products). Event Contracts can generally be thought of as options products in the Web API, and their discovery workflow follows a familiar options-like sequence.

IB's Event Contract instrument records use the following fields inherited from the options model:

- **Underlier** — may or may not be artificial:
  - For **CME products**, a tradable Event Contract will have the relevant CME future as its underlier.
  - For **ForecastEx products**, IB has generated an artificial underlying index which serves as a container for related Event Contracts in the same product class. These artificial indices do not have any associated reference values and are purely an artifact of the option instrument model. However, they can be used to search for groups of related Event Contracts, just as with index options.
- **Symbol** — matches the symbol of the underlier and reflects the issuer's product code.
- **Trading Class** — reflects the issuer's product code for the instrument. For CME Group products, this differentiates Event Contracts from CME futures options. Many CME Group Event Contracts are assigned a Trading Class prefixed with "EC" followed by the symbol of the relevant futures product, to avoid naming collisions with other derivatives.
- **Put or Call (Right)** — Call = Yes, Put = No. ForecastEx instruments do not permit Sell orders; positions are flattened by buying the opposing contract. CME Group Event Contracts permit both buying and selling.
- **Contract Month** — an artificial value used primarily for searching and filtering. Most Event Contract products do not follow monthly series, so these values are not a meaningful attribute of the instrument. They permit filtering by calendar month.
- **Last Trade Date, Time, and Millisecond** — indicates precisely when trading in an Event Contract will cease.
- **Expected Resolution Time** — when the outcome of the tracked event is published and contracts are determined to be in or out of the money. Commonly referred to as the contract's "expiration date".
- **Expected Payout Time** — when contracts are settled and removed from accounts. Proceeds are paid out to those expiring in the money.
- **Measured Period** — as defined in the contract's question. This is the primary date used for organization of contracts (as in an options chain).
- **Strike** — the numerical value on which the event resolution hinges. Though numerical, this value need not represent a price.
- **Instrument description (local symbol)** — in the form `PRODUCT EXPIRATION STRIKE RIGHT`, where:
  - `PRODUCT` is the issuer's product identifier
  - `EXPIRATION` is the date of the instrument's resolution in the form `MmmDD'YY` (e.g., "Sep26'24")
  - `STRIKE` is the numerical value that determines the contract's moneyness at expiration
  - `RIGHT` is a value YES or NO

---

[↑ Back to Table of Contents](#table-of-contents)


### Categorization

Returns event contract category and market tree. ForecastEx forecast contracts are sorted into a three-level category hierarchy for organizational purposes. These categories are metadata rather than immutable attributes of the tradable instruments themselves and can be expected to change slightly over time. The leaf categories (level 3) contain forecast contract "Markets" — groups of tradable contracts sharing questions of the same form.

- **Method:** `GET`
- **URL:** `/forecast/category/tree`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| categories | object | Object with dynamic category ID keys. Each key contains a category object. See nested table below |

**`Category` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | Category name |
| parent_id | string | Identifier of parent category (absent for top-level categories) |
| markets | array\<Market\> | List of markets in this category (only present on leaf categories). See nested table below |

**`Market` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | Market name |
| symbol | string | Market symbol |
| exchange | string | Market exchange |
| conid | int | Market contract identifier |
| product_conid | int | Product contract identifier |

#### Example Response

```json
{
  "categories": {
    "g78664": {
      "name": "Northeast",
      "parent_id": "g17457",
      "markets": [
        {
          "name": "Northeastern US CPI",
          "symbol": "RCNET",
          "exchange": "FORECASTX",
          "conid": 831072285,
          "product_conid": 831072289
        }
      ]
    }
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Markets and Strikes

Provides all contracts for a given underlying market. ForecastEx forecast contracts are modeled as options or futures options, depending on the event they resolve against. Because they are derivative products, they are always listed against an underlier (either an index or futures contract) with its own contract ID separate from the contract IDs of the forecast contracts.

These underlier contract IDs can be used to retrieve relevant historical data sets for the underlying event, where available. For example, the GT (Global Temperature) contracts are listed against a GT index, and the index data set is historical global temperature data sourced from NOAA.

Each Market has a symbol (e.g., FF, HORC, USIP). Forecast contracts, like options, have a strike and expiration. Strike values need not be numeric — for election-related contracts it will be a candidate's name (delivered in the `strike_label` field). All contracts have a true expiration which is the resolution time after which the contract is considered resolved and ceases to exist.

A fully specified question, including the strike value and measured period, is referred to as a "strike" — similar to a specific strike row in a two-sided option chain table. Each such strike has two contracts associated with it: YES and NO. IBKR assigns a separate contract ID to both. Following from the options model: YES is a Call, and NO is a Put.

For each contract:
- The long (canonical) form of the question is delivered in the `longDescription` field.
- A shortened options-style form is delivered in the `shortDescription` field.

Note that YES and NO contracts each have their own bid/ask/last data.

- **Method:** `GET`
- **URL:** `/forecast/contract/market`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| underlyingConid | query | int | yes | Contract identifier of the underlying market |
| exchange | query | string | no | Exchange to retrieve data from (assumed optional) |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| market_name | string | Name of contract's market |
| exchange | string | Exchange that was passed in request |
| symbol | string | Market symbol |
| logo_category | string | Logo category identifier |
| exclude_historical_data | bool | Whether historical data is excluded |
| payout | float | Payout amount |
| contracts | array\<EventContract\> | List of contracts in this market. See nested table below |

**`EventContract` object:**

| Field | Type | Description |
|-------|------|-------------|
| conid | int | Market contract identifier |
| side | string | "Y" or "N" — yes or no contract |
| expiration | string | Contract expiration date in YYYYMMDD format |
| strike | float | Contract strike |
| strike_label | string | Human-readable strike label (e.g., candidate name) |
| expiry_label | string | Human-readable expiry label |
| underlying_conid | int | Underlying asset of the contract |
| time_specifier | string | Time specifier for the contract |

#### Example Response

```json
{
  "market_name": "Georgia Governor Democratic Primary",
  "exchange": "FORECASTX",
  "symbol": "GPGAD",
  "logo_category": "g17467",
  "exclude_historical_data": true,
  "payout": 1.0,
  "contracts": [
    {
      "conid": 767285167,
      "side": "Y",
      "expiration": "20260612",
      "strike": 1.0,
      "strike_label": "Stacey Abrams",
      "expiry_label": "2026",
      "underlying_conid": 766914406,
      "time_specifier": "2026.5.19"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Contract Rules

Provides contract rules for specific binary options.

- **Method:** `GET`
- **URL:** `/forecast/contract/rules`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | int | yes | Contract identifier |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| asset_class | string | Product asset class |
| description | string | Product description |
| market_name | string | Name of contract's market |
| measured_period | string | Measured period for the contract |
| threshold | string | Either strike or strike label depending on the contract |
| source_agency | string | Name of source agency |
| data_and_resolution_link | string | Link to data from source agency |
| last_trade_time | long | Last trade time in epoch seconds |
| product_code | string | Product code / symbol |
| market_rules_link | string | Link to market rules document |
| release_time | long | Release time in epoch seconds |
| payout_time | long | Payout time in epoch seconds |
| payout | string | Formatted payout amount |
| price_increment | string | Formatted price increment amount |
| exchange_timezone | string | Exchange timezone |

#### Example Response

```json
{
  "asset_class": "OPT",
  "description": "The Georgia Democratic Gubernatorial Primary determines the party nominee for governor, shaping state leadership and national political influence.",
  "market_name": "Georgia Governor Democratic Primary",
  "measured_period": "May19'26",
  "threshold": "Stacey Abrams",
  "source_agency": "Georgia Secretary of State Elections Division",
  "data_and_resolution_link": "https://sos.ga.gov/index.php/elections",
  "last_trade_time": 1781301540,
  "product_code": "GPGAD",
  "market_rules_link": "https://data.forecastex.com/regulatory/GPTermsandConditions.pdf",
  "release_time": 1781301540,
  "payout_time": 1781373600,
  "payout": "$1.00",
  "price_increment": "$0.01",
  "exchange_timezone": "US/Central"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Contract Details

Provides contract details for specific event binary options.

- **Method:** `GET`
- **URL:** `/forecast/contract/details`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | int | yes | Contract identifier |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| conid_yes | int | Contract ID of "yes" contract |
| conid_no | int | Contract ID of "no" contract |
| question | string | Contract question (e.g., "Will this happen on this date?") |
| side | string | "Y" or "N" — yes or no contract |
| strike_label | string | Strike label to display |
| strike | float | Contract strike |
| exchange | string | Contract exchange |
| expiration | string | Contract expiration in YYYYMMDD format |
| symbol | string | Contract symbol |
| category | string | Category identifier |
| logo_category | string | Logo category identifier |
| measured_period | string | Measured period for the contract |
| market_name | string | Name of contract's market |
| underlying_conid | int | Underlying asset of the contract |
| payout | float | Payout amount |

#### Example Response

```json
{
  "conid_yes": 767285167,
  "conid_no": 767285169,
  "question": "Will Stacey Abrams win the Georgia Democratic primary for governor in 2026?",
  "side": "Y",
  "strike_label": "Stacey Abrams",
  "strike": 1.0,
  "exchange": "FORECASTX",
  "expiration": "20260612",
  "symbol": "GPGAD",
  "category": "g7428",
  "logo_category": "g17467",
  "measured_period": "May19'26",
  "market_name": "Georgia Governor Democratic Primary",
  "underlying_conid": 766914406,
  "payout": 1.0
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Trading Schedule

Provides contract trading schedules for event contracts.

- **Method:** `GET`
- **URL:** `/forecast/contract/schedules`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | int | yes | Contract identifier |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| timezone | string | Exchange timezone |
| trading_schedules | array\<TradingSchedule\> | List of daily trading schedules. See nested table below |

**`TradingSchedule` object:**

| Field | Type | Description |
|-------|------|-------------|
| day_of_week | string | Day of the week |
| trading_times | array\<TradingInterval\> | List of trading time intervals. See nested table below |

**`TradingInterval` object:**

| Field | Type | Description |
|-------|------|-------------|
| open | string | Start of trading interval (e.g., "12:00 AM") |
| close | string | End of trading interval (e.g., "4:15 PM") |

#### Example Response

```json
{
  "timezone": "US/Central",
  "trading_schedules": [
    {
      "day_of_week": "Saturday",
      "trading_times": [
        {
          "open": "12:00 AM",
          "close": "4:15 PM"
        },
        {
          "open": "4:16 PM",
          "close": "11:59 PM"
        }
      ]
    },
    {
      "day_of_week": "Sunday",
      "trading_times": [
        {
          "open": "12:00 AM",
          "close": "4:15 PM"
        },
        {
          "open": "4:16 PM",
          "close": "11:59 PM"
        }
      ]
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Order Submission

Submission of orders for Event Contracts via the Web API functions like orders for any other instrument. However, it is important to note the differing mechanics between CME Group products and ForecastEx instruments:

- **CME Group** instruments can be bought and sold and function as normal futures options.
- **ForecastEx** instruments cannot be sold, only bought. To exit or reduce a position, one must buy the opposing Event Contract, and IB will net the opposing positions together automatically.

In both cases, no short selling is permitted. See the Orders category for order placement endpoints.

---

[↑ Back to Table of Contents](#table-of-contents)


### Executions and Netting

Positions in forecast contracts are opened by buying either a YES or NO contract. Positions are reduced or closed by buying the opposite contract at the same strike: NO reduces YES, and YES reduces NO.

An opening order will receive normal Bought/Bot execution reports (`"side": "B"`). However, if an account is already long YES or NO, a reduction of that position will produce a series of execution reports:

1. IB sends an execution report of Bot N contracts (`"side": "B"`). This momentarily creates a long position in the opposing contract.
2. IB sends a Netting execution report (`"side": "N"`) reducing the original position.
3. IB sends a Netting execution report (`"side": "N"`) reducing/flattening the opposing position.

The netting execution reports arrive within milliseconds of the first Bot execution. It is also possible to change the side of a position from YES to NO (or vice versa) via a single opposite-side buy order that exceeds the current position size.

## FA Allocation Management

Endpoints for financial advisors to manage allocation groups and presets across sub-accounts. Allows creating, modifying, and deleting groups with configurable allocation methods (equal, net liquidation, ratios, percentages, etc.), querying sub-account balances, and controlling preset behaviors such as auto-close and proportional allocation.

---

[↑ Back to Table of Contents](#table-of-contents)


### Allocatable Sub-Accounts

Retrieves a list of all sub-accounts and returns their net liquidity and available equity for advisors to make decisions on what accounts should be allocated and how.

- **Method:** `GET`
- **URL:** `/iserver/account/allocation/accounts`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| accounts | array\<SubAccount\> | Array containing all sub-accounts held by the advisor. See nested table below |

**`SubAccount` object:**

| Field | Type | Description |
|-------|------|-------------|
| data | array\<AccountData\> | Contains Net liquidation and available equity of the given account ID. See nested table below |
| name | string | Returns the account ID affiliated with the balance data |

**`AccountData` object:**

| Field | Type | Description |
|-------|------|-------------|
| value | string | Contains the price value affiliated with the key |
| key | string | Defines the value of the object. Expected values: "AvailableEquity", "NetLiquidation" |

#### Example Response

```json
{
  "accounts": [
    {
      "data": [
        {
          "value": "2677.89",
          "key": "NetLiquidation"
        },
        {
          "value": "2134.76",
          "key": "AvailableEquity"
        }
      ],
      "name": "U123456"
    },
    {
      "data": [
        {
          "value": "1200.88",
          "key": "NetLiquidation"
        },
        {
          "value": "1000.56",
          "key": "AvailableEquity"
        }
      ],
      "name": "U456789"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Retrieve Single Allocation Group

Retrieves the configuration of a single account group. This describes the name of the allocation group, the specific accounts contained in the group, and the allocation method in use along with any relevant quantities.

- **Method:** `POST`
- **URL:** `/iserver/account/allocation/group/single`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Name of an existing allocation group |

#### Example Request

```json
{
  "name": "Group_1_NetLiq"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| name | string | Name used to refer to your allocation group. This will be used while placing orders |
| accounts | array\<GroupAccount\> | Contains a series of objects depicting which accounts are involved and, for user-defined allocation methods, the distribution value for each sub-account. See nested table below |
| default_method | string | Allocation method code for the allocation group. See Allocation Method Codes for more details |

**`GroupAccount` object:**

| Field | Type | Description |
|-------|------|-------------|
| name | string | The account ID of a given sub-account. Value Format: "U1234567" |
| amount | number | The total distribution value for each sub-account for user-defined allocation methods |

#### Example Response

```json
{
  "name": "Group_1_NetLiq",
  "accounts": [
    {
      "amount": 1,
      "name": "DU1234567"
    },
    {
      "amount": 5,
      "name": "DU9876543"
    }
  ],
  "default_method": "R"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### List All Allocation Groups

Retrieves a list of all of the advisor's allocation groups. This describes the name of the allocation group, number of subaccounts within the group, and the method in use for the group.

- **Method:** `GET`
- **URL:** `/iserver/account/allocation/group`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| data | array\<AllocationGroupSummary\> | Contains object pairs for each allocation group. See nested table below |

**`AllocationGroupSummary` object:**

| Field | Type | Description |
|-------|------|-------------|
| allocation_method | string | Uses the Allocation Method Code to represent which method is implemented |
| size | int | Represents the total number of sub-accounts within the group |
| name | string | The name set for the given allocation group |

#### Example Response

```json
{
  "data": [
    {
      "allocation_method": "N",
      "size": 10,
      "name": "Group_1_NetLiq"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Add Allocation Group

Add a new allocation group. This group can be used to trade.

- **Method:** `POST`
- **URL:** `/iserver/account/allocation/group`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Name used to refer to your allocation group. This will be used while placing orders |
| accounts | array\<GroupAccount\> | yes | Contains a series of objects depicting which accounts are involved and, for user-defined allocation methods, the distribution value for each sub-account. See nested table below |
| default_method | string | no | Allocation method code for the allocation group. See Allocation Method Codes for more details (assumed optional) |

**`GroupAccount` object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | The account ID of a given sub-account. Value Format: "U1234567" |
| amount | number | no | The total distribution value for each sub-account for user-defined allocation methods (assumed optional) |

#### Example Request

```json
{
  "name": "Group_1_NetLiq",
  "accounts": [
    {
      "name": "U1234567",
      "amount": 10
    },
    {
      "name": "U2345678",
      "amount": 5
    }
  ],
  "default_method": "N"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Confirms that the allocation group was properly set |

#### Example Response

```json
{
  "success": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Modify Allocation Group

Modify an existing allocation group.

- **Method:** `PUT`
- **URL:** `/iserver/account/allocation/group`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Name used to refer to your allocation group. If prev_name is specified, this will become the new name of the group |
| prev_name | string | no | Name used to refer to your existing allocation group. Only use this when updating the group name |
| accounts | array\<GroupAccount\> | yes | Contains a series of objects depicting which accounts are involved and, for user-defined allocation methods, the distribution value for each sub-account. See nested table below |
| default_method | string | yes | Allocation method code for the allocation group. See Allocation Method Codes for more details |

**`GroupAccount` object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | The account ID of a given sub-account. Value Format: "U1234567" |
| amount | number | no | The total distribution value for each sub-account for user-defined allocation methods (assumed optional) |

#### Example Request

```json
{
  "name": "new_test_group",
  "prev_name": "Group_1_NetLiq",
  "accounts": [
    {
      "name": "U1234567",
      "amount": 10
    },
    {
      "name": "U2345678",
      "amount": 5
    }
  ],
  "default_method": "A"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Confirms that the allocation group was properly set |

#### Example Response

```json
{
  "success": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Delete Allocation Group

Remove an existing allocation group. This group will no longer be accessible.

- **Method:** `POST`
- **URL:** `/iserver/account/allocation/group/delete`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Name used to refer to your allocation group |

#### Example Request

```json
{
  "name": "Group_1_NetLiq"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Confirms that the allocation group was properly removed |

#### Example Response

```json
{
  "success": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Retrieve Allocation Presets

Retrieve the preset behavior for allocation groups for specific events.

- **Method:** `GET`
- **URL:** `/iserver/account/allocation/presets`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| group_auto_close_positions | bool | Whether positions are automatically closed when removed from a group |
| default_method_for_all | string | Default allocation method code used for all allocation groups without a set value |
| profiles_auto_close_positions | bool | Whether positions are automatically closed for profile-based allocations |
| strict_credit_check | bool | Whether strict credit checking is enabled |
| group_proportional_allocation | bool | Whether proportional allocation is enabled for groups |

#### Example Response

```json
{
  "group_auto_close_positions": false,
  "default_method_for_all": "N",
  "profiles_auto_close_positions": false,
  "strict_credit_check": false,
  "group_proportional_allocation": false
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Set Allocation Presets

Set the preset behavior for allocation groups for specific events.

- **Method:** `POST`
- **URL:** `/iserver/account/allocation/presets`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| default_method_for_all | string | yes | Set the default allocation method to be used for all allocation groups without a set value |
| group_auto_close_positions | bool | yes | Whether positions are automatically closed when removed from a group |
| profiles_auto_close_positions | bool | yes | Whether positions are automatically closed for profile-based allocations |
| strict_credit_check | bool | yes | Whether strict credit checking is enabled |
| group_proportional_allocation | bool | yes | Whether proportional allocation is enabled for groups |

#### Example Request

```json
{
  "default_method_for_all": "E",
  "group_auto_close_positions": true,
  "profiles_auto_close_positions": true,
  "strict_credit_check": false,
  "group_proportional_allocation": false
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Confirms that the preset was properly set |

#### Example Response

```json
{
  "success": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Allocation Method Codes

Interactive Brokers supports two forms of allocation methods: methods with calculations completed by Interactive Brokers, and methods calculated by the user and then specified.

<details>
<summary>Click to expand Allocation Method Codes (2 tables, 7 methods)</summary>

**IB-computed allocation methods:**

| Method | Code |
|--------|------|
| Available Equity | A |
| Equal | E |
| Net Liquidation Value | N |

**User-specified allocation methods** (formerly known as Allocation Profiles):

| Method | Code |
|--------|------|
| Cash Quantity | C |
| Percentages | P |
| Ratios | R |
| Shares | S |

</details>

---

[↑ Back to Table of Contents](#table-of-contents)


### Allocation Preset Combinations

In order to attain specific allocation behaviors, a combination of various settings must be specified. The preset settings are based on the Advisor Presets setting built in TWS. Every time a user logs in to TWS, the presets established in the CPAPI will update to reflect the settings in TWS. Presets adjusted in the Client Portal API will not adjust the settings in TWS.

<details>
<summary>Click to expand Allocation Preset Combinations (2 tables, 5 presets)</summary>

**IB-computed allocation methods:**

| Intended Behavior | Proportional Allocation | Closing Behavior |
|-------------------|------------------------|------------------|
| Make positions be proportional based on method | group_proportional_allocation=false | group_auto_close_positions=true |
| Distribute shares based on method selected | group_proportional_allocation=true | group_auto_close_positions=true |
| Distribute shares based on method selected, do not prioritize accounts that are closing position | group_proportional_allocation=true | group_auto_close_positions=false |

**User-specified allocation methods** (formerly known as Allocation Profiles):

| Intended Behavior | Closing Behavior |
|-------------------|------------------|
| Distribute shares based on method selected | profile_auto_close_positions=true |
| Distribute shares based on method selected, do not prioritize accounts that are closing position | profile_auto_close_positions=false |

</details>

## FYIs and Notifications

Endpoints for managing FYI (For Your Information) notifications, subscriptions, disclaimers, and delivery options including email and device configuration.

---

[↑ Back to Table of Contents](#table-of-contents)


### Unread Bulletins

Returns the total number of unread FYIs.

- **Method:** `GET`
- **URL:** `/fyi/unreadnumber`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| BN | int | Returns the number of unread bulletins |

#### Example Response

```json
{
  "BN": 4
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get a List of Subscriptions

Return the current choices of subscriptions for notifications.

- **Method:** `GET`
- **URL:** `/fyi/settings`

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| A | int | Returns if the subscription can be modified. Only returned if the subscription can be modified. See /fyi/settings/{typecode} for how to modify |
| FC | string | FYI code for enabling or disabling the notification |
| H | int | Disclaimer if the notification was read. 0: Unread, 1: Read |
| FD | string | Returns a detailed description of the topic |
| FN | string | Returns a human readable title for the notification |

#### Example Response

```json
[
  {
    "FC": "M8",
    "H": 0,
    "A": 1,
    "FD": "Notify me when I establish position subject to US dividend tax withholding 871(m) rules.",
    "FN": "871(m) Trades"
  },
  {
    "FC": "AA",
    "H": 0,
    "A": 1,
    "FD": "Notifications related to account activity such as funding, application, trading and market data permission status",
    "FN": "Account Activity"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Enable/Disable Specified Subscription

Configure which typecode you would like to enable/disable.

- **Method:** `POST`
- **URL:** `/fyi/settings/{typecode}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| typecode | path | string | yes | Code used to signify a specific type of FYI template. See FYI Typecodes section for more details |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| enabled | bool | yes | Enable or disable the subscription. true: Enable, false: Disable |

#### Example Request

```json
{
  "enabled": true
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| V | int | Returns 1 to state message was acknowledged |
| T | int | Returns the time in ms to complete the edit |

#### Example Response

```json
{
  "V": 1,
  "T": 10
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### FYI Typecodes

Many FYI endpoints reference a "typecode" value. The table below lists the available codes and what they correspond to.

<details>
<summary>Click to expand FYI Typecodes (23 codes)</summary>

| Typecode | Description |
|----------|-------------|
| BA | Borrow Availability |
| CA | Comparable Algo |
| DA | Dividends Advisory |
| EA | Upcoming Earnings |
| MF | Mutual Fund Advisory |
| OE | Option Expiration |
| PR | Portfolio Builder Rebalance |
| SE | Suspend Order on Economic Event |
| SG | Short Term Gain turning Long Term |
| SM | System Messages |
| T2 | Assignment Realizing Long-Term Gains |
| TO | Takeover |
| UA | User Alert |
| M8 | M871 Trades |
| PS | Platform Use Suggestions |
| DL | Unexercised Option Loss Prevention Reminder |
| PT | Position Transfer |
| CB | Missing Cost Basis |
| MS | Milestones |
| TD | MiFID 10% Depreciation Notice |
| ST | Save Taxes |
| TI | Trade Idea |
| CT | Cash Transfer |

</details>

---

[↑ Back to Table of Contents](#table-of-contents)


### Get disclaimer for a certain kind of FYI

Receive additional disclaimers based on the specified typecode.

- **Method:** `GET`
- **URL:** `/fyi/disclaimer/{typecode}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| typecode | path | string | yes | Code used to signify a specific type of FYI template. See FYI Typecodes section for more details |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| FC | string | Returns the Typecode for the given disclaimer |
| DT | string | Returns the Disclaimer message |

#### Example Response

```json
{
  "FC": "SM",
  "DT": "This communication is provided for information purposes only and is not intended as a recommendation or a solicitation to buy, sell or hold any investment product. Customers are solely responsible for their own trading decisions."
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Mark Disclaimer Read

Mark disclaimer message read.

- **Method:** `PUT`
- **URL:** `/fyi/disclaimer/{typecode}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| typecode | path | string | yes | Code used to signify a specific type of FYI template. See FYI Typecodes section for more details |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| V | int | Returns 1 to state message was acknowledged |
| T | int | Returns the time in ms to complete the edit |

#### Example Response

```json
{
  "V": 1,
  "T": 10
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get Delivery Options

Options for sending FYIs to email and other devices.

- **Method:** `GET`
- **URL:** `/fyi/deliveryoptions`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| M | int | Email option is enabled or not. 0: Email Disabled, 1: Email Enabled |
| E | array\<Device\> | Returns an array of device information. See nested table below |

**`Device` object:**

| Field | Type | Description |
|-------|------|-------------|
| NM | string | Returns the human readable device name |
| I | string | Returns the device identifier |
| UI | string | Returns the unique device ID |
| A | int | Device is enabled or not. 0: Disabled, 1: Enabled |

#### Example Response

```json
{
  "E": [
    {
      "NM": "iPhone",
      "I": "apn://mtws@1234E5E67D8A9012EC3E45D6E7D89A01F2345CDBBB678B9BE0FB12345AF6D789",
      "UI": "apn://mtws@1234E5E67D8A9012EC3E45D6E7D89A01F2345CDBBB678B9BE0FB12345AF6D789",
      "A": 1
    }
  ],
  "M": 1
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Enable/Disable Device Option

Choose whether a particular device is enabled or disabled.

- **Method:** `POST`
- **URL:** `/fyi/deliveryoptions/device`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| deviceName | string | yes | Human readable name of the device |
| deviceId | string | yes | ID Code for the specific device |
| uiName | string | yes | Title used for the interface system |
| enabled | bool | yes | Specify if the device should be enabled or disabled |

#### Example Request

```json
{
  "deviceName": "iPhone",
  "deviceId": "apn://mtws@1234E5E67D8A9012EC3E45D6E7D89A01F2345CDBBB678B9BE0FB12345AF6D789",
  "uiName": "apn://mtws@1234E5E67D8A9012EC3E45D6E7D89A01F2345CDBBB678B9BE0FB12345AF6D789",
  "enabled": true
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| V | int | Returns 1 to state message was acknowledged |
| T | int | Returns the time in ms to complete the edit |

#### Example Response

```json
{
  "V": 1,
  "T": 10
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Delete a Device

Delete a specific device from our saved list of notification devices.

- **Method:** `DELETE`
- **URL:** `/fyi/deliveryoptions/{deviceId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| deviceId | path | string | yes | Display the device identifier to delete from IB's saved list. Can be retrieved from /fyi/deliveryoptions |

#### Response Body

No response message is returned. Instead, you will only receive an empty string with a 200 OK status code indicating a successfully deleted device.

---

[↑ Back to Table of Contents](#table-of-contents)


### Enable/Disable Email Option

Enable or disable your account's primary email to receive notifications.

- **Method:** `PUT`
- **URL:** `/fyi/deliveryoptions/email`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| enabled | query | string | yes | Enable or disable your email. true: Enable, false: Disable |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| V | int | Returns 1 to state message was acknowledged |
| T | int | Returns the time in ms to complete the edit |

#### Example Response

```json
{
  "V": 1,
  "T": 10
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get a list of notifications

Get a list of available notifications.

- **Method:** `GET`
- **URL:** `/fyi/notifications`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| max | query | string | no | Specify the maximum number of notifications to receive. Can request a maximum of 10 notifications (assumed optional) |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| D | string | Notification date |
| ID | string | Unique way to reference the notification |
| FC | string | FYI code, can be used to find whether the disclaimer is accepted or not in settings |
| MD | string | Content of notification |
| MS | string | Title of notification |
| R | string | Return if the notification was read or not. 0: Unread, 1: Read |
| HT | int | Notification type indicator |

#### Example Response

```json
[
  {
    "R": 0,
    "D": "1702469440.0",
    "MS": "IBKR FYI: Option Expiration Notification",
    "MD": "One or more option contracts in your portfolio are set to expire shortly...",
    "ID": "2023121370119463",
    "HT": 0,
    "FC": "OE"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Mark Notification Read

Mark a particular notification message as read or unread.

- **Method:** `PUT`
- **URL:** `/fyi/notifications/{notificationId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| notificationId | path | string | yes | Code used to signify a specific notification to mark |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| V | int | Returns 1 to state message was acknowledged |
| T | int | Returns the time in ms to complete the edit |
| P | object | Returns details about the notification read status. See nested table below |

**`P` object:**

| Field | Type | Description |
|-------|------|-------------|
| R | int | Returns if the message was read (1) or unread (0) |
| ID | string | Returns the ID for the notification |

#### Example Response

```json
{
  "V": 1,
  "T": 5,
  "P": {
    "R": 1,
    "ID": "12345678901234567"
  }
}
```

## Market Data

Endpoints for retrieving live and historical market data snapshots, regulatory snapshots, and managing market data subscriptions.

---

[↑ Back to Table of Contents](#table-of-contents)


### Live Market Data Snapshot

Get Market Data for the given conid(s). A pre-flight request must be made prior to ever receiving data. For some fields, it may take more than a few moments to receive information. See response fields for a list of available fields that can be requested via the fields argument.

> **Prerequisites:** The endpoint /iserver/accounts must be called prior to /iserver/marketdata/snapshot. For derivative contracts the endpoint /iserver/secdef/search must be called first.

- **Method:** `GET`
- **URL:** `/iserver/marketdata/snapshot`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conids | query | string | yes | Contract identifier(s) for the contract(s) of interest. A maximum of 100 conids may be specified. May provide a comma-separated series |
| fields | query | string | yes | Specify a series of tick values to be returned. A maximum of 50 fields may be specified. May provide a comma-separated series of field IDs. See Market Data Fields for more information |

#### Response Body

Returns an array of objects. Each object contains the standard fields below plus any requested market data field IDs as additional keys.

| Field | Type | Description |
|-------|------|-------------|
| server_id | string | Returns the request's identifier |
| conidEx | string | Returns the passed conid field. May include exchange if specified in request |
| conid | int | Returns the contract ID of the request |
| _updated | int | Returns the epoch time of the update in a 13 character integer |
| 6119 | string | Field value of the server_id. Returns the request's identifier |
| 6509 | string | Returns a multi-character value representing the Market Data Availability |
| {field_id} | string | Returns a response for each requested field. Some fields may not be as readily available as others. See the Market Data Fields section for more details |

#### Example Response

```json
[
  {
    "_updated": 1702334859712,
    "conidEx": "265598",
    "conid": 265598,
    "server_id": "q1",
    "6119": "serverId",
    "31": "193.18",
    "84": "193.06",
    "86": "193.14",
    "6509": "RpB"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Market Data Update Frequency

Watchlist market data at Interactive Brokers is derived from time-based snapshot intervals which vary by product and region. This means that a given tick will only update as frequently as its interval allows.

The Web API retains a standard pacing limit of 10 requests per second. For more frequent returns, implement the smd websocket topic in place of the HTTP endpoint.

| Product | Frequency |
|---------|-----------|
| All Products | 500ms |

---

[↑ Back to Table of Contents](#table-of-contents)


### Regulatory Snapshot

> **WARNING:** Each regulatory snapshot made **will incur a fee of $0.01 USD** to the account. **This applies to both live and paper accounts.** If you are already paying for, or are subscribed to, a specific US Network subscription, your account will not be charged.

Send a request for a regulatory snapshot. This will cost $0.01 USD per request unless you are subscribed to the direct exchange market data already.

- **Method:** `GET`
- **URL:** `/md/regsnapshot`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | string | yes | Provide the contract identifier to retrieve market data for |

#### Response Body

The integer fields returned below also correspond to the Market Data Field values used for the standard /iserver/marketdata/snapshot endpoint.

| Field | Type | Description |
|-------|------|-------------|
| conid | int | Returns the contract ID of the request |
| conidEx | string | Returns the contract ID of the request type |
| BboExchange | string | Color for Best Bid/Offer Exchange in hex code |
| HasDelayed | bool | Returns if the data is live (false) or delayed (true) |
| 84 | float | Returns the Bid value |
| 86 | float | Returns the Ask value |
| 88 | int | Returns the Bid size |
| 85 | int | Returns the Ask size |
| BestBidExch | int | Returns the exchange identifier of the current best bid value. Internal use only |
| BestAskExch | int | Returns the exchange identifier of the current best Ask value. Internal use only |
| 31 | float | Returns the most recent Last value |
| 7059 | int | Returns the last traded size |
| LastExch | int | Returns the exchange of the last trade as a binary integer. Internal use only |
| 7057 | string | Returns the series of character codes for the Ask exchange |
| 7068 | string | Returns the series of character codes for the Bid exchange |
| 7058 | string | Returns the series of character codes for the Last exchange |

#### Example Response

```json
{
  "conid": 265598,
  "conidEx": "265598",
  "BboExchange": "#0000FF",
  "HasDelayed": false,
  "84": 193.06,
  "86": 193.14,
  "88": 100,
  "85": 200,
  "BestBidExch": 12,
  "BestAskExch": 14,
  "31": 193.10,
  "7059": 50,
  "LastExch": 8,
  "7057": "QZ",
  "7068": "QZ",
  "7058": "Q"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Market Data Availability

The field 6509 may contain three characters:

- **First character** defines market data timeline: `R` = RealTime, `D` = Delayed, `Z` = Frozen, `Y` = Frozen Delayed, `N` = Not Subscribed
- **Second character** defines the data structure: `P` = Snapshot, `p` = Consolidated
- **Third character** defines the type of data: `B` = Book

| Code | Name | Description |
|------|------|-------------|
| R | RealTime | Data is relayed back in real time without delay, market data subscription(s) are required |
| D | Delayed | Data is relayed back 15-20 min delayed |
| Z | Frozen | Last recorded data at market close, relayed back in real time |
| Y | Frozen Delayed | Last recorded data at market close, relayed back delayed |
| N | Not Subscribed | User does not have the required market data subscription(s) to relay back either real time or delayed data |
| O | Incomplete Market Data API Acknowledgement | The annual Market Data API Acknowledgement has not been completed. Log in to Client Portal, select Settings, find Market Data Subscriptions, and complete the Market Data API Agreement |
| P | Snapshot | Snapshot request is available for contract |
| p | Consolidated | Market data is aggregated across multiple exchanges or venues |
| B | Book | Top of the book data is available for contract |
| d | Performance Details Enabled | Additional performance details are available for this contract. Internal use intended |

---

[↑ Back to Table of Contents](#table-of-contents)


### Market Data Fields

<details>
<summary>Click to expand Market Data Fields (110+ fields)</summary>

| Field | Return Type | Value | Description |
|-------|-------------|-------|-------------|
| 31 | string | Last Price | The last price at which the contract traded. May contain one of the following prefixes: C – Previous day's closing price. H – Trading has halted |
| 55 | string | Symbol | |
| 58 | string | Text | |
| 70 | string | High | Current day high price |
| 71 | string | Low | Current day low price |
| 73 | string | Market Value | The current market value of your position in the security. Calculated with real time market data (even when not subscribed) |
| 74 | string | Avg Price | The average price of the position |
| 75 | string | Unrealized PnL | Unrealized profit or loss. Calculated with real time market data (even when not subscribed) |
| 76 | string | Formatted position | |
| 77 | string | Formatted Unrealized PnL | |
| 78 | string | Daily PnL | Your profit or loss of the day since prior close. Calculated with real time market data (even when not subscribed) |
| 79 | string | Realized PnL | Realized profit or loss. Calculated with real time market data (even when not subscribed) |
| 80 | string | Unrealized PnL % | Unrealized profit or loss expressed in percentage |
| 82 | string | Change | The difference between the last price and the close on the previous trading day |
| 83 | string | Change % | The difference between the last price and the close on the previous trading day in percentage |
| 84 | string | Bid Price | The highest-priced bid on the contract |
| 85 | string | Ask Size | The number of contracts or shares offered at the ask price |
| 86 | string | Ask Price | The lowest-priced offer on the contract |
| 87 | string | Volume | Volume for the day, formatted with 'K' for thousands or 'M' for millions. For higher precision volume refer to field 7762 |
| 88 | string | Bid Size | The number of contracts or shares bid for at the bid price |
| 201 | string | Right | Returns the right of the instrument, such as P for Put or C for Call |
| 6004 | string | Exchange | |
| 6008 | integer | Conid | Contract identifier from IBKR's database |
| 6070 | string | SecType | The asset class of the instrument |
| 6072 | string | Months | |
| 6073 | string | Regular Expiry | |
| 6119 | string | Marker for market data delivery method (similar to request id) | |
| 6457 | integer | Underlying Conid | Use /trsrv/secdef to get more information about the security |
| 6508 | string | Service Params | |
| 6509 | string | Market Data Availability | See Market Data Availability section for details |
| 7051 | string | Company name | |
| 7057 | string | Ask Exch | Displays the exchange(s) offering the SMART price. A=AMEX, C=CBOE, I=ISE, X=PHLX, N=PSE, B=BOX, Q=NASDAQOM, Z=BATS, W=CBOE2, T=NASDAQBX, M=MIAX, H=GEMINI, E=EDGX, J=MERCURY |
| 7058 | string | Last Exch | Displays the exchange(s) offering the SMART price. Same codes as Ask Exch |
| 7059 | string | Last Size | The number of units traded at the last price |
| 7068 | string | Bid Exch | Displays the exchange(s) offering the SMART price. Same codes as Ask Exch |
| 7084 | string | Implied Vol./Hist. Vol % | The ratio of the implied volatility over the historical volatility, expressed as a percentage |
| 7085 | string | Put/Call Interest | Put option open interest/call option open interest for the trading day |
| 7086 | string | Put/Call Volume | Put option volume/call option volume for the trading day |
| 7087 | string | Hist. Vol. % | 30-day real-time historical volatility |
| 7088 | string | Hist. Vol. Close % | Shows the historical volatility based on previous close price |
| 7089 | string | Opt. Volume | Option Volume |
| 7094 | string | Conid + Exchange | |
| 7184 | string | canBeTraded | If contract is a trade-able instrument. Returns 1(true) or 0(false) |
| 7219 | string | Contract Description | |
| 7220 | string | Contract Description | |
| 7221 | string | Listing Exchange | |
| 7280 | string | Industry | Displays the type of industry under which the underlying company can be categorized |
| 7281 | string | Category | Displays a more detailed level of description within the industry |
| 7282 | string | Average Volume | The average daily trading volume over 90 days |
| 7283 | string | Option Implied Vol. % | A prediction of how volatile an underlying will be in the future. At the market volatility estimated for a maturity thirty calendar days forward of the current trading day, based on option prices from two consecutive expiration months. To query the Implied Vol. % of a specific strike refer to field 7633 |
| 7284 | string | Historical volatility % | Deprecated, see field 7087 |
| 7285 | string | Put/Call Ratio | |
| 7292 | string | Cost Basis | Your current position in this security multiplied by the average price and multiplier |
| 7293 | string | 52 Week High | The highest price for the past 52 weeks |
| 7294 | string | 52 Week Low | The lowest price for the past 52 weeks |
| 7295 | string | Open | Today's opening price |
| 7296 | string | Close | Today's closing price |
| 7308 | string | Delta | The ratio of the change in the price of the option to the corresponding change in the price of the underlying |
| 7309 | string | Gamma | The rate of change for the delta with respect to the underlying asset's price |
| 7310 | string | Theta | A measure of the rate of decline the value of an option due to the passage of time |
| 7311 | string | Vega | The amount that the price of an option changes compared to a 1% change in the volatility |
| 7607 | string | Opt. Volume Change % | Today's option volume as a percentage of the average option volume |
| 7633 | string | Implied Vol. % | The implied volatility for the specific strike of the option in percentage. To query the Option Implied Vol. % from the underlying refer to field 7283 |
| 7635 | string | Mark | The mark price is the ask price if ask is less than last price, the bid price if bid is more than the last price, otherwise it's equal to last price |
| 7636 | string | Shortable Shares | Number of shares available for shorting |
| 7637 | string | Fee Rate | Interest rate charged on borrowed shares |
| 7638 | string | Option Open Interest | |
| 7639 | string | % of Mark Value | Displays the market value of the contract as a percentage of the total market value of the account. Calculated with real time market data (even when not subscribed) |
| 7644 | string | Shortable | Describes the level of difficulty with which the security can be sold short |
| 7671 | string | Dividends | This value is the total of the expected dividend payments over the next twelve months per share |
| 7672 | string | Dividends TTM | This value is the total of the expected dividend payments over the last twelve months per share |
| 7674 | string | EMA(200) | Exponential moving average (N=200) |
| 7675 | string | EMA(100) | Exponential moving average (N=100) |
| 7676 | string | EMA(50) | Exponential moving average (N=50) |
| 7677 | string | EMA(20) | Exponential moving average (N=20) |
| 7678 | string | Price/EMA(200) | Price to Exponential moving average (N=200) ratio -1, displayed in percents |
| 7679 | string | Price/EMA(100) | Price to Exponential moving average (N=100) ratio -1, displayed in percents |
| 7724 | string | Price/EMA(50) | Price to Exponential moving average (N=50) ratio -1, displayed in percents |
| 7681 | string | Price/EMA(20) | Price to Exponential moving average (N=20) ratio -1, displayed in percents |
| 7682 | string | Change Since Open | The difference between the last price and the open price |
| 7683 | string | Upcoming Event | Shows the next major company event. Requires Wall Street Horizon subscription |
| 7684 | string | Upcoming Event Date | The date of the next major company event. Requires Wall Street Horizon subscription |
| 7685 | string | Upcoming Analyst Meeting | The date and time of the next scheduled analyst meeting. Requires Wall Street Horizon subscription |
| 7686 | string | Upcoming Earnings | The date and time of the next scheduled earnings/earnings call event. Requires Wall Street Horizon subscription |
| 7687 | string | Upcoming Misc Event | The date and time of the next shareholder meeting, presentation or other event. Requires Wall Street Horizon subscription |
| 7688 | string | Recent Analyst Meeting | The date and time of the most recent analyst meeting. Requires Wall Street Horizon subscription |
| 7689 | string | Recent Earnings | The date and time of the most recent earnings/earning call event. Requires Wall Street Horizon subscription |
| 7690 | string | Recent Misc Event | The date and time of the most recent shareholder meeting, presentation or other event. Requires Wall Street Horizon subscription |
| 7694 | string | Probability of Max Return | Customer implied probability of maximum potential gain |
| 7695 | string | Break Even | Break even points |
| 7696 | string | SPX Delta | Beta Weighted Delta is calculated using the formula: Delta x dollar adjusted beta, where adjusted beta is adjusted by the ratio of the close price |
| 7697 | string | Futures Open Interest | Total number of outstanding futures contracts |
| 7698 | string | Last Yield | Implied yield of the bond if it is purchased at the current last price. Calculated using the Last price on all possible call dates. Assumes prepayment occurs if the bond has call or put provisions and the issuer can offer a lower coupon rate. The yield to worst will be the lowest of the yield to maturity or yield to call |
| 7699 | string | Bid Yield | Implied yield of the bond if it is purchased at the current bid price. Calculated using the Ask on all possible call dates |
| 7700 | string | Probability of Max Return | Customer implied probability of maximum potential gain |
| 7702 | string | Probability of Max Loss | Customer implied probability of maximum potential loss |
| 7703 | string | Profit Probability | Customer implied probability of any gain |
| 7704 | string | Organization Type | |
| 7705 | string | Debt Class | |
| 7706 | string | Ratings | Ratings issued for bond contract |
| 7707 | string | Bond State Code | |
| 7708 | string | Bond Type | |
| 7714 | string | Last Trading Date | |
| 7715 | string | Issue Date | |
| 7720 | string | Ask Yield | Implied yield of the bond if it is purchased at the current offer. Calculated using the Bid on all possible call dates |
| 7741 | string | Prior Close | Yesterday's closing price |
| 7762 | string | Volume Long | High precision volume for the day. For formatted volume refer to field 87 |
| 7768 | string | hasTradingPermissions | If user has trading permissions for specified contract. Returns 1(true) or 0(false) |
| 7920 | string | Daily PnL Raw | Your profit or loss of the day since prior close. Calculated with real-time market data (even when not subscribed) |
| 7921 | string | Cost Basis Raw | Your current position in this security multiplied by the average price and multiplier |

</details>

---

[↑ Back to Table of Contents](#table-of-contents)


### Unavailable Historical Data

The following types of historical data are not available:

- Historical data is filtered by default. See IB's FAQ for more insight.
- Bars whose size is 30 seconds or less older than six months.
- Expired futures data older than two years counting from the future's expiration date.
- Expired options, FOPs, warrants and structured products.
- End of Day (EOD) data for options, FOPs, warrants and structured products.
- Data for expired future spreads.
- Data for securities which are no longer trading.
- Native historical data for combos. Historical data is not stored in the IB database separately for combos; combo historical data in TWS or the API is the sum of data from the legs.
- Historical data for securities which move to a new exchange will often not be available prior to the time of the move.
- Studies and indicators such as Weighted Moving Averages or Bollinger Bands are not available from the API.

---

[↑ Back to Table of Contents](#table-of-contents)


### Historical Market Data

Get historical market data for given conid, length of data is controlled by period and bar.

> **Note:**
> - There's a limit of 5 concurrent requests. Excessive requests will return a "Too many requests" status 429 response.
> - This endpoint provides a maximum of 1000 data points.

- **Method:** `GET`
- **URL:** `/iserver/marketdata/history`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | query | string | yes | Contract identifier for the ticker symbol of interest |
| exchange | query | string | no | Returns the exchange you want to receive data from |
| period | query | string | no | Overall duration for which data should be returned. Default to 1w. Available: {1-30}min, {1-8}h, {1-1000}d, {1-792}w, {1-182}m, {1-15}y (assumed optional) |
| bar | query | string | yes | Individual bars of data to be returned. Possible values: 1min, 2min, 3min, 5min, 10min, 15min, 30min, 1h, 2h, 3h, 4h, 8h, 1d, 1w, 1m. See Step Size to ensure your bar size is supported for your chosen period value |
| startTime | query | string | no | Starting date of the request duration. Format: YYYYMMDD-HH:mm:ss (assumed optional) |
| outsideRth | query | bool | no | Determine if you want data after regular trading hours (assumed optional) |
| source | query | string | no | Type of data to be returned. One of: `Trades`, `Midpoint`, `Bid_Ask`. Trades is passed by default (assumed optional) |

#### Step Size

A step size is the permitted minimum and maximum bar size for any given period.

| period | 1min | 1h | 1d | 1w | 1m | 3m | 6m | 1y | 2y | 3y | 15y |
|--------|------|-----|-----|-----|-----|-----|-----|-----|-----|-----|------|
| bar | 1min | 1min – 8h | 1min – 8h | 10min – 1w | 1h – 1m | 2h – 1m | 4h – 1m | 8h – 1m | 1d – 1m | 1d – 1m | 1w – 1m |
| default bar | 1min | 1min | 1min | 15min | 30min | 1d | 1d | 1d | 1d | 1w | 1w |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| serverId | string | Internal request identifier |
| symbol | string | Returns the ticker symbol of the contract |
| text | string | Returns the long name of the ticker symbol |
| priceFactor | int | Returns the price increment obtained from the display rules |
| startTime | string | Returns the initial time of the historical data request. Returned in UTC formatted as YYYYMMDD-HH:mm:ss |
| high | string | Returns the High values during this time series with format %h/%v/%t. %h is the high price (scaled by priceFactor), %v is volume (volume factor will always be 100, reported volume = actual volume/100), %t is minutes from start time of the chart |
| low | string | Returns the low value during this time series with format %l/%v/%t. %l is the low price (scaled by priceFactor), %v is volume, %t is minutes from start time |
| timePeriod | string | Returns the duration for the historical data request |
| barLength | int | Returns the number of seconds in a bar |
| mdAvailability | string | Returns the Market Data Availability. See the Market Data Availability section for more details |
| mktDataDelay | int | Returns the amount of delay, in milliseconds, to process the historical data request |
| outsideRth | bool | Defines if the market data returned was inside regular trading hours or not |
| tradingDayDuration | int | Duration of the trading day in minutes |
| volumeFactor | int | Returns the factor the volume is multiplied by |
| priceDisplayRule | int | Presents the price display rule used. For internal use only |
| priceDisplayValue | string | Presents the price display rule used. For internal use only |
| chartPanStartTime | string | Chart pan start time |
| direction | int | Direction of data |
| negativeCapable | bool | Returns whether or not the data can return negative values |
| messageVersion | int | Internal use only |
| data | array\<Bar\> | Returns all historical bars for the requested period. See nested table below |
| points | int | Returns the total number of data points in the bar |
| travelTime | int | Returns the amount of time to return the details |

**`Bar` object:**

| Field | Type | Description |
|-------|------|-------------|
| o | float | Returns the Open value of the bar |
| c | float | Returns the Close value of the bar |
| h | float | Returns the High value of the bar |
| l | float | Returns the Low value of the bar |
| v | float | Returns the Volume of the bar |
| t | int | Returns the Operator Timezone Epoch Unix Timestamp of the bar |

#### Example Response

```json
{
  "serverId": "20477",
  "symbol": "AAPL",
  "text": "APPLE INC",
  "priceFactor": 100,
  "startTime": "20230818-08:00:00",
  "high": "17510/472117.45/0",
  "low": "17170/472117.45/0",
  "timePeriod": "1d",
  "barLength": 86400,
  "mdAvailability": "S",
  "mktDataDelay": 0,
  "outsideRth": true,
  "tradingDayDuration": 1440,
  "volumeFactor": 1,
  "priceDisplayRule": 1,
  "priceDisplayValue": "2",
  "negativeCapable": false,
  "messageVersion": 2,
  "data": [
    {
      "o": 173.4,
      "c": 174.7,
      "h": 175.1,
      "l": 171.7,
      "v": 472117.45,
      "t": 16923456000
    }
  ],
  "points": 0,
  "travelTime": 48
}
```

#### Error Responses

**500 System Error:**

```json
{
  "error": "description"
}
```

**429 Too many requests:**

```json
{
  "error": "description"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### HMDS Period and Bar Size

**Valid Period Units** (case sensitive):

| Unit | Description |
|------|-------------|
| S | Seconds |
| d | Day |
| w | Week |
| m | Month |
| y | Year |

**Valid Bar Units:**

| Duration | Bar units allowed | Bar size Interval (Min/Max) |
|----------|-------------------|----------------------------|
| 60 S | secs, mins | 1 secs -> 1 mins |
| 3600 S (1 hour) | secs, mins, hrs | 5 secs -> 1 hours |
| 14400 S (4 hours) | secs, mins, hrs | 10 secs -> 4 hrs |
| 28800 S (8 hours) | secs, mins, hrs | 30 secs -> 8 hrs |
| 1 d | mins, hrs, d | 1 mins -> 1 day |
| 1 w | mins, hrs, d, w | 3 mins -> 1 week |
| 1 m | mins, d, w | 30 mins -> 1 month |
| 1 y | d, w, m | 1 d -> 1 m |

> **Note:** A step size is defined as the ratio between the historical data request's duration period and its granularity (i.e., bar size). Historical Data requests need to be assembled in such a way that only a few thousand bars are returned at a time.

---

[↑ Back to Table of Contents](#table-of-contents)


### Unsubscribe (Single)

Cancel market data for given conid. This can clear all standing market data feeds to invalidate your cache and start fresh.

- **Method:** `POST`
- **URL:** `/iserver/marketdata/unsubscribe`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| conid | int | yes | Enter the contract identifier to cancel the market data feed |

#### Example Request

```json
{
  "conid": 265598
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| success | bool | Returns a confirmation status of your unsubscribe request. A true response indicates that the market data feed has been successfully cancelled |

#### Example Response

```json
{
  "success": true
}
```

#### Error Response

A status 500 response will be sent when attempting to unsubscribe from a market data feed that is not currently open.

```json
{
  "error": "unknown"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Unsubscribe (All)

Cancel all market data request(s). To cancel market data for a specific conid, see /iserver/marketdata/unsubscribe.

- **Method:** `GET`
- **URL:** `/iserver/marketdata/unsubscribeall`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| unsubscribed | bool | Returns a confirmation status of your unsubscribe request |

#### Example Response

```json
{
  "unsubscribed": true
}
```

## Order Monitoring

Endpoints for retrieving live orders, individual order status, and trade execution history.

---

[↑ Back to Table of Contents](#table-of-contents)


### Live Orders

This endpoint requires a pre-flight request. Returns the list of live orders (cancelled, filled, submitted).

To retrieve order information for a specific account, clients must first query the /iserver/account endpoint to switch to the appropriate account.

> **Important:** Filtering orders using the /iserver/account/orders endpoint will prevent order details from coming through over the websocket "sor" topic. To resolve this issue, developers should set `force=true` in a follow-up /iserver/account/orders call to clear any cached behavior surrounding the endpoint prior to calling for the websocket request.

- **Method:** `GET`
- **URL:** `/iserver/account/orders`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| filters | query | string | no | Optionally filter your list of orders by a unique status value. More than one filter can be passed, separated by commas (assumed optional) |
| force | query | bool | no | Force the system to clear saved information and make a fresh request for orders. Submission will appear as a blank array (assumed optional) |

#### Response Body

> **Note:** The /iserver/account/orders endpoint can contain a maximum of 1000 orders.

| Field | Type | Description |
|-------|------|-------------|
| orders | array\<Order\> | Contains all orders placed on the account for the day. See nested table below |
| snapshot | bool | Returns if the data is a snapshot of the account's orders |

**`Order` object:**

| Field | Type | Description |
|-------|------|-------------|
| acct | string | Returns the account ID for the submitted order |
| conidex | string | Returns the contract identifier for the order |
| conid | int | Returns the contract identifier for the order |
| orderId | int | Returns the local order identifier of the order |
| cashCcy | string | Returns the currency used for the order |
| sizeAndFills | string | Returns the size of the order and how much of it has been filled |
| orderDesc | string | Returns the description of the order including the side, size, order type, price, and TIF |
| description1 | string | Returns the local symbol of the order |
| ticker | string | Returns the ticker symbol for the order |
| secType | string | Returns the security type for the order |
| listingExchange | string | Returns the primary listing exchange of the order |
| remainingQuantity | float | Returns the remaining size for the order to fill |
| filledQuantity | float | Returns the size of the order already filled |
| totalSize | float | Returns the total size of the order |
| companyName | string | Returns the company long name |
| status | string | Returns the current status of the order |
| order_ccp_status | string | Returns the current status of the order |
| origOrderType | string | Returns the original order type of the order, whether or not the type has been changed |
| supportsTaxOpt | string | Returns if the order is supported by the Tax Optimizer |
| lastExecutionTime | string | Returns the datetime of the order's most recent execution. Time returned is based on UTC timezone. Format: YYMMDDHHmmss |
| orderType | string | Returns the current order type, or the order at the time of execution |
| bgColor | string | Internal use only |
| fgColor | string | Internal use only |
| order_ref | string | User defined string used to identify the order. Value is set using "cOID" field while placing an order |
| timeInForce | string | Returns the time in force (TIF) of the order |
| lastExecutionTime_r | int | Returns the epoch time of the most recent execution on the order |
| side | string | Returns the side of the order |
| avgPrice | string | Returns the average price of execution for the order |

#### Example Response

```json
{
  "orders": [
    {
      "acct": "U1234567",
      "conidex": "265598",
      "conid": 265598,
      "account": "U1234567",
      "orderId": 1234568790,
      "cashCcy": "USD",
      "sizeAndFills": "5",
      "orderDesc": "Sold 5 Market, GTC",
      "description1": "AAPL",
      "ticker": "AAPL",
      "secType": "STK",
      "listingExchange": "NASDAQ.NMS",
      "remainingQuantity": 0.0,
      "filledQuantity": 5.0,
      "totalSize": 5.0,
      "companyName": "APPLE INC",
      "status": "Filled",
      "order_ccp_status": "Filled",
      "avgPrice": "192.26",
      "origOrderType": "MARKET",
      "supportsTaxOpt": "1",
      "lastExecutionTime": "231211180049",
      "orderType": "Market",
      "order_ref": "Order123",
      "timeInForce": "GTC",
      "lastExecutionTime_r": 1702317649000,
      "side": "SELL"
    }
  ],
  "snapshot": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Order Status

The Order Status endpoint may be used to monitor a single specific order while it remains active. Retrieve the given status of an individual order using the orderId returned by the order placement response or the orderId available in the live order response.

> **Important Notes:**
> - For multi-account structures such as Financial Advisors or linked-account structures, users must call /iserver/account to switch to the affiliated account before requesting order status. It is otherwise expected to result in a 503 error.
> - If an order has been cancelled or filled prior to the active session and there is no cached information saved, querying the order status endpoint would be expected to result in a 503 error.

- **Method:** `GET`
- **URL:** `/iserver/account/order/status/{orderId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| orderId | path | string | yes | Order identifier for the placed order. Returned by the order placement response or the orderId available in the live order response |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| sub_type | string | Internal use only |
| request_id | string | Returns the request ID of the order placed by the user |
| order_id | int | Returns the order ID of the requested order |
| conidex | string | Returns the contract identifier for the order |
| conid | int | Returns the contract identifier for the order |
| symbol | string | Returns the ticker symbol for the order |
| side | string | Returns the side of the order |
| contract_description_1 | string | Returns the local symbol of the order |
| listing_exchange | string | Returns the primary listing exchange of the order |
| option_acct | string | For Client Portal use (Internal use only) |
| company_name | string | Returns the company long name |
| size | string | Returns the quantity of the order |
| total_size | string | Returns the maximum quantity of the order |
| currency | string | Returns the base currency of the order |
| account | string | Returns the account the order was placed for |
| order_type | string | Returns the order type for the given order |
| cum_fill | string | Returns the cumulative fill of the order |
| order_status | string | Returns the current status of the order |
| order_ccp_status | string | Returns the current status of the order as a code |
| order_status_description | string | Returns the human readable response of the order status |
| tif | string | Returns the time in force of the order |
| fg_color | string | For Client Portal use (Internal use only) |
| bg_color | string | For Client Portal use (Internal use only) |
| order_not_editable | bool | Returns whether or not the order can be modified. Relevant for orders currently or already executed |
| editable_fields | string | For Client Portal use (Internal use only) |
| cannot_cancel_order | bool | Returns whether or not the order can be cancelled. Relevant for orders currently or already executed |
| deactivate_order | bool | Return whether or not the order has been marked inactive |
| sec_type | string | Returns the security type of the order's contract |
| available_chart_periods | string | For Client Portal use (Internal use only) |
| order_description | string | Returns the description of the order including the side, size, order type, price, and TIF |
| order_description_with_contract | string | Returns the description of the order including the side, size, symbol, order type, price, and TIF |
| alert_active | int | Returns whether or not there is an active alert available on the order |
| child_order_type | string | Type of the child order. One of: `A` (attached), `B` (beta-hedge), `0` (No Child) |
| order_clearing_account | string | Returns the account ID for the submitted order |
| size_and_fills | string | Returns the size of the order and how much of it has been filled |
| exit_strategy_display_price | string | Displays the price of the order as it resolved its execution |
| exit_strategy_chart_description | string | Returns the description of the order including the side, size, order type, price, and TIF |
| average_price | string | Returns the average price of execution for the order |
| exit_strategy_tool_availability | string | Internal use only |
| allowed_duplicate_opposite | bool | Returns whether or not the opposing order can be placed on the market |
| order_time | string | Returns the datetime of the order placement. Time returned is based on UTC timezone. Format: YYMMDDHHmmss |

#### Example Response

```json
{
  "sub_type": null,
  "request_id": "209",
  "order_id": 1799796559,
  "conidex": "265598",
  "conid": 265598,
  "symbol": "AAPL",
  "side": "S",
  "contract_description_1": "AAPL",
  "listing_exchange": "NASDAQ.NMS",
  "company_name": "APPLE INC",
  "size": "0.0",
  "total_size": "5.0",
  "currency": "USD",
  "account": "U1234567",
  "order_type": "MARKET",
  "cum_fill": "5.0",
  "order_status": "Filled",
  "order_ccp_status": "2",
  "order_status_description": "Order Filled",
  "tif": "DAY",
  "order_not_editable": true,
  "cannot_cancel_order": true,
  "deactivate_order": false,
  "sec_type": "STK",
  "order_description": "Sold 5 Market, Day",
  "order_description_with_contract": "Sold 5 AAPL Market, Day",
  "child_order_type": "0",
  "size_and_fills": "5",
  "average_price": "192.26",
  "allowed_duplicate_opposite": true,
  "order_time": "231211180049"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Order Status Values

For many orders, customers will see orders return an order status with an array of potential values. The values returned from the `order_status` field of the Live Orders object will vary slightly from the format used while using the `filters` parameter from GET /iserver/account/orders.

| Status | Filter Value | Description |
|--------|-------------|-------------|
| Inactive | inactive | Indicates that you are in the process of creating an order and you have not yet activated or transmitted it |
| PendingSubmit | pending_submit | Indicates that you have transmitted your order, but have not yet received confirmation that it has been accepted by the order destination |
| PreSubmitted | pre_submitted | Indicates that an order has been accepted by the system (simulated orders) or an exchange (native orders) and that this order has yet to be elected |
| Submitted | submitted | Indicates that your order has been accepted and is working at the destination |
| Filled | filled | Order has been completely filled |
| PendingCancel | pending_cancel | Indicates that you have sent a request to cancel the order but have not yet received cancel confirmation from the order destination. You may still receive an execution while your cancellation request is pending |
| PreCancelled | pre_cancelled | Indicates that a cancellation request has been accepted by the system but that currently the request is not being recognized, due to system, exchange or other issues. You may still receive an execution while your cancellation request is pending |
| Cancelled | cancelled | Indicates that the balance of your order has been confirmed canceled by the system. This could occur unexpectedly when the destination has rejected your order |
| WarnState | warn_state | Order has a specific warning message such as for basket orders |
| N/A | sort_by_time | There is an initial sort by order state performed so active orders are always above inactive and filled then orders are sorted chronologically |

---

[↑ Back to Table of Contents](#table-of-contents)


### Trades

Returns a list of trades for the currently selected account for current day and six previous days. It is advised to call this endpoint once per session.

- **Method:** `GET`
- **URL:** `/iserver/account/trades`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| days | query | string | no | Specify the number of days to receive executions for, up to a maximum of 7 days. If unspecified, only the current day is returned (assumed optional) |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| execution_id | string | Returns the execution ID for the trade |
| symbol | string | Returns the underlying symbol |
| supports_tax_opt | string | Returns whether or not tax optimizer is supported for the order |
| side | string | Returns the side of the order, Buy or Sell |
| order_description | string | Returns the description of the order including the side, size, symbol, order type, price, and TIF |
| order_ref | string | User defined string used to identify the order. Value is set using "cOID" field while placing an order |
| trade_time | string | Returns the UTC format of the trade time |
| trade_time_r | int | Returns the epoch time of the trade |
| size | float | Returns the quantity of the order |
| price | string | Returns the price of trade execution |
| submitter | string | Returns the username that submitted the order |
| exchange | string | Returns the exchange the order was executed on |
| commission | string | Returns the cost of commission for the trade |
| net_amount | float | Returns the total net cost of the order |
| account | string | Returns the account identifier |
| accountCode | string | Returns the account identifier |
| company_name | string | Returns the long name of the contract's company |
| contract_description_1 | string | Returns the local symbol of the order |
| sec_type | string | Returns the security type of the contract |
| listing_exchange | string | Returns the primary listing exchange of the contract |
| conid | int | Returns the contract identifier of the order |
| conidEx | string | Returns the contract identifier of the order |
| clearing_id | string | Returns the clearing firm identifier |
| clearing_name | string | Returns the clearing firm name |
| liquidation_trade | string | Returns whether the order was part of an account liquidation or not |
| is_event_trading | string | Returns whether the order was part of event trading or not |

#### Example Response

```json
[
  {
    "execution_id": "0000e0d5.6576fd38.01.01",
    "symbol": "AAPL",
    "supports_tax_opt": "1",
    "side": "S",
    "order_description": "Sold 5 @ 192.26 on ISLAND",
    "trade_time": "20231211-18:00:49",
    "trade_time_r": 1702317649000,
    "size": 5.0,
    "price": "192.26",
    "order_ref": "Order123",
    "submitter": "user1234",
    "exchange": "ISLAND",
    "commission": "1.01",
    "net_amount": 961.3,
    "account": "U1234567",
    "accountCode": "U1234567",
    "company_name": "APPLE INC",
    "contract_description_1": "AAPL",
    "sec_type": "STK",
    "listing_exchange": "NASDAQ.NMS",
    "conid": 265598,
    "conidEx": "265598",
    "clearing_id": "IB",
    "clearing_name": "IB",
    "liquidation_trade": "0",
    "is_event_trading": "0"
  }
]
```

## Orders

Endpoints for placing, modifying, cancelling, and previewing orders. Includes reply confirmation workflow, message suppression, combo/spread orders, overnight orders, and cash quantity orders.

---

[↑ Back to Table of Contents](#table-of-contents)


### Place Order

When connected to an IServer Brokerage Session, this endpoint will allow you to submit orders. CP WEB API supports various advanced orderTypes.

**Cash Quantity:** Send orders using monetary value by specifying cashQty instead of quantity, e.g. cashQty: 200. The endpoint /iserver/contract/rules returns list of valid orderTypes in cqtTypes.

**Currency Conversion:** Convert cash from one currency to another by including isCcyConv = true. To specify the cash quantity use fxQTY instead of quantity, e.g. fxQTY: 100.

**IB Algos:** Attach user-defined settings to your trades by using any of IBKR's Algo Orders. Use the endpoint /iserver/contract/{conid}/algos to identify the available strategies for a contract.

> **Notes:**
> - With the exception of OCA groups and bracket orders, the orders endpoint does not currently support the placement of unrelated orders in bulk.
> - Developers should not attempt to place another order until the previous order has been fully acknowledged, that is, when no further warnings are received deferring the client to the reply endpoint.

- **Method:** `POST`
- **URL:** `/iserver/account/{accountId}/orders`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which account should place the order. Financial Advisors should instead specify their allocation group |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| orders | array\<OrderTicket\> | yes | Used to pass the order content. See nested table below |

**`OrderTicket` object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctId | string | no | It should be one of the accounts returned by /iserver/accounts. If not passed, the first one in the list is selected |
| conid | int | conditional | conid is the identifier of the security you want to trade. Using the conid field will force the order to be SMART-routed, even if conidex is specified. You can find the conid with /iserver/secdef/search. Can use conidex instead of conid |
| conidex | string | conditional | conidex is the identifier for the security and exchange you want to trade. Direct routed orders cannot use the conid field in addition to conidex, otherwise the order will be automatically routed to SMART. Can use conidex instead of conid |
| manualIndicator | bool | conditional | **IMPORTANT** This field is required when trading Futures and Futures Options contracts to remain in compliance with CME Group Rule 536-B. true indicates the order was submitted manually through an interface while false indicates an order was submitted autonomously |
| extOperator | string | conditional | **IMPORTANT** This field is required when trading Futures and Futures Options contracts to remain in compliance with CME Group Rule 536-B. Should contain information regarding the submitting user in charge of the API operation at the time of request submission |
| secType | string | no | The contract-identifier (conid) and security type (type) specified as a concatenated value. Format: "conid:type" |
| cOID | string | no | Customer Order ID. An arbitrary string that can be used to identify the order. The value must be unique for a 24h span. Do not set this value for child orders when placing a bracket order |
| parentId | string | no | Only specify for child orders when placing bracket orders. The parentId for the child order(s) must be equal to the cOID (customer order id) of the parent |
| orderType | string | yes | The order-type determines what type of order you want to send. One of: `LMT`, `MKT`, `STP`, `STOP_LIMIT`, `MIDPRICE`, `TRAIL`, `TRAILLMT` |
| listingExchange | string | no | Primary routing exchange for the order. By default we use "SMART" routing. Possible values are available via /iserver/contract/{conid}/info |
| isSingleGroup | bool | no | Set to true if you want to place a single group orders (OCA) |
| outsideRTH | bool | no | Set to true if the order can be executed outside regular trading hours |
| price | float | conditional | This is typically the limit price. For STP/TRAIL this is the stop price. For MIDPRICE this is the option price cap. Required for LMT or STOP_LIMIT |
| auxPrice | float | conditional | Stop price for STOP_LIMIT and TRAILLMT orders. You must specify both price and auxPrice for STOP_LIMIT/TRAILLMT orders. Required for STOP_LIMIT and TRAILLMT orders |
| side | string | yes | One of: `SELL`, `BUY` |
| ticker | string | no | This is the underlying symbol for the contract |
| tif | string | yes | The Time-In-Force determines how long the order remains active on the market. One of: `GTC`, `OPG`, `DAY`, `IOC`, `PAX` (CRYPTO ONLY) |
| trailingAmt | float | conditional | When trailingType is amt, this is the trailing amount. When trailingType is %, it means percentage. Required for TRAIL and TRAILLMT orders |
| trailingType | string | conditional | This is the trailing type for trailing amount. You must specify both trailingType and trailingAmt for TRAIL and TRAILLMT orders. One of: `amt`, `%`. Required for TRAIL and TRAILLMT orders |
| allOrNone | bool | no | Determine if the order should be executed in its entirety at once (true), or if the order may be filled through multiple executions (false) |
| customerAccount | string | conditional | Required for Nondisclosed Omnibus Accounts. A unique identifier for each account within the Omnibus structure. Best practice: hash this value using 5 digits of SHA1 of the account number. Should not be implemented for non-omnibus accounts |
| isProCustomer | bool | conditional | Required for Nondisclosed Omnibus Accounts. Signify whether the subaccount is classified as Professional or Non-Professional. Should not be implemented for non-omnibus accounts |
| referrer | string | no | Custom order reference |
| quantity | float | conditional | Used to designate the total number of shares traded for the order. Only whole share values are supported. Required unless using cashQty or fxQty |
| cashQty | float | no | Used to specify the monetary value of an order instead of the number of shares. When using cashQty don't specify quantity. Cash quantity orders are provided on a non-guaranteed basis. Only supported for Cryptocurrency and Forex contracts |
| fxQty | float | no | This is the cash quantity field which can only be used for Currency Conversion Orders. When using fxQty don't specify quantity |
| useAdaptive | bool | no | If true, the system will use the Price Management Algo to submit the order |
| isCcyConv | bool | no | Set to true if the order is a FX conversion order |
| allocationMethod | string | no | Set the allocation method when placing an order using an FA account for a group. Based on value set in Trader Workstation |
| manualOrderTime | int | no | Only used for Brokers and Advisors. Mark the time to manually record initial order entry. Must be sent as epoch time integer |
| deactivated | bool | no | Functions the same as Saving an Order in Trader Workstation |
| strategy | string | no | Specify which IB Algo algorithm to use for this order |
| strategyParameters | object | no | The IB Algo parameters for the specified algorithm |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| order_id | string | Returns the orders identifier which can be used for order tracking, modification, and cancellation |
| order_status | string | Returns the order status of the current market order. See Order Status Values for more information |
| encrypt_message | string | Returns a "1" to display that the message sent was encrypted |

#### Example Response

```json
[
  {
    "order_id": "1234567890",
    "order_status": "Submitted",
    "encrypt_message": "1"
  }
]
```

#### Alternate Response Object

In some instances, you will receive an ID along with a message about your order. See the Place Order Reply section for more details on resolving the confirmation. Users that wish to avoid receiving /reply messages should consider using the Suppression endpoint to automatically accept them.

> **Important:** The reply must be confirmed before sending any further orders. Otherwise, the order will be invalidated and attempts to confirm invalid replies will result in a timeout (503).

| Field | Type | Description |
|-------|------|-------------|
| id | string | Returns a message ID relating to the particular order's warning confirmation |
| message | array\<string\> | Returns the message warning about why the order wasn't initially transmitted |
| isSuppressed | bool | Returns if a particular warning was suppressed before sending. Always returns false |
| messageIds | array\<string\> | Returns an internal message identifier (Internal use only) |

#### Example Alternate Response

```json
[
  {
    "id": "07a13a5a-4a48-44a5-bb25-5ab37b79186c",
    "message": [
      "The following order \"BUY 5 AAPL NASDAQ.NMS @ 150.0\" price exceeds \nthe Percentage constraint of 3%.\nAre you sure you want to submit this order?"
    ],
    "isSuppressed": false,
    "messageIds": ["o163"]
  }
]
```

#### Order Reject Object

In the event an order is placed that can not be completed based on account details such as trading permissions or funds, customers will receive a 200 OK response along with an error message explaining the issue. This is unique from the 200 response used in the Alternate Response Object, or a potential 500 error resulting from invalid request content.

```json
{
  "error": "We cannot accept an order at the limit price you selected. Please submit your order using a limit price that is closer to the current market price of 197.79. Alternatively, you can convert your order to an Algorithmic Order (IBALGO)."
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Cash Quantity Orders in the Web API

Cash Quantity orders are only supported for Cryptocurrency, Forex, and Stock contracts.

- Stock orders submitted using Cash Quantity field through the API will round down to the nearest whole share. In the event an order is submitted with a value less than one share, the order will be rejected.
- Orders submitted for Crypto or Forex will be traded directly as submitted.

---

[↑ Back to Table of Contents](#table-of-contents)


### Place Order Reply Confirmation

Confirm order precautions and warnings presented from placing orders. Orders **must** be replied to immediately after receiving the reply message. Submitting other orders or other requests will cancel the order and attempts to acknowledge the reply will result in a 503 error.

Users that wish to avoid receiving /reply messages should consider using the Suppression endpoint to automatically accept them.

- **Method:** `POST`
- **URL:** `/iserver/reply/{replyId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| replyId | path | string | yes | Include the id value from the prior order request relating to the particular order's warning confirmation |

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| confirmed | bool | yes | Pass your confirmation to the reply to allow or cancel the order to go through. true will agree to the message and transmit the order. false will decline the message and discard the order |

#### Example Request

```json
{
  "confirmed": true
}
```

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| order_id | string | Returns the orders identifier which can be used for order tracking, modification, and cancellation |
| order_status | string | Returns the order status of the current market order. See Order Status Values for more information |
| encrypt_message | string | Returns a "1" to display that the message sent was encrypted |

#### Example Response

```json
[
  {
    "order_id": "1234567890",
    "order_status": "Submitted",
    "encrypt_message": "1"
  }
]
```

> **Note:** After sending your initial confirmation to the /iserver/reply/{replyId} endpoint, you may receive additional reply messages. These confirmation messages must also be responded to before the order will submit.

---

[↑ Back to Table of Contents](#table-of-contents)


### Respond to a Server Prompt

Respond to a server prompt received via ntf websocket message.

- **Method:** `POST`
- **URL:** `/iserver/notification`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| orderId | int | yes | IB-assigned order identifier obtained from the ntf websocket message that delivered the server prompt |
| reqId | string | yes | IB-assigned request identifier obtained from the ntf websocket message that delivered the server prompt |
| text | string | yes | The selected value from the "options" array delivered in the server prompt ntf websocket message |

#### Example Request

```json
{
  "orderId": 987654321,
  "reqId": "12345",
  "text": "Yes"
}
```

#### Response Body

Returns status text string (e.g., "Success").

---

[↑ Back to Table of Contents](#table-of-contents)


### Preview Order / WhatIf Order

This endpoint allows you to preview an order without actually submitting it and you can get commission information in the response. Also supports bracket orders.

> **Notes:**
> - /whatif orders are also affected by the message suppression endpoint.
> - Clients must query /iserver/marketdata/snapshot for the instrument prior to requesting the /whatif endpoint.

- **Method:** `POST`
- **URL:** `/iserver/account/{accountId}/orders/whatif`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which account should preview the order |

#### Request Body

The body content of the /whatif endpoint follows the same structure as the standard /iserver/account/{accountId}/orders endpoint. See the Place Order section for more details.

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| amount | object | Contains the details about the order cost. See nested table below |
| equity | object | Contains the details about the order's impact on your equity. See nested table below |
| initial | object | Contains the details about the order's impact on your initial margin. See nested table below |
| maintenance | object | Contains the details about the order's impact on your maintenance margin. See nested table below |
| position | object | Contains the details about the order's impact on your current position. See nested table below |
| warn | string | Returns any potential warning message from placing this order. Returns null if no warning is possible |
| error | string | Returns any potential error message from placing this order. Returns null if no error is possible |

**`amount` object:**

| Field | Type | Description |
|-------|------|-------------|
| amount | string | Returns the cost of the base order |
| commission | string | Returns the commission cost of the base order |
| total | string | Returns the total cost of the order |

**`equity`, `initial`, `maintenance`, `position` objects (same structure):**

| Field | Type | Description |
|-------|------|-------------|
| current | string | Returns the current value |
| change | string | Returns the change from the order |
| after | string | Returns the value after the order is traded |

#### Example Response

```json
{
  "amount": {
    "amount": "1,977.60 USD (10 Shares)",
    "commission": "1 USD",
    "total": "1,978.60 USD"
  },
  "equity": {
    "current": "215,415,594",
    "change": "-1",
    "after": "215,415,593"
  },
  "initial": {
    "current": "116,965",
    "change": "652",
    "after": "117,617"
  },
  "maintenance": {
    "current": "106,332",
    "change": "592",
    "after": "106,924"
  },
  "position": {
    "current": "0",
    "change": "10",
    "after": "10"
  },
  "warn": "21/You are trying to submit an order without having market data for this instrument...",
  "error": null
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Overnight Order Submission

Trading with the WebAPI allows users to submit orders in the OVERNIGHT market using both OVERNIGHT exclusive orders as well as OVERNIGHT+DAY orders. This is handled by submitting the affiliated Time-In-Force value when Placing an Order.

**Overnight:** Overnight orders are submitted using the `OVT` time in force value.

```json
{ "tif": "OVT" }
```

**Overnight+DAY:** Overnight+DAY orders are submitted using the `OND` time in force value.

```json
{ "tif": "OND" }
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Combo / Spread Orders

Combination orders or spread orders may also be placed using the same orders endpoint. In the case of combo orders, we must use the conidex instead of conid. The conidex field is a string representation of our combo order parameters.

**Combo Orders follow the format of:** `{spread_conid};;;{leg_conid1}/{ratio},{leg_conid2}/{ratio}`

The spread_conid is a unique identifier used to denote a spread order. For US Stock Combos, only the spread_conid needs to be submitted. For all other countries, you will need to use the format `spread_conid@exchange`.

**Available currency spread conids:**

| Currency | Spread ConID |
|----------|-------------|
| AUD | 61227077 |
| CAD | 61227082 |
| CHF | 61227087 |
| CNH | 136000441 |
| GBP | 58666491 |
| HKD | 61227072 |
| INR | 136000444 |
| JPY | 61227069 |
| KRW | 136000424 |
| MXN | 136000449 |
| SEK | 136000429 |
| SGD | 426116555 |
| USD | 28812380 |

Following the spread_conid, add three semicolons (;;;), then the first leg_conid. After the conid, include a forward slash (/) followed by the spread ratio. The ratio sign indicates the side: positive = Buy (Long), negative = Sell (Short). The ratio value is the multiplier of your quantity value. Additional legs are separated by commas.

**Combo Order Pricing:** Combo orders submit their price values based on the value of the individual leg, multiplied by the ratio. Each leg is then added together to create the final price. Prices can be negative if a sold leg's total value exceeds the bought leg.

Price formula: `[({Cost of Leg 1} * {Ratio of Leg 1}) + ({Cost of Leg n} * {Ratio of Leg n}) + ...]`

---

[↑ Back to Table of Contents](#table-of-contents)


### Bracket Orders and OCA Groups

The available values and structures of Bracket or OCA orders follow the same general structure of individual orders. Bracket and OCA orders require a parent order be submitted, and then each leg, or child order, would include the parent's order ID.

Bracket orders can be submitted sequentially using the default order_id created by Interactive Brokers, OR bracket orders can be submitted using the `cOID` field for the parent order, and then use this same value in each of the child orders in the `parentId` field.

#### Bracket Order Example

A standard bracket order contains a parent order, a profit taker, and a stop loss. The only addition to a standard order is the inclusion of `cOID` in the parent order, and the `parentId` field in the two children.

```json
{
  "orders": [
    {
      "acctId": "U1234567",
      "conid": 265598,
      "cOID": "Parent",
      "orderType": "MKT",
      "listingExchange": "SMART",
      "outsideRTH": true,
      "side": "Buy",
      "referrer": "QuickTrade",
      "tif": "GTC",
      "quantity": 50
    },
    {
      "acctId": "U1234567",
      "conid": 265598,
      "orderType": "STP",
      "listingExchange": "SMART",
      "outsideRTH": false,
      "price": 157.30,
      "side": "Sell",
      "tif": "GTC",
      "quantity": 50,
      "parentId": "Parent"
    },
    {
      "acctId": "U1234567",
      "conid": 265598,
      "orderType": "LMT",
      "listingExchange": "SMART",
      "outsideRTH": false,
      "price": 157.00,
      "side": "Sell",
      "tif": "GTC",
      "quantity": 50,
      "parentId": "Parent"
    }
  ]
}
```

#### OCA Group Example

An OCA group follows the same structure as a bracket order. However, in addition to the standard bracket, each order will include `"isSingleGroup": true`. No additional modifications need to be made.

```json
{
  "orders": [
    {
      "acctId": "U1234567",
      "conid": 265598,
      "cOID": "Parent",
      "orderType": "MKT",
      "listingExchange": "SMART",
      "isSingleGroup": true,
      "outsideRTH": true,
      "side": "Buy",
      "referrer": "QuickTrade",
      "tif": "GTC",
      "quantity": 50
    },
    {
      "acctId": "U1234567",
      "conid": 265598,
      "orderType": "STP",
      "listingExchange": "SMART",
      "isSingleGroup": true,
      "outsideRTH": false,
      "price": 157.30,
      "side": "Sell",
      "tif": "GTC",
      "quantity": 50,
      "parentId": "Parent"
    },
    {
      "acctId": "U1234567",
      "conid": 265598,
      "orderType": "LMT",
      "listingExchange": "SMART",
      "isSingleGroup": true,
      "outsideRTH": false,
      "price": 157.00,
      "side": "Sell",
      "tif": "GTC",
      "quantity": 50,
      "parentId": "Parent"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Cancel Order

Cancels an open order. Must call /iserver/accounts endpoint prior to cancelling an order. Use /iserver/account/orders endpoint to review open-order(s) and get latest order status.

- **Method:** `DELETE`
- **URL:** `/iserver/account/{accountId}/order/{orderId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which account should cancel the order |
| orderId | path | string | yes | The orderID that should be cancelled. Can be retrieved from /iserver/account/orders. Submitting '-1' will cancel all open orders |
| manualIndicator | query | bool | conditional | **IMPORTANT** Required when trading Futures and Futures Options contracts for CME Group Rule 536-B compliance. true indicates the cancellation was done manually, false indicates automated |
| extOperator | query | string | conditional | **IMPORTANT** Required when trading Futures and Futures Options contracts for CME Group Rule 536-B compliance. Should contain information regarding the submitting user |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| msg | string | Returns the confirmation of the request being submitted |
| order_id | int | Returns the orderID of the cancelled order |
| conid | int | Returns the conid for the requested order to be cancelled. Returns -1 for orders that were immediately cancelled on request |
| account | string | Returns the accountId for the requested order to be cancelled. Returns null for orders that were immediately cancelled on request |

#### Example Response

```json
{
  "msg": "Request was submitted",
  "order_id": 123456789,
  "conid": 265598,
  "account": "U1234567"
}
```

#### Error Response

```json
{
  "error": "OrderID 1 doesn't exist"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Modify Order

Modifies an open order. Must call /iserver/accounts endpoint prior to modifying an order. Use /iserver/account/orders endpoint to review open-order(s). The body content should mirror the content of the original order with the desired changes.

- **Method:** `POST`
- **URL:** `/iserver/account/{accountId}/order/{orderId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which account should modify the order |
| orderId | path | string | yes | The orderID that should be modified. Can be retrieved from /iserver/account/orders |

#### Request Body

The body content follows the same structure as the standard /iserver/account/{accountId}/orders endpoint. The content should mirror the content of the original order.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| manualIndicator | bool | conditional | **IMPORTANT** Required when trading Futures and Futures Options contracts for CME Group Rule 536-B compliance. true indicates the modification was done manually, false indicates automated |
| extOperator | string | conditional | **IMPORTANT** Required when trading Futures and Futures Options contracts for CME Group Rule 536-B compliance. Should contain information regarding the submitting user |

See the Place Order section for the full list of body fields.

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| order_id | string | Returns the orders identifier which can be used for order tracking, modification, and cancellation |
| order_status | string | Returns the order status of the current market order. See Order Status Values for more information |
| encrypt_message | string | Returns a "1" to display that the message sent was encrypted |

#### Example Response

```json
[
  {
    "order_id": "1234567890",
    "order_status": "Submitted",
    "encrypt_message": "1"
  }
]
```

#### Alternate Response Object

In some instances, you will receive an ID along with a message about your order. See the Place Order Reply section for more details on resolving the confirmation.

| Field | Type | Description |
|-------|------|-------------|
| id | string | Returns a message ID relating to the particular order's warning confirmation |
| message | array\<string\> | Returns the message warning about why the order wasn't initially transmitted |
| isSuppressed | bool | Returns if a particular warning was suppressed before sending. Always returns false |
| messageIds | array\<string\> | Returns an internal message identifier (Internal use only) |

---

[↑ Back to Table of Contents](#table-of-contents)


### Suppress Messages

Disables a messageId, or series of messageIds, that will no longer prompt the user.

- **Method:** `POST`
- **URL:** `/iserver/questions/suppress`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| messageIds | array\<string\> | yes | The identifier for each warning message to suppress. The array supports up to 51 messages sent in a single request. Any additional values will result in a system error. Only supported message IDs from the Suppressible Message IDs list are accepted. Users should suppress messages on an as-needed basis to avoid unexpected order submissions |

#### Example Request

```json
{
  "messageIds": ["o102"]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| status | string | Verifies that the request has been sent |

#### Example Response

```json
{
  "status": "submitted"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Suppressible MessageIds

<details>
<summary>Click to expand Suppressible MessageIds (26 messages)</summary>

| MessageId | Text |
|-----------|------|
| o163 | The following order exceeds the price percentage limit |
| o354 | You are submitting an order without market data. We strongly recommend against this |
| o382 | The following value exceeds the tick size limit |
| o383 | Order size exceeds the Size Limit |
| o403 | This order will most likely trigger and fill immediately |
| o451 | Order value estimate exceeds the Total Value Limit |
| o2136 | Mixed allocation order warning |
| o2137 | Cross side order warning |
| o2165 | Instrument does not support trading in fractions outside regular trading hours |
| o10082 | Called Bond warning |
| o10138 | Order size modification exceeds the size modification limit |
| o10151 | Warns about risks with Market Orders |
| o10152 | Warns about risks associated with stop orders once they become active |
| o10153 | Confirm Mandatory Cap Price -- IB may set a cap for buy/sell orders to avoid unfair trading |
| o10164 | Cash quantity details are provided on a best efforts basis only |
| o10223 | Cash Quantity Order Confirmation -- orders using monetary value are provided on a non-guaranteed basis |
| o10288 | Warns about risks associated with market orders for Crypto |
| o10331 | Stop order risk warning |
| o10332 | OSL Digital Securities LTD Crypto Order Warning |
| o10333 | Option Exercise at the Money warning |
| o10334 | Order will be placed into current omnibus account instead of currently selected global account |
| o10335 | Serves internal Rapid Entry window |
| o10336 | Security has limited liquidity -- heightened risk of not being able to close position |
| p6 | Order will be distributed over multiple accounts -- familiarize yourself with allocation facilities |
| p12 | Price Management Algo recommendation -- order may be rejected if limit price is too far from reference price |

</details>

---

[↑ Back to Table of Contents](#table-of-contents)


### Reset Suppressed Messages

Resets all messages disabled by the Suppress Messages endpoint.

- **Method:** `POST`
- **URL:** `/iserver/questions/suppress/reset`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| status | string | Verifies that the request has been sent |

#### Example Response

```json
{
  "status": "submitted"
}
```

## Portfolio

Endpoints for retrieving portfolio account information, sub-account listings, allocation breakdowns by asset class/sector/group, and combination positions.

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Accounts

In non-tiered account structures, returns a list of accounts for which the user can view position and account information. This endpoint must be called prior to calling other /portfolio endpoints for those accounts. For querying a list of accounts which the user can trade, see /iserver/accounts. For a list of subaccounts in tiered account structures (e.g. financial advisor or ibroker accounts) see /portfolio/subaccounts.

- **Method:** `GET`
- **URL:** `/portfolio/accounts`

#### Response Body

Returns an array of PortfolioAccount objects. See the shared PortfolioAccount schema below.

#### Example Response

```json
[
  {
    "id": "U1234567",
    "accountId": "U1234567",
    "accountVan": "U1234567",
    "accountTitle": "",
    "displayName": "U1234567",
    "accountAlias": null,
    "accountStatus": 1644814800000,
    "currency": "USD",
    "type": "DEMO",
    "tradingType": "PMRGN",
    "businessType": "IB_PROSERVE",
    "ibEntity": "IBLLC-US",
    "faclient": false,
    "clearingStatus": "O",
    "covestor": false,
    "noClientTrading": false,
    "trackVirtualFXPortfolio": true,
    "parent": {
      "mmc": [],
      "accountId": "",
      "isMParent": false,
      "isMChild": false,
      "isMultiplex": false
    },
    "desc": "U1234567"
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Subaccounts

Used in tiered account structures (such as Financial Advisor and IBroker Accounts) to return a list of up to 100 sub-accounts for which the user can view position and account-related information. This endpoint must be called prior to calling other /portfolio endpoints for those sub-accounts. If you have more than 100 sub-accounts use /portfolio/subaccounts2. To query a list of accounts the user can trade, see /iserver/accounts.

- **Method:** `GET`
- **URL:** `/portfolio/subaccounts`

#### Response Body

Returns an array of PortfolioAccount objects. Same schema as Portfolio Accounts.

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Subaccounts (Large Account Structures)

Used in tiered account structures (such as Financial Advisor and IBroker Accounts) to return a list of sub-accounts, paginated up to 20 accounts per page, for which the user can view position and account-related information. This endpoint must be called prior to calling other /portfolio endpoints for those sub-accounts. If you have less than 100 sub-accounts use /portfolio/subaccounts. To query a list of accounts the user can trade, see /iserver/accounts.

- **Method:** `GET`
- **URL:** `/portfolio/subaccounts2`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| page | query | string | yes | Indicate the page identifier that should be retrieved. Pagination begins at page 0. 20 accounts returned per page |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| metadata | object | Contains metadata about the response data. See nested table below |
| subaccounts | array\<PortfolioAccount\> | Contains all of the accounts and their respective data |

**`metadata` object:**

| Field | Type | Description |
|-------|------|-------------|
| total | int | Displays the total number of accounts returned |
| pageSize | int | Returns the page size |
| pageNum | int | Returns the page number or identifier of the request |

---

[↑ Back to Table of Contents](#table-of-contents)


### Specific Account's Portfolio Information

Account information related to account Id. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/meta`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Specify the AccountID to receive portfolio information for |

#### Response Body

Returns a single PortfolioAccount object. Same schema as Portfolio Accounts (returned as object, not array).

---

[↑ Back to Table of Contents](#table-of-contents)


### PortfolioAccount Schema

The following schema is shared across Portfolio Accounts, Portfolio Subaccounts, Portfolio Subaccounts2, and Portfolio Meta endpoints.

| Field | Type | Description |
|-------|------|-------------|
| id | string | The account ID |
| accountId | string | The account ID |
| accountVan | string | The account alias |
| accountTitle | string | Title of the account |
| displayName | string | Display name for the account |
| accountAlias | string | User customizable account alias. Refer to Configure Account Alias for details |
| accountStatus | int | When the account was opened in unix time |
| currency | string | Base currency of the account |
| type | string | Account Type |
| tradingType | string | Account trading structure |
| businessType | string | Returns the organizational structure of the account |
| ibEntity | string | Returns the entity of Interactive Brokers the account is tied to |
| faclient | bool | If an account is a sub-account to a Financial Advisor |
| clearingStatus | string | Status of the Account. One of: `O` (Open), `P` or `N` (Pending), `A` (Abandoned), `R` (Rejected), `C` (Closed) |
| covestor | bool | Is a Covestor Account |
| noClientTrading | bool | Returns if the client account may trade |
| trackVirtualFXPortfolio | bool | Returns if the account is tracking Virtual FX or not |
| parent | object | Parent account information. See nested table below |
| desc | string | Returns an account description. Format: "accountId - accountAlias" |

**`parent` object:**

| Field | Type | Description |
|-------|------|-------------|
| mmc | array\<string\> | Returns the Money Manager Client Account |
| accountId | string | Account Number for Money Manager Client |
| isMParent | bool | Returns if this is a Multiplex Parent Account |
| isMChild | bool | Returns if this is a Multiplex Child Account |
| isMultiplex | bool | Is a Multiplex Account. These are account models with individual account being parent and managed account being child |

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Allocation (Single)

Information about the account's portfolio allocation by Asset Class, Industry and Category. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/allocation`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Specify the account ID for the request |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| assetClass | object | Contains details pertaining to specific security types. Has `long` and `short` sub-objects with dynamic keys (asset type codes like STK, OPT, CASH) mapped to float values |
| sector | object | Contains details pertaining to specific trade sectors. Has `long` and `short` sub-objects with dynamic keys (sector names) mapped to float values |
| group | object | Contains details pertaining to specific industry groups. Has `long` and `short` sub-objects with dynamic keys (group names) mapped to float values |

#### Example Response

```json
{
  "assetClass": {
    "long": {
      "OPT": 27.12,
      "STK": 317071.39,
      "CASH": 215101100.08
    },
    "short": {
      "OPT": -30.0,
      "CASH": -25.92
    }
  },
  "sector": {
    "long": {
      "Technology": 237511.16,
      "Industrial": 43134.63,
      "Consumer, Cyclical": 22537.63,
      "Financial": 2504.35,
      "Communications": 5116.61
    },
    "short": {
      "Others": -30.0
    }
  },
  "group": {
    "long": {
      "Computers": 121517.38,
      "Semiconductors": 115993.78,
      "Auto Manufacturers": 22537.63,
      "Banks": 2504.35
    },
    "short": {
      "Others": -30.0
    }
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Combination Positions

Provides all positions held in the account acquired as a combination, including values such as ratios, size, and market value.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/combo/positions`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which to retrieve combo positions |
| nocache | query | bool | no | Set if request should be made without caching. Defaults to false |

#### Response Body

Returns an array of objects.

| Field | Type | Description |
|-------|------|-------------|
| name | string | Internal name used to distinguish between combinations |
| description | string | Provides the ratio and leg conIDs for the combo |
| legs | array\<ComboLeg\> | An array containing all legs in the specific combination. See nested table below |
| positions | array\<ComboPosition\> | Provides an array including the leg information in the combo. See nested table below |

**`ComboLeg` object:**

| Field | Type | Description |
|-------|------|-------------|
| conid | string | Returns the conid of one leg of the combo |
| ratio | int | Returns the ratio value for the combo. Can be positive or negative |

**`ComboPosition` object:**

| Field | Type | Description |
|-------|------|-------------|
| acctId | string | Returns the accountId holding the leg |
| conid | int | Returns the contract ID for the specific leg |
| contractDesc | string | Returns the long name for the given contract |
| position | float | Returns the total size of the specific leg in the combination |
| mktPrice | float | Returns the current market price of each share for the leg |
| mktValue | float | Returns the total value of the position in the combo |
| currency | string | Returns the base currency of the leg |
| avgCost | float | Returns the average cost of each share in the position times the multiplier |
| avgPrice | float | Returns the average cost of each share in the position when purchased |
| realizedPnl | float | Returns the total profit made today through trades |
| unrealizedPnl | float | Returns the total potential profit if you were to trade |
| exchs | string | Deprecated. Always returns null |
| expiry | string | Deprecated. Always returns null |
| putOrCall | string | Deprecated. Always returns null |
| multiplier | string | Deprecated. Always returns null |
| strike | float | Deprecated. Always returns 0.0 |
| exerciseStyle | string | Deprecated. Always returns null |
| conExchMap | array | Deprecated. Returns an empty array |
| assetClass | string | Returns the security type of the leg |
| undConid | int | Deprecated. Always returns 0 |

#### Example Response

```json
[
  {
    "name": "CP.CP66a00d50",
    "description": "1*708474422-1*710225103",
    "legs": [
      { "conid": "708474422", "ratio": 1 },
      { "conid": "710225103", "ratio": -1 }
    ],
    "positions": [
      {
        "acctId": "U1234567",
        "conid": 708474422,
        "contractDesc": "SPX AUG2024 5555 P [SPX 240816P05555000 100]",
        "position": 1.0,
        "mktPrice": 59.66,
        "mktValue": 5965.72,
        "currency": "USD",
        "avgCost": 6011.71,
        "avgPrice": 60.12,
        "realizedPnl": 0.0,
        "unrealizedPnl": -45.99,
        "assetClass": "OPT"
      },
      {
        "acctId": "U1234567",
        "conid": 710225103,
        "contractDesc": "SPX AUG2024 5565 C [SPX 240816C05565000 100]",
        "position": -1.0,
        "mktPrice": 78.03,
        "mktValue": -7802.52,
        "currency": "USD",
        "avgCost": 7628.29,
        "avgPrice": 76.28,
        "realizedPnl": 0.0,
        "unrealizedPnl": -174.23,
        "assetClass": "OPT"
      }
    ]
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Allocation (All)

Similar to /portfolio/{accountId}/allocation but returns a consolidated view of all the accounts returned by /portfolio/accounts. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `POST`
- **URL:** `/portfolio/allocation`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctIds | array\<string\> | yes | Contains all account IDs as strings the user should receive data for |

#### Example Request

```json
{
  "acctIds": ["U1234567", "U4567890"]
}
```

#### Response Body

Same structure as Portfolio Allocation (Single) — contains `assetClass`, `sector`, and `group` objects, each with `long` and `short` sub-objects with dynamic keys mapped to float values. Values are consolidated across all specified accounts.

---

[↑ Back to Table of Contents](#table-of-contents)


### Positions

Returns a list of positions for the given account. The endpoint supports paging, each page will return up to 100 positions. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/positions/{pageId}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID to retrieve positions for |
| pageId | path | string | yes | The "page" of positions that should be returned. One page contains a maximum of 100 positions. Pagination starts at 0 |
| model | query | string | no | Code for the model portfolio to compare against (assumed optional) |
| sort | query | string | no | Declare the table to be sorted by which column (assumed optional) |
| direction | query | string | no | The order to sort by. 'a' means ascending, 'd' means descending (assumed optional) |
| period | query | string | no | Period for PnL column. One of: `1D`, `7D`, `1M` (assumed optional) |

#### Response Body

Returns an array of position objects. Each position includes portfolio data (position, mktPrice, mktValue, avgCost, avgPrice, realizedPnl, unrealizedPnl) plus contract details (conid, contractDesc, currency, assetClass, ticker, name, sector, group, etc.) and display rules (incrementRules, displayRule). See the Position & Contract Info endpoint below for the full shared schema, as both endpoints return the same structure.

#### Example Response

```json
[
  {
    "acctId": "U1234567",
    "conid": 756733,
    "contractDesc": "SPY",
    "position": 5.0,
    "mktPrice": 471.16,
    "mktValue": 2355.8,
    "currency": "USD",
    "avgCost": 434.93,
    "avgPrice": 434.93,
    "realizedPnl": 0.0,
    "unrealizedPnl": 181.15,
    "exchs": null,
    "expiry": null,
    "putOrCall": null,
    "multiplier": null,
    "strike": 0.0,
    "exerciseStyle": null,
    "conExchMap": [],
    "assetClass": "STK",
    "undConid": 0,
    "model": ""
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Positions (NEW)

Returns a list of positions for the given account. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint. This endpoint provides near-real time updates and removes caching otherwise found in the /portfolio/{accountId}/positions/{pageId} endpoint.

- **Method:** `GET`
- **URL:** `/portfolio2/{accountId}/positions`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID to retrieve positions for |
| model | query | string | no | Code for the model portfolio to compare against (assumed optional) |
| sort | query | string | no | Declare the table to be sorted by which column (assumed optional) |
| direction | query | string | no | The order to sort by. 'a' means ascending, 'd' means descending (assumed optional) |

#### Response Body

Returns an array of objects with a simplified position schema.

| Field | Type | Description |
|-------|------|-------------|
| position | float | Returns the total size of the position |
| conid | int | Returns the contract ID of the position |
| avgCost | float | Returns the average cost of each share in the position times the multiplier |
| avgPrice | float | Returns the average cost of each share in the position when purchased |
| currency | string | Returns the traded currency for the contract |
| description | string | Returns the local symbol of the order |
| isLastToLoq | string | Returns if the contract is last to liquidate |
| mktPrice | float | Returns the current market price of each share |
| mktValue | float | Returns the total value of the order |
| realizedPnl | float | Returns the total profit made today through trades |
| unrealizedPnl | float | Returns the total potential profit if you were to trade |
| secType | string | Returns the asset class or security type of the contract |
| timestamp | int | Returns the epoch timestamp of the portfolio request |
| assetClass | string | Returns the asset class or security type of the contract |
| sector | string | Returns the contract's sector |
| group | string | Returns the group or industry the contract is affiliated with |
| model | string | Code for the model portfolio to compare against |

#### Example Response

```json
[
  {
    "position": 12.0,
    "conid": 9408,
    "avgCost": 266.21,
    "avgPrice": 266.21,
    "currency": "USD",
    "description": "MCD",
    "isLastToLoq": false,
    "marketPrice": 258.83,
    "marketValue": 3105.96,
    "realizedPnl": 0.0,
    "secType": "STK",
    "timestamp": 1717444668,
    "unrealizedPnl": 88.55,
    "assetClass": "STK",
    "sector": "Consumer, Cyclical",
    "group": "Retail",
    "model": ""
  }
]
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Positions by Conid

Returns a list containing position details only for the specified conid. The initial request will return exclusively the Portfolio information on the contract. Sequential requests for the contract will also return the contract's information and rules.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/position/{conid}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID to retrieve positions for |
| conid | path | string | yes | The contract ID to receive position information on |

#### Response Body

Returns an array of position objects. Same extended schema as the Positions endpoint — includes portfolio data plus contract details and display rules. See Position & Contract Info for the full shared schema.

---

[↑ Back to Table of Contents](#table-of-contents)


### Invalidate Backend Portfolio Cache

Invalidates the cached value for your portfolio's positions and calls the /portfolio/{accountId}/positions/0 endpoint automatically.

- **Method:** `POST`
- **URL:** `/portfolio/{accountId}/positions/invalidate`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | The account ID for which cache to invalidate |

#### Response Body

Returns an array of position objects. Same extended schema as the Positions endpoint — the cache is cleared and fresh position data is returned.

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Summary

Information regarding settled cash, cash balances, etc. in the account's base currency and any other cash balances held in other currencies. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/summary`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Specify the account ID for which account you require summary information on |

#### Response Body

The /summary endpoint returns a Key: Value Object structure. This returns a total of 45-135 unique values used to summarize the account. Responses will come as the base value, containing a summary of all returned details, followed by an identical response name with a trailing "-c" or "-s". "-c" represents commodity values held under the account. "-s" represents all security values held on the account.

Each value object has the following structure:

| Field | Type | Description |
|-------|------|-------------|
| amount | float | Returns the price value regarding the key. May return null if price value not required |
| currency | string | Returns the base currency the response is built with |
| isNull | bool | Returns if the value is unavailable |
| timestamp | int | Returns the time the data was retrieved in epoch time |
| value | string | Returns a string details about the given key. May return null if no string value required |
| severity | int | Internal use only |

#### Example Response

```json
{
  "accountcode": {
    "amount": 0.0,
    "currency": null,
    "isNull": false,
    "timestamp": 1702582422000,
    "value": "U1234567",
    "severity": 0
  },
  "indianstockhaircut": {
    "amount": 0.0,
    "currency": "USD",
    "isNull": false,
    "timestamp": 1702582422000,
    "value": null,
    "severity": 0
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Portfolio Ledger

Information regarding settled cash, cash balances, etc. in the account's base currency and any other cash balances held in other currencies. /portfolio/accounts or /portfolio/subaccounts must be called prior to this endpoint.

- **Method:** `GET`
- **URL:** `/portfolio/{accountId}/ledger`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| accountId | path | string | yes | Specify the account ID for which account you require ledger information on |

#### Response Body

Response is an object with dynamic currency keys (e.g., "USD", "EUR", "BASE"). Each key contains a ledger object for that currency.

**`Ledger` object:**

| Field | Type | Description |
|-------|------|-------------|
| commoditymarketvalue | float | Returns the total market value of commodity positions in the given currency |
| futuremarketvalue | float | Returns the total market value of futures positions in the given currency |
| settledcash | float | Returns the total settled cash for the given currency |
| exchangerate | int | Returns the exchange rate from the base currency to the specified currency |
| sessionid | int | Internal use only |
| cashbalance | float | Returns the total cash available for trading in the given currency |
| corporatebondsmarketvalue | float | Returns the total market value of corporate bond positions in the given currency |
| warrantsmarketvalue | float | Returns the total market value of warrant positions in the given currency |
| netliquidationvalue | float | Returns the current net liquidation value of the positions held in the given currency |
| interest | float | Returns the margin interest rate on the given currency |
| unrealizedpnl | float | Returns the unrealized profit and loss for positions in the given currency |
| stockmarketvalue | float | Returns the total market value of stock positions in the given currency |
| moneyfunds | float | Returns the total market value of money funds positions in the given currency |
| currency | string | Returns the currency's symbol |
| realizedpnl | float | Returns the realized profit and loss for positions in the given currency |
| funds | float | Returns the total market value of all funds positions in the given currency |
| acctcode | string | Returns the account ID for the account owner specified |
| issueroptionsmarketvalue | float | Returns the total market value of all issuer option positions in the given currency |
| key | string | Returns "LedgerList". Internal use only |
| timestamp | int | Returns the timestamp for the value retrieved in epoch time |
| severity | int | Internal use only |
| stockoptionmarketvalue | float | Returns the total market value of all stock option positions in the given currency |
| futuresonlypnl | float | Returns futures-only PnL |
| tbondsmarketvalue | float | Returns the total market value of all treasury bond positions in the given currency |
| futureoptionmarketvalue | float | Returns the total market value of all futures option positions in the given currency |
| cashbalancefxsegment | float | Internal use only |
| secondkey | string | Returns the currency's symbol |
| tbillsmarketvalue | float | Returns the total market value of all treasury bill positions in the given currency |
| dividends | float | Returns the value of dividends held from the given currency |

#### Example Response

```json
{
  "USD": {
    "commoditymarketvalue": 0.0,
    "futuremarketvalue": -1051.0,
    "settledcash": 214716688.0,
    "exchangerate": 1,
    "cashbalance": 214716688.0,
    "netliquidationvalue": 215335840.0,
    "interest": 305569.94,
    "unrealizedpnl": 39695.82,
    "stockmarketvalue": 314123.88,
    "currency": "USD",
    "realizedpnl": 0.0,
    "acctcode": "U1234567",
    "stockoptionmarketvalue": -2.88,
    "dividends": 0.0
  },
  "BASE": {
    "commoditymarketvalue": 0.0,
    "futuremarketvalue": -1051.0,
    "settledcash": 215100080.0,
    "exchangerate": 1,
    "cashbalance": 215100080.0,
    "netliquidationvalue": 215721776.0,
    "interest": 305866.88,
    "unrealizedpnl": 39907.37,
    "stockmarketvalue": 316365.38,
    "currency": "BASE",
    "realizedpnl": 0.0,
    "acctcode": "U1234567",
    "stockoptionmarketvalue": -2.88,
    "dividends": 0.0
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Position and Contract Info

Returns an object containing information about a given position along with its contract details. This is a cross-account endpoint that does not require an accountId in the path.

- **Method:** `GET`
- **URL:** `/portfolio/positions/{conid}`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| conid | path | string | yes | The contract ID to receive position information on |

#### Response Body

Returns an array of objects. Each object contains the full position and contract details schema shared by Positions, Positions by Conid, and Invalidate Cache endpoints.

| Field | Type | Description |
|-------|------|-------------|
| acctId | string | The account ID holding the position |
| conid | int | Returns the contract ID of the position |
| contractDesc | string | Returns the local symbol of the order |
| position | float | Returns the total size of the position |
| mktPrice | float | Returns the current market price of each share |
| mktValue | float | Returns the total value of the position |
| avgCost | float | Returns the average cost of each share in the position times the multiplier |
| avgPrice | float | Returns the average cost of each share in the position when purchased |
| realizedPnl | float | Returns the total profit made today through trades |
| unrealizedPnl | float | Returns the total potential profit if you were to trade |
| exchs | string | Deprecated. Always returns null |
| currency | string | Returns the traded currency for the contract |
| time | int | Returns amount of time in ms to generate the data |
| chineseName | string | Returns the Chinese characters for the symbol |
| allExchanges | string | Returns a comma-separated series of exchanges the given symbol can trade on |
| listingExchange | string | Returns the primary or listing exchange the contract is hosted on |
| countryCode | string | Returns the country code the contract is traded on |
| name | string | Returns the company name |
| assetClass | string | Returns the asset class or security type of the contract |
| expiry | string | Returns the expiry of the contract. Returns null for non-expiry instruments |
| lastTradingDay | string | Returns the last trading day of the contract |
| group | string | Returns the group or industry the contract is affiliated with |
| putOrCall | string | Returns if the contract is a Put or Call option |
| sector | string | Returns the contract's sector |
| sectorGroup | string | Returns the sector's group |
| strike | string | Returns the strike of the contract |
| ticker | string | Returns the ticker symbol of the traded contract |
| undConid | int | Returns the contract's underlyer |
| multiplier | float | Returns the contract multiplier |
| type | string | Returns stock type |
| hasOptions | bool | Returns if contract has tradable options contracts |
| fullName | string | Returns symbol name for requested contract |
| isUS | bool | Returns if the contract is US based or not |
| incrementRules | array\<IncrementRule\> | Returns rules regarding incrementation for market data and order placement |
| displayRule | object | Returns an object containing display content for market data |
| isEventContract | bool | Returns if the contract is an event contract or not |
| pageSize | int | Returns the content size of the request |
| model | string | Model portfolio code |

#### Example Response

```json
[
  {
    "acctId": "U1234567",
    "conid": 265598,
    "contractDesc": "AAPL",
    "position": 614.26,
    "mktPrice": 197.76,
    "mktValue": 121479.28,
    "currency": "USD",
    "avgCost": 192.75,
    "avgPrice": 192.75,
    "realizedPnl": 0.0,
    "unrealizedPnl": 3081.29,
    "exchs": null,
    "assetClass": "STK",
    "undConid": 0,
    "model": ""
  }
]
```

## Portfolio Analyst

Endpoints for analyzing portfolio performance, cumulative returns, and transaction history across accounts and time periods.

---

[↑ Back to Table of Contents](#table-of-contents)


### All Periods

Returns the performance across all available time periods for the given accounts. If more than one account is passed, the result is consolidated.

- **Method:** `POST`
- **URL:** `/pa/allperiods`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctIds | array\<string\> | yes | Include each account ID to receive data for |

#### Example Request

```json
{
  "acctIds": ["U1234567"]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| currencyType | string | Confirms the currency type. If trading exclusively in your base currency, "base" will be returned |
| rc | int | Returns the data identifier (Client Portal Only) |
| view | array\<string\> | Returns the accounts included in the response |
| nd | int | Returns the total data points |
| id | string | Returns the request identifier. Internal use only |
| included | array\<string\> | Returns the accounts included in the response |
| pm | string | Portfolio Measure. Used to indicate TWR or MWR values returned |
| {accountId} | object | Dynamic key for each account. Contains period objects. See nested structure below |

**`{accountId}` object contains:**

| Field | Type | Description |
|-------|------|-------------|
| {period} | object | Dynamic key for each period (1D, 7D, MTD, 1M, YTD, 1Y). See nested structure below |
| periods | array\<string\> | Returns the period ranges included in the response |
| start | string | Returns the starting date of the available data |
| end | string | Returns the end date of the available data |
| baseCurrency | string | Returns the base currency used in the account |
| lastSuccessfulUpdate | string | Timestamp of the last successful data update |

**`{period}` object (e.g., "1D", "YTD", "1Y"):**

| Field | Type | Description |
|-------|------|-------------|
| nav | array\<float\> | Net asset value data points for the period |
| cps | array\<float\> | Cumulative performance data points over the period |
| freq | string | Displays the frequency of the data (e.g., "D" for daily) |
| dates | array\<string\> | Returns the array of dates corresponding to the frequency. Length matches the data arrays |
| startNAV | object | Returns the initial NAV available. Contains `date` (string) and `val` (float) |

#### Example Response

```json
{
  "currencyType": "base",
  "rc": 0,
  "view": ["U1234567"],
  "nd": 366,
  "id": "getPerformanceAllPeriods",
  "included": ["U1234567"],
  "pm": "TWR",
  "U1234567": {
    "1D": {
      "nav": [3666392.54],
      "cps": [0.0005],
      "freq": "D",
      "dates": ["20250603"],
      "startNAV": { "date": "20250602", "val": 3664681.75 }
    },
    "YTD": {
      "nav": [3674381.33, "...", 3666392.54],
      "cps": [0, -0.0061, "...", -0.0021],
      "freq": "D",
      "dates": ["20250101", "...", "20250603"],
      "startNAV": { "date": "20241231", "val": 3674236.82 }
    },
    "periods": ["1D", "7D", "MTD", "1M", "YTD", "1Y"],
    "start": "20240603",
    "end": "20250603",
    "baseCurrency": "USD"
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Account Performance

Returns the performance (MTM) for the given accounts, if more than one account is passed, the result is consolidated.

- **Method:** `POST`
- **URL:** `/pa/performance`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctIds | array\<string\> | yes | Include each account ID to receive data for |
| period | string | yes | Specify the period for which the account should be analyzed. One of: `1D`, `7D`, `MTD`, `1M`, `YTD`, `1Y` |

#### Example Request

```json
{
  "acctIds": ["U1234567"],
  "period": "1D"
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| currencyType | string | Confirms the currency type. If trading primarily in your base currency, "base" will be returned |
| rc | int | Returns the data identifier (Client Portal Only) |
| nav | object | Net asset value data for the account or consolidated accounts. NAV data is not applicable to benchmarks. Contains `data` array, `freq`, and `dates` |
| nd | int | Returns the total data points |
| cps | object | Returns the object containing the Cumulative performance data. Contains `data` array, `freq`, and `dates` |
| tpps | object | Returns the Time period performance data. Contains `data` array, `freq`, and `dates` |
| id | string | Returns the request identifier (getPerformanceData) |
| included | array\<string\> | Returns an array containing accounts reviewed |
| pm | string | Portfolio Measure. Used to indicate TWR or MWR values returned |

**`nav.data`, `cps.data`, and `tpps.data` array items:**

| Field | Type | Description |
|-------|------|-------------|
| idType | string | Returns how identifiers are determined |
| start | string | Returns the first available date for data |
| end | string | Returns the end of the available frequency |
| id | string | Returns the account identifier |
| baseCurrency | string | Returns the base currency used in the account |
| navs | array\<float\> | (nav only) Returns the series of NAV data points |
| startNAV | object | (nav only) Returns the initial NAV. Contains `date` and `val` |
| returns | array\<float\> | (cps/tpps only) Returns all performance values in order between start and end |

#### Example Response

```json
{
  "currencyType": "base",
  "rc": 0,
  "nav": {
    "data": [
      {
        "idType": "acctid",
        "navs": [202767332.12, "...", 215718598.82],
        "start": "20230102",
        "end": "20231213",
        "id": "U1234567",
        "startNAV": { "date": "20221230", "val": 202767761.34 },
        "baseCurrency": "USD"
      }
    ],
    "freq": "D",
    "dates": ["20230102", "...", "20231213"]
  },
  "nd": 346,
  "cps": {
    "data": [
      {
        "idType": "acctid",
        "start": "20230102",
        "end": "20231213",
        "returns": [0, "...", 0.0639],
        "id": "U1234567",
        "baseCurrency": "USD"
      }
    ],
    "freq": "D",
    "dates": ["20230102", "...", "20231213"]
  },
  "tpps": {
    "data": [
      {
        "idType": "acctid",
        "start": "20230102",
        "end": "20231213",
        "returns": [0.0037, 0.0031, 0.0033, 0.0034, 0.02, 0.0127, 0.0036, 0.0036, 0.0034, 0.0012, 0.0026, 0.0017],
        "id": "U1234567",
        "baseCurrency": "USD"
      }
    ],
    "freq": "M",
    "dates": ["202301", "202302", "202303", "202304", "202305", "202306", "202307", "202308", "202309", "202310", "202311", "202312"]
  },
  "id": "getPerformanceData",
  "included": ["U1234567"],
  "pm": "TWR"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Transaction History

Transaction history for a given number of conids and accounts. Types of transactions include dividend payments, buy and sell transactions, and transfers.

- **Method:** `POST`
- **URL:** `/pa/transactions`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| acctIds | array\<string\> | yes | Include each account ID to receive data for |
| conids | array\<int\> | yes | Include contract ID to receive data for. Only supports one contract ID at a time |
| currency | string | yes | Define the currency to display price amounts with. Defaults to USD |
| days | string | no | Specify the number of days to receive transaction data for. Defaults to 90 days of transaction history if unspecified |

#### Example Request

```json
{
  "acctIds": ["U1234567"],
  "conids": [265598],
  "currency": "USD",
  "days": 3
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| rc | int | Client Portal use only |
| nd | int | Client Portal use only |
| rpnl | object | Returns the object containing the realized PnL for the contract. See nested table below |
| currency | string | Returns the currency the account is traded in |
| from | int | Returns the epoch time for the start of requests |
| id | string | Returns the request identifier (getTransactions) |
| to | int | Returns the epoch time for the end of requests |
| includesRealTime | bool | Returns if the trades are up to date or not |
| transactions | array\<Transaction\> | Lists all transaction records. See nested table below |

**`rpnl` object:**

| Field | Type | Description |
|-------|------|-------------|
| data | array\<RealizedPnlEntry\> | Returns an array of realized PnL objects. See nested table below |
| amt | string | Provides the total amount gained or lost from all days returned |

**`RealizedPnlEntry` object:**

| Field | Type | Description |
|-------|------|-------------|
| date | string | Specifies the date for the transaction (YYYYMMDD) |
| cur | string | Specifies the currency of the realized value |
| fxRate | int | Returns the foreign exchange rate |
| side | string | Determines if the day was a loss or gain. One of: `L` (Loss), `G` (Gain) |
| acctid | string | Returns the account ID the trade transacted on |
| amt | string | Returns the amount gained or lost on the day |
| conid | string | Returns the contract ID of the transaction |

**`Transaction` object:**

| Field | Type | Description |
|-------|------|-------------|
| date | string | Returns the human-readable datetime of the transaction. Format: "{Day} {Mon} {DD} 00:00:00 {TZ} {YYYY}" |
| cur | string | Returns the currency of the traded instrument |
| fxRate | int | Returns the forex conversion rate |
| pr | float | Returns the price per share of the transaction |
| qty | int | Returns the total quantity traded. Negative for sell orders, positive for buy orders |
| acctid | string | Returns the account which made the transaction |
| amt | float | Returns the total value of the trade |
| conid | int | Returns the contract identifier |
| type | string | Returns the order side |
| desc | string | Returns the long name for the company |

#### Example Response

```json
{
  "rc": 0,
  "nd": 4,
  "rpnl": {
    "data": [
      {
        "date": "20231211",
        "cur": "USD",
        "fxRate": 1,
        "side": "L",
        "acctid": "U1234567",
        "amt": "12.2516",
        "conid": "265598"
      }
    ],
    "amt": "12.2516"
  },
  "currency": "USD",
  "from": 1702270800000,
  "id": "getTransactions",
  "to": 1702530000000,
  "includesRealTime": true,
  "transactions": [
    {
      "date": "Mon Dec 11 00:00:00 EST 2023",
      "cur": "USD",
      "fxRate": 1,
      "pr": 192.26,
      "qty": -5,
      "acctid": "U1234567",
      "amt": 961.3,
      "conid": 265598,
      "type": "Sell",
      "desc": "Apple Inc"
    }
  ]
}
```

## Scanner

Endpoints for running market scanners to search for contracts matching specific criteria, with configurable instrument types, locations, scan types, and filters.

---

[↑ Back to Table of Contents](#table-of-contents)


### Iserver Scanner Parameters

Returns an object containing all available parameters to be sent for the Iserver scanner request.

- **Method:** `GET`
- **URL:** `/iserver/scanner/params`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| scan_type_list | array\<ScanType\> | Contains all values used as the scanner "type" in the request. See nested table below |
| instrument_list | array\<Instrument\> | Contains all values relevant to the scanner "instrument" request field. See nested table below |
| filter_list | array\<Filter\> | Contains all available filters. See nested table below |
| location_tree | array\<LocationTree\> | Contains all values relevant to the location field of the market scanner request. See nested table below |

**`ScanType` object:**

| Field | Type | Description |
|-------|------|-------------|
| display_name | string | Human readable name for the scanner "type" |
| code | string | Value used for the market scanner request |
| instruments | array\<string\> | Returns all instruments the scanner type can be used with |

**`Instrument` object:**

| Field | Type | Description |
|-------|------|-------------|
| display_name | string | Human readable representation of the instrument type |
| type | string | Value used for the market scanner request |
| filters | array\<string\> | Returns an array of all filters uniquely available to that instrument type |

**`Filter` object:**

| Field | Type | Description |
|-------|------|-------------|
| group | string | Returns the group of filters the request is affiliated with |
| display_name | string | Returns the human-readable identifier for the filter |
| code | string | Value used for the market scanner request |
| type | string | Returns the type of value to be used in the request. Can indicate a range based value, or a single value |

**`LocationTree` object:**

| Field | Type | Description |
|-------|------|-------------|
| display_name | string | Returns the overarching instrument type to designate the location |
| type | string | Returns the code value of the market scanner instrument type value |
| locations | array\<Location\> | Nested locations. See nested table below |

**`Location` object:**

| Field | Type | Description |
|-------|------|-------------|
| display_name | string | Returns the human-readable value of the market scanner's location value |
| type | string | Returns the code value of the market scanner location value |
| locations | array | Always returns an empty array at this depth |

#### Example Response

```json
{
  "scan_type_list": [
    { "display_name": "Top % Gainers", "code": "TOP_PERC_GAIN", "instruments": ["STK", "ETF"] }
  ],
  "instrument_list": [
    { "display_name": "US Stocks", "type": "STK", "filters": ["priceAbove", "priceBelow"] }
  ],
  "filter_list": [
    { "group": "price", "display_name": "Price Above", "code": "priceAbove", "type": "value" }
  ],
  "location_tree": [
    {
      "display_name": "US Stocks",
      "type": "STK",
      "locations": [
        { "display_name": "US Major", "type": "STK.US.MAJOR", "locations": [] }
      ]
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Iserver Market Scanner

Searches for contracts according to the filters specified in /iserver/scanner/params endpoint. Users can receive a maximum of 50 contracts from 1 request.

- **Method:** `POST`
- **URL:** `/iserver/scanner/run`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| instrument | string | yes | Instrument type as the target of the market scanner request. Found in the "instrument_list" section of the /iserver/scanner/params response |
| type | string | yes | Scanner value the market scanner is sorted by. Based on the "scan_type_list" section of the /iserver/scanner/params response |
| location | string | yes | Location value the market scanner is searching through. Based on the "location_tree" section of the /iserver/scanner/params response |
| filter | array\<ScannerFilter\> | no | Contains any additional filters that should apply to the response (assumed optional) |

**`ScannerFilter` object:**

| Field | Type | Description |
|-------|------|-------------|
| code | string | Code value of the filter. Based on the "code" value within the "filter_list" section of the /iserver/scanner/params response |
| value | int | Value corresponding to the input for "code" |

#### Example Request

```json
{
  "instrument": "STK",
  "location": "STK.US.MAJOR",
  "type": "TOP_TRADE_COUNT",
  "filter": [
    { "code": "priceAbove", "value": 5 }
  ]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| contracts | array\<ScannerContract\> | Contains contracts related to the market scanner request. See nested table below |
| scan_data_column_name | string | Used for Client Portal (Internal use only) |

**`ScannerContract` object:**

| Field | Type | Description |
|-------|------|-------------|
| server_id | string | Contract's index in relation to the market scanner type's sorting priority |
| column_name | string | Always returned for the first contract. Used for Client Portal (Internal use only) |
| symbol | string | Returns the contract's ticker symbol |
| conidex | string | Returns the contract ID of the contract |
| con_id | int | Returns the contract ID of the contract |
| available_chart_periods | string | Used for Client Portal (Internal use only) |
| company_name | string | Returns the company long name |
| scan_data | string | Returns the scan data value for the contract |
| contract_description_1 | string | For derivatives like Futures, the local symbol of the contract will be returned |
| listing_exchange | string | Returns the primary listing exchange of the contract |
| sec_type | string | Returns the security type of the contract |

#### Example Response

```json
{
  "contracts": [
    {
      "server_id": "0",
      "symbol": "AMD",
      "conidex": "4391",
      "con_id": 4391,
      "company_name": "ADVANCED MICRO DEVICES",
      "scan_data": "163.773K",
      "contract_description_1": "AMD",
      "listing_exchange": "NASDAQ.NMS",
      "sec_type": "STK"
    }
  ],
  "scan_data_column_name": "Trades"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### HMDS Market Scanner

Request a market scanner from the HMDS service. Can return a maximum of 250 contracts. Developers should first call the /hmds/auth/init endpoint prior to their request to avoid an initial 404 rejection.

- **Method:** `POST`
- **URL:** `/hmds/scanner`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| instrument | string | yes | Specify the type of instrument for the request. Found under the "instrument_list" value of the /iserver/scanner/params request |
| locations | string | yes | Specify the type of location for the request. Found under the "location_tree" value of the /iserver/scanner/params request |
| scanCode | string | yes | Specify the scanner type for the request. Found under the "scan_type_list" value of the /iserver/scanner/params request |
| secType | string | yes | Specify the type of security type for the request. Found under the "location_tree" value of the /iserver/scanner/params request |
| delayedLocations | string | no | Internal use only (assumed optional) |
| maxItems | int | no | Specify how many items should be returned. Default and maximum set to 250 (assumed optional) |
| filters | array\<object\> | conditional | Array of objects containing all filters upon the scanner request. Content contains a series of key:value pairs. While "filters" must be specified in the body, no content in the array needs to be passed |

#### Example Request

```json
{
  "instrument": "BOND",
  "locations": "BOND.US",
  "scanCode": "HIGH_BOND_ASK_YIELD_ALL",
  "secType": "BOND",
  "delayedLocations": "SMART",
  "maxItems": 25,
  "filters": [
    { "bondAskYieldBelow": 15.819 }
  ]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| total | string | Total number of matching contracts |
| size | string | Number of contracts returned in this response |
| offset | string | Offset for pagination |
| scanTime | string | UTC time at which the scan was performed |
| id | string | Scanner identifier |
| position | string | Position cursor for pagination |
| Contracts | object | Contains the Contract array. See nested table below |

**`Contracts.Contract` array items:**

| Field | Type | Description |
|-------|------|-------------|
| inScanTime | string | Returns the time at which the contract was scanned. Always returned in UTC time as a string |
| contractID | string | Returns the contract identifier of the scanned contract |

#### Example Response

```json
{
  "total": "17262",
  "size": "250",
  "offset": "0",
  "scanTime": "20231214-18:55:25",
  "id": "scanner1",
  "position": "v1:AAAAAQABG3gAAAAAAAAA+g==",
  "Contracts": {
    "Contract": [
      {
        "inScanTime": "20231214-18:55:25",
        "contractID": "431424315"
      }
    ]
  }
}
```

## Session

Requests used to designate changes to the web session itself rather than endpoints relating to trades or account data directly.

---

[↑ Back to Table of Contents](#table-of-contents)


### Authentication Status

Current Authentication status to the Brokerage system. Market Data and Trading is not possible if not authenticated, e.g. authenticated shows false.

- **Method:** `POST`
- **URL:** `/iserver/auth/status`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| authenticated | bool | Returns whether your brokerage session is authenticated or not |
| competing | bool | Returns whether you have a competing brokerage session in another connection |
| connected | bool | Returns whether you are connected to the gateway, authenticated or not |
| message | string | If there is a message about your authenticate status, it will be returned here. Authenticated sessions return an empty string |
| MAC | string | IBKR MAC information. Internal use only |
| serverInfo | object | Contains serverName and serverVersion. Internal use only |
| hardware_info | string | IBKR version information. Internal use only |
| fail | string | Returns the reason for failing to retrieve authentication status |

#### Example Response

```json
{
  "authenticated": true,
  "competing": false,
  "connected": true,
  "message": "",
  "MAC": "12:B:B3:23:BF:A0",
  "serverInfo": {
    "serverName": "JifN19053",
    "serverVersion": "Build 10.25.0p, Dec 5, 2023 5:48:12 PM"
  },
  "hardware_info": "3b0679ee|98:A2:B3:23:BC:A0",
  "fail": ""
}
```

#### Alternate Response (Logged Out)

Users that have been timed out or logged out of their session will result in a "false" authentication status, indicating the user is not maintaining a brokerage session.

```json
{
  "authenticated": false,
  "competing": false,
  "connected": false,
  "MAC": "98:B2:C3:45:DE:F6"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Initialize Brokerage Session

This is essential for using all endpoints besides /portfolio, including access to trading and market data.

- **Method:** `POST`
- **URL:** `/iserver/auth/ssodh/init`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| publish | bool | yes | Determines if the request should be sent immediately. Users should always pass true. Otherwise, a 500 response will be returned |
| compete | bool | yes | Determines if other brokerage sessions should be disconnected to prioritize this connection |

#### Example Request

```json
{
  "publish": true,
  "compete": true
}
```

#### Response Body

Same structure as Authentication Status response — returns `authenticated`, `competing`, `connected`, `message`, `MAC`, and `serverInfo`.

#### Example Response

```json
{
  "authenticated": true,
  "competing": false,
  "connected": true,
  "message": "",
  "MAC": "98:F2:B3:23:BF:A0",
  "serverInfo": {
    "serverName": "JifN19053",
    "serverVersion": "Build 10.25.0p, Dec 5, 2023 5:48:12 PM"
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Logout of the current session

Logs the user out of the gateway session. Any further activity requires re-authentication.

- **Method:** `POST`
- **URL:** `/logout`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| status | bool | Returns true if the session was ended |

#### Example Response

```json
{
  "status": true
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Ping the server

If the gateway has not received any requests for several minutes an open session will automatically timeout. The tickle endpoint pings the server to prevent the session from ending. It is expected to call this endpoint approximately every 60 seconds to maintain the connection to the brokerage session.

- **Method:** `POST`
- **URL:** `/tickle`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| session | string | Returns the session identifier of your connection. Can be used for the cookie parameter of your request |
| ssoExpires | int | Displays the time until session expiry in milliseconds |
| collission | bool | Internal use only |
| userId | int | Internal use only |
| hmds | object | Returns any potential historical data-specific information. "No bridge" indicates historical data is not being currently requested |
| iserver | object | Returns the content of the /iserver/auth/status endpoint |

#### Example Response

```json
{
  "session": "bb665d0f55b6289d70bc7380089fc96f",
  "ssoExpires": 460311,
  "collission": false,
  "userId": 123456789,
  "hmds": {
    "error": "no bridge"
  },
  "iserver": {
    "authStatus": {
      "authenticated": true,
      "competing": false,
      "connected": true,
      "message": "",
      "MAC": "98:F2:B3:23:BF:A0",
      "serverInfo": {
        "serverName": "JifN19053",
        "serverVersion": "Build 10.25.0p, Dec 5, 2023 5:48:12 PM"
      }
    }
  }
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Re-authenticate the Brokerage Session (Deprecated)

When using the CP Gateway, this endpoint provides a way to reauthenticate to the Brokerage system as long as there is a valid brokerage session. All interest in reauthenticating the gateway session should be handled using the /iserver/auth/ssodh/init endpoint.

- **Method:** `POST`
- **URL:** `/iserver/reauthenticate`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| message | string | Returns "triggered" to indicate the response was sent |

#### Example Response

```json
{
  "message": "triggered"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Validate SSO

Validates the current session for the SSO user. This endpoint is only valid for Client Portal Gateway and OAuth 2.0 clients.

- **Method:** `GET`
- **URL:** `/sso/validate`

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| USER_ID | int | Internal user identifier |
| USER_NAME | string | Current username logged in for the session |
| RESULT | bool | Confirms if validation was successful. true if session was validated; false if not |
| AUTH_TIME | int | Returns the time of authentication in epoch time |
| SF_ENABLED | bool | Internal use only |
| IS_FREE_TRIAL | bool | Returns if the account is a trial account or a funded account |
| CREDENTIAL | string | Returns the underlying username of the account |
| IP | string | Internal use only. Does not reflect the IP address of the user |
| EXPIRES | int | Returns the time until expiration in milliseconds |
| QUALIFIED_FOR_MOBILE_AUTH | bool | Returns if the customer requires two factor authentication |
| LANDING_APP | string | Used for Client Portal (Internal use only) |
| IS_MASTER | bool | Returns whether the account is a master account (true) or subaccount (false) |
| lastAccessed | int | Returns the last time the user was accessed in epoch time |
| LOGIN_TYPE | int | Returns the login type. 1 for Live, 2 for Paper |
| PAPER_USER_NAME | string | Returns the paper username for the account |
| features | object | Returns supported features such as bonds and option trading |
| region | string | Returns the server region |

#### Example Response

```json
{
  "USER_ID": 123456789,
  "USER_NAME": "user1234",
  "RESULT": true,
  "AUTH_TIME": 1702580846836,
  "SF_ENABLED": false,
  "IS_FREE_TRIAL": false,
  "CREDENTIAL": "user1234",
  "IP": "12.345.678.901",
  "EXPIRES": 415890,
  "QUALIFIED_FOR_MOBILE_AUTH": null,
  "LANDING_APP": "UNIVERSAL",
  "IS_MASTER": false,
  "lastAccessed": 1702581069652,
  "LOGIN_TYPE": 2,
  "PAPER_USER_NAME": "user1234",
  "features": {
    "env": "PROD",
    "wlms": true,
    "realtime": true,
    "bond": true,
    "optionChains": true,
    "calendar": true,
    "newMf": true
  },
  "region": "NJ"
}
```

## Watchlists

Manage watchlists that are used in both Trader Workstation and Client Portal. Can also be used to maintain lists within the Client Portal API.

---

[↑ Back to Table of Contents](#table-of-contents)


### Create a Watchlist

Create a watchlist to monitor a series of contracts.

- **Method:** `POST`
- **URL:** `/iserver/watchlist`

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| id | string | yes | Supply a unique identifier to track a given watchlist. Must supply a number |
| name | string | yes | Supply the human readable name of a given watchlist. Displayed in TWS and Client Portal |
| rows | array\<WatchlistRow\> | yes | Array of contract rows to add to the watchlist. See nested table below |

**`WatchlistRow` object:**

| Field | Type | Description |
|-------|------|-------------|
| C | int | Provide the conid, or contract identifier, of the contract to add |
| H | string | Can be used to add a blank row between contracts in the watchlist (pass empty string) |

#### Example Request

```json
{
  "id": "1234",
  "name": "Test Watchlist",
  "rows": [
    { "C": 8314 },
    { "C": 8894 }
  ]
}
```

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| id | string | Returns the id value used to create the watchlist |
| hash | string | Returns the internal IB hash value of the order |
| name | string | Returns the human-readable name of the watchlist |
| readOnly | bool | Determines if the watchlist is marked as write-restricted |
| instruments | array | Always returns an empty array. Conids supplied will still be in the final watchlist. See the Get Watchlist Information endpoint for the populated list |

#### Example Response

```json
{
  "id": "1234",
  "hash": "1702581306241",
  "name": "Test Watchlist",
  "readOnly": false,
  "instruments": []
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get All Watchlists

Retrieve a list of all available watchlists for the account.

- **Method:** `GET`
- **URL:** `/iserver/watchlists`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| SC | query | string | no | Specify the scope of the request. Valid Values: "USER_WATCHLIST" (assumed optional) |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| data | object | Contains all of the data about the watchlists. See nested tables below |
| action | string | Internal use only. Returns "content" |
| MID | string | Returns the number of times the endpoint was requested this session |

**`data` object:**

| Field | Type | Description |
|-------|------|-------------|
| scanners_only | bool | Shows if the system is only displaying scanners |
| system_lists | array\<WatchlistSummary\> | Returns all IB-created watchlists |
| show_scanners | bool | Returns if scanners are shown |
| bulk_delete | bool | Displays if the watchlists should be deleted |
| user_lists | array\<WatchlistSummary\> | Returns all of the available user-created lists |

**`WatchlistSummary` object:**

| Field | Type | Description |
|-------|------|-------------|
| is_open | bool | Internal use only |
| read_only | bool | Returns if the watchlist can be edited or not |
| name | string | Returns the human-readable name of the watchlist |
| modified | int | Returns the last modification timestamp |
| id | string | Returns the code identifier of the watchlist |
| type | string | Returns the watchlist type. Always returns "watchlist" |

#### Example Response

```json
{
  "data": {
    "scanners_only": false,
    "show_scanners": false,
    "bulk_delete": false,
    "user_lists": [
      {
        "is_open": false,
        "read_only": false,
        "name": "Test Watchlist",
        "modified": 1702581306241,
        "id": "1234",
        "type": "watchlist"
      }
    ]
  },
  "action": "content",
  "MID": "1"
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Get Watchlist Information

Request the contracts listed in a particular watchlist. The first request may only return the values C, conid, and name. Subsequent requests will add additional contract information.

- **Method:** `GET`
- **URL:** `/iserver/watchlist`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| id | query | string | yes | Set equal to the watchlist ID you would like data for |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| id | string | Returns the watchlist identifier |
| hash | string | Returns the internal IB hash value |
| name | string | Returns the human-readable name of the watchlist |
| readOnly | bool | Returns if the watchlist is write-restricted |
| instruments | array\<WatchlistInstrument\> | Array of contracts in the watchlist. See nested table below |

**`WatchlistInstrument` object:**

| Field | Type | Description |
|-------|------|-------------|
| ST | string | Returns the security type of the contract |
| C | string | Returns the contract ID |
| conid | int | Returns the contract ID |
| name | string | Returns the long name of the company |
| fullName | string | Returns the local symbol of the contract |
| assetClass | string | Returns the security type of the contract |
| ticker | string | Returns the ticker symbol for the contract |
| chineseName | string | Returns the Chinese character name for the contract |

#### Example Response

```json
{
  "id": "1234",
  "hash": "1702581306241",
  "name": "Test Watchlist",
  "readOnly": false,
  "instruments": [
    {
      "ST": "STK",
      "C": "8314",
      "conid": 8314,
      "name": "INTL BUSINESS MACHINES CORP",
      "fullName": "IBM",
      "assetClass": "STK",
      "ticker": "IBM",
      "chineseName": "国际商业机器"
    },
    {
      "ST": "STK",
      "C": "8894",
      "conid": 8894,
      "name": "COCA-COLA CO/THE",
      "fullName": "KO",
      "assetClass": "STK",
      "ticker": "KO",
      "chineseName": "可口可乐"
    }
  ]
}
```

---

[↑ Back to Table of Contents](#table-of-contents)


### Delete a Watchlist

Permanently delete a specific watchlist for all platforms.

- **Method:** `DELETE`
- **URL:** `/iserver/watchlist`

#### Parameters

| Name | Location | Type | Required | Description |
|------|----------|------|----------|-------------|
| id | query | string | yes | Include the watchlist ID you wish to delete |

#### Response Body

| Field | Type | Description |
|-------|------|-------------|
| data | object | Returns the data about the deleted watchlist. Contains `deleted` (string) — the ID for the deleted watchlist |
| action | string | Always returns "context" |
| MID | string | Returns the id for the number of times /iserver/watchlist was called this session |

#### Example Response

```json
{
  "data": {
    "deleted": "1234"
  },
  "action": "context",
  "MID": "2"
}
```
