# Order Monitor + Submit Examples — Design

**Date:** 2026-07-02
**Status:** Approved (pending spec review)
**Milestone:** Milestone 7 — Production Readiness (7.1 Samples project)

## 1. Goal

Add **two** console examples that together demonstrate the order lifecycle over
IbkrConduit end-to-end:

- **`IbkrConduit.Examples.OrderMonitor`** — a terminal-UI app that streams the
  account's **order-status** (`OrderUpdatesAsync`, IBKR `sor`) and
  **trade-execution** (`TradeExecutionsAsync`, IBKR `str`) streams and renders
  them live, mirroring the existing `MarketDataStream` app's structure and
  Spectre.Console `Live` UI.
- **`IbkrConduit.Examples.OrderSubmit`** — a one-liner CLI that submits a single
  order to the paper account with details passed as arguments.

**The pair as a demo:** launch `OrderMonitor` in one terminal, fire orders with
`OrderSubmit` from another, and watch each order appear/update in the Orders
table and its fill land in the Executions table. Both apps read the **same**
`.ibkr-credentials/ibkr-credentials.json`. `OrderSubmit` stamps every order with
a customer order id (cOID) that echoes back as `order_ref` on both streams, so
each submitted order is immediately identifiable in the monitor.

This replaces reliance on the older `SubmitAndMonitorOrders.cs` single-file
sample for this workflow.

## 2. Non-goals (YAGNI)

- **Monitor:** no market data, P&L, account-summary, or ledger streams — orders
  + executions only. Read-only; it never submits.
- **Submit:** US stocks only (STK); single orders only — no brackets/OCA, no
  forex/options/futures; order types limited to **MKT** and **LMT** (stop orders
  are an easy future add). No modify/cancel — placement only.
- No shared `Examples.Common` project. `PanelLogBuffer` / `FileLogger` are copied
  into `OrderMonitor` from `MarketDataStream` to keep it self-contained;
  factoring shared helpers out is deferred until a third consumer appears.
- No cross-table join UI in the monitor; correlation is visual via `order_ref` /
  `conid` / `symbol`.

## 3. Streaming API used (OrderMonitor)

Both from `IStreamingOperations` (implemented, Milestone 5):

```csharp
Task<IIbkrSubscription<OrderUpdate>> OrderUpdatesAsync(
    int? days = null, CancellationToken ct = default);

Task<IIbkrSubscription<TradeExecution>> TradeExecutionsAsync(
    bool? realtimeUpdatesOnly = null, int? days = null, CancellationToken ct = default);
```

- **Configure-then-connect:** both subscriptions are created *before*
  `ConnectAsync`, per the `ConnectAsync` contract.
- **Executions replay & dedupe:** on subscribe and after any reconnect IBKR
  replays historical executions up to `days` unless `realtimeUpdatesOnly` is
  true. Consumers **must dedupe on `TradeExecution.ExecutionId`** — the
  executions table does this.
- **Orders replay:** `OrderUpdatesAsync(days)` includes order history per `days`.
  Exact `sor` replay behavior of `days` is confirmed during live validation (§9).

### Relevant model fields

`OrderUpdate`: `OrderId`, `Conid`, `Symbol`, `Side`, `Size`, `OrderType`,
`Price?`, `Status`, `FilledQuantity`, `RemainingQuantity`, `OrderRef?`.

`TradeExecution`: `ExecutionId` (dedupe key), `Symbol`, `Side`, `Size`, `Price`,
`Exchange?`, `TradeTime?` (`YYYYMMDD-HH:mm:ss`), `TradeTimeR?` (epoch ms),
`OrderRef?`, `Conid`, `NetAmount`, `Account`, `SecType?`. Note: `TradeExecution`
carries `order_ref` but **not** `orderId`.

## 4. OrderMonitor architecture (mirrors `MarketDataStream`)

| File | Responsibility |
|---|---|
| `Program.cs` | CLI parsing, DI bootstrap (`AddIbkrClient`), credential load, Ctrl+C / `--duration` cancellation, banner, top-level error handling + exit codes. |
| `OrderMonitorHost.cs` | Coordinator (analogous to `StreamHost`): create both subscriptions, wire an `ActionObserver<T>` per stream into the table states, `ConnectAsync`, run the `Live` render loop, dispose subscriptions/handles on shutdown. |
| `LiveOrderTable.cs` | Order table state keyed by `OrderId` (`ConcurrentDictionary<string, RowState>`); update-in-place. |
| `LiveExecutionTable.cs` | Execution table state: append log, dedupe on `ExecutionId`, newest-first, capped to the most-recent N displayed rows with a running total of all seen. |
| `PanelLogBuffer.cs` / `FileLogger.cs` | Copied verbatim from `MarketDataStream` (Logs panel + optional `--log-file`). |
| `README.md` | Usage, prerequisites, what-it-shows, what-it-demonstrates. |
| `IbkrConduit.Examples.OrderMonitor.csproj` | Same package refs as `MarketDataStream`; `ProjectReference` to `IbkrConduit`; `InternalsVisibleTo` the shared test project. Added to `IbkrConduit.slnx`. |

The `ActionObserver<T>` adapter (Action + logger + label → `IObserver<T>`,
`OnError` logs at Warning, `OnCompleted` no-op) follows the `MarketDataStream`
pattern.

### Data flow

1. Load creds → `BuildServiceProvider` → `IIbkrClient`.
2. `orders = await Streaming.OrderUpdatesAsync(ordersDays, ct)`
3. `execs  = await Streaming.TradeExecutionsAsync(realtimeUpdatesOnly, execDays, ct)`
4. Subscribe an `ActionObserver` on each; fold events into `LiveOrderTable.Upsert`
   / `LiveExecutionTable.Add`.
5. `await Streaming.ConnectAsync(ct)` (after both subscriptions exist).
6. `Live` loop @ 250 ms renders `Rows`:
   **status header / Orders table / Executions table / Logs panel**.
7. Ctrl+C or `--duration` → cancel → dispose subscriptions + `await
   handle.DisposeAsync()` in `finally` → WebSocket closes.

### UI (approved two-stacked-tables layout)

- **Status header:** connection dot (`IsConnected`) + last-msg freshness
  (`LastMessageReceivedAt`) + `N orders · M executions`.
- **Orders table:** `Order · Symbol · Side · Qty · Type · Price · Status ·
  Filled · OrderRef · Age`. `Age` = time since last update for that order,
  colored like market data (default <5s, yellow >5s, red >30s). Rows ordered by
  `OrderId`. **`OrderRef` column added** so orders fired by `OrderSubmit`
  correlate at a glance.
- **Executions table:** `Time · Symbol · Side · Qty · Price · Exch · OrderRef`.
  Newest-first; capped to the most-recent N rows (default 15).

### CLI

| Flag | Meaning | Default |
|---|---|---|
| *(positional)* | none — streams are account-wide | — |
| `--realtime-only` | Suppress replay (executions `realtimeUpdatesOnly=true`, orders `days` omitted). | off (history included) |
| `--days N` | Replay depth in days for both streams. | 1 |
| `--duration <ts>` | Auto-exit after `60s` / `5m` / `1h` / `00:01:30`. | run until Ctrl+C |
| `--log-file <path>` | Tee logs to a file. | off |
| `--log-level <level>` | Min level for the file provider. | Debug |
| `-h` / `--help` / `/?` | Print help, exit 0 without needing credentials. | — |

**Exit codes:** `0` success/graceful cancel · `1` runtime error · `2` bad args.

### Error handling

- Each subscription creation is wrapped in try/catch (`ex is not
  OperationCanceledException`): log a Warning to the panel buffer and continue.
- If **neither** stream subscribes → throw `InvalidOperationException` → exit 1.
- Stream `OnError` → logged at Warning via the panel buffer; the loop keeps running.
- Best-effort disposal in `finally`; disposal exceptions logged at Debug.

## 5. OrderSubmit design

Project `IbkrConduit.Examples.OrderSubmit`, binary `ibkr-conduit-submit`. Reads
the same `.ibkr-credentials/ibkr-credentials.json` as the monitor.

### Order API used

`IOrderOperations` (implemented, Milestone 3):

```csharp
Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> PlaceOrderAsync(...);
Task<Result<OneOf<OrderSubmitted, OrderConfirmationRequired>>> ReplyAsync(replyId, confirmed, ...);
Task<Result<WhatIfResponse>> WhatIfOrderAsync(...);
```

Plus `IContractOperations.SearchBySymbolAsync` (symbol→conid, STK) and
`IPortfolioOperations.GetAccountsAsync` (default account).

### CLI grammar

```
ibkr-conduit-submit <BUY|SELL> <QTY> <SYMBOL> [--market | --limit <price>] [options]
```

Side, quantity, symbol are positional. Order type defaults to `--market`;
`--limit <price>` is mutually exclusive with `--market` and sets `LMT` + `Price`.

| Flag | Meaning | Default |
|---|---|---|
| `--market` | Market order (`MKT`). | (default type) |
| `--limit <price>` | Limit order (`LMT`) at `<price>`. Exclusive with `--market`. | — |
| `--tif <DAY\|GTC\|IOC>` | Time in force. | DAY |
| `--order-ref <str>` | cOID (`CustomerOrderId`). If omitted, auto-generated (§5.1). | auto |
| `--account <id>` | Account to submit under. | first discovered |
| `--yes` | Auto-confirm IBKR order warnings (no prompt). | off (interactive) |
| `--what-if` | Preview commission/margin via `WhatIfOrderAsync`; do not submit. | off |
| `-h` / `--help` / `/?` | Print help, exit 0 without needing credentials. | — |

**Examples:**
```
ibkr-conduit-submit BUY 100 AAPL                 # market buy 100 AAPL
ibkr-conduit-submit BUY 1 QQQ --limit 500        # limit buy, won't fill (good for testing)
ibkr-conduit-submit SELL 2 SPY --tif GTC --yes   # non-interactive
ibkr-conduit-submit BUY 100 AAPL --what-if       # preview only, no submission
```

### 5.1 cOID auto-generation

If `--order-ref` is not supplied, generate a cOID of the form
`submit-<HHmmss>-<rand4>` (short hex suffix) and set it as
`OrderRequest.CustomerOrderId`. Guarantees every submitted order is correlatable
in the monitor's `OrderRef` columns and satisfies IBKR's 24-hour-unique cOID
rule. Generation happens in the runtime flow (uses clock/random), **not** in the
pure arg parser, so the parser stays deterministic and testable.

### 5.2 Flow

1. Load creds from `.ibkr-credentials/ibkr-credentials.json`.
2. Resolve account: `--account` or first from `GetAccountsAsync`.
3. Resolve symbol → conid via `SearchBySymbolAsync` (STK); error if unresolved.
4. Build `OrderRequest` (`Side`, `Quantity`, `OrderType`, `Price?`, `Tif`,
   `CustomerOrderId`).
5. If `--what-if`: `WhatIfOrderAsync`, print the preview, exit 0.
6. `PlaceOrderAsync`. On `OrderConfirmationRequired`: print the messages; if
   `--yes` auto-confirm, else prompt y/n; loop `ReplyAsync` until submitted or
   the user declines (chained confirmations supported).
7. Print `orderId`, the `order_ref` used, and status. Exit 0.

### 5.3 Error handling & exit codes

- `0` — order submitted, or `--what-if` preview printed, or `--help`.
- `1` — runtime error (credentials/network), order rejected by IBKR, or user
  declined at a confirmation prompt.
- `2` — bad CLI arguments (invalid side, non-positive qty, `--limit` without a
  price, `--market` + `--limit` together, invalid `--tif`, unknown flag).

### 5.4 Files

| File | Responsibility |
|---|---|
| `Program.cs` | Arg parsing (delegates to a pure `TryParseArgs`), DI bootstrap, creds, resolve account/symbol, cOID gen, submit + confirmation loop, output, exit codes. |
| `IbkrConduit.Examples.OrderSubmit.csproj` | `ProjectReference` to `IbkrConduit`; `Logging.Console`; `InternalsVisibleTo` the shared test project. Added to `IbkrConduit.slnx`. |
| `README.md` | Usage, examples, prerequisites, correlation-with-monitor note. |

Arg parsing lives in an `internal static TryParseArgs` returning a parsed record
(`Side`, `Quantity`, `Symbol`, `OrderType`, `Price?`, `Tif`, `OrderRef?`,
`Account?`, `Yes`, `WhatIf`) or an error string — mirroring the
`MarketDataStream` parser style and enabling unit tests without any I/O.

## 6. Shared: credentials & testing

**Credentials:** both apps use `OAuthCredentialsFactory.FromFile(
".ibkr-credentials/ibkr-credentials.json")` (consistent with `MarketDataStream`),
so a single setup drives both.

**Testing:** one shared **`tests/IbkrConduit.Examples.Tests`** project (xUnit v3 +
Shouldly, MTP runner per repo convention), built via TDD, referencing both
example projects (internals exposed via `InternalsVisibleTo`). Added to
`IbkrConduit.slnx`. Spectre rendering and network calls are not unit-tested
(covered by §9). Scope:

- **Monitor arg parser** (`OrderMonitor.Program.TryParseArgs`): defaults;
  `--realtime-only`; `--days N` valid/invalid; `--duration` forms; `--log-file`
  / `--log-level` valid/invalid; `--help`; unknown-flag / missing-value errors.
- **`LiveExecutionTable`**: dedupe on `ExecutionId` (replayed duplicate is a
  no-op); newest-first ordering; cap to N most-recent displayed; total-seen count.
- **`LiveOrderTable`**: insert on first `OrderId`; update-in-place on subsequent
  updates (status/filled/price/`OrderRef` merge); row identity stable.
- **Submit arg parser** (`OrderSubmit.Program.TryParseArgs`): valid market/limit
  parses; `--limit` price parsing; side validation (BUY/SELL, case-insensitive);
  non-positive / non-numeric qty; `--market` + `--limit` conflict; `--tif`
  validation; `--yes` / `--what-if` flags; unknown-flag / missing-value errors;
  `--help` short-circuit.

Test naming: `MethodName_Scenario_ExpectedResult`. No network, no file I/O.

## 7. Live validation (manual smoke test)

**Local-only, never in CI.** This validation is a manual run performed on a dev
machine against a **paper** account (money-safe). It uses the gitignored
`.ibkr-credentials/` directory (`.gitignore:65`), which must **never** be
committed. It is **not** an automated CI test: the example unit tests (§6) are
network-free and are the only example tests CI executes. If any automated
live-driven test is ever added, it must be gated to skip in CI via
`[EnvironmentFact("IBKR_CONSUMER_KEY")]` (unset in CI) and/or
`[Trait("Category", "Slow")]` (CI runs `--filter-not-trait "Category=Slow"`).

Steps:

1. Terminal A: run `OrderMonitor` (default, history included).
2. Terminal B: `ibkr-conduit-submit BUY 2 SPY` (market) and
   `ibkr-conduit-submit BUY 1 QQQ --limit 500` (resting limit).
3. Confirm in the monitor: each order row appears and transitions status
   (Submitted → Filled for SPY; resting for QQQ), the SPY fill appears in
   Executions, the `OrderRef` in both tables matches the cOID printed by submit,
   and `● Connected` + freshness update.
4. Re-run the monitor with `--realtime-only`: tables start empty, then only
   post-launch activity appears.
5. `ibkr-conduit-submit BUY 100 AAPL --what-if`: prints a preview, submits
   nothing (monitor shows no new order).

## 8. Structure & isolation

Each unit has one purpose and a small interface:

- `LiveOrderTable` — in: `Upsert(OrderUpdate)` + `RefreshDisplay(now)`; out:
  `Table`. No streaming/DI dependency; testable in isolation.
- `LiveExecutionTable` — in: `Add(TradeExecution)` + `RefreshDisplay(now)`; out:
  `Table` + `TotalSeen`. Owns dedupe/cap. Testable in isolation.
- `OrderMonitorHost` — the only monitor unit touching `IIbkrClient`/streaming.
- `OrderMonitor.Program` / `OrderSubmit.Program` — I/O shells: args, DI,
  credentials, cancellation, exit codes. Pure arg parsing extracted for tests.

## 9. Task breakdown (for the plan)

Independent-ish tasks, each its own branch/PR per repo workflow:

1. **OrderMonitor** app + its unit tests (parser, both tables) + README + slnx.
2. **OrderSubmit** app + its unit tests (parser) + README + slnx.
3. CI already builds solution projects; confirm both new projects and the test
   project are picked up (they are in `IbkrConduit.slnx`).

The shared `tests/IbkrConduit.Examples.Tests` project is created in task 1 and
extended in task 2.
