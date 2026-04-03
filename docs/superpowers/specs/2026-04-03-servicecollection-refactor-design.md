# ServiceCollectionExtensions Refactor — Design Spec

## Goal

Break `ServiceCollectionExtensions.cs` (299 lines) into focused files by concern, and adopt the standard .NET `Action<TOptions>` configuration pattern for `AddIbkrClient`.

## Background

The current file handles LST client setup, session token provider, rate limiter creation, resilience pipeline, session API pipeline, 9 consumer Refit client registrations, 9 operations registrations, WebSocket, Flex, and the unified facade — all in one method. It works correctly but is hard to navigate and modify.

## Changes

### 1. Options Pattern for `AddIbkrClient`

**Before:**
```csharp
var options = new IbkrClientOptions { BaseUrl = "..." };
services.AddIbkrClient(credentials, options);
```

**After:**
```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = OAuthCredentialsFactory.FromEnvironment();
    opts.BaseUrl = "...";
    opts.FlexToken = "...";
});
```

Changes required:
- `IbkrClientOptions`: record → class, `init` → `set`, add `Credentials` property (`IbkrOAuthCredentials?`, validated non-null at registration time)
- `AddIbkrClient` signature: `AddIbkrClient(Action<IbkrClientOptions> configure)` — single parameter
- Remove the old `AddIbkrClient(IbkrOAuthCredentials, IbkrClientOptions?)` signature
- Validation: after lambda runs, throw `ArgumentNullException` if `Credentials` is null

`IbkrClientOptions` does not own disposal of `Credentials` — the consumer manages the `IbkrOAuthCredentials` lifetime as before.

### 2. File Split

Split the 299-line monolith into 5 focused files, all in the `IbkrConduit.Http` namespace:

| File | Class | Visibility | Responsibility |
|------|-------|-----------|----------------|
| `ServiceCollectionExtensions.cs` | `ServiceCollectionExtensions` | `public static` | Public `AddIbkrClient` entry point — constructs options, validates, calls internal registrations in order |
| `SessionServiceRegistration.cs` | `SessionServiceRegistration` | `internal static` | LST HttpClient, `ILiveSessionTokenClient`, `ISessionTokenProvider`, `IIbkrSessionApi` pipeline, `ISessionLifecycleNotifier`, `ISessionManager`, `ITickleTimerFactory` |
| `ConsumerPipelineRegistration.cs` | `ConsumerPipelineRegistration` | `internal static` | `RegisterConsumerRefitClient<T>` helper, all 9 consumer Refit clients, all 9 operations implementations |
| `RateLimitingAndResilienceRegistration.cs` | `RateLimitingAndResilienceRegistration` | `internal static` | Global rate limiter, endpoint rate limiters, Polly resilience pipeline |
| `StreamingAndFlexRegistration.cs` | `StreamingAndFlexRegistration` | `internal static` | WebSocket client, `IStreamingOperations`, Flex HttpClient, `FlexClient`, `IFlexOperations` |

Each internal class exposes a single `Register(IServiceCollection services, IbkrOAuthCredentials credentials, IbkrClientOptions options, string baseUrl)` method.

### 3. Orchestrator

`AddIbkrClient` becomes a short orchestrator:

```csharp
public static IServiceCollection AddIbkrClient(
    this IServiceCollection services,
    Action<IbkrClientOptions> configure)
{
    var options = new IbkrClientOptions();
    configure(options);

    ArgumentNullException.ThrowIfNull(options.Credentials, "IbkrClientOptions.Credentials");

    var credentials = options.Credentials;
    var baseUrl = options.BaseUrl ?? "https://api.ibkr.com";

    services.AddSingleton(options);

    RateLimitingAndResilienceRegistration.Register(services);
    SessionServiceRegistration.Register(services, credentials, options, baseUrl);
    ConsumerPipelineRegistration.Register(services, credentials, baseUrl);
    StreamingAndFlexRegistration.Register(services, credentials, options, baseUrl);

    services.AddSingleton<IIbkrClient, IbkrClient>();

    return services;
}
```

### 4. Caller Updates

All callers must be updated from the old signature to the new lambda pattern:

| Caller | Current | New |
|--------|---------|-----|
| `TestHarness.cs` | `services.AddIbkrClient(_credentials, options)` | `services.AddIbkrClient(opts => { opts.Credentials = _credentials; opts.BaseUrl = ...; })` |
| `OAuthCredentialsFactory` examples | `services.AddIbkrClient(creds)` | `services.AddIbkrClient(opts => { opts.Credentials = creds; })` |
| E2E test base classes | `services.AddIbkrClient(creds, options)` | Lambda pattern |
| Example files | Various | Lambda pattern |
| Unit tests that reference `IbkrClientOptions` | `new IbkrClientOptions { ... }` | Same (class construction unchanged, just `init` → `set`) |

## Testing

Pure refactor + public API change. All 94 integration tests + 398 unit tests must pass after updating callers. No new tests needed — existing tests validate the full pipeline works identically.

## Scope Boundaries

### In Scope
- Split `ServiceCollectionExtensions.cs` into 5 files
- Change `IbkrClientOptions` to class with `set` properties + `Credentials`
- Change `AddIbkrClient` to `Action<IbkrClientOptions>` pattern
- Update all callers

### Out of Scope
- Changing what gets registered (same services, same lifetimes, same pipeline order)
- Adding new configuration options
- Changing `IbkrOAuthCredentials` (stays as a record with `IDisposable`)
