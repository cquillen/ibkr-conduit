# ServiceCollectionExtensions Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Break the 299-line `ServiceCollectionExtensions.cs` monolith into 5 focused files by concern, and adopt the standard .NET `Action<TOptions>` configuration pattern for `AddIbkrClient`.

**Architecture:** The single `AddIbkrClient` method becomes a short orchestrator that constructs options, validates credentials, and delegates to four `internal static` registration classes. `IbkrClientOptions` changes from a `record` to a `class` with `set` properties so it works with the lambda configuration pattern. Credentials move into the options object.

**Tech Stack:** C# / .NET, xUnit v3, Shouldly, Refit, Polly, Microsoft.Extensions.DependencyInjection

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `src/IbkrConduit/Session/IbkrClientOptions.cs` | Modify | Change `record` to `class`, `init` to `set`, add `Credentials` property |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Modify | Slim down to orchestrator calling 4 registration classes |
| `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs` | Create | Global rate limiter, endpoint rate limiters, Polly resilience pipeline |
| `src/IbkrConduit/Http/SessionServiceRegistration.cs` | Create | LST client, session token provider, session API pipeline, lifecycle notifier, session manager, tickle timer |
| `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` | Create | `RegisterConsumerRefitClient<T>` helper, 9 Refit clients, 9 operations |
| `src/IbkrConduit/Http/StreamingAndFlexRegistration.cs` | Create | WebSocket client, streaming ops, Flex HTTP client, Flex ops |
| `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs` | Modify | Update `IbkrClientOptions` tests for class (not record) |
| `tests/IbkrConduit.Tests.Integration/TestHarness.cs` | Modify | Update to lambda pattern, remove `with` expression |
| `tests/IbkrConduit.Tests.Integration/Session/TickleTimerTests.cs` | Modify | Update `TestHarness.CreateAsync` call |
| `tests/IbkrConduit.Tests.Integration_Old/E2E/E2eScenarioBase.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/E2E/Scenario11_FlexWebServiceTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/Session/SessionLifecycleTests.cs` | No change | Constructs `IbkrClientOptions` directly (not via `AddIbkrClient`), `init` to `set` is transparent |
| `tests/IbkrConduit.Tests.Integration_Old/Portfolio/PortfolioAccountsTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/Streaming/StreamingTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/MarketData/MarketDataTests.cs` | Modify | Update to lambda pattern |
| `tests/IbkrConduit.Tests.Integration_Old/Flex/FlexIntegrationTests.cs` | No change | Constructs `FlexClient` directly, does not call `AddIbkrClient` |
| `tools/QueryAccount/Program.cs` | Modify | Update to lambda pattern |
| `tools/ApiCapture/CaptureContext.cs` | Modify | Update to lambda pattern |
| `examples/GetPositions.cs` | Modify | Update to lambda pattern |
| `examples/GetLiveOrders.cs` | Modify | Update to lambda pattern |
| `examples/SubmitAndMonitorOrders.cs` | Modify | Update to lambda pattern |
| `examples/GetTrades.cs` | Modify | Update to lambda pattern |

## Caller Inventory

Every call site for `AddIbkrClient` that must be updated:

| # | File | Current Call | New Call |
|---|------|-------------|---------|
| 1 | `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs` | `services.AddIbkrClient(creds)` and `services.AddIbkrClient(creds, options)` | Lambda pattern |
| 2 | `tests/IbkrConduit.Tests.Integration/TestHarness.cs` | `services.AddIbkrClient(_credentials, clientOptions with { BaseUrl = Server.Url! })` | Lambda pattern (no more `with`) |
| 3 | `tests/IbkrConduit.Tests.Integration_Old/E2E/E2eScenarioBase.cs` | `services.AddIbkrClient(_credentials)` | Lambda pattern |
| 4 | `tests/IbkrConduit.Tests.Integration_Old/E2E/Scenario11_FlexWebServiceTests.cs` | `services.AddIbkrClient(creds, new IbkrClientOptions { FlexToken = flexToken })` | Lambda pattern |
| 5 | `tests/IbkrConduit.Tests.Integration_Old/Portfolio/PortfolioAccountsTests.cs` | `services.AddIbkrClient(creds)` | Lambda pattern |
| 6 | `tests/IbkrConduit.Tests.Integration_Old/Streaming/StreamingTests.cs` | `services.AddIbkrClient(creds)` | Lambda pattern |
| 7 | `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs` | `services.AddIbkrClient(creds)` (x2) | Lambda pattern |
| 8 | `tests/IbkrConduit.Tests.Integration_Old/MarketData/MarketDataTests.cs` | `services.AddIbkrClient(creds)` | Lambda pattern |
| 9 | `tests/IbkrConduit.Tests.Integration_Old/Session/SessionLifecycleTests.cs` | `services.AddIbkrClient(creds)` | Lambda pattern |
| 10 | `tools/QueryAccount/Program.cs` | `services.AddIbkrClient(creds, new IbkrClientOptions { Compete = true })` | Lambda pattern |
| 11 | `tools/ApiCapture/CaptureContext.cs` | `services.AddIbkrClient(_credentials)` | Lambda pattern |
| 12 | `examples/GetPositions.cs` | `services.AddIbkrClient(credentials)` | Lambda pattern |
| 13 | `examples/GetLiveOrders.cs` | `services.AddIbkrClient(credentials)` | Lambda pattern |
| 14 | `examples/SubmitAndMonitorOrders.cs` | `services.AddIbkrClient(credentials)` | Lambda pattern |
| 15 | `examples/GetTrades.cs` | `services.AddIbkrClient(credentials, new IbkrClientOptions { FlexToken = flexToken })` | Lambda pattern |

---

### Task 1: Change IbkrClientOptions from record to class + add Credentials property

**Files:**
- Modify: `src/IbkrConduit/Session/IbkrClientOptions.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs`

This task changes `IbkrClientOptions` from a `record` to a `class`, changes `init` to `set`, and adds the `Credentials` property. The `record` keyword supports `with` expressions; the `class` keyword does not. The only `with` usage is in `TestHarness.cs` (updated in Task 2). All `new IbkrClientOptions { ... }` construction patterns continue to work because object initializer syntax works on `set` properties identically to `init` properties.

**Important:** Unit tests that construct `IbkrClientOptions` directly (in `SessionManagerTests.cs`, `SessionManagerEdgeTests.cs`, `SessionLifecycleTests.cs`, `MarketDataOperationsTests.cs`) do NOT need changes because they use `new IbkrClientOptions { Prop = value }` which works with both `init` and `set`.

- [ ] **Step 1: Update IbkrClientOptions**

Replace the contents of `src/IbkrConduit/Session/IbkrClientOptions.cs` with:

```csharp
using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Auth;

namespace IbkrConduit.Session;

/// <summary>
/// Configuration options for the IBKR client session behavior.
/// </summary>
[ExcludeFromCodeCoverage]
public class IbkrClientOptions
{
    /// <summary>
    /// OAuth credentials for authenticating with the IBKR API.
    /// Required. Validated non-null at registration time by <c>AddIbkrClient</c>.
    /// The consumer manages the <see cref="IbkrOAuthCredentials"/> lifetime (disposal).
    /// </summary>
    public IbkrOAuthCredentials? Credentials { get; set; }

    /// <summary>
    /// Whether to compete with existing sessions when initializing.
    /// Default is <c>true</c>.
    /// </summary>
    public bool Compete { get; set; } = true;

    /// <summary>
    /// List of question message IDs to suppress after session initialization.
    /// </summary>
    public List<string> SuppressMessageIds { get; set; } = new();

    /// <summary>
    /// How long a conid stays in the pre-flight cache before a fresh pre-flight is required.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan PreflightCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Flex Web Service access token. Generated in Client Portal under
    /// Reporting / Flex Queries / Flex Web Configuration.
    /// Required for Flex operations. If null, Flex operations will throw.
    /// </summary>
    public string? FlexToken { get; set; }

    /// <summary>
    /// Override the base URL for all IBKR API requests.
    /// Default is <c>https://api.ibkr.com</c>. Set this to a WireMock server
    /// URL for integration testing.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Interval in seconds between tickle requests to keep the session alive.
    /// Default is 60. Reduce for integration testing.
    /// </summary>
    public int TickleIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How long before token expiry to trigger a proactive refresh.
    /// Default is 1 hour. Reduce for integration testing.
    /// </summary>
    public TimeSpan ProactiveRefreshMargin { get; set; } = TimeSpan.FromHours(1);
}
```

- [ ] **Step 2: Update SessionModelsTests for class behavior**

The test `IbkrClientOptions_DefaultValues_AreCorrect` and `IbkrClientOptions_CustomValues_ArePreserved` in `tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs` still compile and pass because the construction pattern `new IbkrClientOptions { ... }` works with `set` the same as `init`. No code changes needed in this file.

However, verify the tests still pass:

- [ ] **Step 3: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 4: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass. The `with` expression in `TestHarness.cs` will now fail to compile -- this is expected and will be fixed in Task 2. If it fails, proceed to Task 2 which fixes it.

**Note:** If the build fails on the `with` expression in TestHarness.cs, that is expected. Tasks 1 and 2 must be done together to reach a green state. The subagent should proceed directly to Task 2 without committing.

- [ ] **Step 5: Commit (defer until Task 2 completes -- the two tasks form one atomic change)**

Do NOT commit yet. Proceed to Task 2.

---

### Task 2: Change AddIbkrClient to Action\<IbkrClientOptions\> pattern + update all callers

**Files:**
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration/TestHarness.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/E2E/E2eScenarioBase.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/E2E/Scenario11_FlexWebServiceTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/Portfolio/PortfolioAccountsTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/Streaming/StreamingTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/MarketData/MarketDataTests.cs`
- Modify: `tests/IbkrConduit.Tests.Integration_Old/Session/SessionLifecycleTests.cs`
- Modify: `tools/QueryAccount/Program.cs`
- Modify: `tools/ApiCapture/CaptureContext.cs`
- Modify: `examples/GetPositions.cs`
- Modify: `examples/GetLiveOrders.cs`
- Modify: `examples/SubmitAndMonitorOrders.cs`
- Modify: `examples/GetTrades.cs`

- [ ] **Step 1: Change the AddIbkrClient method signature**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, replace the method signature and the first few lines. Change:

```csharp
    public static IServiceCollection AddIbkrClient(
        this IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions? options = null)
    {
        var clientOptions = options ?? new IbkrClientOptions();
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;
```

To:

```csharp
    public static IServiceCollection AddIbkrClient(
        this IServiceCollection services,
        Action<IbkrClientOptions> configure)
    {
        var clientOptions = new IbkrClientOptions();
        configure(clientOptions);

        ArgumentNullException.ThrowIfNull(clientOptions.Credentials, "IbkrClientOptions.Credentials");

        var credentials = clientOptions.Credentials;
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;
```

Everything else in the method stays the same. The `credentials` local variable is now extracted from `clientOptions.Credentials` instead of being a parameter, so all downstream usages continue to work.

- [ ] **Step 2: Update unit tests**

In `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs`, update all test methods.

Replace `AddIbkrClient_RegistersAllRequiredServices`:

```csharp
    [Fact]
    public void AddIbkrClient_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
        provider.GetService<ILiveSessionTokenClient>().ShouldNotBeNull();
    }
```

Replace `AddIbkrClient_WithOptions_RegistersSessionManager`:

```csharp
    [Fact]
    public void AddIbkrClient_WithOptions_RegistersSessionManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.Compete = false;
        });

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
    }
```

Replace `AddIbkrClient_ResolvesRefitClients`:

```csharp
    [Fact]
    public void AddIbkrClient_ResolvesRefitClients()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<IIbkrPortfolioApi>().ShouldNotBeNull();
        provider.GetService<IIbkrContractApi>().ShouldNotBeNull();
        provider.GetService<IIbkrOrderApi>().ShouldNotBeNull();
        provider.GetService<IIbkrMarketDataApi>().ShouldNotBeNull();
    }
```

Replace `AddIbkrClient_ResolvesFacade`:

```csharp
    [Fact]
    public void AddIbkrClient_ResolvesFacade()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var creds = CreateTestCredentials();

        services.AddIbkrClient(opts => opts.Credentials = creds);

        var provider = services.BuildServiceProvider();

        var client = provider.GetService<IIbkrClient>();
        client.ShouldNotBeNull();
        client.Portfolio.ShouldNotBeNull();
        client.Contracts.ShouldNotBeNull();
        client.Orders.ShouldNotBeNull();
        client.MarketData.ShouldNotBeNull();
        client.Streaming.ShouldNotBeNull();
        client.Flex.ShouldNotBeNull();
    }
```

Add a new test for null credentials validation:

```csharp
    [Fact]
    public void AddIbkrClient_NullCredentials_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<ArgumentNullException>(
            () => services.AddIbkrClient(opts => { }));

        ex.ParamName.ShouldBe("IbkrClientOptions.Credentials");
    }
```

- [ ] **Step 3: Update TestHarness.cs**

In `tests/IbkrConduit.Tests.Integration/TestHarness.cs`, the `Initialize` method currently uses a `with` expression which no longer compiles. Replace:

```csharp
        var clientOptions = options ?? new IbkrClientOptions();
        services.AddIbkrClient(_credentials, clientOptions with
        {
            BaseUrl = Server.Url!,
        });
```

With:

```csharp
        var clientOptions = options ?? new IbkrClientOptions();
        services.AddIbkrClient(opts =>
        {
            opts.Credentials = _credentials;
            opts.BaseUrl = Server.Url!;
            opts.Compete = clientOptions.Compete;
            opts.SuppressMessageIds = clientOptions.SuppressMessageIds;
            opts.FlexToken = clientOptions.FlexToken;
            opts.TickleIntervalSeconds = clientOptions.TickleIntervalSeconds;
            opts.ProactiveRefreshMargin = clientOptions.ProactiveRefreshMargin;
            opts.PreflightCacheDuration = clientOptions.PreflightCacheDuration;
        });
```

This copies all properties from the caller-provided options and overrides `BaseUrl` to point at WireMock + sets `Credentials`. This replaces the `with { BaseUrl = ... }` pattern.

- [ ] **Step 4: Update E2eScenarioBase.cs**

In `tests/IbkrConduit.Tests.Integration_Old/E2E/E2eScenarioBase.cs`, replace:

```csharp
        services.AddIbkrClient(_credentials);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = _credentials);
```

- [ ] **Step 5: Update Scenario11_FlexWebServiceTests.cs**

In `tests/IbkrConduit.Tests.Integration_Old/E2E/Scenario11_FlexWebServiceTests.cs`, in the `CreateFlexClient` method, replace:

```csharp
        services.AddIbkrClient(creds, new IbkrClientOptions
        {
            FlexToken = flexToken,
        });
```

With:

```csharp
        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.FlexToken = flexToken;
        });
```

- [ ] **Step 6: Update Old integration tests**

In `tests/IbkrConduit.Tests.Integration_Old/Portfolio/PortfolioAccountsTests.cs`, replace:

```csharp
        services.AddIbkrClient(creds);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = creds);
```

In `tests/IbkrConduit.Tests.Integration_Old/Streaming/StreamingTests.cs`, replace:

```csharp
        services.AddIbkrClient(creds);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = creds);
```

In `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs`, replace BOTH occurrences:

```csharp
        services.AddIbkrClient(creds);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = creds);
```

In `tests/IbkrConduit.Tests.Integration_Old/MarketData/MarketDataTests.cs`, replace:

```csharp
        services.AddIbkrClient(creds);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = creds);
```

In `tests/IbkrConduit.Tests.Integration_Old/Session/SessionLifecycleTests.cs`, there is one `AddIbkrClient` call near the bottom of the file. Replace:

```csharp
        services.AddIbkrClient(creds);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = creds);
```

In `tests/IbkrConduit.Tests.Integration_Old/Flex/FlexIntegrationTests.cs` -- the E2E test at the bottom also calls `AddIbkrClient`. Replace:

```csharp
        services.AddIbkrClient(creds, new IbkrConduit.Session.IbkrClientOptions
        {
            FlexToken = flexToken,
        });
```

With:

```csharp
        services.AddIbkrClient(opts =>
        {
            opts.Credentials = creds;
            opts.FlexToken = flexToken;
        });
```

- [ ] **Step 7: Update tools**

In `tools/QueryAccount/Program.cs`, replace:

```csharp
services.AddIbkrClient(creds, new IbkrClientOptions { Compete = true });
```

With:

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.Compete = true;
});
```

In `tools/ApiCapture/CaptureContext.cs`, replace:

```csharp
        services.AddIbkrClient(_credentials);
```

With:

```csharp
        services.AddIbkrClient(opts => opts.Credentials = _credentials);
```

- [ ] **Step 8: Update examples**

In `examples/GetPositions.cs`, replace:

```csharp
services.AddIbkrClient(credentials);
```

With:

```csharp
services.AddIbkrClient(opts => opts.Credentials = credentials);
```

In `examples/GetLiveOrders.cs`, replace:

```csharp
services.AddIbkrClient(credentials);
```

With:

```csharp
services.AddIbkrClient(opts => opts.Credentials = credentials);
```

In `examples/SubmitAndMonitorOrders.cs`, replace:

```csharp
services.AddIbkrClient(credentials);
```

With:

```csharp
services.AddIbkrClient(opts => opts.Credentials = credentials);
```

In `examples/GetTrades.cs`, replace:

```csharp
services.AddIbkrClient(credentials, new IbkrClientOptions { FlexToken = flexToken });
```

With:

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = credentials;
    opts.FlexToken = flexToken;
});
```

- [ ] **Step 9: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 10: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass (including the new null-credentials test).

- [ ] **Step 11: Lint**

Run: `dotnet format --verify-no-changes`
Expected: No formatting issues.

- [ ] **Step 12: Commit**

```bash
git add src/IbkrConduit/Session/IbkrClientOptions.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs tests/IbkrConduit.Tests.Integration/TestHarness.cs tests/IbkrConduit.Tests.Integration_Old/E2E/E2eScenarioBase.cs tests/IbkrConduit.Tests.Integration_Old/E2E/Scenario11_FlexWebServiceTests.cs tests/IbkrConduit.Tests.Integration_Old/Portfolio/PortfolioAccountsTests.cs tests/IbkrConduit.Tests.Integration_Old/Streaming/StreamingTests.cs tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs tests/IbkrConduit.Tests.Integration_Old/MarketData/MarketDataTests.cs tests/IbkrConduit.Tests.Integration_Old/Session/SessionLifecycleTests.cs tests/IbkrConduit.Tests.Integration_Old/Flex/FlexIntegrationTests.cs tools/QueryAccount/Program.cs tools/ApiCapture/CaptureContext.cs examples/GetPositions.cs examples/GetLiveOrders.cs examples/SubmitAndMonitorOrders.cs examples/GetTrades.cs
```

```
git commit -m "feat: change AddIbkrClient to Action<IbkrClientOptions> pattern

Change IbkrClientOptions from record to class with set properties.
Move credentials into options object. Update all callers to lambda pattern."
```

---

### Task 3: Extract RateLimitingAndResilienceRegistration.cs

**Files:**
- Create: `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create RateLimitingAndResilienceRegistration.cs**

Create `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace IbkrConduit.Http;

/// <summary>
/// Registers rate limiting and resilience services shared across all HTTP pipelines.
/// </summary>
internal static class RateLimitingAndResilienceRegistration
{
    /// <summary>
    /// Registers the global rate limiter, endpoint rate limiters, and Polly resilience pipeline.
    /// </summary>
    internal static void Register(IServiceCollection services)
    {
        var globalRateLimiter = CreateGlobalRateLimiter();
        var endpointRateLimiters = CreateEndpointRateLimiters();
        var resiliencePipeline = CreateResiliencePipeline();

        services.AddSingleton<RateLimiter>(globalRateLimiter);
        services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(endpointRateLimiters);
        services.AddSingleton(resiliencePipeline);
    }

    /// <summary>
    /// Creates the global token bucket rate limiter (10 req/s, queue 500).
    /// </summary>
    private static TokenBucketRateLimiter CreateGlobalRateLimiter() =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 500,
        });

    /// <summary>
    /// Creates the per-endpoint rate limiters for known IBKR endpoints.
    /// </summary>
    private static Dictionary<string, RateLimiter> CreateEndpointRateLimiters()
    {
        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/trades"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/account/pnl/partitioned"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/iserver/marketdata/snapshot"] = CreateEndpointLimiter(10, TimeSpan.FromSeconds(1), 50),
            ["/iserver/scanner/params"] = CreateEndpointLimiter(1, TimeSpan.FromMinutes(15), 50),
            ["/iserver/scanner/run"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(1), 50),
            ["/portfolio/accounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
            ["/portfolio/subaccounts"] = CreateEndpointLimiter(1, TimeSpan.FromSeconds(5), 50),
        };

        return limiters;
    }

    private static TokenBucketRateLimiter CreateEndpointLimiter(
        int tokenLimit, TimeSpan replenishmentPeriod, int queueLimit) =>
        new(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            ReplenishmentPeriod = replenishmentPeriod,
            TokensPerPeriod = tokenLimit,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = queueLimit,
        });

    /// <summary>
    /// Creates the Polly resilience pipeline for retrying transient HTTP errors.
    /// Retries 5xx, 408, and 429 with exponential backoff (1s, 2s, 4s) and jitter.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> CreateResiliencePipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
            })
            .Build();
}
```

- [ ] **Step 2: Update ServiceCollectionExtensions.cs**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, replace these lines:

```csharp
        // Rate limiting and resilience singletons (shared across pipelines)
        var globalRateLimiter = CreateGlobalRateLimiter();
        var endpointRateLimiters = CreateEndpointRateLimiters();
        var resiliencePipeline = CreateResiliencePipeline();

        services.AddSingleton<RateLimiter>(globalRateLimiter);
        services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(endpointRateLimiters);
        services.AddSingleton(resiliencePipeline);
```

With:

```csharp
        // Rate limiting and resilience singletons (shared across pipelines)
        RateLimitingAndResilienceRegistration.Register(services);
```

Also remove the four private methods that were moved: `CreateGlobalRateLimiter`, `CreateEndpointRateLimiters`, `CreateEndpointLimiter`, and `CreateResiliencePipeline`. These are the last ~65 lines of the class (lines 238-299 approximately).

Remove these unused `using` statements from the top of `ServiceCollectionExtensions.cs` if they are no longer needed after this extraction:
- `using System.Threading.RateLimiting;` -- still needed? Check after extraction. (It is NOT needed after moving -- the remaining code does not reference rate limiting types directly.)
- `using Polly;` -- NOT needed after moving.
- `using Polly.Retry;` -- NOT needed after moving.

Actually, keep all usings for now -- they will be needed by the code that remains (session, consumer pipeline, etc. still reference `RateLimiter`, `ResiliencePipeline`). The usings will be cleaned up in subsequent tasks as more code is extracted. Run `dotnet format` to auto-remove any truly unused ones.

- [ ] **Step 3: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "refactor: extract RateLimitingAndResilienceRegistration from ServiceCollectionExtensions"
```

---

### Task 4: Extract SessionServiceRegistration.cs

**Files:**
- Create: `src/IbkrConduit/Http/SessionServiceRegistration.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create SessionServiceRegistration.cs**

Create `src/IbkrConduit/Http/SessionServiceRegistration.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Registers session-related services: LST client, session token provider,
/// session API pipeline, lifecycle notifier, session manager, and tickle timer.
/// </summary>
internal static class SessionServiceRegistration
{
    /// <summary>
    /// Registers all session services into the DI container.
    /// </summary>
    internal static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions options,
        string baseUrl)
    {
        // LST client (plain HttpClient via IHttpClientFactory, not through Refit pipeline)
        var lstClientName = $"IbkrConduit-LST-{credentials.TenantId}";
        services.AddHttpClient(lstClientName, c =>
        {
            c.BaseAddress = new Uri(baseUrl + "/v1/api/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });

        services.AddSingleton<ILiveSessionTokenClient>(sp =>
            new LiveSessionTokenClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                lstClientName,
                sp.GetRequiredService<ILogger<LiveSessionTokenClient>>()));

        // Session token provider
        services.AddSingleton<ISessionTokenProvider>(sp =>
            new SessionTokenProvider(
                credentials,
                sp.GetRequiredService<ILiveSessionTokenClient>()));

        // Client options
        services.AddSingleton(options);

        // Tickle timer factory
        services.AddSingleton<ITickleTimerFactory>(sp =>
            new TickleTimerFactory(
                sp.GetRequiredService<ILogger<TickleTimer>>(),
                options.TickleIntervalSeconds));

        // Internal session API client:
        //   ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        services.AddRefitClient<IIbkrSessionApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken));

        // Session lifecycle notifier
        services.AddSingleton<ISessionLifecycleNotifier, SessionLifecycleNotifier>();

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                options,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<SessionManager>>()));
    }
}
```

- [ ] **Step 2: Update ServiceCollectionExtensions.cs**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, replace all the session-related code block (from the LST client comment through the session manager registration) with a single call:

Replace:

```csharp
        // LST client (plain HttpClient via IHttpClientFactory, not through Refit pipeline)
        var lstClientName = $"IbkrConduit-LST-{credentials.TenantId}";
        services.AddHttpClient(lstClientName, c =>
        {
            c.BaseAddress = new Uri(baseUrl + "/v1/api/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });

        services.AddSingleton<ILiveSessionTokenClient>(sp =>
            new LiveSessionTokenClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                lstClientName,
                sp.GetRequiredService<ILogger<LiveSessionTokenClient>>()));

        // Session token provider
        services.AddSingleton<ISessionTokenProvider>(sp =>
            new SessionTokenProvider(
                credentials,
                sp.GetRequiredService<ILiveSessionTokenClient>()));

        // Client options
        services.AddSingleton(clientOptions);

        // Tickle timer factory
        services.AddSingleton<ITickleTimerFactory>(sp =>
            new TickleTimerFactory(
                sp.GetRequiredService<ILogger<TickleTimer>>(),
                clientOptions.TickleIntervalSeconds));

        // Rate limiting and resilience singletons (shared across pipelines)
        RateLimitingAndResilienceRegistration.Register(services);

        // Internal session API client:
        //   ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        services.AddRefitClient<IIbkrSessionApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken));

        // Session lifecycle notifier
        services.AddSingleton<ISessionLifecycleNotifier, SessionLifecycleNotifier>();

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                clientOptions,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<SessionManager>>()));
```

With:

```csharp
        // Rate limiting and resilience singletons (shared across pipelines)
        RateLimitingAndResilienceRegistration.Register(services);

        // Session services (LST client, token provider, session API, session manager)
        SessionServiceRegistration.Register(services, credentials, clientOptions, baseUrl);
```

Note: the rate limiting registration must come BEFORE session registration because the session API pipeline resolves rate limiter singletons.

- [ ] **Step 3: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Lint**

Run: `dotnet format --verify-no-changes`
Expected: No issues. If unused `using` statements are flagged, remove them.

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Http/SessionServiceRegistration.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "refactor: extract SessionServiceRegistration from ServiceCollectionExtensions"
```

---

### Task 5: Extract ConsumerPipelineRegistration.cs

**Files:**
- Create: `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create ConsumerPipelineRegistration.cs**

Create `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using IbkrConduit.Accounts;
using IbkrConduit.Alerts;
using IbkrConduit.Allocation;
using IbkrConduit.Auth;
using IbkrConduit.Contracts;
using IbkrConduit.Fyi;
using IbkrConduit.MarketData;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Registers consumer-facing Refit clients and their operations implementations.
/// All clients go through the full pipeline: TokenRefreshHandler -> ErrorNormalizationHandler ->
/// ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler.
/// </summary>
internal static class ConsumerPipelineRegistration
{
    /// <summary>
    /// Registers all 9 consumer Refit clients and their operations implementations.
    /// </summary>
    internal static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        string baseUrl)
    {
        // Consumer Refit clients (all go through the full pipeline)
        RegisterConsumerRefitClient<IIbkrPortfolioApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrContractApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrOrderApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrMarketDataApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAccountApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAlertApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrWatchlistApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrFyiApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAllocationApi>(services, credentials, baseUrl);

        // Operations implementations
        services.AddSingleton<IPortfolioOperations, PortfolioOperations>();
        services.AddSingleton<IContractOperations, ContractOperations>();
        services.AddSingleton<IOrderOperations, OrderOperations>();
        services.AddSingleton<IMarketDataOperations, MarketDataOperations>();
        services.AddSingleton<IAccountOperations, AccountOperations>();
        services.AddSingleton<IAlertOperations, AlertOperations>();
        services.AddSingleton<IWatchlistOperations, WatchlistOperations>();
        services.AddSingleton<IFyiOperations, FyiOperations>();
        services.AddSingleton<IAllocationOperations, AllocationOperations>();
    }

    /// <summary>
    /// Registers a consumer-facing Refit client through the full HTTP pipeline.
    /// </summary>
    private static void RegisterConsumerRefitClient<TApi>(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        string baseUrl) where TApi : class
    {
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
            .AddHttpMessageHandler(_ =>
                new ErrorNormalizationHandler())
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));
    }
}
```

- [ ] **Step 2: Update ServiceCollectionExtensions.cs**

Replace the consumer Refit and operations block:

```csharp
        // Consumer Refit clients (all go through the full pipeline):
        //   TokenRefreshHandler -> ErrorNormalizationHandler -> ResilienceHandler ->
        //   GlobalRateLimitingHandler -> EndpointRateLimitingHandler -> OAuthSigningHandler
        RegisterConsumerRefitClient<IIbkrPortfolioApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrContractApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrOrderApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrMarketDataApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAccountApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAlertApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrWatchlistApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrFyiApi>(services, credentials, baseUrl);
        RegisterConsumerRefitClient<IIbkrAllocationApi>(services, credentials, baseUrl);

        // Operations implementations
        services.AddSingleton<IPortfolioOperations, PortfolioOperations>();
        services.AddSingleton<IContractOperations, ContractOperations>();
        services.AddSingleton<IOrderOperations, OrderOperations>();
        services.AddSingleton<IMarketDataOperations, MarketDataOperations>();
        services.AddSingleton<IAccountOperations, AccountOperations>();
        services.AddSingleton<IAlertOperations, AlertOperations>();
        services.AddSingleton<IWatchlistOperations, WatchlistOperations>();
        services.AddSingleton<IFyiOperations, FyiOperations>();
        services.AddSingleton<IAllocationOperations, AllocationOperations>();
```

With:

```csharp
        // Consumer Refit clients and operations implementations
        ConsumerPipelineRegistration.Register(services, credentials, baseUrl);
```

Also remove the `RegisterConsumerRefitClient` private method from the class (it was moved to `ConsumerPipelineRegistration`).

- [ ] **Step 3: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Http/ConsumerPipelineRegistration.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "refactor: extract ConsumerPipelineRegistration from ServiceCollectionExtensions"
```

---

### Task 6: Extract StreamingAndFlexRegistration.cs

**Files:**
- Create: `src/IbkrConduit/Http/StreamingAndFlexRegistration.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Create StreamingAndFlexRegistration.cs**

Create `src/IbkrConduit/Http/StreamingAndFlexRegistration.cs` with:

```csharp
using System;
using System.Net;
using System.Net.Http;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Http;

/// <summary>
/// Registers WebSocket streaming and Flex Web Service clients.
/// </summary>
internal static class StreamingAndFlexRegistration
{
    /// <summary>
    /// Registers streaming (WebSocket) and Flex Web Service services.
    /// </summary>
    internal static void Register(
        IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions options,
        string baseUrl)
    {
        // WebSocket streaming
        services.AddSingleton<IIbkrWebSocketClient>(sp =>
            new IbkrWebSocketClient(
                sp.GetRequiredService<IIbkrSessionApi>(),
                credentials,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<IbkrWebSocketClient>>(),
                () => new ClientWebSocketAdapter()));
        services.AddSingleton<IStreamingOperations>(sp =>
            new StreamingOperations(
                sp.GetRequiredService<IIbkrWebSocketClient>()));

        // Flex Web Service (plain HTTP via IHttpClientFactory, no signing pipeline)
        if (!string.IsNullOrEmpty(options.FlexToken))
        {
            var flexToken = options.FlexToken;
            var flexClientName = $"IbkrConduit-Flex-{credentials.TenantId}";
            services.AddHttpClient(flexClientName, c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("IbkrConduit/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            });

            services.AddSingleton(sp =>
                new FlexClient(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    flexClientName,
                    flexToken,
                    sp.GetRequiredService<ILogger<FlexClient>>()));
            services.AddSingleton<IFlexOperations>(sp =>
                new FlexOperations(sp.GetRequiredService<FlexClient>()));
        }
        else
        {
            services.AddSingleton<IFlexOperations>(_ => new FlexOperations(null));
        }
    }
}
```

- [ ] **Step 2: Update ServiceCollectionExtensions.cs**

Replace the WebSocket and Flex blocks:

```csharp
        // WebSocket streaming
        services.AddSingleton<IIbkrWebSocketClient>(sp =>
            new IbkrWebSocketClient(
                sp.GetRequiredService<IIbkrSessionApi>(),
                credentials,
                sp.GetRequiredService<ISessionLifecycleNotifier>(),
                sp.GetRequiredService<ILogger<IbkrWebSocketClient>>(),
                () => new ClientWebSocketAdapter()));
        services.AddSingleton<IStreamingOperations>(sp =>
            new StreamingOperations(
                sp.GetRequiredService<IIbkrWebSocketClient>()));

        // Flex Web Service (plain HTTP via IHttpClientFactory, no signing pipeline)
        if (!string.IsNullOrEmpty(clientOptions.FlexToken))
        {
            var flexToken = clientOptions.FlexToken;
            var flexClientName = $"IbkrConduit-Flex-{credentials.TenantId}";
            services.AddHttpClient(flexClientName, c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("IbkrConduit/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            });

            services.AddSingleton(sp =>
                new FlexClient(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    flexClientName,
                    flexToken,
                    sp.GetRequiredService<ILogger<FlexClient>>()));
            services.AddSingleton<IFlexOperations>(sp =>
                new FlexOperations(sp.GetRequiredService<FlexClient>()));
        }
        else
        {
            services.AddSingleton<IFlexOperations>(_ => new FlexOperations(null));
        }
```

With:

```csharp
        // WebSocket streaming and Flex Web Service
        StreamingAndFlexRegistration.Register(services, credentials, clientOptions, baseUrl);
```

The final `ServiceCollectionExtensions.cs` should now look like:

```csharp
using System;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string _ibkrBaseUrl = "https://api.ibkr.com";

    /// <summary>
    /// Registers the full IBKR API client pipeline including all Refit interfaces,
    /// operations implementations, and the unified <see cref="IIbkrClient"/> facade.
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> ErrorNormalizationHandler ->
    /// ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler ->
    /// OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> ResilienceHandler -> GlobalRateLimitingHandler ->
    /// EndpointRateLimitingHandler -> OAuthSigningHandler -> HttpClient (no TokenRefreshHandler).
    /// </summary>
    public static IServiceCollection AddIbkrClient(
        this IServiceCollection services,
        Action<IbkrClientOptions> configure)
    {
        var clientOptions = new IbkrClientOptions();
        configure(clientOptions);

        ArgumentNullException.ThrowIfNull(clientOptions.Credentials, "IbkrClientOptions.Credentials");

        var credentials = clientOptions.Credentials;
        var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;

        // Rate limiting and resilience singletons (shared across pipelines)
        RateLimitingAndResilienceRegistration.Register(services);

        // Session services (LST client, token provider, session API, session manager)
        SessionServiceRegistration.Register(services, credentials, clientOptions, baseUrl);

        // Consumer Refit clients and operations implementations
        ConsumerPipelineRegistration.Register(services, credentials, baseUrl);

        // WebSocket streaming and Flex Web Service
        StreamingAndFlexRegistration.Register(services, credentials, clientOptions, baseUrl);

        // Unified facade
        services.AddSingleton<IIbkrClient, IbkrClient>();

        return services;
    }
}
```

Remove all now-unused `using` statements. The final file should only need:
- `using System;`
- `using IbkrConduit.Auth;`
- `using IbkrConduit.Client;`
- `using IbkrConduit.Session;`
- `using Microsoft.Extensions.DependencyInjection;`

Run `dotnet format` to clean up any remaining unused usings.

- [ ] **Step 3: Build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds.

- [ ] **Step 4: Run tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Lint**

Run: `dotnet format --verify-no-changes`
Expected: No issues.

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Http/StreamingAndFlexRegistration.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs
git commit -m "refactor: extract StreamingAndFlexRegistration, complete ServiceCollectionExtensions split"
```

---

## Verification Checklist

After all 6 tasks are complete:

1. `dotnet build --configuration Release` -- 0 errors, 0 warnings
2. `dotnet test --configuration Release` -- all tests pass
3. `dotnet format --verify-no-changes` -- no formatting issues
4. `ServiceCollectionExtensions.cs` is ~40 lines (orchestrator only)
5. 4 new `internal static` registration classes exist in `src/IbkrConduit/Http/`
6. `IbkrClientOptions` is a `class` with `set` properties and a `Credentials` property
7. Every `AddIbkrClient` call site uses the lambda pattern
8. No `with` expressions on `IbkrClientOptions` remain anywhere
9. Registration order is preserved: rate limiting -> session -> consumer -> streaming/flex -> facade
