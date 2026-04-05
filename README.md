# IbkrConduit

[![CI](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml/badge.svg)](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/IbkrConduit)](https://www.nuget.org/packages/IbkrConduit)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A C#/.NET client library for the Interactive Brokers Client Portal Web API (CPAPI 1.0) with OAuth 1.0a authentication, multi-tenant session management, rate limiting, and Flex Web Service integration.

## Disclaimer

**IbkrConduit is an independent, community-developed open source library. It is not affiliated with, endorsed by, or supported by Interactive Brokers LLC or any of its subsidiaries.** Interactive Brokers, IBKR, and related marks are trademarks of Interactive Brokers LLC.

Financial trading involves substantial risk of loss. IbkrConduit is provided as infrastructure software only — it does not provide investment advice and is not responsible for trading decisions or financial outcomes. Use at your own risk. Always test thoroughly with a paper trading account before connecting to a live account.

## Features

- **OAuth 1.0a authentication** — fully headless, no browser or Selenium required
- **Multi-tenant session management** — multiple IBKR accounts in a single process
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

```csharp
// Requires FlexToken in options
var result = await client.Flex.ExecuteQueryAsync("123456");
foreach (var trade in result.Trades)
{
    Console.WriteLine($"{trade.Symbol}: {trade.Quantity} @ {trade.Price}");
}
```

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

## IbkrConduit vs IBKR.Sdk.Client

| Aspect | IbkrConduit | IBKR.Sdk.Client |
|--------|-------------|-----------------|
| API target | Web API 1.0 (CPAPI 1.0) | Web API 2.0 (newer, beta) |
| Auth method | OAuth 1.0a (first-party self-service) | OAuth 2.0 (private_key_jwt) |
| Status | Targets stable, fully documented API | Targets API still in beta |
| Multi-tenant | First-class design consideration | Not specifically documented |
| Error handling | Result\<T\> + IbkrError pattern matching | Exceptions |
| Flex Web Service | Included | Not included |
| Open source | MIT licensed, community driven | Unclear governance |

## API Coverage

92 of 97 Client Portal endpoints covered with recording-validated DTOs and integration tests. See the [API Testing Status Report](docs/api-testing-status-report.md) for details.

## Documentation

- [Design Document](docs/ibkr_conduit_design.md) — architecture, API behaviors, implementation guidance
- [API Testing Status Report](docs/api-testing-status-report.md) — endpoint coverage and validation status
- [API Specification](docs/ibkr-web-api-spec.md) — full REST API reference

## License

MIT License. See [LICENSE](LICENSE) for details.
