# IHttpClientFactory Conversion

**Date:** 2026-04-01
**Status:** Approved
**Goal:** Convert `LiveSessionTokenClient` and `FlexClient` from directly owning `HttpClient` instances to using `IHttpClientFactory` for proper HTTP client lifetime management.

---

## Scope

Two components currently create and own `HttpClient` directly:

1. **`LiveSessionTokenClient`** — holds an `HttpClient` for the LST endpoint
2. **`FlexClient`** — holds an `HttpClient` for the Flex Web Service endpoint

Both should use `IHttpClientFactory` with named clients. This improves:
- HTTP connection pooling and lifetime management
- DNS change handling (HttpClient caches DNS forever)
- Multi-instance safety (each `AddIbkrClient` call gets properly isolated HTTP clients)

---

## Task 1: LiveSessionTokenClient → IHttpClientFactory

### Current

```csharp
public LiveSessionTokenClient(HttpClient httpClient, ILogger<LiveSessionTokenClient> logger)
```

`HttpClient` created in `ServiceCollectionExtensions` with `new HttpClient(handler)`.

### New

```csharp
public LiveSessionTokenClient(IHttpClientFactory httpClientFactory, string clientName, ILogger<LiveSessionTokenClient> logger)
```

Each call creates a fresh `HttpClient` via `_httpClientFactory.CreateClient(_clientName)`.

### DI Registration

Register a named `HttpClient` per `AddIbkrClient` call:

```csharp
var lstClientName = $"IbkrConduit-LST-{credentials.TenantId}";
services.AddHttpClient(lstClientName, c =>
{
    c.BaseAddress = new Uri(_ibkrBaseUrl + "/v1/api/");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
});
```

---

## Task 2: FlexClient → IHttpClientFactory

### Current

```csharp
internal FlexClient(HttpClient httpClient, string flexToken, ILogger<FlexClient> logger)
```

`HttpClient` created in `ServiceCollectionExtensions` with `new HttpClient(handler)`.

### New

```csharp
internal FlexClient(IHttpClientFactory httpClientFactory, string clientName, string flexToken, ILogger<FlexClient> logger)
```

### DI Registration

```csharp
var flexClientName = $"IbkrConduit-Flex-{credentials.TenantId}";
services.AddHttpClient(flexClientName, c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("IbkrConduit/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
});
```

---

## Test Updates

- Update `LiveSessionTokenClientTests` to provide a fake `IHttpClientFactory`
- Update `FlexClientTests` and `FlexClientPollingTests` to provide a fake `IHttpClientFactory`
- Update integration test fakes that construct these clients directly
- Update E2E tests (they use DI, should work automatically)

---

## Files Modified

```
src/IbkrConduit/Auth/LiveSessionTokenClient.cs
src/IbkrConduit/Flex/FlexClient.cs
src/IbkrConduit/Http/ServiceCollectionExtensions.cs
tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs
tests/IbkrConduit.Tests.Unit/Flex/FlexClientTests.cs
tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs
```
