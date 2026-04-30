# Market Data Streaming Example

A console example demonstrating IbkrConduit's WebSocket streaming functionality.
Subscribes to live market data for one or more symbols (forex pairs by default,
US equities supported) and renders the ticks to the terminal as a continuously-
updating table.

## Prerequisites

- A populated `.ibkr-credentials/ibkr-credentials.json` in the **working directory
  from which you invoke `dotnet run`** (not the project directory). Run
  `ibkr-conduit-setup` if you don't have one. The repo root is the typical
  working directory for the example commands shown below.
- An IBKR account with market data permissions for the symbols you want to stream.

## Usage

Run with the default forex pairs (EUR.USD, GBP.USD, USD.JPY, AUD.USD) — these
trade 24/5 and are guaranteed to be active during normal weekday trading:

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release
```

Run with custom symbols (US stocks have no dot; forex pairs use `BASE.QUOTE`):

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release -- AAPL MSFT
```

Mix asset classes:

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release -- AAPL EUR.USD
```

Time-box a run (useful for smoke tests):

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release -- --duration 60s
```

Pass `--help` (or `-h` / `/?`) to print a usage summary and exit without
requiring a credentials file:

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release -- --help
```

Tee every log line (Debug+ from every category) to a file alongside the live UI
— useful when the live table region overwrites a warning before you can read it:

```bash
dotnet run --project examples/IbkrConduit.Examples.MarketDataStream --configuration Release -- --log-file ./debug.log
```

The console keeps its quiet `Warning+` filter; the file captures everything from
`Debug` upward with timestamps, EventIds, and category names.

Press `Ctrl+C` at any time to exit cleanly.

## What it shows

- A status header (`● Connected` / `● Disconnected` plus a "last msg Ns ago"
  freshness indicator) driven by `IStreamingOperations.IsConnected` and
  `IStreamingOperations.LastMessageReceivedAt`.
- One row per resolved symbol with Symbol, Last, Bid, Ask, Volume, % Change, Age.
- Age yellow at >5s, red at >30s.
- % Change green if positive, red if negative.

## What it demonstrates about IbkrConduit

- Standard DI bootstrap via `services.AddIbkrClient(...)`.
- Hybrid contract resolution: `SearchBySymbolAsync` for stocks, `GetCurrencyPairsAsync` for forex.
- Live subscription via `IIbkrClient.Streaming.MarketDataAsync` (returns `IObservable<MarketDataTick>`).
- Graceful disposal: cancellation propagates through the subscription chain;
  the WebSocket closes cleanly on shutdown.
