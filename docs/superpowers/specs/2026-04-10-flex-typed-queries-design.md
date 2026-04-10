# Strongly-Typed Flex Query Methods — Design Spec

## Goal

Replace the single `FlexQueryResult` / `ExecuteQueryAsync` entry point with dedicated strongly-typed methods for each supported Flex query type (Cash Transactions, Trade Confirmations), plus a generic escape hatch for arbitrary queries. Move query IDs from method parameters to `IbkrClientOptions` so consumers configure once at startup.

## Background

The current Flex API has a single return type `FlexQueryResult` with best-effort `Trades` and `OpenPositions` lists. This was convenient when we only had one shape in mind, but now that we've captured multiple Flex query types (Cash Transactions, Trade Confirmations) it's clear each has a distinct schema that deserves its own typed result.

Additionally, query IDs are currently passed as method parameters. Each consumer would pass the same hardcoded ID on every call. Moving them to options is cleaner configuration.

## Design

### Configuration

`IbkrClientOptions` gets a new nested options object:

```csharp
// src/IbkrConduit/Session/IbkrClientOptions.cs
public class IbkrClientOptions
{
    // ... existing properties ...

    /// <summary>
    /// Flex Web Service query IDs for strongly-typed Flex operations.
    /// Configure the IDs for the query templates you want to call via typed methods.
    /// The generic <see cref="IFlexOperations.ExecuteQueryAsync"/> still takes a query ID parameter.
    /// </summary>
    public FlexQueryOptions FlexQueries { get; set; } = new();
}
```

```csharp
// src/IbkrConduit/Flex/FlexQueryOptions.cs
[ExcludeFromCodeCoverage]
public class FlexQueryOptions
{
    /// <summary>
    /// Query ID for the Cash Transactions Flex query template.
    /// Required by <see cref="IFlexOperations.GetCashTransactionsAsync"/>.
    /// Configure in IBKR portal: Reports → Flex Queries → create an Activity Flex
    /// query with the Cash Transactions section enabled, then copy the numeric ID here.
    /// </summary>
    public string? CashTransactionsQueryId { get; set; }

    /// <summary>
    /// Query ID for the Trade Confirmations Flex query template.
    /// Required by <see cref="IFlexOperations.GetTradeConfirmationsAsync"/>.
    /// Configure in IBKR portal: Reports → Flex Queries → create a Trade Confirmation
    /// Flex query with Trade Confirms / Symbol Summary / Orders sections enabled,
    /// then copy the numeric ID here.
    /// </summary>
    public string? TradeConfirmationsQueryId { get; set; }
}
```

### Interface

```csharp
// src/IbkrConduit/Client/IFlexOperations.cs
public interface IFlexOperations
{
    /// <summary>
    /// Fetches cash transactions using the Cash Transactions query template configured
    /// in <see cref="IbkrClientOptions.FlexQueries"/>. The query's configured period
    /// (set in the IBKR portal) determines the date range — runtime date overrides are
    /// not supported because Activity Flex queries can hang server-side on multi-day
    /// range overrides.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <c>IbkrClientOptions.FlexQueries.CashTransactionsQueryId</c> is not set.
    /// </exception>
    Task<Result<CashTransactionsFlexResult>> GetCashTransactionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches trade confirmations for the given date range using the Trade Confirmations
    /// query template configured in <see cref="IbkrClientOptions.FlexQueries"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <c>IbkrClientOptions.FlexQueries.TradeConfirmationsQueryId</c> is not set.
    /// </exception>
    Task<Result<TradeConfirmationsFlexResult>> GetTradeConfirmationsAsync(
        DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary Flex query by ID and returns a generic result with envelope
    /// metadata. Use this for query types without dedicated typed methods.
    /// </summary>
    Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary Flex query with runtime date range override.
    /// Note: multi-day runtime overrides can hang on Activity Flex (`AF`) query types —
    /// in those cases, configure the period in the query template instead.
    /// Trade Confirmation Flex (`TCF`) queries handle runtime overrides reliably.
    /// Date format: yyyyMMdd.
    /// </summary>
    Task<Result<FlexGenericResult>> ExecuteQueryAsync(
        string queryId, string fromDate, string toDate,
        CancellationToken cancellationToken = default);
}
```

### Typed result types

All three typed results follow the same envelope pattern:

```csharp
// src/IbkrConduit/Flex/CashTransactionsFlexResult.cs
[ExcludeFromCodeCoverage]
public record CashTransactionsFlexResult(
    string QueryName,
    DateTimeOffset? GeneratedAt,
    DateOnly? FromDate,   // min fromDate across all FlexStatement elements
    DateOnly? ToDate,     // max toDate across all FlexStatement elements
    IReadOnlyList<FlexCashTransaction> CashTransactions,
    XDocument RawXml);
```

```csharp
// src/IbkrConduit/Flex/TradeConfirmationsFlexResult.cs
[ExcludeFromCodeCoverage]
public record TradeConfirmationsFlexResult(
    string QueryName,
    DateTimeOffset? GeneratedAt,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<FlexTradeConfirmation> TradeConfirmations,
    IReadOnlyList<FlexSymbolSummary> SymbolSummaries,
    IReadOnlyList<FlexOrder> Orders,
    XDocument RawXml);
```

```csharp
// src/IbkrConduit/Flex/FlexGenericResult.cs
[ExcludeFromCodeCoverage]
public record FlexGenericResult(
    string QueryName,
    string QueryType,           // "AF", "TCF", etc. from FlexQueryResponse type attribute
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<FlexStatementInfo> Statements,
    XDocument RawXml);

[ExcludeFromCodeCoverage]
public record FlexStatementInfo(
    string AccountId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string Period,               // "Last365CalendarDays", "Today", etc. ("" when overridden)
    DateTimeOffset? WhenGenerated,
    XElement RawElement);        // for drilling into sections the generic result doesn't parse
```

The generic result is the only one that exposes per-statement info, because consumers of the generic method might genuinely need to know which daily statement a section came from.

### Record types for report items

```csharp
// src/IbkrConduit/Flex/FlexCashTransaction.cs
[ExcludeFromCodeCoverage]
public record FlexCashTransaction
{
    public string AccountId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal FxRateToBase { get; init; }
    public string AssetCategory { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public DateTimeOffset? DateTime { get; init; }
    public DateOnly? SettleDate { get; init; }
    public DateOnly? ReportDate { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;           // "Deposits/Withdrawals", "Broker Interest Received", etc.
    public string TransactionId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string LevelOfDetail { get; init; } = string.Empty;  // "DETAIL"
    public XElement? RawElement { get; init; }
}
```

```csharp
// src/IbkrConduit/Flex/FlexTradeConfirmation.cs
[ExcludeFromCodeCoverage]
public record FlexTradeConfirmation
{
    public string AccountId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string AssetCategory { get; init; } = string.Empty;
    public string SubCategory { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public string TradeId { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string ExecId { get; init; } = string.Empty;
    public DateOnly? TradeDate { get; init; }
    public DateOnly? SettleDate { get; init; }
    public DateOnly? ReportDate { get; init; }
    public DateTimeOffset? OrderTime { get; init; }
    public DateTimeOffset? DateTime { get; init; }
    public string Exchange { get; init; } = string.Empty;
    public string BuySell { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Amount { get; init; }
    public decimal Proceeds { get; init; }
    public decimal NetCash { get; init; }
    public decimal Commission { get; init; }
    public string CommissionCurrency { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty;
    public string LevelOfDetail { get; init; } = string.Empty;  // "EXECUTION"
    public XElement? RawElement { get; init; }
}
```

```csharp
// src/IbkrConduit/Flex/FlexSymbolSummary.cs
[ExcludeFromCodeCoverage]
public record FlexSymbolSummary
{
    public string AccountId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string AssetCategory { get; init; } = string.Empty;
    public string SubCategory { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public string ListingExchange { get; init; } = string.Empty;
    public DateOnly? TradeDate { get; init; }
    public DateOnly? SettleDate { get; init; }
    public DateOnly? ReportDate { get; init; }
    public string BuySell { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }             // average price across all fills for this symbol
    public decimal Amount { get; init; }
    public decimal Proceeds { get; init; }
    public decimal NetCash { get; init; }
    public decimal Commission { get; init; }
    public string LevelOfDetail { get; init; } = string.Empty;  // "SYMBOL_SUMMARY"
    public XElement? RawElement { get; init; }
}
```

```csharp
// src/IbkrConduit/Flex/FlexOrder.cs
[ExcludeFromCodeCoverage]
public record FlexOrder
{
    public string AccountId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string AssetCategory { get; init; } = string.Empty;
    public string SubCategory { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? Conid { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public DateTimeOffset? OrderTime { get; init; }
    public DateOnly? TradeDate { get; init; }
    public DateOnly? SettleDate { get; init; }
    public DateOnly? ReportDate { get; init; }
    public string Exchange { get; init; } = string.Empty;
    public string BuySell { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Amount { get; init; }
    public decimal Proceeds { get; init; }
    public decimal NetCash { get; init; }
    public decimal Commission { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public string LevelOfDetail { get; init; } = string.Empty;  // "ORDER"
    public XElement? RawElement { get; init; }
}
```

Every item type has a `RawElement` property for fall-through access to attributes the DTO doesn't surface. This mirrors the existing `FlexTrade` pattern and is useful when consumers need a field we haven't typed yet.

### Parsing

A new internal static class handles all parsing:

```csharp
// src/IbkrConduit/Flex/FlexResultParser.cs
internal static class FlexResultParser
{
    public static CashTransactionsFlexResult ParseCashTransactions(XDocument doc);
    public static TradeConfirmationsFlexResult ParseTradeConfirmations(XDocument doc);
    public static FlexGenericResult ParseGeneric(XDocument doc);

    // Shared helpers
    private static string GetQueryName(XDocument doc);
    private static string GetQueryType(XDocument doc);
    private static DateTimeOffset? GetGeneratedAt(XDocument doc);  // from first FlexStatement's whenGenerated
    private static (DateOnly? from, DateOnly? to) GetDateRange(XDocument doc);
    private static IReadOnlyList<FlexStatementInfo> ParseStatements(XDocument doc);

    // Per-item parsers
    private static FlexCashTransaction ParseCashTransaction(XElement element);
    private static FlexTradeConfirmation ParseTradeConfirmation(XElement element);
    private static FlexSymbolSummary ParseSymbolSummary(XElement element);
    private static FlexOrder ParseOrder(XElement element);

    // Date parsing — handles both "yyyyMMdd" and "yyyy-MM-dd"
    private static DateOnly? ParseFlexDate(string? value);

    // DateTime parsing — handles both "yyyyMMdd;HHmmss" and "yyyy-MM-dd;HH:mm:ss TZ"
    private static DateTimeOffset? ParseFlexDateTime(string? value);
}
```

Parsing is separate from `FlexOperations` so the parsers can be unit tested against captured fixture XML independently.

### Parsing rules

- **Date fields** (`tradeDate`, `settleDate`, `reportDate`, `fromDate`, `toDate`): try `yyyyMMdd` first, then `yyyy-MM-dd`. Return `null` on failure.
- **DateTime fields** (`whenGenerated`, `dateTime`, `orderTime`): try `yyyyMMdd;HHmmss` first, then `yyyy-MM-dd;HH:mm:ss zzz`. Return `null` on failure.
- **Decimal fields**: use `decimal.TryParse(..., NumberStyles.Number, CultureInfo.InvariantCulture)`. Return `0m` on failure.
- **Nullable int** (`conid`): use `int.TryParse`. Return `null` on failure.
- **String fields**: take the attribute value directly, default to empty string if missing.
- **`CashTransactions`**: flatten `XDocument.Descendants("CashTransaction")` across all statements.
- **`TradeConfirmations`**: flatten `Descendants("TradeConfirm")` — these are the individual executions (`levelOfDetail="EXECUTION"` or similar).
- **`SymbolSummaries`**: flatten `Descendants("SymbolSummary")`.
- **`Orders`**: flatten `Descendants("Order")`.

### FlexOperations implementation

```csharp
public async Task<Result<CashTransactionsFlexResult>> GetCashTransactionsAsync(
    CancellationToken cancellationToken = default)
{
    EnsureFlexConfigured();
    var queryId = _options.FlexQueries.CashTransactionsQueryId
        ?? throw new InvalidOperationException(
            "Cash Transactions queries require IbkrClientOptions.FlexQueries.CashTransactionsQueryId. " +
            "Create an Activity Flex query with the Cash Transactions section enabled in the IBKR portal " +
            "(Reports → Flex Queries), then set the numeric query ID in AddIbkrClient options.");

    var docResult = await ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);
    var result = docResult.Map(FlexResultParser.ParseCashTransactions);
    return WithThrowSetting(result);
}

public async Task<Result<TradeConfirmationsFlexResult>> GetTradeConfirmationsAsync(
    DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
{
    EnsureFlexConfigured();
    var queryId = _options.FlexQueries.TradeConfirmationsQueryId
        ?? throw new InvalidOperationException(
            "Trade Confirmations queries require IbkrClientOptions.FlexQueries.TradeConfirmationsQueryId. " +
            "Create a Trade Confirmation Flex query in the IBKR portal (Reports → Flex Queries), " +
            "then set the numeric query ID in AddIbkrClient options.");

    var fromStr = fromDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    var toStr = toDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    var docResult = await ExecuteInternalAsync(queryId, fromStr, toStr, cancellationToken);
    var result = docResult.Map(FlexResultParser.ParseTradeConfirmations);
    return WithThrowSetting(result);
}

public async Task<Result<FlexGenericResult>> ExecuteQueryAsync(
    string queryId, CancellationToken cancellationToken = default)
{
    EnsureFlexConfigured();
    var docResult = await ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);
    var result = docResult.Map(FlexResultParser.ParseGeneric);
    return WithThrowSetting(result);
}

public async Task<Result<FlexGenericResult>> ExecuteQueryAsync(
    string queryId, string fromDate, string toDate, CancellationToken cancellationToken = default)
{
    EnsureFlexConfigured();
    var docResult = await ExecuteInternalAsync(queryId, fromDate, toDate, cancellationToken);
    var result = docResult.Map(FlexResultParser.ParseGeneric);
    return WithThrowSetting(result);
}
```

`ExecuteInternalAsync` is the existing private method that does SendRequest → PollForStatement → return `Result<XDocument>`. No changes needed to that layer — only the public API adds a parsing step.

### Deletions

- `src/IbkrConduit/Flex/FlexQueryResult.cs` — replaced by three new result types
- `src/IbkrConduit/Flex/FlexModels.cs` — contents removed (`FlexTrade` and `FlexPosition` replaced; new types go into separate files)

### Migration impact

- **`CaptureFlexQuery/Program.cs`** — currently prints `result.Value.Trades.Count` and `result.Value.OpenPositions.Count`. After refactor:
  - Uses the generic `ExecuteQueryAsync(queryId)` (since the tool accepts any query ID)
  - Prints `QueryType`, `QueryName`, `Statements.Count`, and relies on consumers to inspect RawXml for section-specific content
- **`examples/GetTrades.cs`** — currently uses the old `ExecuteQueryAsync` and accesses `.Trades`. Update to use `GetTradeConfirmationsAsync(DateOnly, DateOnly)` with a configured query ID, or the generic method if the example is meant to show custom queries.
- **`FlexOperationsPollingTests.cs`** — current tests use a fake `FlexClient` and assert on `FlexQueryResult.RawXml`. Need to update to expect `FlexGenericResult` (or the typed results) and update the assertions.
- **`FlexTests.cs` (integration)** — same updates for the new return types.

## Testing

### Unit tests

**FlexResultParser (new):**
- `ParseCashTransactions` with real captured XML → returns `CashTransactionsFlexResult` with correct QueryName ("Cash Transactions - API"), non-null date range, 3 CashTransactions entries (the $1M deposit + 2 interest payments from the Feb 2026 capture)
- `ParseTradeConfirmations` with real captured XML → returns `TradeConfirmationsFlexResult` with correct QueryName ("E2E-Test"), 39 TradeConfirmations, 2 SymbolSummaries, 39 Orders
- `ParseGeneric` with either capture → returns correct QueryType ("AF" or "TCF"), non-empty Statements list
- `ParseFlexDate` handles both `"20260201"` and `"2026-02-01"` formats, returns null for invalid input, null for empty string
- `ParseFlexDateTime` handles both `"20260409;135737"` and `"2026-04-09;21:23:54 EDT"` formats
- `ParseCashTransaction`, `ParseTradeConfirmation`, `ParseSymbolSummary`, `ParseOrder` each extract all fields from a sample element correctly
- Missing/empty attributes produce default values (empty string, 0, null) not exceptions
- `FromDate`/`ToDate` aggregation: min `fromDate` and max `toDate` across all statements when the result has multiple daily statements

**FlexOperations new methods:**
- `GetCashTransactionsAsync` with query ID not configured → throws `InvalidOperationException` with the configured error message
- `GetTradeConfirmationsAsync` with query ID not configured → throws `InvalidOperationException`
- `GetCashTransactionsAsync` with success response → returns `Result<CashTransactionsFlexResult>.Success` via the internal pipeline
- `GetTradeConfirmationsAsync(DateOnly, DateOnly)` formats the dates as `yyyyMMdd` and passes them to `ExecuteInternalAsync`
- `ExecuteQueryAsync(queryId)` and `ExecuteQueryAsync(queryId, from, to)` return `Result<FlexGenericResult>`
- `ThrowOnApiError = true` behavior: typed methods also honor this, returning `result.EnsureSuccess()` which throws `IbkrApiException` wrapping the error

### Integration tests

Add or update tests in `FlexTests.cs`:
- Stub a WireMock response with a Cash Transactions fixture, configure `FlexQueries.CashTransactionsQueryId`, call `GetCashTransactionsAsync`, verify typed fields
- Stub a Trade Confirmations fixture, call `GetTradeConfirmationsAsync` with dates, verify the three collections are populated
- Stub a generic query response, call `ExecuteQueryAsync`, verify `FlexGenericResult.Statements` is parsed
- Missing query ID throws `InvalidOperationException` at call time (not wrapped in Result)

### Fixture files

Copy the two captured XML files into test fixtures so unit tests can use real wire shapes:
- `tests/IbkrConduit.Tests.Unit/Flex/Fixtures/cash-transactions.xml` — from `recordings/flex/2026-04-09T175735-1464458.xml` (with account ID sanitized to `U1234567`)
- `tests/IbkrConduit.Tests.Unit/Flex/Fixtures/trade-confirmations.xml` — from `recordings/flex/2026-04-10T012351-1454602-20260401-20260409.xml` (with account ID sanitized)

## Scope Boundaries

### In Scope

- `FlexQueryOptions` on `IbkrClientOptions`
- Four public methods: `GetCashTransactionsAsync`, `GetTradeConfirmationsAsync(DateOnly, DateOnly)`, `ExecuteQueryAsync(queryId)`, `ExecuteQueryAsync(queryId, fromDate, toDate)`
- Three typed result records: `CashTransactionsFlexResult`, `TradeConfirmationsFlexResult`, `FlexGenericResult`
- Four typed item records: `FlexCashTransaction`, `FlexTradeConfirmation`, `FlexSymbolSummary`, `FlexOrder`
- `FlexStatementInfo` for the generic result
- `FlexResultParser` static class with all parsing logic
- Deletion of `FlexQueryResult`, `FlexTrade`, `FlexPosition`, `FlexModels.cs`
- Fixture files from real captures
- Updates to `CaptureFlexQuery` tool and examples

### Out of Scope

- **Open Positions typed support** — we don't have a captured sample, and it's not part of the Trade Confirmations capture we inspected. A future `GetOpenPositionsAsync` can be added when we have a real Activity Flex capture with open positions configured.
- **Corporate Actions typed support** — the captured report was empty (no events on the paper account). Consumers wanting corporate actions can use the generic `ExecuteQueryAsync` for now.
- **Position Summary, NetAssetValue, or other Activity Flex sections** — same reasoning. Add typed methods one at a time as real captures become available.
- **Making `FlexResultParser` public** — stays internal. Consumers get typed results; they don't need to call the parser directly.
- **Changes to `FlexClient` transport layer** — no changes.
- **Changes to `FlexErrorCodes` or the Result<T> pattern** — no changes.
- **Request-side modeling (e.g., `FlexQueryRequest` builder)** — query IDs are just strings, no builder needed.
