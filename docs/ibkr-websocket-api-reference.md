# IBKR Client Portal Web API — WebSocket Reference

> **Provenance:** Transcribed from Interactive Brokers' Client Portal Web API v1
> documentation (IBKR Campus, WebSockets section) on 2026-07-02. This is a
> convenience copy for offline reference; IBKR's published docs are authoritative.
> Command strings and JSON payloads are reproduced verbatim; surrounding prose is
> lightly reformatted for readability.

## Topic quick reference

| Domain | Subscribe | Unsubscribe | Target segment | Notes |
|---|---|---|---|---|
| Account summary | `ssd+{accountId}+{…}` | `usd+{accountId}+{}` | accountId (required) | |
| Account ledger | `sld+{accountId}+{…}` | `uld+{accountId}+{}` | accountId (required) | new message every 10s |
| Market data | `smd+{conid}+{…}` | `umd+{conid}+{}` | conid (required) | **stream ends after 15 min**; re-request after 10 min. Limited by Market Data Lines |
| Historical market data | `smh+{conid}+{…}` | `umh+{serverId}` | serverId (from response) | max 5 concurrent; responds once but must still unsubscribe |
| BookTrader price ladder | `sbd+{acctId}+{conid}+{exchange}` | `ubd+{acctId}` | acctId | requires L2 depth subscription |
| Live order updates | `sor+{…}` | `uor+{}` | none | |
| Profit & loss | `spl+{}` | `upl+{}` | none | |
| Trades / executions | `str+{…}` | `utr` | none | **cancel is bare `utr`, no braces** |
| Session ping | `tic` | — | — | keep-alive, ≥ once/minute |

Unsolicited topics (`act`, `sts`, `blt`, `system`, `ntf`) cannot be requested or
cancelled — IBKR pushes them automatically.

---

## Account Operations

### Subscribe Account Summary

**Topic:** `ssd` — subscribes to a stream of account summary messages for the specified account.

**Topic Target:** `accountId` (required) — the account whose account summary data will be subscribed.

**Parameters:**
- `keys`: Array of Strings. Pass specific account summary data keys to receive messages concerning only those keys. Passing no named keys delivers account summary messages containing values for the selected account. Example values: `"AccruedCash-S"`, `"ExcessLiquidity-S"`.
- `fields`: Array of Strings. Pass specific account summary field names to filter responses to only these fields for the requested keys. Passing no named fields delivers all available data points. Example values: `"currency"`, `"monetaryValue"`.

```
ssd+DU1234567+{
    "keys":["AccruedCash-S","ExcessLiquidity-S"],
    "fields":["currency","monetaryValue"]
}
```

**Account Summary Topic Messages** — `result` is an array of JSON objects, each corresponding to an account summary value:
- `key`: String. Name of the account summary value. Always returned.
- `timestamp`: Number (integer). When the value was retrieved. Always returned.
- `value`: String. A non-monetary value (dates, account titles, etc.).
- `monetaryValue`: Number. A monetary value; returned when the key pertains to pricing/balance.
- `currency`: String. The currency of `monetaryValue` (e.g. `"USD"`, `"EUR"`, `"HKD"`).
- `severity`: Number (integer). Internal use only.

```json
{"result":[
    {"key":"key1","currency":"currency","monetaryValue":monetaryValue,"severity":0,"timestamp":timestamp},
    {"key":"key2","currency":"currency","value":value,"severity":0,"timestamp":timestamp}
]}
```

### Unsubscribe Account Summary

**Topic:** `usd` — unsubscribes the user from account summary information for the specified account.

**Topic Target:** `accountId` (required) — the account whose account summary messages will be unsubscribed.

**Parameters:** none.

```
usd+DU1234567+{}
```

**Unsubscribe Message** (arrives once): `{"result":"unsubscribed from summary"}`

### Subscribe Account Ledger

**Topic:** `sld` — subscribes to a stream of account ledger messages for the specified account, sorted by currency.

**Topic Target:** `accountId` (required) — the account whose ledger data will be subscribed.

**Parameters:**
- `keys`: Array of Strings. Ledger currency keys (e.g. `"LedgerListEUR"`, `"LedgerListUSD"`, `"LedgerListBASE"`). No keys → all currencies.
- `fields`: Array of Strings. Ledger field names (e.g. `"cashBalance"`, `"exchangeRate"`). No fields → all data points.

```
sld+DU1234567+{
    "keys":["LedgerListBASE","LedgerListEUR"],
    "fields":["cashBalance","exchangeRate"]
}
```

**Account Ledger Topic Messages:** A new message is published every 10 seconds until `sld` is unsubscribed. A message only delivers a currency's field data when a change occurred in the preceding interval; otherwise the currency's entry is "blank" (only the currency key and a timestamp). Currency values of JSON number type include a fractional component and may include an exponential (`E`) component.

`result` is an array of objects, one per currency. Always-returned fields: `key` (e.g. `"LedgerListUSD"` / `"LedgerListBASE"`), `timestamp`. Other fields include `acctCode`, `cashbalance`, `cashBalanceFXSegment`, `commodityMarketValue`, `corporateBondsMarketValue`, `dividends`, `exchangeRate`, `funds`, `marketValue`, `optionMarketValue`, `interest`, `issueOptionsMarketValue`, `moneyFunds`, `netLiquidationValue`, `realizedPnl`, `unrealizedPnl`, `secondKey`, `settledCash`, `stockMarketValue`, `tBillsMarketValue`, `tBondsMarketValue`, `warrantsMarketValue`, `severity` (internal).

```json
{
  "result": [
    {
      "acctCode": "DU1234567",
      "cashbalance": 2.0201311791131118E8,
      "key": "LedgerListBASE",
      "exchangeRate": 1.0,
      "netLiquidationValue": 2.0280151634374067E8,
      "unrealizedPnl": 249013.5397937378,
      "secondKey": "BASE",
      "settledCash": 2.0201311791131118E8,
      "severity": 0,
      "stockMarketValue": 391710.74028015137,
      "timestamp": 1700248325
    },
    {"key": "LedgerListUSD", "timestamp": 1700248325},
    {"key": "LedgerListEUR", "timestamp": 1700248325}
  ],
  "topic": "sld+DU1234567"
}
```

### Unsubscribe Account Ledger

**Topic:** `uld` — unsubscribes from account ledger messages for the specified account.

**Topic Target:** `accountId` (required).

**Parameters:** none.

```
uld+DU1234567+{}
```

**Unsubscribe Message** (arrives once): `{"result":"unsubscribed from ledger"}`

---

## Market Data

### Market Data Request

**Topic:** `smd` — subscribes the user to watchlist market data (streaming, top-of-book, level one).

> **IMPORTANT:** Market data streams terminate after 15 minutes. Users must send a
> new request after 10 minutes to continue retrieving data.
> **NOTE:** The maximum number of market data subscriptions is based on your
> account's Market Data Lines.

**Topic Target:** `conid` (required) — a single contract identifier. Contracts use SMART routing by default; to specify an exchange use `conId@EXCHANGE`.

**Arguments:** `fields`: Array of Strings (optional) — field IDs, each passed as a string. See the Market Data Fields section.

```
smd+conId+{"fields":["field_1","field_2","field_n"]}
```

Watchlist data is derived from time-based snapshot intervals (all products: 500ms).

**Response** fields include `server_id`, `conidEx`, `conid`, `_updated` (13-char epoch), `6119` (server_id), the requested `fields`, `6509` (market data availability), and `topic` (restates `smd+conid`).

### Cancel Market Data

**Topic:** `umd` — unsubscribes the user from watchlist market data.

**Topic Target:** `conid` (required) — a single contract identifier.

**Arguments:** null.

```
umd+conId+{}
```

**Response:** No response is returned upon unsubscribing; the market data for the given conid simply ends.

### Historical Market Data Request

**Topic:** `smh` — subscribes the user to historical bar data.

> **NOTE:** Max 5 concurrent historical data requests at a time.
> **NOTE:** Historical data responds only once, but customers must still unsubscribe.

**Topic Target:** `conid` (required). **Arguments:** `exchange`, `period`, `bar`, `outsideRth`, `source`, `format` (all JSON; empty `{}` allowed).

```
smh+conid+{"exchange":"exchange","period":"period","bar":"bar","outsideRth":outsideRth,"source":"source","format":"format"}
```

Response includes `serverId` (used to cancel the stream), `symbol`, `data` (array of bars with `o`/`c`/`l`/`h`/`v`/`t`), `points`, `topic`, and various metadata.

| Parameter | Valid values |
|---|---|
| `period` | `{1-30}min`, `{1-8}h`, `{1-1000}d`, `{1-792}w`, `{1-182}m`, `{1-15}y` |
| `bar` | `1min`,`2min`,`3min`,`5min`,`10min`,`15min`,`30min`,`1h`,`2h`,`3h`,`4h`,`8h`,`1d`,`1w`,`1m` |
| `outsideRth` | `true`/`false` |
| `source` | `midpoint`, `trades`, `bid_ask`, `bid`, `ask` |
| `format` | `%o` open, `%c` close, `%h` high, `%l` low, `%v` volume |

### Cancel Historical Market Data

**Topic:** `umh` — unsubscribes the user from historical bar data.

**Arguments:** `serverId` (required) — passed initially from the historical data response.

```
umh+{serverId}
```

**Response:** No response; the stream for the given serverId ends and one of the five subscription slots frees up.

### Subscribe to BookTrader Price Ladder

**Topic:** `sbd` — subscribes to BookTrader price ladder data. Requires an L2 (Depth of Book) market data subscription.

**Topic Target:** `acctId` (required, single), `conid` (required, single), `exchange` (optional routing identifier).

```
sbd+acctId+conid+exchange
```

Response contains `topic` and `data` (array of ladder rows with `row`, `focus`, `price`, and optional `ask`/`bid`).

### Cancel Price Ladder Subscription

**Topic:** `ubd` — unsubscribes the user from price ladder data.

**Arguments:** `acctId` (required) — the account that made the request.

```
ubd+{acctId}
```

**Response:** No response; the data stream for the given acctId ends.

---

## Miscellaneous Operations

### Exercise Options

Exercising via Client Portal requires confirming details across multiple WebSocket requests. Maintain Live Order Updates while exercising to confirm results.

Initiate with a handshake passing the `exercise` argument, then pass the option's ConID to the `CEX` field:

```
shs+exercise+{"CEX":"Your_Option_Conid"}
```

This acknowledges the topic (`{"topic":"shs+exercise"}`), then returns messages with available `user_action` options (`Submit`/`Cancel`), contract/position info, and a tracking `id`. In-the-money warnings may also arrive (informational; no reply required).

Construct the exercise request via the `inp` topic with the `exercise` argument, passing `user_input` as the action and the `id` from the prior `shs+exercise` response. Set `make_final:true` to make the exercise final; `value` is the quantity to exercise:

```
inp+exercise+{"action":"user_input","data":{"id":"5","user_action":"submit","exercise":{"allowed":"not_shown","make_final":true,"value":5}}}
```

Additional confirmations/warnings arrive with a new `id`; continue with:

```
inp+exercise+{"action":"user_input","data":{"id":"7","user_action":"continue"}}
```

Once submitted, the order appears in the `sor` WebSocket with `"side":"EXER"`.

---

## Order & Position Operations

### Request Live Order Updates

**Topic:** `sor` — subscribes the user to live order updates. Query `/iserver/account/orders` for all of the current day's orders before subscribing.

**Arguments:** `filters`: Array of String — a single string indicating an exclusive Order Status value to return.

```
sor+{"filters":["Submitted"]}
```

**Response:** `topic` and `args` (array of order objects). Fields include `acct`, `conid`, `orderId`, `cashCcy`, `sizeAndFills`, `orderDesc`, `description1`, `ticker`, `secType`, `listingExchange`, `remainingQuantity`, `filledQuantity`, `companyName`, `status` (Presubmitted/Submitted/Filled/Cancelled), `origOrderType`, `supportsTaxOpt`, `lastExecutionTime`, `lastExecutionTime_r`, `order_ref` (cOID), `orderType` (MARKET/LIMIT/STOP), `side` (BUY/SELL), `timeInForce`, `price`, `bgColor`, `fgColor`.

### Cancel Live Order Updates

**Topic:** `uor` — cancels the live order updates subscription.

**Arguments:** do not pass arguments.

```
uor+{}
```

**Response:** No response; the order updates stream ends.

### Request Profit & Loss

**Topic:** `spl` — subscribes the user to live profit and loss information.

**Arguments:** do not pass arguments.

```
spl+{}
```

**Response:** `topic` and `args` (object). Per-account object `acctId.Core` contains `rowType`, `dpl` (daily P&L), `nl` (net liquidity), `upl` (unrealized P&L), `uel` (unrounded excess liquidity), `mv` (market value).

```json
{"topic":"spl","args":{"acctId.Core":{"rowType":rowType,"dpl":dpl,"nl":nl,"upl":upl,"uel":uel,"mv":mv}}}
```

### Cancel Profit & Loss

**Topic:** `upl` — cancels the subscription to profit and loss information.

**Arguments:** do not pass arguments.

```
upl+{}
```

**Response:** No response is returned.

### Request Trades Data

**Topic:** `str` — subscribes the user to trades data (all executions data while streamed).

**Arguments:**
- `realtimeUpdatesOnly`: bool (optional). Whether to display only real-time executions vs. historical too. Default `false`.
- `days`: int (optional). Number of days of executions to return. Default `1`.

```
str+{"realtimeUpdatesOnly":realtimeUpdatesOnly,"days":days}
```

**Response:** `topic` and `args` (array of execution objects). Fields include `execution_id`, `symbol`, `supports_tax_opt`, `side`, `order_description`, `trade_time` (`YYYYMMDD-HH:mm:ss` UTC), `trade_time_r` (epoch), `size`, `order_ref` (cOID), `price`, `exchange`, `net_amount`, `account`, `accountCode`, `company_name`, `contract_description_1`, `contract_description_2`, `sec_type`, `conid`, `conidEx`, `open_close`, `liquidation_trade`, `is_event_trading`.

### Cancel Trades Data

**Topic:** `utr` — cancels the trades data subscription.

```
utr
```

> Note: unlike other cancels, `utr` is sent **without** a trailing `+{}`.

**Response:** Nothing is returned upon cancellation.

---

## Session

### Maintain Session (Ping)

**Topic:** `tic` — ping the WebSocket to keep the session alive (for `/iserver` or `/ccp` endpoints). Ping at least once per minute. A `/tickle` request is still required every few minutes / when the session expires (`/sso/validate` returns `0`).

**Arguments:** do not pass arguments.

```
tic
```

---

## Unsolicited Messages

These messages cannot be directly requested — IBKR returns them automatically as events arise.

### Account Updates (`act`)

Details about the brokerage accounts the logged-in user can access. An initial message is sent when the connection is first established, with supplemental messages on account changes. `args` contains `accounts`, `acctProps`, `aliases`, `allowFeatures` (feature flags), `chartPeriods` (per asset type), `groups`, `profiles`, `selectedAccount`, `serverInfo`, `sessionId`, `isFT`, `isPaper`, etc.

```json
{"topic":"act","args":{"accounts":[],"acctProps":{"All":{...}},"selectedAccount":"selectedAccount","isPaper":isPaper}}
```

### Authentication Status (`sts`)

On initial connection, `sts` relays the current authentication status. Status updates (e.g. from competing sessions) are also relayed here.

```json
{"topic":"sts","args":{"authenticated":authenticated}}
```

### Bulletins (`blt`)

Urgent messages concerning exchange issues, system problems, and trading information, with a message and a unique identifier.

```json
{"topic":"blt","args":{"id":"id","message":"message"}}
```

### System Connection Messages (`system`)

On initial connection, `system` relays a confirmation with the corresponding username. Every 10 seconds thereafter, a heartbeat with the corresponding unix time (in milliseconds) is relayed.

```json
{"topic":"system","success":"success"}
```

### Notifications (`ntf`)

A brief message regarding trading activity. Fields in `args`:
- `id`: String. Identifier for the specific notification.
- `text`: String. Body text for the notification.
- `title`: String. Title / headline for the notification.
- `url`: String. If relevant, a URL where the user can read more.

```json
{
    "topic": "ntf",
    "args": {
        "id": "id",
        "text": "text",
        "title": "title",
        "url": "url"
    }
}
```
