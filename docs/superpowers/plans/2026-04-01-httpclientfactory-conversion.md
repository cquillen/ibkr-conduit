# IHttpClientFactory Conversion Plan

**Date:** 2026-04-01
**Goal:** Convert `LiveSessionTokenClient` and `FlexClient` from directly owning `HttpClient` instances to using `IHttpClientFactory` with named clients for proper HTTP connection lifetime management.
**Architecture:** Replace stored `HttpClient` fields with `IHttpClientFactory` + client name; create `HttpClient` per-call via factory. Register named clients in DI with `services.AddHttpClient(name, ...)`.
**Tech Stack:** .NET, xUnit v3, Shouldly, IHttpClientFactory

---

## Task 1: Convert LiveSessionTokenClient to IHttpClientFactory

### Step 1.1: Write failing test — LiveSessionTokenClient accepts IHttpClientFactory

Update `LiveSessionTokenClientTests` to use a `FakeHttpClientFactory` instead of passing `HttpClient` directly.

**Test changes** (`tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs`):

Add `FakeHttpClientFactory` class and update all test helper code to construct `LiveSessionTokenClient` with `(IHttpClientFactory, string clientName, ILogger)`.

```csharp
// Add at bottom of file, replacing existing FakeHttpHandler:
private sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly Uri? _baseAddress;

    public FakeHttpClientFactory(HttpMessageHandler handler, Uri? baseAddress = null)
    {
        _handler = handler;
        _baseAddress = baseAddress;
    }

    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient(_handler, disposeHandler: false);
        if (_baseAddress != null)
        {
            client.BaseAddress = _baseAddress;
        }

        return client;
    }
}
```

Update the two existing tests to construct `LiveSessionTokenClient` with the new signature:
```csharp
var factory = new FakeHttpClientFactory(handler, new Uri("https://api.ibkr.com/v1/api/"));
var client = new LiveSessionTokenClient(factory, "test-lst", NullLogger<LiveSessionTokenClient>.Instance);
```

Remove the `using var httpClient = new HttpClient(handler) { ... }` blocks.

**Run tests — expect compilation failure** (constructor signature mismatch).

### Step 1.2: Implement LiveSessionTokenClient changes

Update `src/IbkrConduit/Auth/LiveSessionTokenClient.cs`:

- Change constructor to accept `(IHttpClientFactory httpClientFactory, string clientName, ILogger<LiveSessionTokenClient> logger)`
- Store `_httpClientFactory` and `_clientName` fields
- In `GetLiveSessionTokenAsync`, create `using var httpClient = _httpClientFactory.CreateClient(_clientName);`
- Replace all `_httpClient` references with local `httpClient`

```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly string _clientName;
private readonly ILogger<LiveSessionTokenClient> _logger;

public LiveSessionTokenClient(IHttpClientFactory httpClientFactory, string clientName, ILogger<LiveSessionTokenClient> logger)
{
    _httpClientFactory = httpClientFactory;
    _clientName = clientName;
    _logger = logger;
}
```

In `GetLiveSessionTokenAsync`:
```csharp
using var httpClient = _httpClientFactory.CreateClient(_clientName);
// Use httpClient instead of _httpClient for BaseAddress and SendAsync
```

**Run tests — expect pass.**

**Commit:** `feat: convert LiveSessionTokenClient to IHttpClientFactory`

---

## Task 2: Convert FlexClient to IHttpClientFactory

### Step 2.1: Write failing test — FlexClient accepts IHttpClientFactory

Update both `FlexClientTests` and `FlexClientPollingTests` to use a `FakeHttpClientFactory`.

**FlexClientTests** (`tests/IbkrConduit.Tests.Unit/Flex/FlexClientTests.cs`):

Add `FakeHttpClientFactory` and update `CreateClient` helper:
```csharp
private static FlexClient CreateClient(HttpMessageHandler handler)
{
    var factory = new FakeHttpClientFactory(handler);
    return new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
}

private sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) =>
        new HttpClient(_handler, disposeHandler: false);
}
```

**FlexClientPollingTests** (`tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs`):

Same pattern — add `FakeHttpClientFactory` and update `CreateClient`:
```csharp
private static FlexClient CreateClient(HttpMessageHandler handler)
{
    var factory = new FakeHttpClientFactory(handler);
    return new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
}

private sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) =>
        new HttpClient(_handler, disposeHandler: false);
}
```

**Run tests — expect compilation failure** (constructor signature mismatch).

### Step 2.2: Implement FlexClient changes

Update `src/IbkrConduit/Flex/FlexClient.cs`:

- Change constructor to accept `(IHttpClientFactory httpClientFactory, string clientName, string flexToken, ILogger<FlexClient> logger)`
- Store `_httpClientFactory` and `_clientName`
- Remove `_httpClient` field; keep `_baseUrl` as configurable or use `_defaultBaseUrl`
- In `SendRequestAsync` and `PollForStatementAsync`, create `var httpClient = _httpClientFactory.CreateClient(_clientName);`

```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly string _clientName;
private readonly string _flexToken;
private readonly string _baseUrl;
private readonly ILogger<FlexClient> _logger;

public FlexClient(IHttpClientFactory httpClientFactory, string clientName, string flexToken, ILogger<FlexClient> logger)
{
    _httpClientFactory = httpClientFactory;
    _clientName = clientName;
    _flexToken = flexToken;
    _baseUrl = _defaultBaseUrl;
    _logger = logger;
}
```

For `SendRequestAsync` and `PollForStatementAsync`, create client at the start:
```csharp
var httpClient = _httpClientFactory.CreateClient(_clientName);
```

**Run tests — expect pass.**

**Commit:** `feat: convert FlexClient to IHttpClientFactory`

---

## Task 3: Update ServiceCollectionExtensions DI registration

### Step 3.1: Update ServiceCollectionExtensions

Update `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`:

**LST registration:**
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

services.AddSingleton<ILiveSessionTokenClient>(sp =>
    new LiveSessionTokenClient(
        sp.GetRequiredService<IHttpClientFactory>(),
        lstClientName,
        sp.GetRequiredService<ILogger<LiveSessionTokenClient>>()));
```

**Flex registration:**
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

services.AddSingleton(sp =>
    new FlexClient(
        sp.GetRequiredService<IHttpClientFactory>(),
        flexClientName,
        flexToken,
        sp.GetRequiredService<ILogger<FlexClient>>()));
```

**Run full build + tests — expect pass.**

**Commit:** `feat: wire IHttpClientFactory named clients in DI registration`

---

## Task 4: Final verification

1. `dotnet build --configuration Release` — zero warnings
2. `dotnet test --configuration Release` — all pass
3. `dotnet format --verify-no-changes` — clean

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
