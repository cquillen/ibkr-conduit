# Dynamic Multi-Tenant Client Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `IIbkrClientManager` that creates, retrieves, and tears down per-credential IbkrConduit instances at runtime — one isolated child `ServiceProvider` per tenant — without restarting the app.

**Architecture:** A singleton manager (registered via `AddIbkrClientManager`) builds one child `ServiceProvider` per tenant by reusing the existing per-tenant registration (extracted into `BuildTenantServices`). It owns a thread-safe registry and the tenants' credentials/lifetimes. Adds are eager (authenticate + connect on `AddAsync`); removes are abrupt (cancel in-flight → close socket → best-effort `/logout` → dispose). Telemetry gains a per-tenant `TenantId` dimension, and a no-op `ISharedRateGovernor` seam is pre-wired for a future shared IP governor.

**Tech Stack:** C#/.NET, Microsoft.Extensions.DependencyInjection (child `ServiceProvider`s), Refit + `IHttpClientFactory`, xUnit v3 + Shouldly (tests), WireMock.Net (integration).

**Spec:** [`docs/superpowers/specs/2026-06-30-multi-tenant-client-manager-design.md`](../specs/2026-06-30-multi-tenant-client-manager-design.md)

**Each task = one PR.** Build order is linear (1 → 8); tasks 2, 4, 5 are independent of each other but all precede 6/7.

---

## File Structure

**New files:**
- `src/IbkrConduit/Diagnostics/TenantContext.cs` — per-provider holder `{ string TenantId }` (trivial).
- `src/IbkrConduit/Http/ISharedRateGovernor.cs` — seam interface + `NoOpSharedRateGovernor`.
- `src/IbkrConduit/Client/IManagedTenant.cs` — internal seam `{ IIbkrClient Client }` (lets the manager be unit-tested with a fake).
- `src/IbkrConduit/Client/ManagedTenant.cs` — real tenant holder; disposal does best-effort `/logout`, then disposes the child provider and credentials.
- `src/IbkrConduit/Client/ITenantBuilder.cs` — testability seam.
- `src/IbkrConduit/Client/TenantBuilder.cs` — real builder (child provider + eager init).
- `src/IbkrConduit/Client/IIbkrClientManager.cs` — public manager interface.
- `src/IbkrConduit/Client/IbkrClientManager.cs` — manager implementation.

**Modified files:**
- `src/IbkrConduit/Session/IbkrClientOptions.cs` — add internal `Clone()`.
- `src/IbkrConduit/Flex/FlexQueryOptions.cs` — add internal `Clone()`.
- `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` — extract `BuildTenantServices`; add `AddIbkrClient` guard; add `AddIbkrClientManager`.
- `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs` — inject `ISharedRateGovernor`, call `AcquireAsync`.
- `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs` — register `NoOpSharedRateGovernor`.
- Telemetry tag sites (Task 5): the rate-limit/signing handlers, operations classes, `SessionManager`, `TickleTimer`, `IbkrWebSocketClient`.

**Test files:**
- `tests/IbkrConduit.Tests.Unit/Session/IbkrClientOptionsCloneTests.cs`
- `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsGuardTests.cs`
- `tests/IbkrConduit.Tests.Unit/Http/BuildTenantServicesTests.cs`
- `tests/IbkrConduit.Tests.Unit/Http/SharedRateGovernorTests.cs`
- `tests/IbkrConduit.Tests.Unit/Diagnostics/TenantTaggingTests.cs`
- `tests/IbkrConduit.Tests.Unit/Client/IbkrClientManagerTests.cs`
- `tests/IbkrConduit.Tests.Integration/MultiTenant/ClientManagerTests.cs`

**Commands (this repo uses xUnit v3 + Microsoft Testing Platform — VSTest `--filter` does NOT work):**
- Build: `dotnet build --configuration Release`
- Run a unit test class: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*ClassName*"`
- Run an integration test class: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*ClassName*"`
- Lint: `dotnet format --verify-no-changes`

---

## Task 1: `IbkrClientOptions.Clone()` (deep-ish copy for per-tenant options)

**Files:**
- Modify: `src/IbkrConduit/Session/IbkrClientOptions.cs`
- Modify: `src/IbkrConduit/Flex/FlexQueryOptions.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Session/IbkrClientOptionsCloneTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using IbkrConduit.Flex;
using IbkrConduit.Session;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Session;

public class IbkrClientOptionsCloneTests
{
    [Fact]
    public void Clone_MutatingCloneList_DoesNotAffectOriginal()
    {
        var original = new IbkrClientOptions();
        original.SuppressMessageIds.Add("o1");
        original.FlexQueries.CashTransactionsQueryId = "100";

        var clone = original.Clone();
        clone.SuppressMessageIds.Add("c1");
        clone.FlexQueries.CashTransactionsQueryId = "200";
        clone.TickleIntervalSeconds = 999;

        original.SuppressMessageIds.ShouldBe(new[] { "o1" });
        original.FlexQueries.CashTransactionsQueryId.ShouldBe("100");
        original.TickleIntervalSeconds.ShouldBe(60);
    }

    [Fact]
    public void Clone_CopiesScalarValues()
    {
        var original = new IbkrClientOptions
        {
            TickleIntervalSeconds = 30,
            StrictResponseValidation = true,
            FlexToken = "flex",
            BaseUrl = "https://example.test",
        };

        var clone = original.Clone();

        clone.TickleIntervalSeconds.ShouldBe(30);
        clone.StrictResponseValidation.ShouldBeTrue();
        clone.FlexToken.ShouldBe("flex");
        clone.BaseUrl.ShouldBe("https://example.test");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrClientOptionsCloneTests*"`
Expected: FAIL — `IbkrClientOptions` does not contain a definition for `Clone`.

- [ ] **Step 3: Add `FlexQueryOptions.Clone()`**

In `src/IbkrConduit/Flex/FlexQueryOptions.cs`, add inside the class:

```csharp
/// <summary>Creates a shallow copy of these query IDs.</summary>
internal FlexQueryOptions Clone() => new()
{
    CashTransactionsQueryId = CashTransactionsQueryId,
    TradeConfirmationsQueryId = TradeConfirmationsQueryId,
};
```

- [ ] **Step 4: Add `IbkrClientOptions.Clone()`**

In `src/IbkrConduit/Session/IbkrClientOptions.cs`, add inside the class:

```csharp
/// <summary>
/// Creates a per-tenant copy: scalars are copied, the mutable
/// <see cref="SuppressMessageIds"/> list and <see cref="FlexQueries"/> are
/// deep-copied so a per-tenant override cannot mutate the shared baseline.
/// </summary>
internal IbkrClientOptions Clone() => new()
{
    Credentials = Credentials,
    Compete = Compete,
    SuppressMessageIds = new List<string>(SuppressMessageIds),
    PreflightCacheDuration = PreflightCacheDuration,
    FlexToken = FlexToken,
    BaseUrl = BaseUrl,
    TickleIntervalSeconds = TickleIntervalSeconds,
    TickleFailureIntervalSeconds = TickleFailureIntervalSeconds,
    WebSocketHeartbeatIntervalSeconds = WebSocketHeartbeatIntervalSeconds,
    StreamingBufferSize = StreamingBufferSize,
    ProactiveRefreshMargin = ProactiveRefreshMargin,
    StrictResponseValidation = StrictResponseValidation,
    ThrowOnApiError = ThrowOnApiError,
    FlexPollTimeout = FlexPollTimeout,
    FlexQueries = FlexQueries.Clone(),
};
```

Note: both option types are `[ExcludeFromCodeCoverage]`; `Clone()` is a pure pass-through with no branching, so the exclusion stays valid. The behavior is still covered by the unit test above.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrClientOptionsCloneTests*"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Session/IbkrClientOptions.cs src/IbkrConduit/Flex/FlexQueryOptions.cs tests/IbkrConduit.Tests.Unit/Session/IbkrClientOptionsCloneTests.cs
git commit -m "feat(options): add IbkrClientOptions.Clone for per-tenant config"
```

---

## Task 2: Double-registration guard on `AddIbkrClient` (D)

**Files:**
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsGuardTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using IbkrConduit.Http;
using IbkrConduit.Tests.Unit.TestSupport; // existing helper that builds fake IbkrOAuthCredentials
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class ServiceCollectionExtensionsGuardTests
{
    [Fact]
    public void AddIbkrClient_CalledTwice_Throws()
    {
        using var creds = FakeCredentials.Create(); // see note below
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(o => o.Credentials = creds);

        var ex = Should.Throw<InvalidOperationException>(
            () => services.AddIbkrClient(o => o.Credentials = creds));

        ex.Message.ShouldContain("IIbkrClientManager");
    }
}
```

Note on `FakeCredentials.Create()`: reuse the repo's existing synthetic-credential helper used by current unit tests (search `tests/IbkrConduit.Tests.Unit` for how `IbkrOAuthCredentials` are constructed with throwaway RSA keys, e.g. in existing `OAuthSigningHandler`/`SessionManager` tests). If no shared helper exists, add one to `tests/IbkrConduit.Tests.Unit/TestSupport/FakeCredentials.cs` that returns `new IbkrOAuthCredentials("t1", "CONSUMERK", "token", "secret", RSA.Create(2048), RSA.Create(2048), BigInteger.One)`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*ServiceCollectionExtensionsGuardTests*"`
Expected: FAIL — second `AddIbkrClient` does not throw.

- [ ] **Step 3: Add the marker and guard**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, add a private marker type at the bottom of the class:

```csharp
/// <summary>Marker proving AddIbkrClient has already run on a collection.</summary>
private sealed class IbkrClientRegistrationMarker;
```

At the very top of `AddIbkrClient` (after `ArgumentNullException`-style guards, before building options), add:

```csharp
if (services.Any(d => d.ServiceType == typeof(IbkrClientRegistrationMarker)))
{
    throw new InvalidOperationException(
        "AddIbkrClient has already been called on this IServiceCollection. " +
        "Register at most one IbkrConduit per IServiceProvider, or use " +
        "IIbkrClientManager (AddIbkrClientManager) to host multiple accounts.");
}

services.AddSingleton<IbkrClientRegistrationMarker>();
```

Add `using System.Linq;` if not already present.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*ServiceCollectionExtensionsGuardTests*"`
Expected: PASS.

- [ ] **Step 5: Run the full unit suite to confirm no regression**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit`
Expected: PASS (no existing test registers `AddIbkrClient` twice on one collection).

- [ ] **Step 6: Commit**

```bash
git add src/IbkrConduit/Http/ServiceCollectionExtensions.cs tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsGuardTests.cs
git commit -m "feat(di): guard against double AddIbkrClient registration"
```

---

## Task 3: Extract `BuildTenantServices` (behavior-preserving refactor)

**Files:**
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Http/BuildTenantServicesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class BuildTenantServicesTests
{
    [Fact]
    public void BuildTenantServices_ResolvesIIbkrClient()
    {
        using var creds = FakeCredentials.Create();
        var options = new IbkrClientOptions { Credentials = creds, BaseUrl = "https://api.test" };
        var services = new ServiceCollection();
        services.AddLogging();

        ServiceCollectionExtensions.BuildTenantServices(services, creds, options, options.BaseUrl!);

        using var provider = services.BuildServiceProvider();
        provider.GetService<IIbkrClient>().ShouldNotBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*BuildTenantServicesTests*"`
Expected: FAIL — `BuildTenantServices` does not exist.

- [ ] **Step 3: Extract the method**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`, move the body of `AddIbkrClient` that runs *after* options validation (the `endpointMap` build through the `IIbkrClient` facade registration — current lines ~48–83) into a new `internal static` method, and have `AddIbkrClient` call it:

```csharp
public static IServiceCollection AddIbkrClient(
    this IServiceCollection services,
    Action<IbkrClientOptions> configure)
{
    if (services.Any(d => d.ServiceType == typeof(IbkrClientRegistrationMarker)))
    {
        throw new InvalidOperationException( /* message from Task 2 */ );
    }
    services.AddSingleton<IbkrClientRegistrationMarker>();

    var clientOptions = new IbkrClientOptions();
    configure(clientOptions);
    ValidateOptions(clientOptions);

    var credentials = clientOptions.Credentials!;
    var baseUrl = clientOptions.BaseUrl ?? _ibkrBaseUrl;

    BuildTenantServices(services, credentials, clientOptions, baseUrl);
    return services;
}

/// <summary>
/// Registers one fully-isolated IbkrConduit graph (all Refit pipelines,
/// operations, session lifecycle, health, and the IIbkrClient facade) into
/// <paramref name="services"/> for a single tenant's credentials. Shared by
/// the single-account AddIbkrClient path and IIbkrClientManager's per-tenant
/// child providers, so both build an identical graph.
/// </summary>
internal static void BuildTenantServices(
    IServiceCollection services,
    IbkrOAuthCredentials credentials,
    IbkrClientOptions clientOptions,
    string baseUrl)
{
    var endpointMap = RefitEndpointMap.Build([
        typeof(IIbkrPortfolioApi), typeof(IIbkrContractApi), typeof(IIbkrOrderApi),
        typeof(IIbkrMarketDataApi), typeof(IIbkrAccountApi), typeof(IIbkrAlertApi),
        typeof(IIbkrWatchlistApi), typeof(IIbkrFyiApi), typeof(IIbkrEventContractApi),
    ]);
    services.AddSingleton(endpointMap);

    RateLimitingAndResilienceRegistration.Register(services);
    SessionServiceRegistration.Register(services, credentials, clientOptions, baseUrl);
    ConsumerPipelineRegistration.Register(services, credentials, clientOptions, endpointMap, baseUrl);
    StreamingAndFlexRegistration.Register(services, credentials, clientOptions, baseUrl);

    services.AddSingleton(_ => new LastSuccessfulCallTracker(TimeProvider.System));
    services.AddSingleton(new HealthStatusOptions());
    services.AddSingleton<SessionHealthState>();
    services.AddSingleton<IHealthStatusCollector>(sp =>
        new HealthStatusCollector(
            sp.GetRequiredService<IIbkrSessionApi>(),
            sp.GetRequiredService<ISessionTokenProvider>(),
            sp.GetRequiredService<IIbkrWebSocketClient>(),
            sp.GetRequiredService<LastSuccessfulCallTracker>(),
            sp.GetRequiredService<RateLimiter>(),
            sp.GetRequiredService<HealthStatusOptions>(),
            sp.GetRequiredService<SessionHealthState>(),
            TimeProvider.System));

    services.AddSingleton<IIbkrClient, IbkrClient>();
}
```

Keep `ValidateOptions` and the marker on `AddIbkrClient` only — `BuildTenantServices` does not validate or add the marker (the manager validates separately and must be callable per tenant).

- [ ] **Step 4: Run tests to verify pass + no regression**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*BuildTenantServicesTests*"`
Expected: PASS.
Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*SessionTests*"`
Expected: PASS (the extraction is behavior-preserving; existing integration tests still pass).

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Http/ServiceCollectionExtensions.cs tests/IbkrConduit.Tests.Unit/Http/BuildTenantServicesTests.cs
git commit -m "refactor(di): extract BuildTenantServices from AddIbkrClient"
```

---

## Task 4: `ISharedRateGovernor` no-op seam (B seam)

**Files:**
- Create: `src/IbkrConduit/Http/ISharedRateGovernor.cs`
- Modify: `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs`
- Modify: `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Http/SharedRateGovernorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using IbkrConduit.Http;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class SharedRateGovernorTests
{
    [Fact]
    public async Task NoOpSharedRateGovernor_AcquireAsync_CompletesImmediately()
    {
        ISharedRateGovernor governor = new NoOpSharedRateGovernor();
        await Should.NotThrowAsync(() => governor.AcquireAsync(CancellationToken.None).AsTask());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*SharedRateGovernorTests*"`
Expected: FAIL — `ISharedRateGovernor` / `NoOpSharedRateGovernor` do not exist.

- [ ] **Step 3: Create the interface and no-op**

Create `src/IbkrConduit/Http/ISharedRateGovernor.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Http;

/// <summary>
/// Process-wide gate shared by every tenant, acquired before each tenant's own
/// rate limiter. The default implementation is a no-op; a future spec replaces
/// it with an adaptive IP-level governor (penalty-box back-off, concurrency
/// ceiling) without changing call sites. See the multi-tenant design spec, item B.
/// </summary>
public interface ISharedRateGovernor
{
    /// <summary>Acquires permission to proceed with one outbound request.</summary>
    ValueTask AcquireAsync(CancellationToken cancellationToken);
}

/// <summary>Pass-through governor: imposes no shared limit.</summary>
[ExcludeFromCodeCoverage]
public sealed class NoOpSharedRateGovernor : ISharedRateGovernor
{
    /// <inheritdoc />
    public ValueTask AcquireAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
```

- [ ] **Step 4: Wire the call site into `GlobalRateLimitingHandler`**

In `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs`:

Add field + constructor parameter:

```csharp
private readonly RateLimiter _limiter;
private readonly ISharedRateGovernor _governor;
private readonly ILogger<GlobalRateLimitingHandler> _logger;

public GlobalRateLimitingHandler(
    RateLimiter limiter, ISharedRateGovernor governor, ILogger<GlobalRateLimitingHandler> logger)
{
    _limiter = limiter;
    _governor = governor;
    _logger = logger;

    IbkrConduitDiagnostics.Meter.CreateObservableGauge(
        "ibkr.conduit.ratelimiter.global.queue_depth",
        () => _limiter.GetStatistics()?.CurrentQueuedCount ?? 0);
}
```

In `SendAsync`, acquire the shared governor first (before the per-tenant limiter):

```csharp
protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken cancellationToken)
{
    await _governor.AcquireAsync(cancellationToken);

    var sw = Stopwatch.StartNew();
    using var lease = await _limiter.AcquireAsync(1, cancellationToken);
    // ... rest unchanged ...
}
```

- [ ] **Step 5: Register the no-op in the per-tenant pipeline**

In `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs`, inside `Register`, add (so each tenant resolves a governor; the manager will later seed a shared instance — Task 7):

```csharp
services.TryAddSingleton<ISharedRateGovernor, NoOpSharedRateGovernor>();
```

Add `using Microsoft.Extensions.DependencyInjection.Extensions;` for `TryAddSingleton`. Using `TryAdd` means a manager-seeded `ISharedRateGovernor` already present in the child collection wins.

- [ ] **Step 6: Run tests to verify pass + no regression**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*SharedRateGovernorTests*"`
Expected: PASS.
Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*RateLimit*" "*SessionTests*"`
Expected: PASS (no-op governor does not change behavior).

- [ ] **Step 7: Commit**

```bash
git add src/IbkrConduit/Http/ISharedRateGovernor.cs src/IbkrConduit/Http/GlobalRateLimitingHandler.cs src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs tests/IbkrConduit.Tests.Unit/Http/SharedRateGovernorTests.cs
git commit -m "feat(http): add no-op ISharedRateGovernor seam in global rate limiter"
```

---

## Task 5: `TenantContext` + per-tenant telemetry tagging (C)

**Files:**
- Create: `src/IbkrConduit/Diagnostics/TenantContext.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` (register a default `TenantContext` in `BuildTenantServices`)
- Modify emit sites (checklist in Step 5)
- Test: `tests/IbkrConduit.Tests.Unit/Diagnostics/TenantTaggingTests.cs`

- [ ] **Step 1: Write the failing test (representative components)**

```csharp
using System.Diagnostics.Metrics;
using IbkrConduit.Diagnostics;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Diagnostics;

public class TenantTaggingTests
{
    [Fact]
    public void TenantContext_ExposesTenantId()
    {
        var ctx = new TenantContext("tenant-42");
        ctx.TenantId.ShouldBe("tenant-42");
    }

    // Representative metric-tag assertion: drive one tagged component (the
    // GlobalRateLimitingHandler via its rejected counter, or SessionManager's
    // session counter) and assert the emitted measurement carries
    // LogFields.TenantId == the tenant id, using a MeterListener.
    // (Wire this once the chosen component is updated in Step 5.)
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*TenantTaggingTests*"`
Expected: FAIL — `TenantContext` does not exist.

- [ ] **Step 3: Create `TenantContext`**

Create `src/IbkrConduit/Diagnostics/TenantContext.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Diagnostics;

/// <summary>
/// Per-provider singleton carrying the tenant identity used to tag telemetry
/// (metrics, spans, log scopes) so multiple tenants in one process are
/// distinguishable. Seeded once per child provider with the explicit tenant id.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TenantContext(string TenantId);
```

- [ ] **Step 4: Register a default `TenantContext` in `BuildTenantServices`**

In `BuildTenantServices` (Task 3), add near the top so single-account usage also has a value (its `TenantId` = the credentials' `TenantId`):

```csharp
services.AddSingleton(new TenantContext(credentials.TenantId));
```

The manager seeds its own `TenantContext(tenantId)` into the child collection *before* calling `BuildTenantServices` (Task 7) — but `BuildTenantServices` uses `AddSingleton`, which would add a second. To let the manager win, change this line to `services.TryAddSingleton(new TenantContext(credentials.TenantId));` and add `using Microsoft.Extensions.DependencyInjection.Extensions;`. Because the manager normalizes `credentials.TenantId = tenantId` (Task 7), both values are identical anyway; `TryAdd` is belt-and-suspenders.

- [ ] **Step 5: Stamp `LogFields.TenantId` at each emit site**

For each component below: add a `TenantContext` constructor parameter (resolved from DI), store it, and add the tenant tag at its metric/span emit calls. The pattern per site:

- Metrics: add to the `TagList`/`KeyValuePair[]` passed to `.Add`/`.Record`:
  `new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId)`
- Spans: after `StartActivity(...)`:
  `activity?.SetTag(LogFields.TenantId, _tenant.TenantId);`
- Logs: wrap the component's primary operation in `using var _ = _logger.BeginScope(new Dictionary<string, object> { [LogFields.TenantId] = _tenant.TenantId });` (or include in existing structured log state).

Checklist (each is its own constructor edit + tag insertion; all resolve `TenantContext` from DI, available because Step 4 registers it):
- [ ] `src/IbkrConduit/Auth/OAuthSigningHandler.cs` — span/metrics at request signing.
- [ ] `src/IbkrConduit/Http/GlobalRateLimitingHandler.cs` — `_rejectedCount.Add`, wait span.
- [ ] `src/IbkrConduit/Http/EndpointRateLimitingHandler.cs` — wait/reject metrics.
- [ ] `src/IbkrConduit/Session/SessionManager.cs` — session/refresh counters + spans.
- [ ] `src/IbkrConduit/Session/TickleTimer.cs` — tickle counters.
- [ ] `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` — message/reconnect/heartbeat counters + the connection-state gauge tag.
- [ ] `src/IbkrConduit/Client/OrderOperations.cs` — submission/cancel/question counters + spans.
- [ ] `src/IbkrConduit/Client/MarketDataOperations.cs` — snapshot/preflight/history counters + spans.
- [ ] `src/IbkrConduit/Client/FlexOperations.cs` — query/poll/error counters + spans.
- [ ] `src/IbkrConduit/Client/PortfolioOperations.cs` — operation spans.

Worked example — `GlobalRateLimitingHandler` (already has a `TenantContext` available via DI after Step 4; add the field + ctor param exactly as the `_governor` field was added in Task 4):

```csharp
private readonly TenantContext _tenant;
// ctor: add `TenantContext tenant` parameter, assign `_tenant = tenant;`

// at the rejected counter:
_rejectedCount.Add(1, new KeyValuePair<string, object?>(LogFields.TenantId, _tenant.TenantId));
```

- [ ] **Step 6: Complete the representative metric test**

In `TenantTaggingTests`, add a test that builds a tenant provider with a known id via `BuildTenantServices`, resolves a tagged component, triggers one emit, and asserts the captured measurement's tags contain `LogFields.TenantId == "<id>"` using a `MeterListener` filtered to that instrument. (Use `SessionManager`'s session counter or the rate-limiter rejected counter — whichever is simplest to trigger in isolation.)

- [ ] **Step 7: Run tests + lint**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*TenantTaggingTests*"`
Expected: PASS.
Run: `dotnet test --project tests/IbkrConduit.Tests.Unit` and `dotnet build --configuration Release`
Expected: PASS (every emit-site constructor change compiles; existing tests that construct these components directly must pass the new `TenantContext` — update those test constructions to pass `new TenantContext("test")`).

- [ ] **Step 8: Commit**

```bash
git add src/IbkrConduit/Diagnostics/TenantContext.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs src/IbkrConduit/Auth src/IbkrConduit/Http src/IbkrConduit/Session src/IbkrConduit/Streaming src/IbkrConduit/Client tests/IbkrConduit.Tests.Unit
git commit -m "feat(telemetry): tag metrics, spans, and logs with TenantId"
```

---

## Task 6: `ManagedTenant` + `ITenantBuilder` + `TenantBuilder`

**Files:**
- Create: `src/IbkrConduit/Client/IManagedTenant.cs`
- Create: `src/IbkrConduit/Client/ManagedTenant.cs`
- Create: `src/IbkrConduit/Client/ITenantBuilder.cs`
- Create: `src/IbkrConduit/Client/TenantBuilder.cs`
- Test: covered indirectly by the manager unit tests (faked `ITenantBuilder`) and the integration tests (real `TenantBuilder`); no standalone unit test for the real builder (it does real I/O — exercised in Task 8).

- [ ] **Step 1: Create `IManagedTenant` + `ManagedTenant`**

Create `src/IbkrConduit/Client/IManagedTenant.cs`:

```csharp
namespace IbkrConduit.Client;

/// <summary>
/// One live tenant owned by the manager — abstracted so the manager's registry
/// and lifecycle logic is unit-testable with a fake. Disposal performs the
/// tenant's graceful teardown.
/// </summary>
internal interface IManagedTenant : IAsyncDisposable
{
    /// <summary>The tenant's client facade.</summary>
    IIbkrClient Client { get; }
}
```

Create `src/IbkrConduit/Client/ManagedTenant.cs`:

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;

namespace IbkrConduit.Client;

/// <summary>
/// Real tenant: its isolated child provider, the resolved client, and the
/// manager-owned credentials. Disposal does a best-effort IBKR logout (frees the
/// server-side session slot) BEFORE tearing down the child provider — which stops
/// the tickle timer and closes the socket — then disposes the credentials.
/// </summary>
internal sealed class ManagedTenant(
    ServiceProvider provider, IIbkrClient client, IbkrOAuthCredentials credentials) : IManagedTenant
{
    public IIbkrClient Client { get; } = client;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await provider.GetRequiredService<IIbkrSessionApi>().LogoutAsync();
        }
        catch
        {
            // Best-effort cleanup — never let a logout failure block teardown.
        }

        await provider.DisposeAsync();   // disposes session manager, tickle timer, socket, etc.
        credentials.Dispose();
    }
}
```

Not `[ExcludeFromCodeCoverage]`: the best-effort-logout branch is real behavior, verified by the Task 8 integration "remove hits /logout" test.

- [ ] **Step 2: Create `ITenantBuilder` (seam)**

Create `src/IbkrConduit/Client/ITenantBuilder.cs`:

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Builds a fully-isolated, live tenant graph: child provider + eager session
/// init + WebSocket connect. Abstracted so the manager's registry/lifecycle logic
/// is unit-testable without network. The real implementation is <see cref="TenantBuilder"/>.
/// </summary>
internal interface ITenantBuilder
{
    /// <summary>
    /// Builds the child provider for <paramref name="credentials"/>, eagerly
    /// authenticates and connects, and returns the live tenant. Throws on failure
    /// after disposing any partially-built graph.
    /// </summary>
    Task<IManagedTenant> BuildAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor,
        CancellationToken cancellationToken);
}
```

Add `using IbkrConduit.Http;` for `ISharedRateGovernor`.

- [ ] **Step 3: Create `TenantBuilder` (real)**

Create `src/IbkrConduit/Client/TenantBuilder.cs`:

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Diagnostics;
using IbkrConduit.Http;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Builds a tenant's child <see cref="ServiceProvider"/> by reusing
/// <see cref="ServiceCollectionExtensions.BuildTenantServices"/>, seeding the
/// shared root services, then eagerly initializing the session and WebSocket.
/// </summary>
internal sealed class TenantBuilder(ILoggerFactory loggerFactory) : ITenantBuilder
{
    public async Task<IManagedTenant> BuildAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(sharedGovernor);          // shared instance wins (TryAdd in pipeline)
        services.AddSingleton(new TenantContext(tenantId));

        var baseUrl = effectiveOptions.BaseUrl ?? "https://api.ibkr.com";
        ServiceCollectionExtensions.BuildTenantServices(services, credentials, effectiveOptions, baseUrl);

        var provider = services.BuildServiceProvider();
        try
        {
            var client = provider.GetRequiredService<IIbkrClient>();
            // Eager: force session init then connect the stream.
            await provider.GetRequiredService<ISessionManager>()
                .EnsureInitializedAsync(cancellationToken);
            await provider.GetRequiredService<IIbkrWebSocketClient>()
                .ConnectAsync(cancellationToken);
            return new ManagedTenant(provider, client, credentials);
        }
        catch
        {
            await provider.DisposeAsync();
            credentials.Dispose();   // success hands ownership to ManagedTenant; failure cleans up here
            throw;
        }
    }
}
```

Method names are verified against the current code: `ISessionManager.EnsureInitializedAsync(ct)` (session init) and `IIbkrWebSocketClient.ConnectAsync(ct)` (stream connect) — both `internal`, resolvable from the child provider. The `"https://api.ibkr.com"` literal mirrors `ServiceCollectionExtensions._ibkrBaseUrl`; optionally expose that const as `internal` and reference it instead of duplicating.

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build --configuration Release`
Expected: PASS. (No standalone test here; `TenantBuilder` is exercised by Task 8 integration tests and the manager uses the interface.)

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Client/ManagedTenant.cs src/IbkrConduit/Client/ITenantBuilder.cs src/IbkrConduit/Client/TenantBuilder.cs
git commit -m "feat(client): add ITenantBuilder + TenantBuilder for per-tenant graphs"
```

---

## Task 7: `IIbkrClientManager` + `IbkrClientManager` + `AddIbkrClientManager`

**Files:**
- Create: `src/IbkrConduit/Client/IIbkrClientManager.cs`
- Create: `src/IbkrConduit/Client/IbkrClientManager.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` (add `AddIbkrClientManager`)
- Test: `tests/IbkrConduit.Tests.Unit/Client/IbkrClientManagerTests.cs`

- [ ] **Step 1: Create the public interface**

Create `src/IbkrConduit/Client/IIbkrClientManager.cs`:

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Manages multiple isolated IbkrConduit instances — one per credential/tenant —
/// in a single process, with runtime add/remove. See the multi-tenant design spec.
/// </summary>
public interface IIbkrClientManager : IAsyncDisposable
{
    /// <summary>
    /// Adds a tenant: builds its isolated graph, eagerly authenticates and connects
    /// the WebSocket, and returns its client. Throws <see cref="InvalidOperationException"/>
    /// if <paramref name="tenantId"/> is already active; throws (and leaves nothing
    /// registered) if authentication fails. The manager takes ownership of
    /// <paramref name="credentials"/> and disposes them on remove/dispose.
    /// </summary>
    Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a tenant's client, or <c>false</c> if not active.</summary>
    bool TryGetClient(string tenantId, out IIbkrClient client);

    /// <summary>Gets a tenant's client; throws if not active.</summary>
    IIbkrClient GetClient(string tenantId);

    /// <summary>Tears a tenant down (cancel in-flight, close socket, logout, dispose). Returns <c>false</c> if not active.</summary>
    Task<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>The currently active tenant ids.</summary>
    IReadOnlyCollection<string> ActiveTenants { get; }
}
```

- [ ] **Step 2: Write the failing unit tests (faked builder)**

```csharp
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Client;

public class IbkrClientManagerTests
{
    private static IbkrOAuthCredentials Creds(string tenant) =>
        new(tenant, "CONSUMERK", "tok", "sec", RSA.Create(2048), RSA.Create(2048), BigInteger.One);

    private static IbkrClientManager NewManager(ITenantBuilder builder) =>
        new(builder, new IbkrClientOptions(), new NoOpSharedRateGovernor());

    [Fact]
    public async Task AddAsync_NewTenant_IsRetrievable()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);

        var client = await mgr.AddAsync("t1", Creds("t1"));

        client.ShouldNotBeNull();
        mgr.TryGetClient("t1", out _).ShouldBeTrue();
        mgr.ActiveTenants.ShouldBe(new[] { "t1" });
    }

    [Fact]
    public async Task AddAsync_DuplicateTenant_Throws()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => mgr.AddAsync("t1", Creds("t1")));
    }

    [Fact]
    public async Task AddAsync_BuildFails_DisposesCredsAndRegistersNothing()
    {
        var builder = new FakeTenantBuilder { ThrowOnBuild = true };
        await using var mgr = NewManager(builder);
        var creds = Creds("t1");

        await Should.ThrowAsync<InvalidOperationException>(() => mgr.AddAsync("t1", creds));

        mgr.ActiveTenants.ShouldBeEmpty();
        Should.Throw<ObjectDisposedException>(() => creds.SignaturePrivateKey.ExportParameters(true));
    }

    [Fact]
    public async Task RemoveAsync_PresentTenant_TearsDownAndReturnsTrue()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"));

        (await mgr.RemoveAsync("t1")).ShouldBeTrue();
        mgr.TryGetClient("t1", out _).ShouldBeFalse();
        builder.LastTenant!.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_AbsentTenant_ReturnsFalse() =>
        (await NewManager(new FakeTenantBuilder()).RemoveAsync("nope")).ShouldBeFalse();

    [Fact]
    public void GetClient_Absent_Throws() =>
        Should.Throw<KeyNotFoundException>(() => NewManager(new FakeTenantBuilder()).GetClient("nope"));

    [Fact]
    public async Task DisposeAsync_TearsDownAllTenants()
    {
        var builder = new FakeTenantBuilder();
        var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"));
        await mgr.AddAsync("t2", Creds("t2"));

        await mgr.DisposeAsync();

        builder.Built.ShouldAllBe(t => t.Disposed);
    }
}
```

Add a `FakeTenantBuilder` test double in the same file or `tests/IbkrConduit.Tests.Unit/Client/FakeTenantBuilder.cs`:

```csharp
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;

namespace IbkrConduit.Tests.Unit.Client;

// Records build/dispose without any I/O. Returns IManagedTenant (defined in
// Task 6), so the fake needs no real ServiceProvider.
internal sealed class FakeTenantBuilder : ITenantBuilder
{
    public bool ThrowOnBuild { get; set; }
    public List<FakeManagedTenant> Built { get; } = new();
    public FakeManagedTenant? LastTenant => Built.Count > 0 ? Built[^1] : null;

    public Task<IManagedTenant> BuildAsync(
        string tenantId, IbkrOAuthCredentials credentials, IbkrClientOptions effectiveOptions,
        ISharedRateGovernor sharedGovernor, CancellationToken cancellationToken)
    {
        if (ThrowOnBuild)
        {
            credentials.Dispose();                       // builder owns cleanup on failure
            throw new InvalidOperationException("auth failed");
        }
        var tenant = new FakeManagedTenant();
        Built.Add(tenant);
        return Task.FromResult<IManagedTenant>(tenant);
    }
}

internal sealed class FakeManagedTenant : IManagedTenant
{
    public bool Disposed { get; private set; }
    public IIbkrClient Client { get; } = new StubIbkrClient();
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
```

`StubIbkrClient` is a minimal `IIbkrClient` implementation returning `null!`/throwing for members the manager tests never call (only identity is asserted). Place it under `tests/IbkrConduit.Tests.Unit/Client/StubIbkrClient.cs`.

> **Note:** `IManagedTenant` and the builder's dispose-credentials-on-failure behavior are both defined in Task 6, so the `AddAsync_BuildFails` test simply asserts the contract that the **builder** disposes credentials when it throws.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrClientManagerTests*"`
Expected: FAIL — `IbkrClientManager` does not exist.

- [ ] **Step 4: Implement `IbkrClientManager`**

Create `src/IbkrConduit/Client/IbkrClientManager.cs`:

```csharp
using System.Collections.Concurrent;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <inheritdoc />
internal sealed class IbkrClientManager(
    ITenantBuilder builder,
    IbkrClientOptions baselineOptions,
    ISharedRateGovernor sharedGovernor) : IIbkrClientManager
{
    private readonly ConcurrentDictionary<string, IManagedTenant> _tenants = new(StringComparer.Ordinal);
    private int _disposed;

    public IReadOnlyCollection<string> ActiveTenants => _tenants.Keys.ToArray();

    public async Task<IIbkrClient> AddAsync(
        string tenantId,
        IbkrOAuthCredentials credentials,
        Action<IbkrClientOptions>? configureOverrides = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(credentials);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        // Normalize identity: explicit tenantId wins (records are immutable -> with).
        var normalized = credentials with { TenantId = tenantId };

        // Reserve the slot atomically before any slow work.
        var reservation = new TaskCompletionSource<IManagedTenant>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentinel = new ReservedTenant(reservation.Task);
        if (!_tenants.TryAdd(tenantId, sentinel))
        {
            normalized.Dispose();
            throw new InvalidOperationException($"Tenant '{tenantId}' is already active.");
        }

        try
        {
            var effective = baselineOptions.Clone();
            configureOverrides?.Invoke(effective);
            effective.Credentials = normalized;

            var tenant = await builder.BuildAsync(tenantId, normalized, effective, sharedGovernor, cancellationToken);
            _tenants[tenantId] = tenant;       // replace sentinel with the live tenant
            reservation.SetResult(tenant);
            return tenant.Client;
        }
        catch
        {
            _tenants.TryRemove(tenantId, out _);
            reservation.TrySetException(new InvalidOperationException($"Failed to add tenant '{tenantId}'."));
            throw;                              // builder already disposed creds + provider on failure
        }
    }

    public bool TryGetClient(string tenantId, out IIbkrClient client)
    {
        if (_tenants.TryGetValue(tenantId, out var tenant) && tenant is not ReservedTenant)
        {
            client = tenant.Client;
            return true;
        }
        client = null!;
        return false;
    }

    public IIbkrClient GetClient(string tenantId) =>
        TryGetClient(tenantId, out var client)
            ? client
            : throw new KeyNotFoundException($"Tenant '{tenantId}' is not active.");

    public async Task<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!_tenants.TryRemove(tenantId, out var tenant) || tenant is ReservedTenant)
        {
            return false;
        }
        await tenant.DisposeAsync();           // teardown: provider dispose (stops tickle, closes socket, disposes creds)
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
        foreach (var id in _tenants.Keys.ToArray())
        {
            if (_tenants.TryRemove(id, out var tenant) && tenant is not ReservedTenant)
            {
                await tenant.DisposeAsync();
            }
        }
    }

    /// <summary>Placeholder entry occupying a tenant slot while its build is in flight.</summary>
    private sealed class ReservedTenant(Task<IManagedTenant> pending) : IManagedTenant
    {
        public IIbkrClient Client => throw new InvalidOperationException("Tenant is still initializing.");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<IManagedTenant> Pending { get; } = pending;
    }
}
```

> **Teardown realization (verified):** the spec's teardown (stop tickle → cancel in-flight → close socket → best-effort `/logout` → dispose provider → dispose creds) is realized by `ManagedTenant.DisposeAsync` (Task 6). It calls the verified `IIbkrSessionApi.LogoutAsync()` (`POST /v1/api/logout`, already on the Refit interface) best-effort first, then `provider.DisposeAsync()` — which disposes `ISessionManager`, `TickleTimer`, and `IbkrWebSocketClient`. Confirm those components' `DisposeAsync` stop the tickle loop and close the socket gracefully (they implement `IAsyncDisposable`); if any does not, that is a one-line fix in that component, not new manager code. In-flight requests are cancelled abruptly by provider disposal (no managed drain), per the spec.

- [ ] **Step 5: Add `AddIbkrClientManager` registration**

In `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>Marker proving AddIbkrClientManager has already run.</summary>
private sealed class IbkrClientManagerRegistrationMarker;

/// <summary>
/// Registers the multi-tenant <see cref="IIbkrClientManager"/> singleton with the
/// given baseline options applied to every tenant. Credentials are supplied per
/// tenant via <see cref="IIbkrClientManager.AddAsync"/>, not here.
/// </summary>
public static IServiceCollection AddIbkrClientManager(
    this IServiceCollection services,
    Action<IbkrClientOptions>? configureBaseline = null)
{
    if (services.Any(d => d.ServiceType == typeof(IbkrClientManagerRegistrationMarker)))
    {
        throw new InvalidOperationException("AddIbkrClientManager has already been called on this IServiceCollection.");
    }
    services.AddSingleton<IbkrClientManagerRegistrationMarker>();

    var baseline = new IbkrClientOptions();
    configureBaseline?.Invoke(baseline);

    services.TryAddSingleton<ISharedRateGovernor, NoOpSharedRateGovernor>();
    services.AddSingleton<ITenantBuilder, TenantBuilder>();
    services.AddSingleton<IIbkrClientManager>(sp =>
        new IbkrClientManager(
            sp.GetRequiredService<ITenantBuilder>(),
            baseline,
            sp.GetRequiredService<ISharedRateGovernor>()));

    return services;
}
```

Add `using Microsoft.Extensions.DependencyInjection.Extensions;` for `TryAddSingleton`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*IbkrClientManagerTests*"`
Expected: PASS (all 7 tests).
Run: `dotnet build --configuration Release` and `dotnet format --verify-no-changes`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/IbkrConduit/Client/IIbkrClientManager.cs src/IbkrConduit/Client/IbkrClientManager.cs src/IbkrConduit/Client/IManagedTenant.cs src/IbkrConduit/Client/ManagedTenant.cs src/IbkrConduit/Client/ITenantBuilder.cs src/IbkrConduit/Client/TenantBuilder.cs src/IbkrConduit/Http/ServiceCollectionExtensions.cs tests/IbkrConduit.Tests.Unit/Client
git commit -m "feat(client): add IIbkrClientManager for runtime multi-tenant lifecycle"
```

---

## Task 8: Integration tests (WireMock) — eager add, isolation, remove, 401 recovery

**Files:**
- Test: `tests/IbkrConduit.Tests.Integration/MultiTenant/ClientManagerTests.cs`

These use the full DI stack via `AddIbkrClientManager` against WireMock — **no fakes**. Mirror the harness setup of an existing integration test (e.g. `tests/IbkrConduit.Tests.Integration/Session/SessionTests.cs`) for WireMock stubbing of the OAuth/LST flow (`/oauth/...`, `ssodh/init`), the WebSocket endpoint, and per-endpoint stubs. Use synthetic credentials from the existing `TestCredentials` helper.

- [ ] **Step 1: Eager add — full flow succeeds**

Write a test that stubs the LST → `ssodh/init` → WS connect path and one data endpoint, then:

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddIbkrClientManager(o => o.BaseUrl = wireMock.Url);
await using var provider = services.BuildServiceProvider();
var mgr = provider.GetRequiredService<IIbkrClientManager>();

var client = await mgr.AddAsync("acct-a", TestCredentials.Create("acct-a"));

var accounts = await client.Accounts.GetAccountsAsync(CancellationToken.None);
accounts.Accounts.ShouldNotBeEmpty();
mgr.ActiveTenants.ShouldBe(new[] { "acct-a" });
```

Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*ClientManagerTests*"`
Expected: this test PASSES once stubs are wired.

- [ ] **Step 2: Two-tenant isolation (headline)**

Add two tenants concurrently with distinct ids/credentials (distinct consumer keys). Assert WireMock received signed requests for both, that each tenant's request carried its own consumer key (inspect the captured `Authorization` headers / `oauth_consumer_key`), and that `GetClient("acct-a")` and `GetClient("acct-b")` return distinct instances. Drive one API call on each and assert both succeed independently.

- [ ] **Step 3: Remove hits `/logout` and de-registers**

After adding `acct-a`, call `await mgr.RemoveAsync("acct-a")`. Assert: returns `true`; WireMock recorded a `POST /logout` (or the verified teardown call from Task 7's `/logout` decision); `mgr.TryGetClient("acct-a", out _)` is `false`; `GetClient` throws `KeyNotFoundException`.

- [ ] **Step 4: 401 recovery within a managed tenant (mandatory per testing rules)**

Stub a data endpoint to return `401` on first call then `200` on retry, with a fresh LST + `ssodh/init` stub for the re-auth. Add a tenant, call the endpoint, and assert: first response is 401-driven re-auth (new LST + `ssodh/init` observed), the original request is retried, and the final result succeeds. This verifies `TokenRefreshHandler` still works inside a manager-built child provider.

- [ ] **Step 5: Telemetry attribution**

With a `MeterListener` (or `ActivityListener`) capturing during calls on `acct-a` and `acct-b`, assert emitted measurements carry `LogFields.TenantId` equal to the respective tenant id — proving the two tenants are distinguishable.

- [ ] **Step 6: Run the integration class + commit**

Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*ClientManagerTests*"`
Expected: PASS (all scenarios).

```bash
git add tests/IbkrConduit.Tests.Integration/MultiTenant/ClientManagerTests.cs
git commit -m "test(multitenant): integration tests for IIbkrClientManager"
```

---

## Final Steps (after Task 8)

- [ ] **Full check:** `dotnet build --configuration Release` && `dotnet test --configuration Release` && `dotnet format --verify-no-changes` (run as three separate commands per repo bash rules).
- [ ] **Update `docs/implementation-status.md`:** add a "Milestone 8 — Multi-Tenant Client Manager" section marking these tasks Done, linking the spec.
- [ ] **Update README / consumer docs:** document `AddIbkrClientManager` + `IIbkrClientManager` as the multi-account entry point; note the single-account `AddIbkrClient` path is unchanged and that two credentials must not be registered on one `IServiceCollection`.

## Deferred (separate specs/PRs — do NOT build here)

- **Spec B:** adaptive shared IP rate governor (replace `NoOpSharedRateGovernor`).
- **Two-account E2E:** real `[EnvironmentFact]` test gated on a second paper-account credential set.
- **`ReplaceAsync`:** credential rotation, only if a real need emerges.
