# M4: Server Failure Resilience Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full-pipeline integration tests for server failure scenarios (503 retry, HTML error pages, connection timeout, 500 "Not ready" retry) using WireMock + the complete DI stack via `TestHarness`.

**Architecture:** All tests live in a new `ResilienceTests.cs` file alongside `AuthFailureTests.cs` in the `Pipeline/` directory. Each test exercises the full handler pipeline: `TokenRefreshHandler -> ErrorNormalizationHandler -> ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient -> WireMock`. Response sequences are controlled via WireMock scenarios (`.InScenario` / `.WillSetStateTo` / `.WhenStateIs`). To avoid 7+ second retry delays in tests, we add a `configureServices` callback to `TestHarness.CreateAsync` that lets tests replace the production Polly pipeline with a zero-delay version.

**Tech Stack:** xUnit v3, Shouldly, WireMock.Net, Polly v8, System.Text.Json

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `tests/IbkrConduit.Tests.Integration/TestHarness.cs` | Modify | Add optional `Action<IServiceCollection>?` parameter to allow DI overrides (e.g., zero-delay resilience pipeline) |
| `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs` | Create | 6 integration tests for scenarios 6-9 |

---

## Pipeline Behavior Summary (for test design)

Request path: `TokenRefreshHandler -> ErrorNormalizationHandler -> ResilienceHandler -> ... -> HttpClient -> WireMock`

Response path: `WireMock -> HttpClient -> ... -> ResilienceHandler -> ErrorNormalizationHandler -> TokenRefreshHandler`

Key interactions:
- **ResilienceHandler** retries 5xx, 408, 429 with exponential backoff. Max 3 retry attempts (4 total calls).
- **ErrorNormalizationHandler** runs *outside* ResilienceHandler. It processes the final response after all retries are exhausted. For non-success responses, it calls `TryParseErrorMessage(body)` which catches `JsonException` on non-JSON bodies, then throws `IbkrApiException`.
- For 503 on generic paths (like `/iserver/accounts`), `RemapStatusCode` returns 503 unchanged (no remapping rule applies).
- For 500 on generic paths, `RemapStatusCode` returns 500 unchanged.
- `HandleNonSuccess` for 503/500 throws `IbkrApiException` with the (possibly null) parsed error message.

---

### Task 1: Add `configureServices` Callback to `TestHarness`

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/TestHarness.cs`

**Why:** The production Polly pipeline uses 1s exponential backoff with jitter. A test with 3 retries would take ~7 seconds. Adding a DI override callback lets resilience tests replace the pipeline with a zero-delay version, keeping tests fast (~0s instead of ~7s).

- [ ] **Step 1: Add `configureServices` parameter to `CreateAsync` and `Initialize`**

In `tests/IbkrConduit.Tests.Integration/TestHarness.cs`, update the `CreateAsync` method signature and `Initialize` method:

```csharp
/// <param name="configureServices">Optional callback to customize the DI container before building (e.g., replace resilience pipeline).</param>
public static Task<TestHarness> CreateAsync(
    IbkrClientOptions? options = null,
    double tokenExpiryHours = 24,
    Action<IServiceCollection>? configureServices = null)
{
    var harness = new TestHarness();
    harness.Initialize(options, tokenExpiryHours, configureServices);
    return Task.FromResult(harness);
}
```

Update `Initialize`:

```csharp
private void Initialize(
    IbkrClientOptions? options = null,
    double tokenExpiryHours = 24,
    Action<IServiceCollection>? configureServices = null)
{
    // ... existing code up to AddIbkrClient ...

    // Allow test-specific DI overrides (e.g., zero-delay resilience pipeline)
    configureServices?.Invoke(services);

    _provider = services.BuildServiceProvider();
    Client = _provider.GetRequiredService<IIbkrClient>();
}
```

The `configureServices?.Invoke(services)` line goes **after** `services.AddIbkrClient(...)` and **before** `services.BuildServiceProvider()`. This lets callers replace any singleton registered by `AddIbkrClient` — the last registration wins for singletons.

- [ ] **Step 2: Verify existing tests still pass**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release`

Expected: All existing tests pass — the new parameter is optional with default `null`, so no callers are affected.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/TestHarness.cs
git commit -m "feat: add configureServices callback to TestHarness for DI overrides"
```

---

### Task 2: Scenario 6 — 503 Service Unavailable with Retry and Success

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** WireMock returns 503 twice, then 200. `ResilienceHandler` retries, eventually succeeds. The response reaches `ErrorNormalizationHandler` as a 200 (success path), so no exception is thrown.

- [ ] **Step 1: Create `ResilienceTests.cs` with the 503-retry-success test**

Create `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`:

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Polly;
using Polly.Retry;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

public class ResilienceTests : IAsyncDisposable
{
    private TestHarness? _harness;

    /// <summary>
    /// Creates a zero-delay Polly pipeline for fast test execution.
    /// Same retry logic as production (5xx, 408, 429, max 3 retries) but no backoff delay.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> CreateZeroDelayPipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests),
            })
            .Build();

    private Task<TestHarness> CreateHarnessWithZeroDelayResilience() =>
        TestHarness.CreateAsync(configureServices: services =>
        {
            services.AddSingleton(CreateZeroDelayPipeline());
        });

    [Fact]
    public async Task Request_503ThenSuccess_RetriesAndReturnsResult()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // First two calls return 503, third returns 200
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WillSetStateTo("first-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WhenStateIs("first-retry")
            .WillSetStateTo("second-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WhenStateIs("second-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
```

Note: The `using Microsoft.Extensions.DependencyInjection;` import is needed for the `AddSingleton` extension method. Add it to the usings block:

```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_503ThenSuccess_RetriesAndReturnsResult"`

Expected: PASS. WireMock returns 503 twice, ResilienceHandler retries, third call returns 200, and the Accounts response is deserialized correctly.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add 503 retry-and-success pipeline integration test"
```

---

### Task 3: Scenario 6b — 503 Exhausts All Retries

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** WireMock always returns 503. After 4 attempts (1 + 3 retries), `ResilienceHandler` gives up. `ErrorNormalizationHandler` receives the 503 and throws `IbkrApiException` with status 503 (no remapping for `/iserver/accounts`).

- [ ] **Step 1: Add the 503-exhausted test**

Add to `ResilienceTests.cs` (inside the class, before `DisposeAsync`):

```csharp
[Fact]
public async Task Request_503AllRetriesExhausted_ThrowsIbkrApiException()
{
    _harness = await CreateHarnessWithZeroDelayResilience();

    // All calls return 503 — retries will be exhausted
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .RespondWith(
            Response.Create()
                .WithStatusCode(503)
                .WithBody("Service Unavailable"));

    var ex = await Should.ThrowAsync<IbkrApiException>(
        _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken));

    ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.ServiceUnavailable);
}
```

Add the missing using at the top of the file:

```csharp
using IbkrConduit.Errors;
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_503AllRetriesExhausted_ThrowsIbkrApiException"`

Expected: PASS. After 4 attempts, `ErrorNormalizationHandler` throws `IbkrApiException` with status 503.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add 503 retry-exhausted pipeline integration test"
```

---

### Task 4: Scenario 7 — Non-JSON HTML Error Page on 500

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** WireMock always returns 500 with an HTML body (`<html>Service Unavailable</html>`). After retries are exhausted, `ErrorNormalizationHandler` receives the 500 with HTML. `TryParseErrorMessage` catches the `JsonException` from parsing HTML, returns `null`. The handler throws `IbkrApiException` with status 500 and a `null` `ErrorMessage`. The body is preserved in `RawResponseBody`. The key assertion: **it must not crash**.

- [ ] **Step 1: Add the HTML error page test**

Add to `ResilienceTests.cs`:

```csharp
[Fact]
public async Task Request_500WithHtmlBody_ThrowsIbkrApiExceptionWithoutCrashing()
{
    _harness = await CreateHarnessWithZeroDelayResilience();

    var htmlBody = "<html><body><h1>Service Unavailable</h1></body></html>";

    // All calls return 500 with HTML — not JSON
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .RespondWith(
            Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "text/html")
                .WithBody(htmlBody));

    var ex = await Should.ThrowAsync<IbkrApiException>(
        _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken));

    ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
    ex.ErrorMessage.ShouldBeNull();
    ex.RawResponseBody.ShouldContain("Service Unavailable");
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_500WithHtmlBody_ThrowsIbkrApiExceptionWithoutCrashing"`

Expected: PASS. `ErrorNormalizationHandler.TryParseErrorMessage` catches `JsonException` on the HTML body, returns `null`. The handler throws `IbkrApiException` normally.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add HTML error page (non-JSON 500) pipeline integration test"
```

---

### Task 5: Scenario 8 — Connection Timeout Produces Clean Error

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** WireMock adds a long delay (exceeding `HttpClient.Timeout`). The request times out, producing a `TaskCanceledException` (wrapping `TimeoutException`) or `HttpRequestException`. The test verifies a clean exception propagates — not an obscure stack trace or `NullReferenceException`.

**Approach:** Use WireMock's `.WithDelay(TimeSpan)` to make the server respond after a very long delay. Set `HttpClient.Timeout` to a short value. However, `TestHarness` doesn't expose `HttpClient.Timeout` configuration. Instead, use WireMock's response delay feature: configure a 60-second delay, but cancel the request after a short CancellationToken timeout. This produces an `OperationCanceledException` / `TaskCanceledException`.

Actually, simpler approach: WireMock can refuse connections by stopping the server mid-test. But that's flaky. Better: use WireMock `.WithDelay` + a short `CancellationTokenSource` timeout.

- [ ] **Step 1: Add the connection timeout test**

Add to `ResilienceTests.cs`:

```csharp
[Fact]
public async Task Request_ConnectionTimeout_ThrowsCleanException()
{
    _harness = await CreateHarnessWithZeroDelayResilience();

    // Server responds with a 30-second delay — longer than our cancellation timeout
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"accounts":["U1234567"]}""")
                .WithDelay(TimeSpan.FromSeconds(30)));

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

    var ex = await Should.ThrowAsync<TaskCanceledException>(
        _harness.Client.Accounts.GetAccountsAsync(cts.Token));

    // Verify it's a clean cancellation, not an obscure internal error
    ex.ShouldNotBeNull();
    ex.CancellationToken.IsCancellationRequested.ShouldBeTrue();
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_ConnectionTimeout_ThrowsCleanException"`

Expected: PASS. The `CancellationTokenSource` cancels after 500ms, producing a clean `TaskCanceledException`. The Polly pipeline propagates cancellation correctly without wrapping it in a confusing exception.

**Troubleshooting:** If the exception type is `OperationCanceledException` instead of `TaskCanceledException`, change the assertion to `Should.ThrowAsync<OperationCanceledException>` — `TaskCanceledException` inherits from `OperationCanceledException`, so either is acceptable. The key is that no `NullReferenceException`, `InvalidOperationException`, or other obscure error escapes.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add connection timeout clean-error pipeline integration test"
```

---

### Task 6: Scenario 9 — 500 "Not Ready" Retried Then Succeeds

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** First call returns `500` with body `{"error":"Not ready"}`. `ResilienceHandler` retries (it sees 500 = 5xx). Second call returns 200 with valid accounts JSON. `ErrorNormalizationHandler` processes the successful 200.

This is the "cache warm-up" pattern: some IBKR endpoints return `{"error":"Not ready"}` with 500 until their internal cache is populated.

- [ ] **Step 1: Add the "Not ready" retry test**

Add to `ResilienceTests.cs`:

```csharp
[Fact]
public async Task Request_500NotReadyThenSuccess_RetriesAndReturnsResult()
{
    _harness = await CreateHarnessWithZeroDelayResilience();

    // First call returns 500 "Not ready"
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .InScenario("not-ready")
        .WillSetStateTo("warmed-up")
        .RespondWith(
            Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":"Not ready"}"""));

    // Second call succeeds
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .InScenario("not-ready")
        .WhenStateIs("warmed-up")
        .RespondWith(
            Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

    var result = await _harness.Client.Accounts.GetAccountsAsync(
        TestContext.Current.CancellationToken);

    result.ShouldNotBeNull();
    result.Accounts.ShouldContain("U1234567");
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_500NotReadyThenSuccess_RetriesAndReturnsResult"`

Expected: PASS. ResilienceHandler sees 500, retries, gets 200 on the second attempt.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add 500 'Not ready' retry-and-success pipeline integration test"
```

---

### Task 7: Scenario 9b — 500 "Not Ready" Exhausts Retries

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`

**Scenario:** Server always returns `500` with `{"error":"Not ready"}`. After all retries exhausted, `ErrorNormalizationHandler` receives the 500 and throws `IbkrApiException`. The `ErrorMessage` should be `"Not ready"` since `TryParseErrorMessage` can parse the JSON.

- [ ] **Step 1: Add the "Not ready" exhausted test**

Add to `ResilienceTests.cs`:

```csharp
[Fact]
public async Task Request_500NotReadyAllRetriesExhausted_ThrowsWithErrorMessage()
{
    _harness = await CreateHarnessWithZeroDelayResilience();

    // All calls return 500 "Not ready"
    _harness.Server.Given(
        Request.Create()
            .WithPath("/v1/api/iserver/accounts")
            .UsingGet())
        .RespondWith(
            Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":"Not ready"}"""));

    var ex = await Should.ThrowAsync<IbkrApiException>(
        _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken));

    ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
    ex.ErrorMessage.ShouldBe("Not ready");
    ex.RawResponseBody.ShouldContain("Not ready");
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "Request_500NotReadyAllRetriesExhausted_ThrowsWithErrorMessage"`

Expected: PASS. `TryParseErrorMessage` successfully parses `{"error":"Not ready"}` and extracts `"Not ready"`. `IbkrApiException` is thrown with `ErrorMessage = "Not ready"`.

- [ ] **Step 3: Commit**

```
git add tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
git commit -m "test: add 500 'Not ready' retry-exhausted pipeline integration test"
```

---

### Task 8: Run Full Suite and Final Commit

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release`

Expected: All tests pass, including the 6 new resilience tests and all existing tests.

- [ ] **Step 2: Run lint check**

Run: `dotnet format tests/IbkrConduit.Tests.Integration --verify-no-changes`

Expected: No formatting issues. If there are issues, run `dotnet format tests/IbkrConduit.Tests.Integration` to fix them, then commit the formatting fix.

- [ ] **Step 3: Run full build**

Run: `dotnet build --configuration Release`

Expected: Zero warnings (TreatWarningsAsErrors is enabled).

---

## Test Summary

| # | Test Name | Scenario | Expected Behavior |
|---|---|---|---|
| 1 | `Request_503ThenSuccess_RetriesAndReturnsResult` | 503 x2, then 200 | ResilienceHandler retries, returns successful accounts response |
| 2 | `Request_503AllRetriesExhausted_ThrowsIbkrApiException` | 503 x4 (all fail) | After 4 attempts, ErrorNormalizationHandler throws IbkrApiException(503) |
| 3 | `Request_500WithHtmlBody_ThrowsIbkrApiExceptionWithoutCrashing` | 500 with HTML body x4 | TryParseErrorMessage catches JsonException, throws IbkrApiException with null ErrorMessage |
| 4 | `Request_ConnectionTimeout_ThrowsCleanException` | 30s delay + 500ms cancel | Clean TaskCanceledException, no obscure stack trace |
| 5 | `Request_500NotReadyThenSuccess_RetriesAndReturnsResult` | 500 "Not ready" then 200 | ResilienceHandler retries, returns successful accounts response |
| 6 | `Request_500NotReadyAllRetriesExhausted_ThrowsWithErrorMessage` | 500 "Not ready" x4 | IbkrApiException with ErrorMessage="Not ready" |

**Total new tests: 6** (covers all 4 scenarios, with exhausted-retry variants for scenarios 6 and 9)
