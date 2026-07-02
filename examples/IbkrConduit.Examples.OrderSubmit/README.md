# Order Submit Example

A one-liner CLI that submits a single US-stock order (market or limit) to the
paper account over IbkrConduit. Pair it with the `OrderMonitor` example: run the
monitor, then fire orders with this app and watch them appear.

Each order is stamped with a customer order id (cOID) — supplied via
`--order-ref` or auto-generated — which echoes back as `order_ref` on both
streams, so every order is identifiable in the monitor.

## Prerequisites

- A populated `.ibkr-credentials/ibkr-credentials.json` in the working directory
  you invoke `dotnet run` from. Run `ibkr-conduit-setup` if you don't have one.

## Usage

```bash
# Market buy 100 AAPL
dotnet run --project examples/IbkrConduit.Examples.OrderSubmit --configuration Release -- BUY 100 AAPL

# Limit buy 1 QQQ at $500 (won't fill — useful for testing)
dotnet run --project examples/IbkrConduit.Examples.OrderSubmit --configuration Release -- BUY 1 QQQ --limit 500

# Non-interactive (auto-confirm IBKR warnings)
dotnet run --project examples/IbkrConduit.Examples.OrderSubmit --configuration Release -- SELL 2 SPY --tif GTC --yes

# Preview commission/margin without submitting
dotnet run --project examples/IbkrConduit.Examples.OrderSubmit --configuration Release -- BUY 100 AAPL --what-if
```

## Options

- `--market` (default) / `--limit <price>` — order type (mutually exclusive).
- `--tif DAY|GTC|IOC` — time in force (default DAY).
- `--order-ref <str>` — cOID (auto-generated if omitted).
- `--account <id>` — account to submit under (default: first discovered).
- `--yes` — auto-confirm IBKR order warnings.
- `--what-if` — preview only, do not submit.

## Exit codes

- `0` — submitted, or `--what-if` preview printed.
- `1` — runtime error, order rejected, or confirmation declined.
- `2` — bad CLI arguments.
