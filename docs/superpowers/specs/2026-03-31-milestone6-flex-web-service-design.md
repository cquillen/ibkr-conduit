# Milestone 6 — Flex Web Service

**Date:** 2026-03-31
**Status:** Draft
**Goal:** Add Flex Web Service support for retrieving trade confirmations, activity statements, and other Flex Query reports.

---

## Scope

M6 adds the Flex Web Service as a new surface on the `IIbkrClient` facade. After M6:

1. `client.Flex.ExecuteQueryAsync(queryId)` — requests a report, polls until ready, returns parsed result
2. `client.Flex.ExecuteQueryAsync(queryId, fromDate, toDate)` — with date range override
3. `FlexQueryResult` — provides raw `XDocument` plus typed helpers for common sections (Trades, OpenPositions)
4. `IbkrClientOptions.FlexToken` — configurable Flex Web Service access token

### Deferred

- **Full typed models for all Flex sections** — only Trades and OpenPositions modeled. Consumers use `RawXml` for other sections.
- **Scheduled delivery (email/FTP)** — out of scope for the library.

---

## Architecture

### Flex Web Service is Independent from CP Web API

- **Different base URL:** `https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/`
- **Different auth:** Long-lived Flex token as query parameter (no OAuth, no signing)
- **Different format:** XML responses (not JSON)
- **No shared pipeline:** No signing handler, no rate limiting, no session management

### Component Diagram

```
client.Flex (IFlexOperations)
    │
    ▼
FlexOperations
    │
    ▼
FlexClient (internal)
    ├── HttpClient → https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/
    ├── Step 1: GET /SendRequest?t={token}&q={queryId}&v=3 → reference code
    ├── Step 2: GET /GetStatement?t={token}&q={referenceCode}&v=3 → XML report
    └── Polls with backoff until ready (1s → 2s → 5s, max 60s)
```

---

## Task 6.1 — FlexClient + Models

### IbkrClientOptions Addition

```csharp
/// <summary>
/// Flex Web Service access token. Generated in Client Portal under
/// Reporting → Flex Queries → Flex Web Configuration.
/// Required for Flex operations. If null, Flex operations will throw.
/// </summary>
public string? FlexToken { get; init; }
```

### FlexClient (internal)

**Constructor:** Takes `HttpClient`, `string flexToken`, `ILogger<FlexClient>`

**Base URL:** `https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/`

**`ExecuteQueryAsync(string queryId, string? fromDate, string? toDate, CancellationToken)`:**

1. **Step 1 — Request generation:**
   - `GET /SendRequest?t={flexToken}&q={queryId}&v=3`
   - Optional: `&fd={fromDate}&td={toDate}` (format: `yyyyMMdd`)
   - Parse XML response for `<Status>` and `<ReferenceCode>`
   - If status is not "Success", throw with error message from `<ErrorMessage>`

2. **Step 2 — Poll for results:**
   - `GET /GetStatement?t={flexToken}&q={referenceCode}&v=3`
   - If response contains `<ErrorCode>1019</ErrorCode>` ("Statement generation in progress"), wait and retry
   - Poll with backoff: 1s, 2s, 3s, 5s, 5s... up to 60s total
   - If timeout exceeded, throw `TimeoutException`
   - On success, return the `XDocument`

### SendRequest Response Model

```xml
<FlexStatementResponse timestamp="...">
    <Status>Success</Status>
    <ReferenceCode>1234567890</ReferenceCode>
    <Url>https://...</Url>
</FlexStatementResponse>
```

### Error Response Model

```xml
<FlexStatementResponse timestamp="...">
    <Status>Fail</Status>
    <ErrorCode>1234</ErrorCode>
    <ErrorMessage>Description of the error</ErrorMessage>
</FlexStatementResponse>
```

### Known Error Codes

- `1003` — Too many requests (rate limited)
- `1004` — Invalid token
- `1005` — Query ID not found
- `1019` — Statement generation in progress (retry)

### FlexQueryException

```csharp
public class FlexQueryException : Exception
{
    public int ErrorCode { get; }
    public FlexQueryException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

---

## Task 6.2 — IFlexOperations + FlexQueryResult

### IFlexOperations

```csharp
public interface IFlexOperations
{
    /// <summary>
    /// Executes a Flex Query and returns the parsed result.
    /// Handles the two-step request/poll flow internally.
    /// </summary>
    Task<FlexQueryResult> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a Flex Query with a date range override.
    /// Date format: yyyyMMdd.
    /// </summary>
    Task<FlexQueryResult> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default);
}
```

### FlexQueryResult

```csharp
public class FlexQueryResult
{
    /// <summary>The full XML document returned by the Flex Web Service.</summary>
    public XDocument RawXml { get; }

    /// <summary>
    /// Typed trade records from the Trades or TradeConfirmations section, if present.
    /// Returns empty list if the section is not in the query template.
    /// </summary>
    public IReadOnlyList<FlexTrade> Trades { get; }

    /// <summary>
    /// Typed open position records from the OpenPositions section, if present.
    /// Returns empty list if the section is not in the query template.
    /// </summary>
    public IReadOnlyList<FlexPosition> OpenPositions { get; }
}
```

### FlexTrade

```csharp
public record FlexTrade
{
    public string AccountId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Proceeds { get; init; }
    public decimal Commission { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string TradeDate { get; init; } = string.Empty;
    public string TradeTime { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty;
    public string Exchange { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string ExecId { get; init; } = string.Empty;

    /// <summary>Raw XML element for accessing any additional attributes.</summary>
    public XElement? RawElement { get; init; }
}
```

### FlexPosition

```csharp
public record FlexPosition
{
    public string AccountId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Position { get; init; }
    public decimal MarkPrice { get; init; }
    public decimal PositionValue { get; init; }
    public decimal CostBasis { get; init; }
    public decimal UnrealizedPnl { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string AssetClass { get; init; } = string.Empty;

    /// <summary>Raw XML element for accessing any additional attributes.</summary>
    public XElement? RawElement { get; init; }
}
```

### XML Parsing

The Flex XML structure (for Activity Statements):
```xml
<FlexQueryResponse queryName="..." type="AF">
  <FlexStatements count="1">
    <FlexStatement accountId="U1234567" ...>
      <Trades>
        <Trade accountId="..." symbol="AAPL" conid="265598" ... />
        <Trade ... />
      </Trades>
      <OpenPositions>
        <OpenPosition accountId="..." symbol="SPY" conid="756733" ... />
      </OpenPositions>
      <!-- Other sections based on query template -->
    </FlexStatement>
  </FlexStatements>
</FlexQueryResponse>
```

For Trade Confirmations, the structure uses `<TradeConfirmations>` / `<TradeConfirmation>` instead of `<Trades>` / `<Trade>`.

The parser looks for both element names and maps XML attributes to record properties. Unknown attributes are accessible via `RawElement`.

### FlexOperations

Simple wrapper that validates the Flex token is configured, delegates to `FlexClient`, and wraps the result in `FlexQueryResult`.

---

## Task 6.3 — IIbkrClient Facade + DI

### IIbkrClient Changes

Add:
```csharp
IFlexOperations Flex { get; }
```

### DI Registration

- Register a dedicated `HttpClient` for Flex (no signing pipeline, just plain HTTP)
- Register `FlexClient` (singleton)
- Register `IFlexOperations` → `FlexOperations` (singleton)
- If `FlexToken` is null, register a `FlexOperations` that throws `InvalidOperationException` on use — don't fail at registration time since Flex is optional

---

## Task 6.4 — Tests

### Unit Tests

- `FlexClient` XML parsing: success response → reference code extracted
- `FlexClient` XML parsing: error response → `FlexQueryException` thrown
- `FlexQueryResult` parsing: trades section mapped to `FlexTrade` records
- `FlexQueryResult` parsing: open positions section mapped to `FlexPosition` records
- `FlexQueryResult` parsing: missing section → empty list
- `FlexOperations` throws when `FlexToken` is null

### Integration Tests (WireMock)

- Two-step flow: mock SendRequest → reference code, mock GetStatement → XML report
- Polling: first GetStatement returns "in progress", second returns data

### E2E Test

Conditional on both `IBKR_FLEX_TOKEN` and `IBKR_FLEX_QUERY_ID` environment variables.
Uses the DI pattern:

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddIbkrClient(creds, new IbkrClientOptions
{
    FlexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN"),
});

var client = provider.GetRequiredService<IIbkrClient>();
var queryId = Environment.GetEnvironmentVariable("IBKR_FLEX_QUERY_ID")!;
var result = await client.Flex.ExecuteQueryAsync(queryId, ct);
result.RawXml.ShouldNotBeNull();
```

Note: The Flex token and query ID are separate from OAuth credentials.
Flex token is stored at `tools/flex_token`, query ID at `tools/flex_query_id`.

Use a custom `[EnvironmentFact("IBKR_FLEX_TOKEN")]` since the Flex E2E tests
have different credential requirements than the OAuth E2E tests.

---

## Project Structure (New/Modified Files)

### New Files

```
src/IbkrConduit/
  Flex/
    FlexClient.cs
    FlexQueryResult.cs
    FlexModels.cs
    FlexQueryException.cs
  Client/
    IFlexOperations.cs
    FlexOperations.cs

tests/IbkrConduit.Tests.Unit/
  Flex/
    FlexClientTests.cs
    FlexQueryResultTests.cs

tests/IbkrConduit.Tests.Integration/
  Flex/
    FlexIntegrationTests.cs
```

### Modified Files

```
src/IbkrConduit/Session/IbkrClientOptions.cs — add FlexToken
src/IbkrConduit/Client/IIbkrClient.cs — add Flex property
src/IbkrConduit/Client/IbkrClient.cs — add Flex
src/IbkrConduit/Http/ServiceCollectionExtensions.cs — register Flex components
```

---

## Dependency Graph

```
Task 6.1 (FlexClient + models)
         │
         ▼
Task 6.2 (IFlexOperations + FlexQueryResult)
         │
         ▼
Task 6.3 (facade + DI)
         │
         ▼
Task 6.4 (tests)
```

All sequential — each builds on the prior.
