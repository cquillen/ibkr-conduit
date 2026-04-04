# M2: Portfolio Integration Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write integration tests for the 4 remaining portfolio endpoints (positions, summary, ledger, partitioned PnL), update DTOs to match wire format, and add endpoint-specific scenario tests.

**Architecture:** Each endpoint gets a success test (fixture-based, asserting all DTO fields) and a 401 recovery test (WireMock scenario: first call 401, re-auth triggers new LST + ssodh/init, retry succeeds). Endpoint-specific tests cover empty positions page and large summary payload spot-check. All tests go in the existing `PortfolioTests.cs` file using the established `TestHarness` pattern.

**Tech Stack:** C# / .NET 10 / xUnit v3 / Shouldly / WireMock.Net / Refit / System.Text.Json

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs` | Update Position DTO (add 4 missing fields, fix `Conid` type to `long`), add `severity` to AccountSummaryEntry, expand LedgerEntry to all 30 fields |
| Create | `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-page0.json` | Positions success fixture |
| Create | `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-empty.json` | Empty positions page fixture |
| Create | `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-summary.json` | Account summary fixture (representative subset) |
| Create | `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-ledger.json` | Ledger fixture |
| Create | `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-iserver-account-pnl-partitioned.json` | Partitioned PnL fixture |
| Modify | `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs` | Add 12 new tests for positions, summary, ledger, PnL |

---

### Task 1: Update Position DTO to Match Wire Format

The current `Position` record uses `int Conid` but the wire format sends `320227571` which fits `int` but the spec requires `long`. It also is missing 4 fields from the recording: `exerciseStyle`, `conExchMap`, `undConid`, `model`. The current DTO has fields (`Name`, `Sector`, `Ticker`, `IsUs`) not in this recording but potentially present in other responses -- keep them as nullable since `[JsonExtensionData]` would catch them anyway. The DTO is a positional record, so new fields must be added as parameters.

**Files:**
- Modify: `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs:167-193`

- [ ] **Step 1: Update the Position record**

Replace the existing `Position` record with one that includes all fields from the recording plus the existing fields kept as nullable. Change `Conid` from `int` to `long`:

```csharp
/// <summary>
/// Represents a position in a portfolio account.
/// </summary>
[ExcludeFromCodeCoverage]
public record Position(
    [property: JsonPropertyName("acctId")] string? AccountId,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long Conid,
    [property: JsonPropertyName("contractDesc")] string? ContractDescription,
    [property: JsonPropertyName("position")] decimal Quantity,
    [property: JsonPropertyName("mktPrice")] decimal MarketPrice,
    [property: JsonPropertyName("mktValue")] decimal MarketValue,
    [property: JsonPropertyName("avgCost")] decimal AverageCost,
    [property: JsonPropertyName("avgPrice")] decimal AveragePrice,
    [property: JsonPropertyName("realizedPnl")] decimal RealizedPnl,
    [property: JsonPropertyName("unrealizedPnl")] decimal UnrealizedPnl,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("exchs")] string? Exchs,
    [property: JsonPropertyName("expiry")] string? Expiry,
    [property: JsonPropertyName("putOrCall")] string? PutOrCall,
    [property: JsonPropertyName("multiplier")] decimal? Multiplier,
    [property: JsonPropertyName("strike")] decimal Strike,
    [property: JsonPropertyName("exerciseStyle")] string? ExerciseStyle,
    [property: JsonPropertyName("conExchMap")] List<string>? ConExchMap,
    [property: JsonPropertyName("assetClass")] string? AssetClass,
    [property: JsonPropertyName("undConid")] long UndConid,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("sector")] string? Sector,
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("isUS")] bool? IsUs)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 2: Build to verify no compilation errors**

Run: `dotnet build /workspace/ibkr-conduit --configuration Release`

Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs
git commit -m "feat: update Position DTO with missing wire fields and fix Conid to long"
```

---

### Task 2: Add Severity Field to AccountSummaryEntry

The recording shows each summary entry has a `severity` field (int) that the current DTO is missing.

**Files:**
- Modify: `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs:204-217`

- [ ] **Step 1: Add severity to AccountSummaryEntry**

Replace the existing `AccountSummaryEntry` record:

```csharp
/// <summary>
/// Represents an entry in the account summary response.
/// Keys are dynamic field names like "netliquidationvalue", "totalcashvalue", etc.
/// </summary>
/// <param name="Amount">The numeric amount.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="IsNull">Whether the value is null/unavailable.</param>
/// <param name="Timestamp">Unix timestamp of the value.</param>
/// <param name="Value">String representation of the value.</param>
/// <param name="Severity">Severity level of the entry.</param>
[ExcludeFromCodeCoverage]
public record AccountSummaryEntry(
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("isNull")] bool IsNull,
    [property: JsonPropertyName("timestamp")] long? Timestamp,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("severity")] int Severity)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build /workspace/ibkr-conduit --configuration Release`

Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs
git commit -m "feat: add severity field to AccountSummaryEntry DTO"
```

---

### Task 3: Expand LedgerEntry DTO to Match Wire Format

The current `LedgerEntry` has 9 fields. The recording shows 30 fields. Add all missing fields.

**Files:**
- Modify: `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs:232-249`

- [ ] **Step 1: Replace LedgerEntry with full field set**

```csharp
/// <summary>
/// Represents a ledger entry for a currency in the account.
/// Keys in the parent dictionary are currency codes like "USD", "EUR", "BASE".
/// </summary>
[ExcludeFromCodeCoverage]
public record LedgerEntry(
    [property: JsonPropertyName("commoditymarketvalue")] decimal CommodityMarketValue,
    [property: JsonPropertyName("futuremarketvalue")] decimal FutureMarketValue,
    [property: JsonPropertyName("settledcash")] decimal SettledCash,
    [property: JsonPropertyName("exchangerate")] decimal ExchangeRate,
    [property: JsonPropertyName("sessionid")] int SessionId,
    [property: JsonPropertyName("cashbalance")] decimal CashBalance,
    [property: JsonPropertyName("corporatebondsmarketvalue")] decimal CorporateBondsMarketValue,
    [property: JsonPropertyName("warrantsmarketvalue")] decimal WarrantsMarketValue,
    [property: JsonPropertyName("netliquidationvalue")] decimal NetLiquidationValue,
    [property: JsonPropertyName("interest")] decimal Interest,
    [property: JsonPropertyName("unrealizedpnl")] decimal UnrealizedPnl,
    [property: JsonPropertyName("stockmarketvalue")] decimal StockMarketValue,
    [property: JsonPropertyName("moneyfunds")] decimal MoneyFunds,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("realizedpnl")] decimal RealizedPnl,
    [property: JsonPropertyName("funds")] decimal Funds,
    [property: JsonPropertyName("acctcode")] string? AcctCode,
    [property: JsonPropertyName("issueroptionsmarketvalue")] decimal IssuerOptionsMarketValue,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("severity")] int Severity,
    [property: JsonPropertyName("stockoptionmarketvalue")] decimal StockOptionMarketValue,
    [property: JsonPropertyName("futuresonlypnl")] decimal FuturesOnlyPnl,
    [property: JsonPropertyName("tbondsmarketvalue")] decimal TBondsMarketValue,
    [property: JsonPropertyName("futureoptionmarketvalue")] decimal FutureOptionMarketValue,
    [property: JsonPropertyName("cashbalancefxsegment")] decimal CashBalanceFxSegment,
    [property: JsonPropertyName("secondkey")] string? SecondKey,
    [property: JsonPropertyName("tbillsmarketvalue")] decimal TBillsMarketValue,
    [property: JsonPropertyName("endofbundle")] int EndOfBundle,
    [property: JsonPropertyName("dividends")] decimal Dividends,
    [property: JsonPropertyName("cryptocurrencyvalue")] decimal CryptocurrencyValue)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build /workspace/ibkr-conduit --configuration Release`

Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs
git commit -m "feat: expand LedgerEntry DTO to all 30 wire fields"
```

---

### Task 4: Create Fixture Files

Create 5 fixture files from the recording data. All account IDs sanitized to `U1234567`, all names to `Test User`.

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-page0.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-empty.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-summary.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-ledger.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-iserver-account-pnl-partitioned.json`

- [ ] **Step 1: Create positions page 0 fixture**

Create `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-page0.json`:

```json
{
  "Request": {
    "Path": "/v1/api/portfolio/U1234567/positions/0",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": [
      {
        "acctId": "U1234567",
        "conid": 320227571,
        "contractDesc": "QQQ",
        "position": 3.0,
        "mktPrice": 584.6799927,
        "mktValue": 1754.04,
        "currency": "USD",
        "avgCost": 584.31333335,
        "avgPrice": 584.31333335,
        "realizedPnl": 0.0,
        "unrealizedPnl": 1.1,
        "exchs": null,
        "expiry": null,
        "putOrCall": null,
        "multiplier": null,
        "strike": 0.0,
        "exerciseStyle": null,
        "conExchMap": [],
        "assetClass": "STK",
        "undConid": 0,
        "model": ""
      }
    ]
  }
}
```

- [ ] **Step 2: Create empty positions page fixture**

Create `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-empty.json`:

```json
{
  "Request": {
    "Path": "/v1/api/portfolio/U1234567/positions/999",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": []
  }
}
```

- [ ] **Step 3: Create account summary fixture**

Create `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-summary.json` with a representative subset (5 entries covering different value types):

```json
{
  "Request": {
    "Path": "/v1/api/portfolio/U1234567/summary",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "accountcode": {
        "amount": 0.0,
        "currency": null,
        "isNull": false,
        "timestamp": 1775169606,
        "value": "U1234567",
        "severity": 0
      },
      "netliquidation": {
        "amount": 1005165.5,
        "currency": "USD",
        "isNull": false,
        "timestamp": 1775169606,
        "value": "1005165.5",
        "severity": 0
      },
      "buyingpower": {
        "amount": 3984328.97,
        "currency": "USD",
        "isNull": false,
        "timestamp": 1775169606,
        "value": "3984328.97",
        "severity": 0
      },
      "totalcashvalue": {
        "amount": 971869.6,
        "currency": "USD",
        "isNull": false,
        "timestamp": 1775169606,
        "value": "971869.6",
        "severity": 0
      },
      "initmarginreq": {
        "amount": 15276.38,
        "currency": "USD",
        "isNull": false,
        "timestamp": 1775169606,
        "value": "15276.38",
        "severity": 0
      }
    }
  }
}
```

- [ ] **Step 4: Create ledger fixture**

Create `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-ledger.json`:

```json
{
  "Request": {
    "Path": "/v1/api/portfolio/U1234567/ledger",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "USD": {
        "commoditymarketvalue": 0.0,
        "futuremarketvalue": 0.0,
        "settledcash": 971869.6,
        "exchangerate": 1,
        "sessionid": 1,
        "cashbalance": 971869.6,
        "corporatebondsmarketvalue": 0.0,
        "warrantsmarketvalue": 0.0,
        "netliquidationvalue": 1005165.5,
        "interest": 2682.45,
        "unrealizedpnl": 151.57,
        "stockmarketvalue": 30613.4,
        "moneyfunds": 0.0,
        "currency": "USD",
        "realizedpnl": 0.0,
        "funds": 0.0,
        "acctcode": "U1234567",
        "issueroptionsmarketvalue": 0.0,
        "key": "LedgerList",
        "timestamp": 1775169606,
        "severity": 0,
        "stockoptionmarketvalue": 0.0,
        "futuresonlypnl": 0.0,
        "tbondsmarketvalue": 0.0,
        "futureoptionmarketvalue": 0.0,
        "cashbalancefxsegment": 0.0,
        "secondkey": "USD",
        "tbillsmarketvalue": 0.0,
        "endofbundle": 1,
        "dividends": 0.0,
        "cryptocurrencyvalue": 0.0
      },
      "BASE": {
        "commoditymarketvalue": 0.0,
        "futuremarketvalue": 0.0,
        "settledcash": 971869.6,
        "exchangerate": 1,
        "sessionid": 1,
        "cashbalance": 971869.6,
        "corporatebondsmarketvalue": 0.0,
        "warrantsmarketvalue": 0.0,
        "netliquidationvalue": 1005165.5,
        "interest": 2682.45,
        "unrealizedpnl": 151.57,
        "stockmarketvalue": 30613.4,
        "moneyfunds": 0.0,
        "currency": "BASE",
        "realizedpnl": 0.0,
        "funds": 0.0,
        "acctcode": "U1234567",
        "issueroptionsmarketvalue": 0.0,
        "key": "LedgerList",
        "timestamp": 1775169606,
        "severity": 0,
        "stockoptionmarketvalue": 0.0,
        "futuresonlypnl": 0.0,
        "tbondsmarketvalue": 0.0,
        "futureoptionmarketvalue": 0.0,
        "cashbalancefxsegment": 0.0,
        "secondkey": "BASE",
        "tbillsmarketvalue": 0.0,
        "endofbundle": 1,
        "dividends": 0.0,
        "cryptocurrencyvalue": 0.0
      }
    }
  }
}
```

- [ ] **Step 5: Create partitioned PnL fixture**

Create `tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-iserver-account-pnl-partitioned.json`:

```json
{
  "Request": {
    "Path": "/v1/api/iserver/account/pnl/partitioned",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "upnl": {}
    }
  }
}
```

- [ ] **Step 6: Verify fixtures are included in build output**

Check the `.csproj` to make sure `Fixtures/**/*.json` is copied to output. If not, this needs to be added.

Run: `dotnet build /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release`

Expected: Build succeeds. Verify fixture files appear in `bin/Release/net10.0/Fixtures/Portfolio/`.

- [ ] **Step 7: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-page0.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-positions-empty.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-summary.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-portfolio-ledger.json
git add tests/IbkrConduit.Tests.Integration/Fixtures/Portfolio/GET-iserver-account-pnl-partitioned.json
git commit -m "test: add fixture files for portfolio positions, summary, ledger, and PnL"
```

---

### Task 5: Write Positions Success Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `PortfolioTests.cs` after the existing `GetAccounts_401Recovery_ReauthenticatesAndRetries` test:

```csharp
[Fact]
public async Task GetPositions_ReturnsAllFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/portfolio/U1234567/positions/0",
        FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-page0"));

    var positions = await _harness.Client.Portfolio.GetPositionsAsync(
        "U1234567", 0, TestContext.Current.CancellationToken);

    positions.ShouldNotBeEmpty();
    var pos = positions[0];
    pos.AccountId.ShouldBe("U1234567");
    pos.Conid.ShouldBe(320227571L);
    pos.ContractDescription.ShouldBe("QQQ");
    pos.Quantity.ShouldBe(3.0m);
    pos.MarketPrice.ShouldBe(584.6799927m);
    pos.MarketValue.ShouldBe(1754.04m);
    pos.Currency.ShouldBe("USD");
    pos.AverageCost.ShouldBe(584.31333335m);
    pos.AveragePrice.ShouldBe(584.31333335m);
    pos.RealizedPnl.ShouldBe(0.0m);
    pos.UnrealizedPnl.ShouldBe(1.1m);
    pos.Exchs.ShouldBeNull();
    pos.Expiry.ShouldBeNull();
    pos.PutOrCall.ShouldBeNull();
    pos.Multiplier.ShouldBeNull();
    pos.Strike.ShouldBe(0.0m);
    pos.ExerciseStyle.ShouldBeNull();
    pos.ConExchMap.ShouldNotBeNull();
    pos.ConExchMap.ShouldBeEmpty();
    pos.AssetClass.ShouldBe("STK");
    pos.UndConid.ShouldBe(0L);
    pos.Model.ShouldBe("");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetPositions_ReturnsAllFields"`

Expected: PASS. (The DTO was already updated in Task 1, and the fixture was created in Task 4.)

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add positions success integration test"
```

---

### Task 6: Write Positions 401 Recovery Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the 401 recovery test for positions**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetPositions_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/positions/0")
            .UsingGet())
        .InScenario("positions-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/positions/0")
            .UsingGet())
        .InScenario("positions-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-page0")));

    var positions = await _harness.Client.Portfolio.GetPositionsAsync(
        "U1234567", 0, TestContext.Current.CancellationToken);

    positions.ShouldNotBeEmpty();
    positions[0].Conid.ShouldBe(320227571L);

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetPositions_401Recovery"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add positions 401 recovery integration test"
```

---

### Task 7: Write Empty Positions Page Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the empty page test**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetPositions_EmptyPage_ReturnsEmptyList()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/portfolio/U1234567/positions/999",
        FixtureLoader.LoadBody("Portfolio", "GET-portfolio-positions-empty"));

    var positions = await _harness.Client.Portfolio.GetPositionsAsync(
        "U1234567", 999, TestContext.Current.CancellationToken);

    positions.ShouldBeEmpty();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetPositions_EmptyPage"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add empty positions page integration test"
```

---

### Task 8: Write Account Summary Success Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the summary success test**

The summary returns 143 keys. The test spot-checks representative entries to verify DTO deserialization without asserting all keys.

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetAccountSummary_ReturnsAllEntryFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/portfolio/U1234567/summary",
        FixtureLoader.LoadBody("Portfolio", "GET-portfolio-summary"));

    var summary = await _harness.Client.Portfolio.GetAccountSummaryAsync(
        "U1234567", TestContext.Current.CancellationToken);

    summary.ShouldNotBeEmpty();
    summary.Count.ShouldBe(5);

    // Verify a string-value entry
    summary.ShouldContainKey("accountcode");
    var accountCode = summary["accountcode"];
    accountCode.Amount.ShouldBe(0.0m);
    accountCode.Currency.ShouldBeNull();
    accountCode.IsNull.ShouldBeFalse();
    accountCode.Timestamp.ShouldBe(1775169606L);
    accountCode.Value.ShouldBe("U1234567");
    accountCode.Severity.ShouldBe(0);

    // Verify a numeric entry
    summary.ShouldContainKey("netliquidation");
    var netLiq = summary["netliquidation"];
    netLiq.Amount.ShouldBe(1005165.5m);
    netLiq.Currency.ShouldBe("USD");
    netLiq.IsNull.ShouldBeFalse();
    netLiq.Value.ShouldBe("1005165.5");

    // Verify buying power entry
    summary.ShouldContainKey("buyingpower");
    summary["buyingpower"].Amount.ShouldBe(3984328.97m);

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetAccountSummary_ReturnsAllEntryFields"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add account summary success integration test"
```

---

### Task 9: Write Account Summary 401 Recovery Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the 401 recovery test for summary**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetAccountSummary_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/summary")
            .UsingGet())
        .InScenario("summary-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/summary")
            .UsingGet())
        .InScenario("summary-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-summary")));

    var summary = await _harness.Client.Portfolio.GetAccountSummaryAsync(
        "U1234567", TestContext.Current.CancellationToken);

    summary.ShouldNotBeEmpty();
    summary.ShouldContainKey("netliquidation");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetAccountSummary_401Recovery"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add account summary 401 recovery integration test"
```

---

### Task 10: Write Ledger Success Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the ledger success test**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetLedger_ReturnsAllFields()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/portfolio/U1234567/ledger",
        FixtureLoader.LoadBody("Portfolio", "GET-portfolio-ledger"));

    var ledger = await _harness.Client.Portfolio.GetLedgerAsync(
        "U1234567", TestContext.Current.CancellationToken);

    ledger.ShouldNotBeEmpty();
    ledger.Count.ShouldBe(2);
    ledger.ShouldContainKey("USD");
    ledger.ShouldContainKey("BASE");

    var usd = ledger["USD"];
    usd.CashBalance.ShouldBe(971869.6m);
    usd.NetLiquidationValue.ShouldBe(1005165.5m);
    usd.SettledCash.ShouldBe(971869.6m);
    usd.ExchangeRate.ShouldBe(1m);
    usd.SessionId.ShouldBe(1);
    usd.CorporateBondsMarketValue.ShouldBe(0.0m);
    usd.WarrantsMarketValue.ShouldBe(0.0m);
    usd.Interest.ShouldBe(2682.45m);
    usd.UnrealizedPnl.ShouldBe(151.57m);
    usd.StockMarketValue.ShouldBe(30613.4m);
    usd.MoneyFunds.ShouldBe(0.0m);
    usd.Currency.ShouldBe("USD");
    usd.RealizedPnl.ShouldBe(0.0m);
    usd.Funds.ShouldBe(0.0m);
    usd.AcctCode.ShouldBe("U1234567");
    usd.IssuerOptionsMarketValue.ShouldBe(0.0m);
    usd.Key.ShouldBe("LedgerList");
    usd.Timestamp.ShouldBe(1775169606L);
    usd.Severity.ShouldBe(0);
    usd.StockOptionMarketValue.ShouldBe(0.0m);
    usd.FuturesOnlyPnl.ShouldBe(0.0m);
    usd.TBondsMarketValue.ShouldBe(0.0m);
    usd.FutureOptionMarketValue.ShouldBe(0.0m);
    usd.CashBalanceFxSegment.ShouldBe(0.0m);
    usd.SecondKey.ShouldBe("USD");
    usd.TBillsMarketValue.ShouldBe(0.0m);
    usd.EndOfBundle.ShouldBe(1);
    usd.Dividends.ShouldBe(0.0m);
    usd.CryptocurrencyValue.ShouldBe(0.0m);
    usd.CommodityMarketValue.ShouldBe(0.0m);
    usd.FutureMarketValue.ShouldBe(0.0m);

    // Verify BASE entry exists with correct currency
    var baseEntry = ledger["BASE"];
    baseEntry.Currency.ShouldBe("BASE");
    baseEntry.SecondKey.ShouldBe("BASE");

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetLedger_ReturnsAllFields"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add ledger success integration test"
```

---

### Task 11: Write Ledger 401 Recovery Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the 401 recovery test for ledger**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetLedger_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/ledger")
            .UsingGet())
        .InScenario("ledger-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/portfolio/U1234567/ledger")
            .UsingGet())
        .InScenario("ledger-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-portfolio-ledger")));

    var ledger = await _harness.Client.Portfolio.GetLedgerAsync(
        "U1234567", TestContext.Current.CancellationToken);

    ledger.ShouldNotBeEmpty();
    ledger.ShouldContainKey("USD");

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetLedger_401Recovery"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add ledger 401 recovery integration test"
```

---

### Task 12: Write Partitioned PnL Success Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the PnL success test**

The recording shows `upnl` as empty `{}` (no active positions at capture time). The test verifies the empty dictionary deserializes correctly.

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetPartitionedPnl_ReturnsEmptyUpnl()
{
    _harness.StubAuthenticatedGet(
        "/v1/api/iserver/account/pnl/partitioned",
        FixtureLoader.LoadBody("Portfolio", "GET-iserver-account-pnl-partitioned"));

    var pnl = await _harness.Client.Portfolio.GetPartitionedPnlAsync(
        TestContext.Current.CancellationToken);

    pnl.ShouldNotBeNull();
    pnl.Upnl.ShouldNotBeNull();
    pnl.Upnl.ShouldBeEmpty();

    _harness.VerifyHandshakeOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetPartitionedPnl_ReturnsEmptyUpnl"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add partitioned PnL success integration test"
```

---

### Task 13: Write Partitioned PnL 401 Recovery Test

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs`

- [ ] **Step 1: Write the 401 recovery test for PnL**

Add to `PortfolioTests.cs`:

```csharp
[Fact]
public async Task GetPartitionedPnl_401Recovery_ReauthenticatesAndRetries()
{
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/pnl/partitioned")
            .UsingGet())
        .InScenario("pnl-401")
        .WillSetStateTo("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/account/pnl/partitioned")
            .UsingGet())
        .InScenario("pnl-401")
        .WhenStateIs("token-expired")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Portfolio", "GET-iserver-account-pnl-partitioned")));

    var pnl = await _harness.Client.Portfolio.GetPartitionedPnlAsync(
        TestContext.Current.CancellationToken);

    pnl.ShouldNotBeNull();
    pnl.Upnl.ShouldNotBeNull();

    _harness.VerifyReauthenticationOccurred();
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration --configuration Release --filter "GetPartitionedPnl_401Recovery"`

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioTests.cs
git commit -m "test: add partitioned PnL 401 recovery integration test"
```

---

### Task 14: Run Full Test Suite and Format Check

**Files:** None (verification only)

- [ ] **Step 1: Run all tests**

Run: `dotnet test /workspace/ibkr-conduit --configuration Release`

Expected: All tests pass (existing + 12 new = ~14 total in PortfolioTests).

- [ ] **Step 2: Run format check**

Run: `dotnet format /workspace/ibkr-conduit --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 3: Run build**

Run: `dotnet build /workspace/ibkr-conduit --configuration Release`

Expected: Build succeeds with 0 errors, 0 warnings.

---

## Test Inventory

| # | Test Name | Endpoint | Type |
|---|-----------|----------|------|
| 1 | `GetAccounts_ReturnsAllFields` | accounts | Success (existing) |
| 2 | `GetAccounts_401Recovery_ReauthenticatesAndRetries` | accounts | 401 Recovery (existing) |
| 3 | `GetPositions_ReturnsAllFields` | positions | Success |
| 4 | `GetPositions_401Recovery_ReauthenticatesAndRetries` | positions | 401 Recovery |
| 5 | `GetPositions_EmptyPage_ReturnsEmptyList` | positions | Empty page |
| 6 | `GetAccountSummary_ReturnsAllEntryFields` | summary | Success + large payload |
| 7 | `GetAccountSummary_401Recovery_ReauthenticatesAndRetries` | summary | 401 Recovery |
| 8 | `GetLedger_ReturnsAllFields` | ledger | Success |
| 9 | `GetLedger_401Recovery_ReauthenticatesAndRetries` | ledger | 401 Recovery |
| 10 | `GetPartitionedPnl_ReturnsEmptyUpnl` | PnL | Success |
| 11 | `GetPartitionedPnl_401Recovery_ReauthenticatesAndRetries` | PnL | 401 Recovery |

**Total: 11 tests** (2 existing + 9 new, since the "pagination with sort" scenario from the spec was dropped -- the `GetPositionsAsync` operations facade does not expose sort/direction parameters, so testing sorting would require calling the Refit interface directly which breaks the integration test pattern of going through `IIbkrClient`).

**Note on spec gap:** The spec listed "pagination with sort" as an additional scenario for positions. The `IPortfolioOperations.GetPositionsAsync` method only accepts `accountId` and `page` -- it does not pass through `sort` or `direction` to the underlying Refit call. If sort/direction support is needed through the operations facade, that would be a separate feature task to update `IPortfolioOperations` and `PortfolioOperations` first.
