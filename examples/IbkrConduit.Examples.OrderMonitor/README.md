# Order Monitor Example

A console example that streams the account's order-status and trade-execution
streams over IbkrConduit's WebSocket and renders them live as two stacked
Spectre.Console tables (Orders update-in-place; Executions append newest-first).

Pair it with the `OrderSubmit` example: run this monitor, then submit orders
from another terminal and watch them appear.

## Prerequisites

- A populated `.ibkr-credentials/ibkr-credentials.json` in the working directory
  you invoke `dotnet run` from (the repo root is typical). Run
  `ibkr-conduit-setup` if you don't have one.

## Usage

Stream with the default 1 day of replayed history, then live:

```bash
dotnet run --project examples/IbkrConduit.Examples.OrderMonitor --configuration Release
```

Start with empty tables and show only post-launch activity:

```bash
dotnet run --project examples/IbkrConduit.Examples.OrderMonitor --configuration Release -- --realtime-only
```

Time-box a run (useful for an unattended smoke test):

```bash
dotnet run --project examples/IbkrConduit.Examples.OrderMonitor --configuration Release -- --duration 60s
```

Tee logs to a file:

```bash
dotnet run --project examples/IbkrConduit.Examples.OrderMonitor --configuration Release -- --log-file ./monitor.log
```

Press `Ctrl+C` at any time to exit cleanly.

## What it shows

- A status header (`● Connected` / `● Disconnected`, last-message freshness, and
  `N orders · M executions`).
- Orders table: Order, Symbol, Side, Qty, Type, Price, Status, Filled, OrderRef,
  Age (yellow >5s, red >30s since last update).
- Executions table: Time, Symbol, Side, Qty, Price, Exch, OrderRef — newest-first,
  deduped on execution id, capped to the most recent rows.
