# IbkrConduit

[![CI](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml/badge.svg)](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/IbkrConduit)](https://www.nuget.org/packages/IbkrConduit)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A C#/.NET client library for the Interactive Brokers Client Portal Web API (CPAPI 1.0) with OAuth 1.0a authentication, automatic session management, rate limiting, and Flex Web Service integration.

## Disclaimer

**IbkrConduit is an independent, community-developed open source library. It is not affiliated with, endorsed by, or supported by Interactive Brokers LLC or any of its subsidiaries.** "Interactive Brokers," "IBKR," "TWS," "Client Portal," and all related product and service names are trademarks or registered trademarks of Interactive Brokers LLC. Their use herein is for identification purposes only and does not imply endorsement.

**Not a financial service.** The author and contributors of IbkrConduit are not broker-dealers, registered investment advisors, or financial service providers. This library does not provide investment advice, trading recommendations, or portfolio management services of any kind.

**Trading risk.** Financial trading, including trading in securities, options, futures, and event contracts, involves substantial risk of loss and is not suitable for all investors. You may lose more than your initial investment. Past performance of any trading system, methodology, or strategy is not indicative of future results.

**No warranty on financial data or execution.** IbkrConduit transmits data to and from the Interactive Brokers API without independent verification. The author and contributors make no representations or warranties regarding the accuracy, completeness, or timeliness of market data, account information, order execution, or any other financial data passing through this library. The software is provided "as is" under the terms of the [MIT License](LICENSE).

**Limitation of liability.** In no event shall the author or contributors be liable for any direct, indirect, incidental, special, consequential, or exemplary damages arising from the use of this software, including but not limited to trading losses, missed opportunities, data inaccuracies, or system failures, regardless of the cause of action or the theory of liability.

**Your responsibility.** You are solely responsible for evaluating the suitability of this software for your purposes, for all trading decisions made using this software, and for compliance with all applicable laws and regulations. Consult a qualified financial professional before making investment decisions. Always test thoroughly with a paper trading account before connecting to a live account.

## Features

- **OAuth 1.0a authentication** — fully headless, no browser or Selenium required
- **Automatic session lifecycle** — token refresh, tickle keepalive, brokerage session init
- **Result-based error handling** — `Result<T>` pattern with `IbkrError` hierarchy for pattern matching; optional exception mode via `ThrowOnApiError`
- **Rate limiting** — global and per-endpoint token bucket limiters
- **Order management** — place, modify, cancel with automatic question/reply confirmation flow
- **Portfolio and positions** — accounts, positions, summary, ledger, allocation, performance
- **Market data** — snapshots with automatic pre-flight handling, historical bars, scanners
- **WebSocket streaming** — real-time market data, order updates, P&L, account summary
- **Flex Web Service** — trade confirmations and activity statements via XML queries
- **Contract lookup** — symbol search, security definitions, trading rules, option chains
- **Alerts** — create, list, activate, delete MTA alerts
- **Watchlists** — create, list, get, delete
- **Event contracts** — ForecastEx prediction market browsing
- **FYI notifications** — settings, delivery options, disclaimers
- **Storage-agnostic credentials** — accepts pre-loaded RSA keys and strings, not file paths

## Installation

```bash
dotnet add package IbkrConduit
```

Requires .NET 8.0 or later.

## Quick Start

### 1. Register the client

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();

// Load credentials from environment variables
using var creds = OAuthCredentialsFactory.FromEnvironment();

services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    // opts.FlexToken = "your-flex-token";       // Optional: for Flex Web Service
    // opts.ThrowOnApiError = true;              // Optional: throw instead of Result.Failure
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();
```

### 2. Validate credentials at startup

```csharp
// Fail fast if credentials are invalid
await client.ValidateConnectionAsync();
```

### 3. Use the API

Every operation returns `Result<T>` — inspect success/failure without exceptions:

```csharp
// Get portfolio accounts
var result = await client.Portfolio.GetAccountsAsync();
if (result.IsSuccess)
{
    foreach (var account in result.Value)
    {
        Console.WriteLine($"{account.Id}: {account.AccountTitle}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error.Message}");
}
```

Pattern match on error types:

```csharp
var result = await client.Orders.PlaceOrderAsync(accountId, order);
if (!result.IsSuccess)
{
    switch (result.Error)
    {
        case IbkrRateLimitError { RetryAfter: var delay }:
            await Task.Delay(delay ?? TimeSpan.FromSeconds(5));
            break;
        case IbkrOrderRejectedError { RejectionMessage: var msg }:
            logger.LogWarning("Order rejected: {Message}", msg);
            break;
        case IbkrApiError e:
            logger.LogError("API error {Status}: {Message}", e.StatusCode, e.Message);
            break;
    }
}
```

Or use functional helpers:

```csharp
var accounts = (await client.Portfolio.GetAccountsAsync())
    .Match(
        accounts => accounts,
        error => throw new Exception($"Failed: {error.Message}"));
```

### Order placement with confirmation flow

```csharp
var order = new OrderRequest
{
    Conid = 265598,          // AAPL
    Side = "BUY",
    Quantity = 10,
    OrderType = "LMT",
    Price = 150.00m,
    Tif = "GTC",
};

var result = await client.Orders.PlaceOrderAsync(accountId, order);
if (result.IsSuccess)
{
    result.Value.Switch(
        submitted => Console.WriteLine($"Order placed: {submitted.OrderId}"),
        confirmation =>
        {
            Console.WriteLine($"Confirmation required: {string.Join(", ", confirmation.Messages)}");
            // Confirm the order
            var reply = await client.Orders.ReplyAsync(confirmation.ReplyId, true);
        });
}
```

### Market data

```csharp
// Snapshot (handles pre-flight automatically)
var snapshot = await client.MarketData.GetSnapshotAsync(
    new[] { 265598 },           // conids
    new[] { "31", "84", "86" }  // last, bid, ask
);

// Historical bars
var history = await client.MarketData.GetHistoryAsync(265598, "1d", "5min");
```

### Flex Web Service

Strongly-typed methods for common report types, plus a generic escape hatch for any query:

```csharp
// Configure query IDs at startup (from IBKR portal → Reports → Flex Queries)
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.FlexToken = "your-flex-token";
    opts.FlexQueries.CashTransactionsQueryId = "1464458";
    opts.FlexQueries.TradeConfirmationsQueryId = "1454602";
});

// Cash transactions (uses the query template's configured period)
var cashResult = await client.Flex.GetCashTransactionsAsync();
foreach (var tx in cashResult.Value.CashTransactions)
{
    Console.WriteLine($"{tx.SettleDate} {tx.Type} {tx.Amount:C} {tx.Description}");
}

// Trade confirmations (with DateOnly date range)
var tradesResult = await client.Flex.GetTradeConfirmationsAsync(
    new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9));
foreach (var tc in tradesResult.Value.TradeConfirmations)
{
    Console.WriteLine($"{tc.TradeDate} {tc.BuySell} {tc.Quantity} {tc.Symbol} @ {tc.Price}");
}

// Generic: any Flex query by ID
var generic = await client.Flex.ExecuteQueryAsync("your-query-id");
Console.WriteLine($"Query: {generic.Value.QueryName} ({generic.Value.QueryType})");
```

> **Contributors:** To add support for additional Flex report types, see [docs/flex-report-types.md](docs/flex-report-types.md).

## Environment Variables

When using `OAuthCredentialsFactory.FromEnvironment()`:

| Variable | Required | Description |
|----------|----------|-------------|
| `IBKR_CONSUMER_KEY` | Yes | OAuth consumer key from IBKR self-service portal |
| `IBKR_ACCESS_TOKEN` | Yes | OAuth access token |
| `IBKR_ACCESS_TOKEN_SECRET` | Yes | OAuth access token secret |
| `IBKR_SIGNATURE_KEY` | Yes | Base64-encoded PEM private signing key |
| `IBKR_ENCRYPTION_KEY` | Yes | Base64-encoded PEM private encryption key |
| `IBKR_DH_PRIME` | Yes | Hex-encoded Diffie-Hellman prime |
| `IBKR_TENANT_ID` | No | Tenant identifier (defaults to consumer key) |

You can also construct `IbkrOAuthCredentials` directly with pre-loaded `RSA` keys and a `BigInteger` DH prime — no environment variables required.

## Credential Setup

IbkrConduit includes a setup tool that generates all required OAuth 1.0a cryptographic keys, walks you through the IBKR portal configuration, and produces a ready-to-use JSON credential file:

```bash
dotnet run --project tools/IbkrConduit.Setup
```

The interactive wizard handles key generation (RSA 2048-bit + DH parameters), portal upload instructions, credential collection, and connection validation. Credentials are saved to `.ibkr-credentials/ibkr-credentials.json` and can be loaded directly:

```csharp
using var creds = OAuthCredentialsFactory.FromFile(".ibkr-credentials/ibkr-credentials.json");
services.AddIbkrClient(opts => opts.Credentials = creds);
```

For production deployments, store the JSON in a secret manager and load via `OAuthCredentialsFactory.FromJson(jsonString)`.

For the full walkthrough, manual steps, and troubleshooting, see [docs/credential-setup.md](docs/credential-setup.md).

## Configuration Options

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;                                    // Required
    opts.Compete = true;                                         // Compete with existing sessions (default: true)
    opts.SuppressMessageIds = new List<string> { "o163" };       // Auto-suppress order confirmation messages
    opts.FlexToken = "your-token";                               // Enable Flex Web Service
    opts.ThrowOnApiError = false;                                // true = throw IbkrApiException instead of Result.Failure
    opts.StrictResponseValidation = false;                       // true = throw on unexpected/missing JSON fields
    opts.TickleIntervalSeconds = 60;                             // Session keepalive interval
    opts.ProactiveRefreshMargin = TimeSpan.FromHours(1);         // Re-auth before token expires
    opts.PreflightCacheDuration = TimeSpan.FromMinutes(5);       // Market data pre-flight cache TTL
});
```

## Error Handling

All facade methods return `Result<T>`. Errors are represented by the `IbkrError` hierarchy:

| Error Type | When |
|------------|------|
| `IbkrApiError` | Generic API error (non-2xx response) |
| `IbkrSessionError` | Authentication/session failure |
| `IbkrRateLimitError` | HTTP 429 from IBKR (has `RetryAfter`) |
| `IbkrOrderRejectedError` | Order rejected via 200 OK with error body |
| `IbkrHiddenError` | Non-order 200 OK with `{"error":"..."}` or `{"success":false}` |
| `IbkrFlexError` | Flex Web Service error (has `ErrorCode`, `IsRetryable`, `CodeDescription`) |

For exception-based handling, either enable `ThrowOnApiError` globally or call `.EnsureSuccess()` per-call:

```csharp
// Per-call
var accounts = (await client.Portfolio.GetAccountsAsync()).EnsureSuccess().Value;

// Global
opts.ThrowOnApiError = true;
// Now all calls throw IbkrApiException on failure
```

Catch and pattern match:

```csharp
try
{
    var accounts = (await client.Portfolio.GetAccountsAsync()).Value;
}
catch (IbkrApiException ex) when (ex.Error is IbkrRateLimitError rle)
{
    await Task.Delay(rle.RetryAfter ?? TimeSpan.FromSeconds(5));
}
```

## Observability

IbkrConduit includes built-in tracing (`System.Diagnostics.ActivitySource`), metrics (`System.Diagnostics.Metrics`), and structured logging (`ILogger` with `[LoggerMessage]` source generation). No additional NuGet dependencies are required.

```csharp
// Subscribe to traces and metrics
services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("IbkrConduit"))
    .WithMetrics(b => b.AddMeter("IbkrConduit"));

// Enable request/response audit logging (Debug level, raw HTTP bodies)
services.AddLogging(b => b
    .AddConsole()
    .SetMinimumLevel(LogLevel.Warning)
    .AddFilter("IbkrConduit.Http.AuditLogHandler", LogLevel.Debug));
```

The audit log handler captures raw request and response bodies at Debug level — useful for debugging deserialization issues, unexpected API responses, or verifying what was actually sent to IBKR. Bodies are truncated at 4 KB and Authorization headers are never logged.

For the full list of spans, metrics, and log categories, see [docs/observability.md](docs/observability.md).

## IbkrConduit vs IBKR.Sdk.Client

| Aspect | IbkrConduit | IBKR.Sdk.Client |
|--------|-------------|-----------------|
| API target | Web API 1.0 (CPAPI 1.0) | Web API 2.0 (newer, beta) |
| Auth method | OAuth 1.0a (first-party self-service) | OAuth 2.0 (private_key_jwt) |
| Status | Targets stable, fully documented API | Targets API still in beta |
| Session management | Automatic lifecycle (tickle, refresh, init) | Manual |
| Error handling | Result\<T\> + IbkrError pattern matching | Exceptions |
| Flex Web Service | Included | Not included |
| Open source | MIT licensed, community driven | Unclear governance |

## API Coverage

92 of 97 Client Portal endpoints covered with recording-validated DTOs and integration tests. See the [API Testing Status Report](docs/api-testing-status-report.md) for details.

## Documentation

- [Credential Setup Guide](docs/credential-setup.md) — generating keys, portal configuration, loading credentials
- [Design Document](docs/ibkr_conduit_design.md) — architecture, API behaviors, implementation guidance
- [API Testing Status Report](docs/api-testing-status-report.md) — endpoint coverage and validation status
- [API Specification](docs/ibkr-web-api-spec.md) — full REST API reference

## License

MIT License. See [LICENSE](LICENSE) for details.
