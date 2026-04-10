# Adding Strongly-Typed Flex Report Types

Guide for contributors adding new typed Flex query methods to the library.

## Overview

The Flex Web Service supports many report types (Activity Flex, Trade Confirmation Flex, etc.), each with multiple configurable sections (Trades, Cash Transactions, Open Positions, Corporate Actions, etc.). The library provides typed methods for commonly-used report types and a generic escape hatch for everything else.

**Current typed methods:**
- `GetCashTransactionsAsync()` → `CashTransactionsFlexResult`
- `GetTradeConfirmationsAsync(DateOnly, DateOnly)` → `TradeConfirmationsFlexResult`

**Generic method (any query type):**
- `ExecuteQueryAsync(queryId)` → `FlexGenericResult`

## When to Add a Typed Method

Add a typed method when:
- You have a **real captured XML response** from the IBKR Flex Web Service (not hand-crafted)
- The report type is **commonly used** by consumers of the library
- The XML schema has been **verified against a live account**

Do NOT add typed methods based on documentation alone — IBKR's docs don't always match the wire format.

## Step-by-Step Process

### 1. Capture a Real Response

Use the `CaptureFlexQuery` tool to capture a real XML response from the IBKR Flex Web Service:

```bash
source tools/set-e2e-env.sh
dotnet run --project tools/CaptureFlexQuery -- <query-id> --poll-timeout 120
```

The XML is saved to `recordings/flex/`. Inspect it to understand the element names, attribute names, and nesting structure.

**Tips:**
- For Activity Flex (`AF`) queries, disable "Breakout by Day" in the IBKR portal to get a single consolidated `<FlexStatement>` instead of one per trading day. The parser handles both shapes, but consolidated is 10x smaller.
- Runtime date overrides (`--from`/`--to`) work reliably for Trade Confirmation Flex (`TCF`) queries but can hang indefinitely for Activity Flex (`AF`) queries. For `AF`, configure the period in the query template instead.
- If the response is empty (no data for the configured period), try a wider date range or use an account with more activity.

### 2. Create the Item Record

Create a new file in `src/IbkrConduit/Flex/` for the item record. Follow the pattern of existing records:

```csharp
// src/IbkrConduit/Flex/FlexCorporateAction.cs
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Represents a corporate action record parsed from a Flex Query response.
/// </summary>
[ExcludeFromCodeCoverage]
public record FlexCorporateAction
{
    /// <summary>The account ID associated with the action.</summary>
    public string AccountId { get; init; } = string.Empty;

    // ... fields matching the XML attributes from the captured response ...

    /// <summary>Raw XML element for accessing any additional attributes.</summary>
    public XElement? RawElement { get; init; }
}
```

**Conventions:**
- `[ExcludeFromCodeCoverage]` — pure data records
- Property names are PascalCase versions of the XML attribute names
- String properties default to `string.Empty`
- Numeric properties: `decimal` for money/prices, `int?` for IDs (nullable because the attribute may be empty)
- Date properties: `DateOnly?` for dates, `DateTimeOffset?` for timestamps
- Always include `XElement? RawElement` as the last property — consumers may need attributes you didn't map

### 3. Create the Result Record

```csharp
// src/IbkrConduit/Flex/CorporateActionsFlexResult.cs
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Result of a Corporate Actions Flex query.
/// </summary>
[ExcludeFromCodeCoverage]
public record CorporateActionsFlexResult(
    string QueryName,
    DateTimeOffset? GeneratedAt,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<FlexCorporateAction> CorporateActions,
    XDocument RawXml);
```

All result records follow the same envelope pattern: `QueryName`, `GeneratedAt`, `FromDate` (min across statements), `ToDate` (max across statements), one or more typed collections, and `RawXml` for fall-through access.

### 4. Add a Parser Method

Add a new static method to `src/IbkrConduit/Flex/FlexResultParser.cs`:

```csharp
public static CorporateActionsFlexResult ParseCorporateActions(XDocument doc)
{
    var (from, to) = GetDateRange(doc);
    var actions = doc.Descendants("CorporateAction").Select(ParseCorporateAction).ToList();
    return new CorporateActionsFlexResult(
        GetQueryName(doc),
        GetGeneratedAt(doc),
        from,
        to,
        actions,
        doc);
}

private static FlexCorporateAction ParseCorporateAction(XElement el) =>
    new()
    {
        AccountId = Attr(el, "accountId"),
        // ... map each XML attribute to its property ...
        RawElement = el,
    };
```

**Key helper methods already available in `FlexResultParser`:**
- `Attr(el, name)` — reads a string attribute, returns `""` if missing
- `AttrNullableInt(el, name)` — parses an int attribute, returns `null` if missing/invalid
- `AttrDecimal(el, name)` — parses a decimal attribute, returns `0m` if missing/invalid
- `ParseFlexDate(value)` — parses `"yyyyMMdd"` or `"yyyy-MM-dd"`, returns `null` on failure
- `ParseFlexDateTime(value)` — parses `"yyyyMMdd;HHmmss"` or `"yyyy-MM-dd;HH:mm:ss TZ"` (handles EDT/EST/etc.)
- `GetQueryName(doc)` — extracts `queryName` attribute from `<FlexQueryResponse>`
- `GetGeneratedAt(doc)` — extracts `whenGenerated` from the first `<FlexStatement>`
- `GetDateRange(doc)` — computes min `fromDate` / max `toDate` across all `<FlexStatement>` elements

**Element name matters:** use `doc.Descendants("CorporateAction")` where `"CorporateAction"` is the exact XML element name from the captured response. This flattens across all `<FlexStatement>` wrappers (handles both breakout-by-day on and off).

### 5. Add a Query ID to FlexQueryOptions

```csharp
// src/IbkrConduit/Flex/FlexQueryOptions.cs
/// <summary>
/// Query ID for the Corporate Actions Flex query template.
/// Required by <see cref="IFlexOperations.GetCorporateActionsAsync"/>.
/// </summary>
public string? CorporateActionsQueryId { get; set; }
```

### 6. Add the Method to IFlexOperations

```csharp
// src/IbkrConduit/Client/IFlexOperations.cs

/// <summary>
/// Fetches corporate actions using the Corporate Actions query template configured
/// in <c>IbkrClientOptions.FlexQueries</c>.
/// </summary>
Task<Result<CorporateActionsFlexResult>> GetCorporateActionsAsync(
    CancellationToken cancellationToken = default);
```

**Date parameters:** Only add `DateOnly fromDate, DateOnly toDate` parameters if the report type supports runtime date overrides reliably (verified by testing with the capture tool). Trade Confirmation Flex (`TCF`) does; Activity Flex (`AF`) generally does not.

### 7. Implement in FlexOperations

```csharp
// src/IbkrConduit/Client/FlexOperations.cs

public async Task<Result<CorporateActionsFlexResult>> GetCorporateActionsAsync(
    CancellationToken cancellationToken = default)
{
    EnsureFlexConfigured();
    var queryId = _options.FlexQueries.CorporateActionsQueryId
        ?? throw new InvalidOperationException(
            "Corporate Actions queries require IbkrClientOptions.FlexQueries.CorporateActionsQueryId. " +
            "Create an Activity Flex query with the Corporate Actions section enabled in the IBKR portal " +
            "(Reports → Flex Queries), then set the numeric query ID in AddIbkrClient options.");

    var docResult = await ExecuteInternalAsync(queryId, fromDate: null, toDate: null, cancellationToken);
    var result = docResult.Map(FlexResultParser.ParseCorporateActions);
    return WithThrowSetting(result);
}
```

This is the standard three-line pattern:
1. Validate the query ID is configured (throw `InvalidOperationException` with setup guidance if not)
2. Call `ExecuteInternalAsync` to run the two-step Flex flow
3. Map the raw `XDocument` to the typed result using the parser

### 8. Add Tests

**Unit tests** (`tests/IbkrConduit.Tests.Unit/Flex/FlexResultParserTests.cs`):
- Copy the captured XML to `tests/IbkrConduit.Tests.Unit/Flex/Fixtures/` (sanitize account IDs: replace real IDs with `U1234567`)
- Test that the parser extracts the correct number of items
- Test that key fields are populated (amounts, dates, descriptions)
- Test empty/missing sections produce empty lists, not exceptions
- Test that `FromDate`/`ToDate`/`GeneratedAt` envelope fields are parsed

**Integration tests** (`tests/IbkrConduit.Tests.Integration/Flex/FlexTests.cs`):
- Stub WireMock with the fixture XML, configure the query ID, call the typed method
- Verify `result.IsSuccess` and typed field values
- Test missing query ID throws `InvalidOperationException` with the correct guidance message

**Fixture files** need a copy rule in the test project's `.csproj`:
```xml
<!-- Already present for *.xml — no change needed if your fixture is .xml -->
<None Include="Flex\Fixtures\**\*.xml" CopyToOutputDirectory="PreserveNewest" />
```

### 9. Update the CaptureFlexQuery Tool (Optional)

If you want the capture tool to print a summary for the new type, update `tools/CaptureFlexQuery/Program.cs` to inspect `FlexGenericResult.Statements` for your section's element name. This is optional — the tool already prints `QueryName`, `QueryType`, and `Statements.Count` for all query types.

## Architecture Reference

```
Consumer code
    │
    ▼
IFlexOperations.GetXxxAsync()          ← public interface
    │
    ▼
FlexOperations.GetXxxAsync()           ← reads query ID from options, calls ExecuteInternalAsync
    │
    ▼
FlexOperations.ExecuteInternalAsync()  ← orchestrates SendRequest → PollForStatement
    │                                     classification, retry, timeout, metrics
    ▼
FlexClient.SendRequestAsync()          ← thin HTTP wrapper, returns Result<XDocument>
FlexClient.GetStatementAsync()         ← thin HTTP wrapper, returns Result<XDocument>
    │
    ▼
FlexResultParser.ParseXxx()            ← static parser, XDocument → typed result
```

**Layer separation:**
- `FlexClient` — HTTP transport only. Never add domain logic here.
- `FlexOperations` — orchestration: polling, classification, retries, timeout, metrics. Public methods live here.
- `FlexResultParser` — XML parsing only. Stateless, testable against fixture files without any HTTP infrastructure.

## Checklist

- [ ] Real captured XML response from the IBKR Flex Web Service
- [ ] Sanitized fixture file in `tests/.../Flex/Fixtures/` (account IDs → `U1234567`)
- [ ] Item record in `src/IbkrConduit/Flex/` with `[ExcludeFromCodeCoverage]` and `RawElement`
- [ ] Result record with envelope (QueryName, GeneratedAt, FromDate, ToDate, typed list, RawXml)
- [ ] Parser method in `FlexResultParser` using `Descendants()` for flattening
- [ ] Query ID property in `FlexQueryOptions` with XML doc comment
- [ ] Method on `IFlexOperations` with XML doc comment
- [ ] Implementation in `FlexOperations` — three-line pattern (validate ID, execute, map+throw)
- [ ] Unit tests: parser with fixture, empty section, missing section, envelope fields
- [ ] Integration tests: success with WireMock, missing query ID throws
- [ ] `dotnet build --configuration Release` — 0 warnings
- [ ] `dotnet test --configuration Release` — all tests pass
- [ ] `dotnet format --verify-no-changes` — clean
