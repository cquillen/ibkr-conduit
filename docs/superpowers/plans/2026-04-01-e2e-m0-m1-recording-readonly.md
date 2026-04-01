# E2E Test Suite M0+M1: Recording Infrastructure & Read-Only Scenarios — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build response recording infrastructure and implement 3 read-only E2E scenarios covering 31 endpoints against a real IBKR paper account.

**Architecture:** RecordingDelegatingHandler injected via IHttpMessageHandlerBuilderFilter captures request/response pairs to WireMock-compatible JSON files. E2E tests use the full AddIbkrClient DI pipeline with OAuthCredentialsFactory.FromEnvironment(). All tests gated by [EnvironmentFact("IBKR_CONSUMER_KEY")] and serialized via [Collection("IBKR E2E")].

**Tech Stack:** xUnit v3, Shouldly, IHttpClientFactory, System.Text.Json, Refit

---

## Branch

`feat/e2e-m0-m1-recording-readonly`

## File Inventory

```
tests/IbkrConduit.Tests.Integration/
  Recording/
    RecordingContext.cs
    RecordingDelegatingHandler.cs
    RecordingHandlerFilter.cs
  E2E/
    E2eScenarioBase.cs
    Scenario01_AccountDiscoveryTests.cs
    Scenario02_ContractResearchTests.cs
    Scenario03_MarketDataScannerTests.cs

tests/IbkrConduit.Tests.Unit/
  Recording/
    RecordingContextTests.cs
    RecordingDelegatingHandlerTests.cs

.gitignore  (append Recordings/ entry)
Directory.Packages.props  (add Microsoft.Extensions.Http if not already present)
tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj  (add Microsoft.Extensions.Http package ref)
```

## Conventions Reminder

- xUnit v3, Shouldly assertions (NOT Assert)
- Test naming: `MethodName_Scenario_ExpectedResult` or `ScenarioName_Step_ExpectedResult`
- `CancellationToken` via `TestContext.Current.CancellationToken`
- File-scoped namespaces, `var` for all variables, nullable enabled
- `[EnvironmentFact("IBKR_CONSUMER_KEY")]` gates E2E tests
- `[Collection("IBKR E2E")]` serializes all E2E tests
- E2E DI: `OAuthCredentialsFactory.FromEnvironment()` → `services.AddIbkrClient(creds)` → `provider.GetRequiredService<IIbkrClient>()`
- `IIbkrSessionApi` is internal but accessible via `InternalsVisibleTo` from the integration test project; resolvable from DI
- Never chain bash commands — one command per Bash tool call
- Build: `dotnet build /workspace/ibkr-conduit --configuration Release`
- Test: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release`
- Unit test: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release`

---

## Task 0: Project Setup & .gitignore

**Files:**
- `/workspace/ibkr-conduit/.gitignore`
- `/workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`

**Steps:**

- [ ] 0.1: Append `**/Recordings/` to `.gitignore` so recorded response files are never committed.

- [ ] 0.2: Add `<PackageReference Include="Microsoft.Extensions.Http" />` to the integration test `.csproj` if not already present. This is needed for `IHttpMessageHandlerBuilderFilter`. Note: the version is already defined in `Directory.Packages.props` at `10.0.5`.

- [ ] 0.3: Verify the project builds cleanly:
  ```
  dotnet build /workspace/ibkr-conduit --configuration Release
  ```

---

## Task 1: RecordingContext

**File:** `tests/IbkrConduit.Tests.Integration/Recording/RecordingContext.cs`

**Steps:**

- [ ] 1.1: **RED** — Write unit test `tests/IbkrConduit.Tests.Unit/Recording/RecordingContextTests.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Unit.Recording;

  public class RecordingContextTests
  {
      [Fact]
      public void NextStep_ReturnsIncrementingValues()

      [Fact]
      public void NextStep_WhenCalledConcurrently_ProducesUniqueValues()

      [Fact]
      public void Reset_ResetsCounterAndSetsScenarioName()

      [Fact]
      public void ScenarioName_DefaultsToNull()
  }
  ```

  - `NextStep_ReturnsIncrementingValues`: call `NextStep()` 3 times, verify returns 1, 2, 3.
  - `NextStep_WhenCalledConcurrently_ProducesUniqueValues`: launch 100 tasks calling `NextStep()`, collect results, verify all unique and count == 100.
  - `Reset_ResetsCounterAndSetsScenarioName`: call `NextStep()` twice, then `Reset("new-scenario")`, verify `ScenarioName == "new-scenario"` and next `NextStep()` returns 1.
  - `ScenarioName_DefaultsToNull`: new instance has `ScenarioName == null`.

- [ ] 1.2: Run tests, verify they fail (class does not exist).

- [ ] 1.3: **GREEN** — Create `RecordingContext.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Integration.Recording;

  /// <summary>
  /// Thread-safe context for tracking the current recording scenario and step counter.
  /// </summary>
  public sealed class RecordingContext
  {
      private int _stepCounter;

      /// <summary>
      /// The name of the currently active recording scenario, or null if not recording.
      /// </summary>
      public string? ScenarioName { get; private set; }

      /// <summary>
      /// Returns the next step number (1-based, thread-safe).
      /// </summary>
      public int NextStep() => Interlocked.Increment(ref _stepCounter);

      /// <summary>
      /// Resets the counter and sets a new scenario name.
      /// </summary>
      public void Reset(string scenarioName)
      {
          ScenarioName = scenarioName;
          Interlocked.Exchange(ref _stepCounter, 0);
      }
  }
  ```

- [ ] 1.4: Run tests, verify all 4 pass.

---

## Task 2: RecordingDelegatingHandler

**File:** `tests/IbkrConduit.Tests.Integration/Recording/RecordingDelegatingHandler.cs`

**Steps:**

- [ ] 2.1: **RED** — Write unit test `tests/IbkrConduit.Tests.Unit/Recording/RecordingDelegatingHandlerTests.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Unit.Recording;

  public class RecordingDelegatingHandlerTests
  {
      [Fact]
      public async Task SendAsync_WhenRecordingActive_WritesJsonFile()

      [Fact]
      public async Task SendAsync_WhenRecordingInactive_DoesNotWriteFile()

      [Fact]
      public async Task SendAsync_PassesThroughResponseUnchanged()

      [Fact]
      public async Task SendAsync_SanitizesAuthorizationHeader()

      [Fact]
      public async Task SendAsync_SanitizesOAuthTokenInQueryString()

      [Fact]
      public async Task SendAsync_SanitizesCookieHeader()

      [Fact]
      public async Task SendAsync_FileNameFollowsStepMethodSlugPattern()

      [Fact]
      public async Task SendAsync_WritesWireMockCompatibleJsonStructure()

      [Fact]
      public async Task SendAsync_BuffersResponseBodyForDownstreamConsumers()
  }
  ```

  Test strategy: use a simple `DelegatingHandler` as inner handler that returns a canned `HttpResponseMessage`. Write to a temp directory. Verify file content after each call.

  Key test details:
  - `SendAsync_WhenRecordingActive_WritesJsonFile`: set `RecordingContext.Reset("test-scenario")`, send GET to `/v1/api/portfolio/accounts`, verify file exists at `{tempDir}/test-scenario/001-GET-portfolio-accounts.json`.
  - `SendAsync_WhenRecordingInactive_DoesNotWriteFile`: leave `ScenarioName` null, verify no files written.
  - `SendAsync_PassesThroughResponseUnchanged`: verify the response body returned to the caller matches the original response body.
  - `SendAsync_SanitizesAuthorizationHeader`: set `Authorization: Bearer secret`, verify the written JSON has `"REDACTED"` for the auth header.
  - `SendAsync_SanitizesOAuthTokenInQueryString`: include `?oauth_token=secret123` in the URL, verify written JSON has `oauth_token=REDACTED`.
  - `SendAsync_SanitizesCookieHeader`: set `Cookie: api=secretcookie`, verify written JSON has `"api=REDACTED"`.
  - `SendAsync_FileNameFollowsStepMethodSlugPattern`: verify step 1 → `001-`, step 2 → `002-`, method is uppercase, slug is derived from path with `/v1/api/` prefix stripped and slashes replaced by hyphens.
  - `SendAsync_WritesWireMockCompatibleJsonStructure`: parse the written file as JSON, verify it has `Request.Path`, `Request.Methods`, `Response.StatusCode`, `Response.Body`, `Metadata.Scenario`, `Metadata.Step`, `Metadata.RecordedAt`.
  - `SendAsync_BuffersResponseBodyForDownstreamConsumers`: read the response body after handler returns, verify it is complete and correct (not empty or consumed).

- [ ] 2.2: Run tests, verify they fail.

- [ ] 2.3: **GREEN** — Create `RecordingDelegatingHandler.cs`:

  Constructor parameters:
  - `RecordingContext context`
  - `string outputDirectory`

  `SendAsync` implementation:
  1. Call `base.SendAsync(request, cancellationToken)` to get the response.
  2. If `context.ScenarioName` is null, return response immediately (no-op).
  3. Read request body (if any) via `request.Content?.ReadAsStringAsync()`.
  4. Read response body via `response.Content.ReadAsStringAsync()`.
  5. Reconstruct response content: `response.Content = new StringContent(responseBody, Encoding.UTF8, mediaType)`.
  6. Build sanitized request record (strip Authorization, Cookie, oauth_token from query).
  7. Build WireMock-compatible JSON object:
     ```json
     {
       "Request": {
         "Path": "/v1/api/portfolio/accounts",
         "Methods": ["GET"],
         "Body": null
       },
       "Response": {
         "StatusCode": 200,
         "Headers": { "Content-Type": "application/json" },
         "Body": "..."
       },
       "Metadata": {
         "Scenario": "scenario-01-session-discovery",
         "Step": 1,
         "RecordedAt": "2026-04-01T12:00:00Z"
       }
     }
     ```
  8. Derive file name: `{step:D3}-{METHOD}-{slug}.json` where slug = path with `/v1/api/` stripped, slashes → hyphens, trimmed.
  9. Create scenario directory if it doesn't exist.
  10. Write JSON file with `JsonSerializer.Serialize` using `JsonSerializerOptions { WriteIndented = true }`.
  11. Return the response.

  Sanitization helper method:
  - Replace `Authorization` header value with `"REDACTED"`.
  - Replace `Cookie` header value with `"api=REDACTED"`.
  - In the URL path/query, replace `oauth_token=...&` or `oauth_token=...$` with `oauth_token=REDACTED`.
  - Replace `oauth_signature=...` with `oauth_signature=REDACTED`.

- [ ] 2.4: Run tests, verify all pass.

- [ ] 2.5: **REFACTOR** — Review handler for clarity. Ensure no allocation waste in the no-op path.

---

## Task 3: RecordingHandlerFilter

**File:** `tests/IbkrConduit.Tests.Integration/Recording/RecordingHandlerFilter.cs`

**Steps:**

- [ ] 3.1: **RED** — Add unit tests to `RecordingDelegatingHandlerTests.cs` (or a separate `RecordingHandlerFilterTests.cs`):

  ```csharp
  namespace IbkrConduit.Tests.Unit.Recording;

  public class RecordingHandlerFilterTests
  {
      [Fact]
      public void Configure_WhenRecordingEnabled_WrapsWithRecordingHandler()

      [Fact]
      public void Configure_WhenRecordingDisabled_PassesThrough()
  }
  ```

  - `Configure_WhenRecordingEnabled_WrapsWithRecordingHandler`: create filter with recording enabled, call `Configure`, verify the outermost handler is a `RecordingDelegatingHandler`.
  - `Configure_WhenRecordingDisabled_PassesThrough`: create filter with recording disabled, call `Configure`, verify no `RecordingDelegatingHandler` in the chain.

  Note: The filter implements `IHttpMessageHandlerBuilderFilter`. Testing it requires creating an `HttpMessageHandlerBuilder` mock or using a real one. Since `IHttpMessageHandlerBuilderFilter.Configure` takes `Action<HttpMessageHandlerBuilder>` and returns `Action<HttpMessageHandlerBuilder>`, we can test the returned action's behavior.

- [ ] 3.2: Run tests, verify they fail.

- [ ] 3.3: **GREEN** — Create `RecordingHandlerFilter.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Integration.Recording;

  /// <summary>
  /// Injects <see cref="RecordingDelegatingHandler"/> into all HttpClient pipelines
  /// when response recording is enabled via IBKR_RECORD_RESPONSES environment variable.
  /// </summary>
  public sealed class RecordingHandlerFilter : IHttpMessageHandlerBuilderFilter
  {
      private readonly RecordingContext _context;
      private readonly bool _enabled;
      private readonly string _outputDirectory;

      public RecordingHandlerFilter(RecordingContext context, string? outputDirectory = null)
      {
          _context = context;
          _enabled = string.Equals(
              Environment.GetEnvironmentVariable("IBKR_RECORD_RESPONSES"),
              "true", StringComparison.OrdinalIgnoreCase);
          _outputDirectory = outputDirectory
              ?? Path.Combine(AppContext.BaseDirectory, "Recordings");
      }

      public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
      {
          if (!_enabled)
          {
              return next;
          }

          return builder =>
          {
              next(builder);
              builder.AdditionalHandlers.Insert(0,
                  new RecordingDelegatingHandler(_context, _outputDirectory));
          };
      }
  }
  ```

- [ ] 3.4: Run tests, verify all pass.

- [ ] 3.5: Verify full build:
  ```
  dotnet build /workspace/ibkr-conduit --configuration Release
  ```

---

## Task 4: E2eScenarioBase

**File:** `tests/IbkrConduit.Tests.Integration/E2E/E2eScenarioBase.cs`

**Steps:**

- [ ] 4.1: Create `E2eScenarioBase.cs`. No TDD for this class — it is pure DI wiring with no branching logic. Mark it `[ExcludeFromCodeCoverage]`.

  ```csharp
  namespace IbkrConduit.Tests.Integration.E2E;

  /// <summary>
  /// Base class for E2E scenario tests. Provides DI setup, IIbkrClient creation,
  /// and optional response recording infrastructure.
  /// </summary>
  [ExcludeFromCodeCoverage]
  public abstract class E2eScenarioBase : IAsyncDisposable
  {
      private ServiceProvider? _provider;
      private IbkrOAuthCredentials? _credentials;
      private RecordingContext? _recordingContext;

      /// <summary>
      /// Creates the full DI pipeline and returns the IIbkrClient facade.
      /// Also returns the ServiceProvider for resolving internal APIs (e.g., IIbkrSessionApi).
      /// </summary>
      protected (ServiceProvider Provider, IIbkrClient Client) CreateClient()
      {
          _credentials = OAuthCredentialsFactory.FromEnvironment();
          var services = new ServiceCollection();
          services.AddLogging();
          services.AddIbkrClient(_credentials);

          // Register recording infrastructure if IBKR_RECORD_RESPONSES=true
          if (string.Equals(
              Environment.GetEnvironmentVariable("IBKR_RECORD_RESPONSES"),
              "true", StringComparison.OrdinalIgnoreCase))
          {
              _recordingContext = new RecordingContext();
              services.AddSingleton(_recordingContext);
              services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
                  new RecordingHandlerFilter(_recordingContext));
          }

          _provider = services.BuildServiceProvider();
          var client = _provider.GetRequiredService<IIbkrClient>();
          return (_provider, client);
      }

      /// <summary>
      /// Begins recording API interactions under the given scenario name.
      /// </summary>
      protected void StartRecording(string scenarioName)
      {
          _recordingContext?.Reset(scenarioName);
      }

      /// <summary>
      /// Stops recording (sets scenario name to null so handler becomes no-op).
      /// </summary>
      protected void StopRecording()
      {
          // Setting ScenarioName to null is not directly supported by Reset,
          // but the handler checks for null. We can leave it as-is —
          // recording simply stops when the test ends.
      }

      /// <summary>
      /// Convenience accessor for the CancellationToken from the test context.
      /// </summary>
      protected static CancellationToken CT => TestContext.Current.CancellationToken;

      public async ValueTask DisposeAsync()
      {
          if (_provider is not null)
          {
              await _provider.DisposeAsync();
          }

          _credentials?.Dispose();
          GC.SuppressFinalize(this);
      }
  }
  ```

- [ ] 4.2: Verify build succeeds:
  ```
  dotnet build /workspace/ibkr-conduit --configuration Release
  ```

---

## Task 5: Scenario 1 — Account Discovery & Session Validation

**File:** `tests/IbkrConduit.Tests.Integration/E2E/Scenario01_AccountDiscoveryTests.cs`

**Endpoints covered (11):** `oauth/live_session_token`, `ssodh/init`, `tickle`, `questions/suppress`, `sso/validate`, `iserver/auth/status`, `iserver/accounts`, `iserver/account/search`, `iserver/account/{id}`, `iserver/account` (switch), `iserver/dynaccount`, `questions/suppress/reset`

**Steps:**

- [ ] 5.1: Create `Scenario01_AccountDiscoveryTests.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Integration.E2E;

  [Collection("IBKR E2E")]
  public class Scenario01_AccountDiscoveryTests : E2eScenarioBase
  {
  ```

- [ ] 5.2: Write happy path test:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task AccountDiscovery_FullWorkflow_ExercisesAllSessionAndAccountEndpoints()
  ```

  Test steps:
  1. `var (provider, client) = CreateClient();`
  2. `StartRecording("scenario-01-account-discovery");`
  3. **Trigger session init** (covers LST, ssodh/init, tickle, suppress):
     `var accountsResponse = await client.Accounts.GetAccountsAsync(CT);`
     - Verify: `accountsResponse.ShouldNotBeNull()`, `accountsResponse.Accounts.ShouldNotBeEmpty()`.
     - Capture: `var accountId = accountsResponse.Accounts[0];`
  4. **Validate SSO** — resolve internal API:
     `var sessionApi = provider.GetRequiredService<IIbkrSessionApi>();`
     `var ssoResult = await sessionApi.ValidateSsoAsync(CT);`
     - Verify: `ssoResult.Result.ShouldBeTrue()`.
  5. **Check auth status**:
     `var authStatus = await sessionApi.GetAuthStatusAsync(CT);`
     - Verify: `authStatus.Authenticated.ShouldBeTrue()`.
  6. **Search accounts** by first 2 chars:
     `var pattern = accountId[..2];`
     `var searchResults = await client.Accounts.SearchAccountsAsync(pattern, CT);`
     - Verify: `searchResults.ShouldNotBeEmpty()`.
     - Verify: `searchResults.ShouldContain(r => r.AccountId == accountId)`.
  7. **Get account info**:
     `var accountInfo = await client.Accounts.GetAccountInfoAsync(accountId, CT);`
     - Verify: `accountInfo.AccountId.ShouldBe(accountId)`.
  8. **Switch account** (switch to current — idempotent):
     `var switchResult = await client.Accounts.SwitchAccountAsync(accountId, CT);`
     - Verify: `switchResult.Set.ShouldBeTrue()`.
  9. **Set dynamic account**:
     `var dynResult = await client.Accounts.SetDynAccountAsync(accountId, CT);`
     - Verify: `dynResult.Set.ShouldBeTrue()`.
  10. **Reset suppressed questions**:
      `var resetResult = await sessionApi.ResetSuppressedQuestionsAsync(CT);`
      - Verify: `resetResult.ShouldNotBeNull()`.

- [ ] 5.3: Write error case tests:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task SearchAccounts_NonExistentPattern_ReturnsEmptyOrNoMatch()
  ```
  - Call `client.Accounts.SearchAccountsAsync("ZZZZZ", CT)`.
  - Verify: result is empty or contains no match.
  - IBKR may return empty list or may throw — follow quirks process.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetAccountInfo_InvalidAccountId_ThrowsApiException()
  ```
  - Call `client.Accounts.GetAccountInfoAsync("INVALID999", CT)`.
  - Expect: `Should.ThrowAsync<Refit.ApiException>()` or equivalent error.
  - Follow quirks process if IBKR returns 200 with error body.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task SwitchAccount_InvalidAccountId_ThrowsApiException()
  ```
  - Call `client.Accounts.SwitchAccountAsync("INVALID999", CT)`.
  - Expect: `Should.ThrowAsync<Refit.ApiException>()` or equivalent error.

- [ ] 5.4: Run the full integration test suite to verify existing WireMock tests still pass:
  ```
  dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release
  ```
  (E2E tests will be skipped without IBKR_CONSUMER_KEY.)

---

## Task 6: Scenario 2 — Contract Research

**File:** `tests/IbkrConduit.Tests.Integration/E2E/Scenario02_ContractResearchTests.cs`

**Endpoints covered (12):** `secdef/search`, `contract/{conid}/info`, `contract/rules`, `trsrv/secdef`, `trsrv/secdef/schedule`, `secdef/strikes`, `secdef/info`, `trsrv/stocks`, `trsrv/futures`, `trsrv/all-conids`, `currency/pairs`, `exchangerate`

**Steps:**

- [ ] 6.1: Create `Scenario02_ContractResearchTests.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Integration.E2E;

  [Collection("IBKR E2E")]
  public class Scenario02_ContractResearchTests : E2eScenarioBase
  {
  ```

- [ ] 6.2: Write happy path test:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task ContractResearch_FullWorkflow_ExercisesAllContractEndpoints()
  ```

  Test steps:
  1. `var (provider, client) = CreateClient();`
  2. `StartRecording("scenario-02-contract-research");`
  3. **Search by symbol "AAPL"**:
     `var searchResults = await client.Contracts.SearchBySymbolAsync("AAPL", CT);`
     - Verify: `searchResults.ShouldNotBeEmpty()`.
     - Capture: `var aaplConid = searchResults.First(r => r.Symbol == "AAPL").Conid;`
  4. **Get contract details**:
     `var details = await client.Contracts.GetContractDetailsAsync(aaplConid.ToString(), CT);`
     - Verify: `details.ShouldNotBeNull()`.
  5. **Get trading rules**:
     `var rules = await client.Contracts.GetTradingRulesAsync(new TradingRulesRequest(aaplConid, null, null, null, null), CT);`
     - Verify: `rules.ShouldNotBeNull()`.
  6. **Get security definitions by conid**:
     `var secDefs = await client.Contracts.GetSecurityDefinitionsByConidAsync(aaplConid.ToString(), CT);`
     - Verify: `secDefs.ShouldNotBeNull()`.
  7. **Get trading schedule**:
     `var schedule = await client.Contracts.GetTradingScheduleAsync("STK", "AAPL", aaplConid.ToString(), cancellationToken: CT);`
     - Verify: `schedule.ShouldNotBeEmpty()`.
  8. **Get option strikes** — compute nearest month as `DateTime.UtcNow.ToString("yyyyMM")`:
     `var nearestMonth = DateTime.UtcNow.ToString("yyyyMM");`
     `var strikes = await client.Contracts.GetOptionStrikesAsync(aaplConid.ToString(), "OPT", nearestMonth, cancellationToken: CT);`
     - Verify: `strikes.ShouldNotBeNull()`.
     - Note: if current month has no options, try next month. Follow quirks process.
  9. **Get security definition info** for options:
     `var secDefInfo = await client.Contracts.GetSecurityDefinitionInfoAsync(aaplConid.ToString(), "OPT", nearestMonth, cancellationToken: CT);`
     - Verify: `secDefInfo.ShouldNotBeEmpty()`.
  10. **Get stocks by symbol**:
      `var stocks = await client.Contracts.GetStocksBySymbolAsync("AAPL", CT);`
      - Verify: `stocks.ShouldContainKey("AAPL")`.
  11. **Get futures by symbol**:
      `var futures = await client.Contracts.GetFuturesBySymbolAsync("ES", CT);`
      - Verify: `futures.ShouldNotBeEmpty()`.
  12. **Get all conids by exchange**:
      `var conids = await client.Contracts.GetAllConidsByExchangeAsync("NASDAQ", CT);`
      - Verify: `conids.ShouldNotBeEmpty()`.
  13. **Get currency pairs**:
      `var pairs = await client.Contracts.GetCurrencyPairsAsync("USD", CT);`
      - Verify: `pairs.ShouldNotBeEmpty()`.
  14. **Get exchange rate**:
      `var rate = await client.Contracts.GetExchangeRateAsync("USD", "EUR", CT);`
      - Verify: `rate.ShouldNotBeNull()`.
      - Verify: rate value is positive (exact field depends on `ExchangeRateResponse` shape).

- [ ] 6.3: Write error case tests:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task SearchBySymbol_NonExistentSymbol_ReturnsEmpty()
  ```
  - Call `client.Contracts.SearchBySymbolAsync("ZZZZNOTREAL", CT)`.
  - Verify: result is empty.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetContractDetails_InvalidConid_ThrowsOrReturnsError()
  ```
  - Call `client.Contracts.GetContractDetailsAsync("999999999", CT)`.
  - Expect: `Should.ThrowAsync<Refit.ApiException>()` or error body. Follow quirks process.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetTradingRules_ZeroConid_ThrowsOrReturnsError()
  ```
  - Call `client.Contracts.GetTradingRulesAsync(new TradingRulesRequest(0, null, null, null, null), CT)`.
  - Expect: error response. Follow quirks process.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetExchangeRate_SameCurrency_ReturnsOneOrError()
  ```
  - Call `client.Contracts.GetExchangeRateAsync("USD", "USD", CT)`.
  - Expect: rate == 1.0 or an error. Follow quirks process.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetOptionStrikes_InvalidMonth_ReturnsEmptyOrError()
  ```
  - Call `client.Contracts.GetOptionStrikesAsync(aaplConid.ToString(), "OPT", "190001", cancellationToken: CT)`.
  - Expect: empty strikes or error. Follow quirks process.
  - Note: this test needs an AAPL conid, so either inline a search or use a known conid (265598 for AAPL on NASDAQ). Prefer searching to avoid hardcoding.

- [ ] 6.4: Run integration tests:
  ```
  dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release
  ```

---

## Task 7: Scenario 3 — Market Data & Scanners

**File:** `tests/IbkrConduit.Tests.Integration/E2E/Scenario03_MarketDataScannerTests.cs`

**Endpoints covered (8):** `marketdata/snapshot` (x2 for pre-flight), `marketdata/history`, `scanner/params`, `scanner/run`, `hmds/scanner`, `md/regsnapshot` (opt-in), `marketdata/unsubscribe`, `marketdata/unsubscribeall`

**Steps:**

- [ ] 7.1: Create `Scenario03_MarketDataScannerTests.cs`:

  ```csharp
  namespace IbkrConduit.Tests.Integration.E2E;

  [Collection("IBKR E2E")]
  public class Scenario03_MarketDataScannerTests : E2eScenarioBase
  {
  ```

- [ ] 7.2: Write happy path test:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task MarketDataAndScanners_FullWorkflow_ExercisesAllMarketDataEndpoints()
  ```

  Test steps:
  1. `var (provider, client) = CreateClient();`
  2. `StartRecording("scenario-03-market-data-scanners");`
  3. **Look up SPY conid**:
     `var searchResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);`
     `var spyConid = searchResults.First(r => r.Symbol == "SPY").Conid;`
  4. **Get snapshot — pre-flight** (first call may return empty/partial):
     `var snapshot1 = await client.MarketData.GetSnapshotAsync([spyConid], ["31", "84"], CT);`
     - Verify: `snapshot1.ShouldNotBeNull()` (may be empty — that's expected for pre-flight).
  5. **Wait for pre-flight to complete**:
     `await Task.Delay(3000, CT);`
  6. **Get snapshot — data populated**:
     `var snapshot2 = await client.MarketData.GetSnapshotAsync([spyConid], ["31", "84"], CT);`
     - Verify: `snapshot2.ShouldNotBeEmpty()`.
     - Note: The `MarketDataOperations.GetSnapshotAsync` may already handle pre-flight internally. If so, step 4 might return data. Adjust based on actual behavior.
  7. **Get historical bars**:
     `var history = await client.MarketData.GetHistoryAsync(spyConid, "5d", "1d", cancellationToken: CT);`
     - Verify: `history.ShouldNotBeNull()`.
     - Verify: history contains bar data (exact field depends on `HistoricalDataResponse` shape).
  8. **Get scanner parameters**:
     `var scanParams = await client.MarketData.GetScannerParametersAsync(CT);`
     - Verify: `scanParams.ShouldNotBeNull()`.
  9. **Run iserver scanner**:
     `var scanResult = await client.MarketData.RunScannerAsync(new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", null), CT);`
     - Verify: `scanResult.ShouldNotBeNull()`.
     - Verify: `scanResult.Contracts.ShouldNotBeEmpty()`.
  10. **Run HMDS scanner**:
      `var hmdsResult = await client.MarketData.RunHmdsScannerAsync(new HmdsScannerRequest("STK", "STK.US.MAJOR", "TOP_PERC_GAIN", "STK", 10, null), CT);`
      - Verify: `hmdsResult.ShouldNotBeNull()`.
  11. **(Optional) Regulatory snapshot** — gated by IBKR_ALLOW_PAID_ENDPOINTS:
      ```csharp
      if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IBKR_ALLOW_PAID_ENDPOINTS")))
      {
          var regSnapshot = await client.MarketData.GetRegulatorySnapshotAsync(spyConid, CT);
          regSnapshot.ShouldNotBeNull();
      }
      ```
  12. **Unsubscribe SPY**:
      `var unsubResult = await client.MarketData.UnsubscribeAsync(spyConid, CT);`
      - Verify: `unsubResult.ShouldNotBeNull()`.
  13. **Unsubscribe all** (also serves as cleanup):
      `var unsubAllResult = await client.MarketData.UnsubscribeAllAsync(CT);`
      - Verify: `unsubAllResult.ShouldNotBeNull()`.

- [ ] 7.3: Write error case tests:

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetSnapshot_InvalidConid_ReturnsEmptyOrError()
  ```
  - Call `client.MarketData.GetSnapshotAsync([0], ["31"], CT)`.
  - Verify: result is empty, contains error fields, or throws. Follow quirks process.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task GetHistory_InvalidPeriod_ThrowsOrReturnsError()
  ```
  - Look up SPY conid first.
  - Call `client.MarketData.GetHistoryAsync(spyConid, "0min", "1d", cancellationToken: CT)`.
  - Expect: `Should.ThrowAsync<Refit.ApiException>()` or error body.

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task Unsubscribe_NeverSubscribedConid_HandlesGracefully()
  ```
  - Call `client.MarketData.UnsubscribeAsync(999999999, CT)`.
  - Verify: does not throw, returns a response (may indicate not subscribed).

  ```csharp
  [EnvironmentFact("IBKR_CONSUMER_KEY")]
  public async Task RunScanner_InvalidScanType_ThrowsOrReturnsError()
  ```
  - Call `client.MarketData.RunScannerAsync(new ScannerRequest("STK", "NONEXISTENT_SCAN", "STK.US.MAJOR", null), CT)`.
  - Expect: error response. Follow quirks process.

- [ ] 7.4: Ensure all market data cleanup happens in finally blocks:

  ```csharp
  try
  {
      // ... all market data steps ...
  }
  finally
  {
      await client.MarketData.UnsubscribeAllAsync(CT);
  }
  ```

- [ ] 7.5: Run full test suite:
  ```
  dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release
  ```

---

## Task 8: Final Verification & Cleanup

- [ ] 8.1: Run full build:
  ```
  dotnet build /workspace/ibkr-conduit --configuration Release
  ```

- [ ] 8.2: Run all unit tests:
  ```
  dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release
  ```

- [ ] 8.3: Run all integration tests (WireMock tests only — E2E skipped without env vars):
  ```
  dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj --configuration Release
  ```

- [ ] 8.4: Run lint:
  ```
  dotnet format /workspace/ibkr-conduit --verify-no-changes
  ```

- [ ] 8.5: Verify no secrets or credential files are staged:
  ```
  git diff --cached --name-only
  ```

- [ ] 8.6: Review all new files for:
  - File-scoped namespaces
  - `var` for all variable declarations
  - XML doc comments on all public types/methods
  - `CancellationToken` passed through all async chains
  - No `Assert.*` — only Shouldly
  - LF line endings

---

## Dependency Graph

```
Task 0 (setup)
  ├── Task 1 (RecordingContext)
  │     └── Task 2 (RecordingDelegatingHandler)
  │           └── Task 3 (RecordingHandlerFilter)
  │                 └── Task 4 (E2eScenarioBase)
  │                       ├── Task 5 (Scenario 1)
  │                       ├── Task 6 (Scenario 2)
  │                       └── Task 7 (Scenario 3)
  └────────────────────────── Task 8 (Final Verification)
```

Tasks 5, 6, and 7 are independent and can be implemented in parallel once Task 4 is complete.

---

## Risk & Mitigation

| Risk | Mitigation |
|---|---|
| IBKR paper account returns unexpected status codes | Follow quirks verification process from spec. Comment discoveries. Use `[Fact(Skip = "...")]` for ambiguous cases. |
| Option strikes endpoint returns empty for current month | Try current month + 1. AAPL always has options so at least one month will work. |
| Scanner endpoints return empty during off-hours | `TOP_PERC_GAIN` should have data even outside RTH. If empty, verify shape only. |
| Market data pre-flight timing | `GetSnapshotAsync` in `MarketDataOperations` may already handle pre-flight with retry. If so, remove manual delay. |
| `IIbkrSessionApi` not resolvable from DI | Already confirmed: registered by `AddRefitClient<IIbkrSessionApi>()` in `ServiceCollectionExtensions`, and `InternalsVisibleTo` grants access from integration tests. |
| Recording handler breaks response for downstream consumers | Unit test `SendAsync_BuffersResponseBodyForDownstreamConsumers` explicitly verifies response body remains readable after recording. |
