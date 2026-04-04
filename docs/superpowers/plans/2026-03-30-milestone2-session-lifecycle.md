# Milestone 2 — Session Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Library can initialize a brokerage session, keep it alive, and recover from failures — validated against a paper account.

**Architecture:** `SessionManager` orchestrates session lifecycle (init, tickle, re-auth, shutdown) behind an `ISessionManager` interface. A `TokenRefreshHandler` DelegatingHandler intercepts 401 responses and triggers re-authentication transparently. Two separate HTTP client pipelines prevent recursive re-auth loops: the consumer API pipeline includes `TokenRefreshHandler -> OAuthSigningHandler`, while the internal session API pipeline includes only `OAuthSigningHandler`.

**Tech Stack:** .NET 10 / .NET 8 (multi-target), xUnit v3, Shouldly, WireMock.Net, Refit 10.1.6, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions

**Spec:** `docs/superpowers/specs/2026-03-30-milestone2-session-lifecycle-design.md`

---

## Dependency Graph

```
Task 2.1 (session interfaces)    Task 2.2 (token refresh support)
         │                                │
         ├────────────────┬───────────────┘
         ▼                │
Task 2.3 (tickle timer)   │
         │                │
         ├────────────────┘
         ▼
Task 2.4 (session manager)
         │
         ▼
Task 2.5 (token refresh handler)
         │
         ▼
Task 2.6 (pipeline wiring)
         │
         ▼
Task 2.7 (integration tests)
         │
         ▼
Task 2.8 (ibind suppression IDs)
```

**Parallel opportunities:** Tasks 2.1 and 2.2 can run in parallel on separate branches. Tasks 2.3-2.8 are sequential.

---

## File Structure

### New Source Files (`src/IbkrConduit/`)

| File | Responsibility |
|---|---|
| `Session/IIbkrSessionApi.cs` | Refit interface for session endpoints (ssodh/init, tickle, suppress, logout) |
| `Session/SsodhInitRequest.cs` | Request model for brokerage session initialization |
| `Session/SsodhInitResponse.cs` | Response model for brokerage session initialization |
| `Session/TickleResponse.cs` | Response model for POST /tickle including nested auth status |
| `Session/SuppressRequest.cs` | Request model for question suppression |
| `Session/SuppressResponse.cs` | Response model for question suppression |
| `Session/LogoutResponse.cs` | Response model for POST /logout |
| `Session/IbkrClientOptions.cs` | Options record for compete flag and suppression message IDs |
| `Session/ITickleTimer.cs` | Internal interface for periodic tickle |
| `Session/TickleTimer.cs` | PeriodicTimer-based tickle with failure callback |
| `Session/ISessionManager.cs` | Internal interface for session lifecycle management |
| `Session/SessionManager.cs` | Orchestrates init, tickle, re-auth, shutdown |
| `Session/TokenRefreshHandler.cs` | DelegatingHandler that retries on 401 after re-auth |

### New Test Files

| File | What it tests |
|---|---|
| `tests/IbkrConduit.Tests.Unit/Session/SessionTokenProviderRefreshTests.cs` | RefreshAsync on SessionTokenProvider |
| `tests/IbkrConduit.Tests.Unit/Session/TickleTimerTests.cs` | TickleTimer start, stop, failure callback |
| `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs` | SessionManager init, re-auth, dispose |
| `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs` | 401 retry, tickle skip, request cloning |
| `tests/IbkrConduit.Tests.Integration/Session/SessionLifecycleTests.cs` | Full lifecycle with WireMock |

### Modified Files

| File | Change |
|---|---|
| `src/IbkrConduit/Auth/ISessionTokenProvider.cs` | Add `RefreshAsync` method |
| `src/IbkrConduit/Auth/SessionTokenProvider.cs` | Implement `RefreshAsync` |
| `src/IbkrConduit/Auth/OAuthSigningHandler.cs` | Add `ISessionManager` parameter, call `EnsureInitializedAsync` |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Register new components, two HTTP client pipelines |
| `Directory.Packages.props` | Add `Microsoft.Extensions.Logging.Abstractions` |
| `src/IbkrConduit/IbkrConduit.csproj` | Add `Microsoft.Extensions.Logging.Abstractions` PackageReference |

---

## Task 2.1: Refit Session Interfaces + Response Models

**Branch:** `feat/m2-session-interfaces`
**PR scope:** `IIbkrSessionApi` Refit interface, all request/response models, `IbkrClientOptions`, and unit tests for model serialization.

**Files:**
- Create: `src/IbkrConduit/Session/IIbkrSessionApi.cs`
- Create: `src/IbkrConduit/Session/SsodhInitRequest.cs`
- Create: `src/IbkrConduit/Session/SsodhInitResponse.cs`
- Create: `src/IbkrConduit/Session/TickleResponse.cs`
- Create: `src/IbkrConduit/Session/SuppressRequest.cs`
- Create: `src/IbkrConduit/Session/SuppressResponse.cs`
- Create: `src/IbkrConduit/Session/LogoutResponse.cs`
- Create: `src/IbkrConduit/Session/IbkrClientOptions.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs`

### Sub-task 2.1a: Request/Response models + serialization tests

- [ ] **Step 1: Write failing tests for model serialization**

File: `tests/IbkrConduit.Tests.Unit/Session/SessionModelsTests.cs`

```csharp
using System.Text.Json;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionModelsTests
{
    [Fact]
    public void SsodhInitRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new SsodhInitRequest(Publish: true, Compete: true);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"publish\":true");
        json.ShouldContain("\"compete\":true");
    }

    [Fact]
    public void SsodhInitResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"authenticated":true,"connected":true,"competing":false}""";

        var response = JsonSerializer.Deserialize<SsodhInitResponse>(json);

        response.ShouldNotBeNull();
        response.Authenticated.ShouldBeTrue();
        response.Connected.ShouldBeTrue();
        response.Competing.ShouldBeFalse();
    }

    [Fact]
    public void TickleResponse_Deserializes_NestedAuthStatus()
    {
        var json = """
        {
            "session": "abc123",
            "iserver": {
                "authStatus": {
                    "authenticated": true,
                    "competing": false,
                    "connected": true
                }
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TickleResponse>(json);

        response.ShouldNotBeNull();
        response.Session.ShouldBe("abc123");
        response.Iserver.ShouldNotBeNull();
        response.Iserver!.AuthStatus.ShouldNotBeNull();
        response.Iserver.AuthStatus!.Authenticated.ShouldBeTrue();
        response.Iserver.AuthStatus.Competing.ShouldBeFalse();
        response.Iserver.AuthStatus.Connected.ShouldBeTrue();
    }

    [Fact]
    public void TickleResponse_Deserializes_WithNullIserver()
    {
        var json = """{"session":"abc123"}""";

        var response = JsonSerializer.Deserialize<TickleResponse>(json);

        response.ShouldNotBeNull();
        response.Session.ShouldBe("abc123");
        response.Iserver.ShouldBeNull();
    }

    [Fact]
    public void SuppressRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new SuppressRequest(MessageIds: new List<string> { "o163", "o451" });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"messageIds\"");
        json.ShouldContain("\"o163\"");
        json.ShouldContain("\"o451\"");
    }

    [Fact]
    public void SuppressResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"status":"submitted"}""";

        var response = JsonSerializer.Deserialize<SuppressResponse>(json);

        response.ShouldNotBeNull();
        response.Status.ShouldBe("submitted");
    }

    [Fact]
    public void LogoutResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"confirmed":true}""";

        var response = JsonSerializer.Deserialize<LogoutResponse>(json);

        response.ShouldNotBeNull();
        response.Confirmed.ShouldBeTrue();
    }

    [Fact]
    public void IbkrClientOptions_DefaultValues_AreCorrect()
    {
        var options = new IbkrClientOptions();

        options.Compete.ShouldBeTrue();
        options.SuppressMessageIds.ShouldBeEmpty();
    }

    [Fact]
    public void IbkrClientOptions_CustomValues_ArePreserved()
    {
        var options = new IbkrClientOptions
        {
            Compete = false,
            SuppressMessageIds = new List<string> { "o163" },
        };

        options.Compete.ShouldBeFalse();
        options.SuppressMessageIds.Count.ShouldBe(1);
        options.SuppressMessageIds[0].ShouldBe("o163");
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionModelsTests" --configuration Release`

Expected: FAIL — types `SsodhInitRequest`, `SsodhInitResponse`, `TickleResponse`, etc. do not exist.

- [ ] **Step 3: Implement SsodhInitRequest**

File: `src/IbkrConduit/Session/SsodhInitRequest.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Request body for POST /iserver/auth/ssodh/init to initialize a brokerage session.
/// </summary>
/// <param name="Publish">Whether to publish the session.</param>
/// <param name="Compete">Whether to compete with existing sessions.</param>
public record SsodhInitRequest(
    [property: JsonPropertyName("publish")] bool Publish,
    [property: JsonPropertyName("compete")] bool Compete);
```

- [ ] **Step 4: Implement SsodhInitResponse**

File: `src/IbkrConduit/Session/SsodhInitResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /iserver/auth/ssodh/init.
/// </summary>
public class SsodhInitResponse
{
    /// <summary>
    /// Whether the session is authenticated.
    /// </summary>
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    /// <summary>
    /// Whether the session is connected to the backend.
    /// </summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; init; }

    /// <summary>
    /// Whether this session is competing with another.
    /// </summary>
    [JsonPropertyName("competing")]
    public bool Competing { get; init; }
}
```

- [ ] **Step 5: Implement TickleResponse and nested types**

File: `src/IbkrConduit/Session/TickleResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /tickle. Contains session status and optional iserver auth status.
/// </summary>
public class TickleResponse
{
    /// <summary>
    /// The session identifier.
    /// </summary>
    [JsonPropertyName("session")]
    public string Session { get; init; } = string.Empty;

    /// <summary>
    /// Optional iserver status including authentication state.
    /// </summary>
    [JsonPropertyName("iserver")]
    public TickleIserverStatus? Iserver { get; init; }
}

/// <summary>
/// Iserver status block within a tickle response.
/// </summary>
public class TickleIserverStatus
{
    /// <summary>
    /// Authentication status of the iserver connection.
    /// </summary>
    [JsonPropertyName("authStatus")]
    public TickleAuthStatus? AuthStatus { get; init; }
}

/// <summary>
/// Authentication status details from a tickle response.
/// </summary>
public class TickleAuthStatus
{
    /// <summary>
    /// Whether the session is authenticated.
    /// </summary>
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }

    /// <summary>
    /// Whether this session is competing with another.
    /// </summary>
    [JsonPropertyName("competing")]
    public bool Competing { get; init; }

    /// <summary>
    /// Whether the session is connected to the backend.
    /// </summary>
    [JsonPropertyName("connected")]
    public bool Connected { get; init; }
}
```

- [ ] **Step 6: Implement SuppressRequest**

File: `src/IbkrConduit/Session/SuppressRequest.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Request body for POST /iserver/questions/suppress.
/// </summary>
/// <param name="MessageIds">List of message IDs to suppress.</param>
public record SuppressRequest(
    [property: JsonPropertyName("messageIds")] List<string> MessageIds);
```

- [ ] **Step 7: Implement SuppressResponse**

File: `src/IbkrConduit/Session/SuppressResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /iserver/questions/suppress.
/// </summary>
public class SuppressResponse
{
    /// <summary>
    /// Status of the suppression request.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
```

- [ ] **Step 8: Implement LogoutResponse**

File: `src/IbkrConduit/Session/LogoutResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Session;

/// <summary>
/// Response from POST /logout.
/// </summary>
public class LogoutResponse
{
    /// <summary>
    /// Whether the logout was confirmed by the server.
    /// </summary>
    [JsonPropertyName("confirmed")]
    public bool Confirmed { get; init; }
}
```

- [ ] **Step 9: Implement IbkrClientOptions**

File: `src/IbkrConduit/Session/IbkrClientOptions.cs`

```csharp
namespace IbkrConduit.Session;

/// <summary>
/// Configuration options for the IBKR client session behavior.
/// </summary>
public record IbkrClientOptions
{
    /// <summary>
    /// Whether to compete with existing sessions when initializing.
    /// Default is <c>true</c>.
    /// </summary>
    public bool Compete { get; init; } = true;

    /// <summary>
    /// List of question message IDs to suppress after session initialization.
    /// </summary>
    public List<string> SuppressMessageIds { get; init; } = new();
}
```

- [ ] **Step 10: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionModelsTests" --configuration Release`

Expected: All 9 tests PASS.

### Sub-task 2.1b: IIbkrSessionApi Refit interface

- [ ] **Step 11: Implement IIbkrSessionApi**

File: `src/IbkrConduit/Session/IIbkrSessionApi.cs`

```csharp
using Refit;

namespace IbkrConduit.Session;

/// <summary>
/// Refit interface for IBKR session management endpoints.
/// Used internally by <see cref="SessionManager"/> to initialize, maintain, and tear down sessions.
/// </summary>
public interface IIbkrSessionApi
{
    /// <summary>
    /// Initializes a brokerage session via SSO/DH.
    /// </summary>
    [Post("/v1/api/iserver/auth/ssodh/init")]
    Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
        [Body] SsodhInitRequest request);

    /// <summary>
    /// Sends a tickle to keep the session alive and check auth status.
    /// </summary>
    [Post("/v1/api/tickle")]
    Task<TickleResponse> TickleAsync();

    /// <summary>
    /// Suppresses specified question message IDs to avoid interactive prompts.
    /// </summary>
    [Post("/v1/api/iserver/questions/suppress")]
    Task<SuppressResponse> SuppressQuestionsAsync(
        [Body] SuppressRequest request);

    /// <summary>
    /// Logs out and terminates the brokerage session.
    /// </summary>
    [Post("/v1/api/logout")]
    Task<LogoutResponse> LogoutAsync();
}
```

- [ ] **Step 12: Run full build to verify no compilation errors**

Run: `dotnet build --configuration Release`

Expected: Build succeeded with 0 warnings, 0 errors.

- [ ] **Step 13: Run all tests to verify nothing broken**

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 14: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 15: Commit**

```
feat: add Refit session interfaces and response models

Adds IIbkrSessionApi (ssodh/init, tickle, suppress, logout),
all request/response DTOs, and IbkrClientOptions record.
```

---

## Task 2.2: SessionTokenProvider Refresh Support

**Branch:** `feat/m2-token-refresh-support`
**PR scope:** Add `RefreshAsync` to `ISessionTokenProvider` and `SessionTokenProvider` + unit tests.

**Files:**
- Modify: `src/IbkrConduit/Auth/ISessionTokenProvider.cs`
- Modify: `src/IbkrConduit/Auth/SessionTokenProvider.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Session/SessionTokenProviderRefreshTests.cs`

### Sub-task 2.2a: Add RefreshAsync to interface and implementation

- [ ] **Step 1: Write failing test for RefreshAsync**

File: `tests/IbkrConduit.Tests.Unit/Session/SessionTokenProviderRefreshTests.cs`

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionTokenProviderRefreshTests
{
    [Fact]
    public async Task RefreshAsync_ReplacesExistingCachedToken()
    {
        var originalToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var refreshedToken = new LiveSessionToken(
            new byte[] { 0x04, 0x05, 0x06 },
            DateTimeOffset.UtcNow.AddHours(48));

        var client = new SequentialFakeLstClient(originalToken, refreshedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        // Acquire first token
        var first = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        first.ShouldBe(originalToken);

        // Refresh should acquire a new token
        var refreshed = await provider.RefreshAsync(CancellationToken.None);
        refreshed.ShouldBe(refreshedToken);
        client.CallCount.ShouldBe(2);

        // Subsequent GetLiveSessionTokenAsync should return the refreshed token
        var cached = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        cached.ShouldBe(refreshedToken);
        client.CallCount.ShouldBe(2); // no additional call
    }

    [Fact]
    public async Task RefreshAsync_WithoutPriorGet_AcquiresNewToken()
    {
        var token = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new SequentialFakeLstClient(token);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result = await provider.RefreshAsync(CancellationToken.None);

        result.ShouldBe(token);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCalls_OnlyRefreshesOnce()
    {
        var originalToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var refreshedToken = new LiveSessionToken(
            new byte[] { 0x04, 0x05, 0x06 },
            DateTimeOffset.UtcNow.AddHours(48));

        var client = new SequentialFakeLstClient(originalToken, refreshedToken);
        client.Delay = TimeSpan.FromMilliseconds(50);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        // Prime the cache
        await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        // Fire multiple concurrent refreshes
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.RefreshAsync(CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All should get the same refreshed token
        foreach (var result in results)
        {
            result.ShouldBe(refreshedToken);
        }

        // Only 2 calls total: 1 initial + 1 refresh (not 10 refreshes)
        client.CallCount.ShouldBe(2);
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = System.Security.Cryptography.RSA.Create(2048);
        var encKey = System.Security.Cryptography.RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new System.Numerics.BigInteger(23));
    }

    private class SequentialFakeLstClient : ILiveSessionTokenClient
    {
        private readonly LiveSessionToken[] _tokens;
        private int _index;

        public SequentialFakeLstClient(params LiveSessionToken[] tokens)
        {
            _tokens = tokens;
        }

        public int CallCount { get; private set; }
        public TimeSpan Delay { get; set; }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(
            IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            CallCount++;
            var token = _tokens[Math.Min(_index, _tokens.Length - 1)];
            _index++;
            return token;
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionTokenProviderRefreshTests" --configuration Release`

Expected: FAIL — `RefreshAsync` method does not exist on `ISessionTokenProvider` or `SessionTokenProvider`.

- [ ] **Step 3: Add RefreshAsync to ISessionTokenProvider**

File: `src/IbkrConduit/Auth/ISessionTokenProvider.cs`

Replace the entire file with:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// Abstracts Live Session Token acquisition, caching, and refresh from the signing handler.
/// </summary>
public interface ISessionTokenProvider
{
    /// <summary>
    /// Gets the current Live Session Token, acquiring it if necessary.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Acquires a fresh Live Session Token, replacing any cached value.
    /// Thread-safe: concurrent callers are serialized and subsequent callers
    /// receive the already-refreshed token.
    /// </summary>
    Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement RefreshAsync in SessionTokenProvider**

File: `src/IbkrConduit/Auth/SessionTokenProvider.cs`

Replace the entire file with:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// Lazy-acquires and caches the Live Session Token. Thread-safe via semaphore.
/// Supports forced refresh for re-authentication scenarios.
/// </summary>
public class SessionTokenProvider : ISessionTokenProvider, IDisposable
{
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ILiveSessionTokenClient _lstClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private LiveSessionToken? _cached;

    /// <summary>
    /// Creates a new provider that will acquire the LST on first use.
    /// </summary>
    public SessionTokenProvider(IbkrOAuthCredentials credentials, ILiveSessionTokenClient lstClient)
    {
        _credentials = credentials;
        _lstClient = lstClient;
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
    {
        if (_cached != null)
        {
            return _cached;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

- [ ] **Step 5: Update existing FakeTokenProvider in OAuthSigningHandlerTests to implement RefreshAsync**

The existing `FakeTokenProvider` in `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs` needs to add the `RefreshAsync` method. In the `FakeTokenProvider` inner class, add:

```csharp
public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
    Task.FromResult(_token);
```

The full updated inner class becomes:

```csharp
private class FakeTokenProvider : ISessionTokenProvider
{
    private readonly LiveSessionToken _token;

    public FakeTokenProvider(LiveSessionToken token) => _token = token;

    public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_token);

    public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_token);
}
```

- [ ] **Step 6: Update existing FakeTokenProvider in PortfolioAccountsTests to implement RefreshAsync**

The existing `FakeTokenProvider` in `tests/IbkrConduit.Tests.Integration/Portfolio/PortfolioAccountsTests.cs` needs the same change. Add:

```csharp
public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
    Task.FromResult(_token);
```

The full updated inner class becomes:

```csharp
private class FakeTokenProvider : ISessionTokenProvider
{
    private readonly LiveSessionToken _token;

    public FakeTokenProvider(LiveSessionToken token) => _token = token;

    public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_token);

    public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_token);
}
```

- [ ] **Step 7: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionTokenProviderRefreshTests" --configuration Release`

Expected: All 3 tests PASS.

- [ ] **Step 8: Run full test suite to verify no regressions**

Run: `dotnet test --configuration Release`

Expected: All tests PASS (existing SessionTokenProviderTests and OAuthSigningHandlerTests still pass).

- [ ] **Step 9: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 10: Commit**

```
feat: add RefreshAsync to SessionTokenProvider

Extends ISessionTokenProvider with RefreshAsync that acquires a new LST
and replaces the cached value. Thread-safe via existing SemaphoreSlim.
```

---

## Task 2.3: TickleTimer

**Branch:** `feat/m2-tickle-timer`
**PR scope:** `ITickleTimer` interface, `TickleTimer` implementation with `PeriodicTimer`, failure callback, and unit tests.
**Depends on:** Task 2.1 (merged) for `IIbkrSessionApi` and `TickleResponse`.

**Files:**
- Create: `src/IbkrConduit/Session/ITickleTimer.cs`
- Create: `src/IbkrConduit/Session/TickleTimer.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Session/TickleTimerTests.cs`
- Modify: `Directory.Packages.props` — add `Microsoft.Extensions.Logging.Abstractions`
- Modify: `src/IbkrConduit/IbkrConduit.csproj` — add `Microsoft.Extensions.Logging.Abstractions` PackageReference

### Sub-task 2.3a: Add logging dependency

- [ ] **Step 1: Add Microsoft.Extensions.Logging.Abstractions to Directory.Packages.props**

In `Directory.Packages.props`, add within the `<ItemGroup>` after the `Microsoft.Extensions.DependencyInjection.Abstractions` line:

```xml
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
```

- [ ] **Step 2: Add PackageReference to IbkrConduit.csproj**

In `src/IbkrConduit/IbkrConduit.csproj`, add within the PackageReference `<ItemGroup>`:

```xml
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

### Sub-task 2.3b: ITickleTimer interface and TickleTimer implementation

- [ ] **Step 3: Write failing tests for TickleTimer**

File: `tests/IbkrConduit.Tests.Unit/Session/TickleTimerTests.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TickleTimerTests
{
    [Fact]
    public async Task StartAsync_CallsTickleOnInterval()
    {
        var sessionApi = new FakeSessionApi();
        var failureCount = 0;
        Func<CancellationToken, Task> onFailure = _ =>
        {
            Interlocked.Increment(ref failureCount);
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = new CancellationTokenSource();
        await timer.StartAsync(cts.Token);

        // Wait enough time for at least 2 ticks
        await Task.Delay(2500);

        await timer.StopAsync();

        sessionApi.TickleCallCount.ShouldBeGreaterThanOrEqualTo(2);
        failureCount.ShouldBe(0);
    }

    [Fact]
    public async Task StartAsync_WhenTickleReturnsUnauthenticated_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { Authenticated = false };
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = new CancellationTokenSource();
        await timer.StartAsync(cts.Token);

        // Wait for the failure callback to fire
        var completed = await Task.WhenAny(failureTcs.Task, Task.Delay(5000));
        completed.ShouldBe(failureTcs.Task, "Failure callback should have been invoked");

        await timer.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTickleThrows_InvokesFailureCallback()
    {
        var sessionApi = new FakeSessionApi { ShouldThrow = true };
        var failureTcs = new TaskCompletionSource();
        Func<CancellationToken, Task> onFailure = _ =>
        {
            failureTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = new CancellationTokenSource();
        await timer.StartAsync(cts.Token);

        var completed = await Task.WhenAny(failureTcs.Task, Task.Delay(5000));
        completed.ShouldBe(failureTcs.Task, "Failure callback should have been invoked on exception");

        await timer.StopAsync();
    }

    [Fact]
    public async Task StopAsync_StopsTickling()
    {
        var sessionApi = new FakeSessionApi();
        Func<CancellationToken, Task> onFailure = _ => Task.CompletedTask;

        var timer = new TickleTimer(
            sessionApi,
            onFailure,
            NullLogger<TickleTimer>.Instance,
            intervalSeconds: 1);

        using var cts = new CancellationTokenSource();
        await timer.StartAsync(cts.Token);

        // Let one tick happen
        await Task.Delay(1500);
        await timer.StopAsync();

        var countAfterStop = sessionApi.TickleCallCount;

        // Wait to ensure no more ticks happen
        await Task.Delay(2000);

        sessionApi.TickleCallCount.ShouldBe(countAfterStop);
    }

    private class FakeSessionApi : IIbkrSessionApi
    {
        public int TickleCallCount { get; private set; }
        public bool Authenticated { get; set; } = true;
        public bool ShouldThrow { get; set; }

        public Task<TickleResponse> TickleAsync()
        {
            TickleCallCount++;

            if (ShouldThrow)
            {
                throw new HttpRequestException("Simulated tickle failure");
            }

            return Task.FromResult(new TickleResponse
            {
                Iserver = new TickleIserverStatus
                {
                    AuthStatus = new TickleAuthStatus
                    {
                        Authenticated = Authenticated,
                        Connected = true,
                        Competing = false,
                    },
                },
            });
        }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request) =>
            Task.FromResult(new SsodhInitResponse { Authenticated = true, Connected = true });

        public Task<SuppressResponse> SuppressQuestionsAsync(SuppressRequest request) =>
            Task.FromResult(new SuppressResponse { Status = "submitted" });

        public Task<LogoutResponse> LogoutAsync() =>
            Task.FromResult(new LogoutResponse { Confirmed = true });
    }
}
```

- [ ] **Step 4: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "TickleTimerTests" --configuration Release`

Expected: FAIL — types `ITickleTimer` and `TickleTimer` do not exist.

- [ ] **Step 5: Implement ITickleTimer**

File: `src/IbkrConduit/Session/ITickleTimer.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// Periodically sends tickle requests to keep the brokerage session alive.
/// Notifies a callback when the session is detected as dead.
/// </summary>
internal interface ITickleTimer
{
    /// <summary>
    /// Starts the periodic tickle timer.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the periodic tickle timer and awaits the background task.
    /// </summary>
    Task StopAsync();
}
```

- [ ] **Step 6: Implement TickleTimer**

File: `src/IbkrConduit/Session/TickleTimer.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Uses <see cref="PeriodicTimer"/> to send tickle requests at a fixed interval.
/// When the session is detected as unauthenticated or the tickle call fails,
/// the failure callback is invoked to trigger re-authentication.
/// </summary>
internal class TickleTimer : ITickleTimer
{
    private readonly IIbkrSessionApi _sessionApi;
    private readonly Func<CancellationToken, Task> _onFailure;
    private readonly ILogger<TickleTimer> _logger;
    private readonly int _intervalSeconds;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    /// <summary>
    /// Creates a new tickle timer.
    /// </summary>
    /// <param name="sessionApi">Refit client for session endpoints.</param>
    /// <param name="onFailure">Callback invoked when the session is detected as dead.</param>
    /// <param name="logger">Logger for tickle events.</param>
    /// <param name="intervalSeconds">Interval between tickle requests in seconds. Default is 60.</param>
    public TickleTimer(
        IIbkrSessionApi sessionApi,
        Func<CancellationToken, Task> onFailure,
        ILogger<TickleTimer> logger,
        int intervalSeconds = 60)
    {
        _sessionApi = sessionApi;
        _onFailure = onFailure;
        _logger = logger;
        _intervalSeconds = intervalSeconds;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        if (_backgroundTask != null)
        {
            try
            {
                await _backgroundTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        _cts?.Dispose();
        _cts = null;
        _backgroundTask = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var response = await _sessionApi.TickleAsync();
                var isAuthenticated = response.Iserver?.AuthStatus?.Authenticated ?? false;

                if (!isAuthenticated)
                {
                    _logger.LogWarning("Tickle response indicates session is not authenticated");
                    await _onFailure(cancellationToken);
                }
                else
                {
                    _logger.LogDebug("Tickle successful — session is authenticated");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tickle failed with exception");
                try
                {
                    await _onFailure(cancellationToken);
                }
                catch (Exception cbEx)
                {
                    _logger.LogError(cbEx, "Failure callback threw an exception");
                }
            }
        }
    }
}
```

- [ ] **Step 7: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "TickleTimerTests" --configuration Release`

Expected: All 4 tests PASS.

- [ ] **Step 8: Run full build and test suite**

Run: `dotnet build --configuration Release`

Expected: Build succeeded with 0 warnings.

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 9: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 10: Commit**

```
feat: add TickleTimer for periodic session keepalive

Implements ITickleTimer using PeriodicTimer with configurable interval.
Invokes failure callback when tickle detects dead session or throws.
```

---

## Task 2.4: SessionManager

**Branch:** `feat/m2-session-manager`
**PR scope:** `ISessionManager`, `SessionManager` with state machine, `EnsureInitializedAsync`, `ReauthenticateAsync`, `DisposeAsync`, and unit tests.
**Depends on:** Tasks 2.1, 2.2, 2.3 (all merged).

**Files:**
- Create: `src/IbkrConduit/Session/ISessionManager.cs`
- Create: `src/IbkrConduit/Session/SessionManager.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`

### Sub-task 2.4a: ISessionManager interface

- [ ] **Step 1: Write failing tests for SessionManager**

File: `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionManagerTests
{
    [Fact]
    public async Task EnsureInitializedAsync_FirstCall_InitializesSession()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.SessionApi.InitCallCount.ShouldBe(1);
        deps.SessionApi.LastInitRequest.ShouldNotBeNull();
        deps.SessionApi.LastInitRequest!.Publish.ShouldBeTrue();
        deps.SessionApi.LastInitRequest.Compete.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureInitializedAsync_SecondCall_DoesNotReinitialize()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);
        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.SessionApi.InitCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_AcquiresLstBeforeInit()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.TokenProvider.GetCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithSuppressIds_SuppressesQuestions()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163", "o451" },
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.SessionApi.SuppressCallCount.ShouldBe(1);
        deps.SessionApi.LastSuppressRequest.ShouldNotBeNull();
        deps.SessionApi.LastSuppressRequest!.MessageIds.ShouldBe(
            new List<string> { "o163", "o451" });
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithoutSuppressIds_SkipsSuppression()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.SessionApi.SuppressCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task EnsureInitializedAsync_StartsTickleTimer()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        deps.TickleTimerFactory.CreatedTimer.ShouldNotBeNull();
        deps.TickleTimerFactory.CreatedTimer!.Started.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_RefreshesTokenAndReinitsSession()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        // Initialize first
        await manager.EnsureInitializedAsync(CancellationToken.None);

        // Trigger re-auth
        await manager.ReauthenticateAsync(CancellationToken.None);

        deps.TokenProvider.RefreshCallCount.ShouldBe(1);
        deps.SessionApi.InitCallCount.ShouldBe(2); // once for init, once for re-auth
    }

    [Fact]
    public async Task ReauthenticateAsync_StopsAndRestartsTickleTimer()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);
        var firstTimer = deps.TickleTimerFactory.CreatedTimer!;

        await manager.ReauthenticateAsync(CancellationToken.None);

        firstTimer.Stopped.ShouldBeTrue();
        // A new timer was created and started
        deps.TickleTimerFactory.CreateCount.ShouldBe(2);
        deps.TickleTimerFactory.CreatedTimer!.Started.ShouldBeTrue();
    }

    [Fact]
    public async Task ReauthenticateAsync_WithSuppressIds_ResuppressesQuestions()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163" },
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);
        await manager.ReauthenticateAsync(CancellationToken.None);

        deps.SessionApi.SuppressCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task DisposeAsync_CallsLogout()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_StopsTickleTimer()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);
        var timer = deps.TickleTimerFactory.CreatedTimer!;

        await manager.DisposeAsync();

        timer.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WithoutInit_DoesNotThrow()
    {
        var deps = CreateDependencies();

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        // Should not throw even if never initialized
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_LogoutThrows_DoesNotPropagate()
    {
        var deps = CreateDependencies();
        deps.SessionApi.LogoutShouldThrow = true;

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(CancellationToken.None);

        // Should not throw
        await manager.DisposeAsync();
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public FakeSessionTokenProvider TokenProvider { get; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class FakeSessionTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token = new(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            return Task.FromResult(_token);
        }

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            return Task.FromResult(new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.AddHours(24)));
        }
    }

    internal class FakeTickleTimerFactory : ITickleTimerFactory
    {
        public int CreateCount { get; private set; }
        public FakeTickleTimer? CreatedTimer { get; private set; }

        public ITickleTimer Create(
            IIbkrSessionApi sessionApi,
            Func<CancellationToken, Task> onFailure)
        {
            CreateCount++;
            CreatedTimer = new FakeTickleTimer();
            return CreatedTimer;
        }
    }

    internal class FakeTickleTimer : ITickleTimer
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public int InitCallCount { get; private set; }
        public int SuppressCallCount { get; private set; }
        public int LogoutCallCount { get; private set; }
        public SsodhInitRequest? LastInitRequest { get; private set; }
        public SuppressRequest? LastSuppressRequest { get; private set; }
        public bool LogoutShouldThrow { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request)
        {
            InitCallCount++;
            LastInitRequest = request;
            return Task.FromResult(new SsodhInitResponse
            {
                Authenticated = true,
                Connected = true,
            });
        }

        public Task<TickleResponse> TickleAsync() =>
            Task.FromResult(new TickleResponse
            {
                Iserver = new TickleIserverStatus
                {
                    AuthStatus = new TickleAuthStatus
                    {
                        Authenticated = true,
                        Connected = true,
                    },
                },
            });

        public Task<SuppressResponse> SuppressQuestionsAsync(SuppressRequest request)
        {
            SuppressCallCount++;
            LastSuppressRequest = request;
            return Task.FromResult(new SuppressResponse { Status = "submitted" });
        }

        public Task<LogoutResponse> LogoutAsync()
        {
            LogoutCallCount++;
            if (LogoutShouldThrow)
            {
                throw new HttpRequestException("Simulated logout failure");
            }

            return Task.FromResult(new LogoutResponse { Confirmed = true });
        }
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionManagerTests" --configuration Release`

Expected: FAIL — types `ISessionManager`, `SessionManager`, `ITickleTimerFactory` do not exist.

- [ ] **Step 3: Implement ISessionManager**

File: `src/IbkrConduit/Session/ISessionManager.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// Manages the lifecycle of an IBKR brokerage session: initialization,
/// keepalive, re-authentication, and shutdown.
/// </summary>
internal interface ISessionManager : IAsyncDisposable
{
    /// <summary>
    /// Ensures the brokerage session is initialized. On first call, acquires an LST,
    /// initializes the session, suppresses questions, and starts the tickle timer.
    /// Subsequent calls return immediately if the session is already ready.
    /// </summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Forces re-authentication: refreshes the LST, re-initializes the brokerage session,
    /// re-suppresses questions, and restarts the tickle timer. Thread-safe.
    /// </summary>
    Task ReauthenticateAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement ITickleTimerFactory**

The tests require a factory to decouple `SessionManager` from `TickleTimer` construction. This is needed because the tickle timer takes a failure callback that references the `SessionManager` itself.

File: Add to `src/IbkrConduit/Session/ITickleTimer.cs` (append after the existing interface):

Replace the entire file with:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// Periodically sends tickle requests to keep the brokerage session alive.
/// Notifies a callback when the session is detected as dead.
/// </summary>
internal interface ITickleTimer
{
    /// <summary>
    /// Starts the periodic tickle timer.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the periodic tickle timer and awaits the background task.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Factory for creating <see cref="ITickleTimer"/> instances.
/// Decouples <see cref="SessionManager"/> from direct <see cref="TickleTimer"/> construction.
/// </summary>
internal interface ITickleTimerFactory
{
    /// <summary>
    /// Creates a new tickle timer with the given session API and failure callback.
    /// </summary>
    ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure);
}
```

- [ ] **Step 5: Implement TickleTimerFactory**

Add to the bottom of `src/IbkrConduit/Session/TickleTimer.cs`:

Append after the closing brace of the `TickleTimer` class:

```csharp

/// <summary>
/// Default factory that creates <see cref="TickleTimer"/> instances.
/// </summary>
internal class TickleTimerFactory : ITickleTimerFactory
{
    private readonly ILogger<TickleTimer> _logger;

    /// <summary>
    /// Creates a new factory with the given logger.
    /// </summary>
    public TickleTimerFactory(ILogger<TickleTimer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ITickleTimer Create(IIbkrSessionApi sessionApi, Func<CancellationToken, Task> onFailure) =>
        new TickleTimer(sessionApi, onFailure, _logger);
}
```

- [ ] **Step 6: Implement SessionManager**

File: `src/IbkrConduit/Session/SessionManager.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Session;

/// <summary>
/// Orchestrates brokerage session lifecycle: lazy initialization, periodic tickle,
/// re-authentication on failure, and clean shutdown.
/// </summary>
internal class SessionManager : ISessionManager
{
    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly ITickleTimerFactory _tickleTimerFactory;
    private readonly IIbkrSessionApi _sessionApi;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<SessionManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    private SessionState _state = SessionState.Uninitialized;
    private ITickleTimer? _tickleTimer;
    private CancellationTokenSource? _proactiveRefreshCts;
    private LiveSessionToken? _currentLst;

    /// <summary>
    /// Creates a new session manager.
    /// </summary>
    public SessionManager(
        ISessionTokenProvider sessionTokenProvider,
        ITickleTimerFactory tickleTimerFactory,
        IIbkrSessionApi sessionApi,
        IbkrClientOptions options,
        ILogger<SessionManager> logger)
    {
        _sessionTokenProvider = sessionTokenProvider;
        _tickleTimerFactory = tickleTimerFactory;
        _sessionApi = sessionApi;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_state == SessionState.Ready)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_state == SessionState.Ready)
            {
                return;
            }

            _state = SessionState.Initializing;
            _logger.LogInformation("Initializing brokerage session");

            _currentLst = await _sessionTokenProvider.GetLiveSessionTokenAsync(cancellationToken);

            await _sessionApi.InitializeBrokerageSessionAsync(
                new SsodhInitRequest(Publish: true, Compete: _options.Compete));

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds));
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            _state = SessionState.Ready;
            _logger.LogInformation("Brokerage session initialized successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReauthenticateAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_state == SessionState.ShuttingDown)
            {
                return;
            }

            _state = SessionState.Reauthenticating;
            _logger.LogInformation("Re-authenticating brokerage session");

            if (_tickleTimer != null)
            {
                await _tickleTimer.StopAsync();
            }

            CancelProactiveRefresh();

            _currentLst = await _sessionTokenProvider.RefreshAsync(cancellationToken);

            await _sessionApi.InitializeBrokerageSessionAsync(
                new SsodhInitRequest(Publish: true, Compete: _options.Compete));

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds));
            }

            _tickleTimer = _tickleTimerFactory.Create(_sessionApi, OnTickleFailureAsync);
            await _tickleTimer.StartAsync(cancellationToken);

            ScheduleProactiveRefresh();

            _state = SessionState.Ready;
            _logger.LogInformation("Brokerage session re-authenticated successfully");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_state == SessionState.ShuttingDown)
        {
            return;
        }

        _state = SessionState.ShuttingDown;

        if (_tickleTimer != null)
        {
            await _tickleTimer.StopAsync();
        }

        CancelProactiveRefresh();

        if (_state != SessionState.Uninitialized || _tickleTimer != null)
        {
            try
            {
                await _sessionApi.LogoutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Logout failed during shutdown — ignoring");
            }
        }

        _semaphore.Dispose();
        _disposeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    private Task OnTickleFailureAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Tickle failure detected — triggering re-authentication");
        return ReauthenticateAsync(cancellationToken);
    }

    private void ScheduleProactiveRefresh()
    {
        if (_currentLst == null)
        {
            return;
        }

        CancelProactiveRefresh();

        var timeUntilRefresh = _currentLst.Expiry - DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        if (timeUntilRefresh <= TimeSpan.Zero)
        {
            return;
        }

        _proactiveRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        var token = _proactiveRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeUntilRefresh, token);
                if (!token.IsCancellationRequested)
                {
                    await ReauthenticateAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Proactive refresh failed");
            }
        }, token);
    }

    private void CancelProactiveRefresh()
    {
        if (_proactiveRefreshCts != null)
        {
            _proactiveRefreshCts.Cancel();
            _proactiveRefreshCts.Dispose();
            _proactiveRefreshCts = null;
        }
    }

    private enum SessionState
    {
        Uninitialized,
        Initializing,
        Ready,
        Reauthenticating,
        ShuttingDown,
    }
}
```

- [ ] **Step 7: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SessionManagerTests" --configuration Release`

Expected: All 11 tests PASS.

- [ ] **Step 8: Run full build and test suite**

Run: `dotnet build --configuration Release`

Expected: Build succeeded with 0 warnings.

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 9: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 10: Commit**

```
feat: add SessionManager for brokerage session lifecycle

Implements ISessionManager with state machine: Uninitialized -> Initializing
-> Ready -> Reauthenticating. Coordinates LST acquisition, ssodh/init,
question suppression, tickle timer, and proactive token refresh.
```

---

## Task 2.5: TokenRefreshHandler

**Branch:** `feat/m2-token-refresh-handler`
**PR scope:** `TokenRefreshHandler` DelegatingHandler that retries on 401 after re-auth, and unit tests.
**Depends on:** Task 2.4 (merged) for `ISessionManager`.

**Files:**
- Create: `src/IbkrConduit/Session/TokenRefreshHandler.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs`

- [ ] **Step 1: Write failing tests for TokenRefreshHandler**

File: `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs`

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class TokenRefreshHandlerTests
{
    [Fact]
    public async Task SendAsync_SuccessfulResponse_ReturnsWithoutRetry()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(1);
        sessionManager.ReauthCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_401Response_TriggersReauthAndRetries()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        });

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2);
        sessionManager.ReauthCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_401OnTickle_DoesNotRetry()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.PostAsync("/v1/api/tickle", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(1);
        sessionManager.ReauthCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_401OnRetry_ReturnsSecond401()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        var innerHandler = new FakeInnerHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var response = await client.GetAsync("/v1/api/portfolio/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        callCount.ShouldBe(2); // original + one retry
        sessionManager.ReauthCallCount.ShouldBe(1); // only one re-auth
    }

    [Fact]
    public async Task SendAsync_WithRequestBody_RetryPreservesBody()
    {
        var sessionManager = new FakeSessionManager();
        var callCount = 0;
        string? secondRequestBody = null;
        var innerHandler = new FakeInnerHandler(async req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            secondRequestBody = req.Content != null
                ? await req.Content.ReadAsStringAsync()
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };

        var content = new StringContent("""{"publish":true,"compete":true}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/v1/api/iserver/auth/ssodh/init", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondRequestBody.ShouldBe("""{"publish":true,"compete":true}""");
    }

    private class FakeSessionManager : ISessionManager
    {
        public int ReauthCallCount { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken)
        {
            ReauthCallCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeInnerHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = req => Task.FromResult(handler(req));

        public FakeInnerHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request);
    }
}
```

- [ ] **Step 2: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "TokenRefreshHandlerTests" --configuration Release`

Expected: FAIL — `TokenRefreshHandler` does not exist.

- [ ] **Step 3: Implement TokenRefreshHandler**

File: `src/IbkrConduit/Session/TokenRefreshHandler.cs`

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// DelegatingHandler that detects 401 responses, triggers session re-authentication
/// via <see cref="ISessionManager"/>, and retries the request once. Tickle requests
/// are excluded from retry to avoid masking dead session detection.
/// </summary>
internal class TokenRefreshHandler : DelegatingHandler
{
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Creates a new token refresh handler.
    /// </summary>
    /// <param name="sessionManager">Session manager for re-authentication.</param>
    public TokenRefreshHandler(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer request content before sending (needed for replay on retry)
        byte[]? bufferedContent = null;
        string? contentType = null;
        if (request.Content != null)
        {
            bufferedContent = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Skip retry for tickle requests — dead tickle means dead session
        if (request.RequestUri != null &&
            request.RequestUri.AbsolutePath.Contains("/tickle", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        // Trigger re-authentication
        await _sessionManager.ReauthenticateAsync(cancellationToken);

        // Clone the request for retry
        using var retryRequest = CloneRequest(request, bufferedContent, contentType);

        // Dispose the original 401 response
        response.Dispose();

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage original, byte[]? bufferedContent, string? contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (bufferedContent != null)
        {
            clone.Content = new ByteArrayContent(bufferedContent);
            if (contentType != null)
            {
                clone.Content.Headers.Remove("Content-Type");
                clone.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        // Copy headers (skip Content headers, already set above)
        foreach (var header in original.Headers)
        {
            // Skip Authorization — it will be re-added by OAuthSigningHandler
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
```

- [ ] **Step 4: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "TokenRefreshHandlerTests" --configuration Release`

Expected: All 5 tests PASS.

- [ ] **Step 5: Run full build and test suite**

Run: `dotnet build --configuration Release`

Expected: Build succeeded with 0 warnings.

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 6: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 7: Commit**

```
feat: add TokenRefreshHandler for 401 retry with re-auth

DelegatingHandler intercepts 401 responses, triggers SessionManager
re-authentication, and retries the request once. Skips retry for
tickle requests to avoid masking dead session detection.
```

---

## Task 2.6: Pipeline Wiring + OAuthSigningHandler Update

**Branch:** `feat/m2-pipeline-wiring`
**PR scope:** Update `OAuthSigningHandler` to call `EnsureInitializedAsync`, update `ServiceCollectionExtensions` to register all M2 components with two HTTP client pipelines, and integration test for DI resolution.
**Depends on:** Task 2.5 (merged).

**Files:**
- Modify: `src/IbkrConduit/Auth/OAuthSigningHandler.cs`
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs`

### Sub-task 2.6a: Update OAuthSigningHandler

- [ ] **Step 1: Write failing test for EnsureInitializedAsync call**

Add a new test to `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs`:

```csharp
[Fact]
public async Task SendAsync_CallsEnsureInitializedAsync_BeforeSigning()
{
    var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
    var provider = new FakeTokenProvider(new LiveSessionToken(
        lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

    var sessionManager = new FakeSessionManager();

    var innerHandler = new FakeInnerHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

    var signingHandler = new OAuthSigningHandler(
        provider, "MYKEY", "mytoken", sessionManager)
    {
        InnerHandler = innerHandler,
    };

    using var httpClient = new HttpClient(signingHandler)
    {
        BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
    };

    await httpClient.GetAsync("portfolio/accounts", TestContext.Current.CancellationToken);

    sessionManager.EnsureInitCalled.ShouldBeTrue();
}
```

Also add the `FakeSessionManager` inner class to the test file:

```csharp
private class FakeSessionManager : ISessionManager
{
    public bool EnsureInitCalled { get; private set; }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        EnsureInitCalled = true;
        return Task.CompletedTask;
    }

    public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Add required `using` statements at the top:

```csharp
using IbkrConduit.Session;
```

- [ ] **Step 2: Run tests — expect compilation failure**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "SendAsync_CallsEnsureInitializedAsync_BeforeSigning" --configuration Release`

Expected: FAIL — `OAuthSigningHandler` constructor does not accept `ISessionManager` parameter.

- [ ] **Step 3: Update OAuthSigningHandler to accept and call ISessionManager**

File: `src/IbkrConduit/Auth/OAuthSigningHandler.cs`

Replace the entire file with:

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;

namespace IbkrConduit.Auth;

/// <summary>
/// DelegatingHandler that signs outgoing HTTP requests with OAuth HMAC-SHA256
/// using the Live Session Token. Ensures the brokerage session is initialized
/// before signing. Also sets required HTTP headers that the IBKR API gateway
/// (Akamai CDN) expects on every request.
/// </summary>
public class OAuthSigningHandler : DelegatingHandler
{
    private static readonly ProductInfoHeaderValue _defaultUserAgent = new("IbkrConduit", "1.0");

    private readonly ISessionTokenProvider _tokenProvider;
    private readonly string _consumerKey;
    private readonly string _accessToken;
    private readonly ISessionManager? _sessionManager;

    /// <summary>
    /// Creates a new signing handler with session management.
    /// </summary>
    public OAuthSigningHandler(
        ISessionTokenProvider tokenProvider,
        string consumerKey,
        string accessToken,
        ISessionManager? sessionManager = null)
    {
        _tokenProvider = tokenProvider;
        _consumerKey = consumerKey;
        _accessToken = accessToken;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_sessionManager != null)
        {
            await _sessionManager.EnsureInitializedAsync(cancellationToken);
        }

        var lst = await _tokenProvider.GetLiveSessionTokenAsync(cancellationToken);

        var signer = new HmacSha256Signer(lst.Token);
        var baseStringBuilder = new StandardBaseStringBuilder();
        var headerBuilder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var url = request.RequestUri!.ToString();
        var method = request.Method.Method;

        var authHeaderValue = headerBuilder.Build(method, url, _consumerKey, _accessToken);

        // Use TryAddWithoutValidation since the OAuth header format doesn't fit
        // the standard AuthenticationHeaderValue scheme/parameter model
        request.Headers.TryAddWithoutValidation("Authorization", authHeaderValue);

        // IBKR's API gateway (Akamai CDN) returns 403 if no User-Agent is present
        if (request.Headers.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.Add(_defaultUserAgent);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 4: Update existing tests to use new constructor signature**

The existing tests in `OAuthSigningHandlerTests.cs` use the 3-parameter constructor. Since `sessionManager` defaults to `null`, the existing tests should still compile without changes. Verify by running:

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "OAuthSigningHandlerTests" --configuration Release`

Expected: All tests PASS (existing tests use 3-param constructor which still works with optional 4th param, and new test passes).

### Sub-task 2.6b: Update ServiceCollectionExtensions

- [ ] **Step 5: Write failing test for updated DI wiring**

File: `tests/IbkrConduit.Tests.Unit/Http/ServiceCollectionExtensionsTests.cs`

```csharp
using System;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIbkrClient_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        services.AddIbkrClient<IIbkrPortfolioApi>(creds);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
        provider.GetService<ILiveSessionTokenClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_WithOptions_RegistersSessionManager()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();
        var options = new IbkrClientOptions { Compete = false };

        services.AddIbkrClient<IIbkrPortfolioApi>(creds, options);

        var provider = services.BuildServiceProvider();

        provider.GetService<ISessionTokenProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddIbkrClient_ResolvesRefitClient()
    {
        var services = new ServiceCollection();
        var creds = CreateTestCredentials();

        services.AddIbkrClient<IIbkrPortfolioApi>(creds);

        var provider = services.BuildServiceProvider();

        var api = provider.GetService<IIbkrPortfolioApi>();
        api.ShouldNotBeNull();
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = RSA.Create(2048);
        var encKey = RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new BigInteger(23));
    }
}
```

- [ ] **Step 6: Run tests — may fail due to missing registrations**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "ServiceCollectionExtensionsTests" --configuration Release`

Expected: Some tests may fail because `AddIbkrClient` does not yet register `ISessionManager`, `ITickleTimerFactory`, or `TokenRefreshHandler`.

- [ ] **Step 7: Update ServiceCollectionExtensions**

File: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`

Replace the entire file with:

```csharp
using System;
using System.Net.Http;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace IbkrConduit.Http;

/// <summary>
/// Extension methods for registering IBKR API clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string _ibkrBaseUrl = "https://api.ibkr.com";
    private const string _sessionApiClientName = "IbkrSessionApi";

    /// <summary>
    /// Registers the IBKR API client pipeline for the given tenant.
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> OAuthSigningHandler -> HttpClient.
    /// Internal session pipeline: Refit -> OAuthSigningHandler -> HttpClient (no TokenRefreshHandler).
    /// </summary>
    public static IServiceCollection AddIbkrClient<TApi>(
        this IServiceCollection services,
        IbkrOAuthCredentials credentials,
        IbkrClientOptions? options = null) where TApi : class
    {
        var clientOptions = options ?? new IbkrClientOptions();

        // LST client (plain HttpClient, not through Refit pipeline)
        services.AddSingleton<ILiveSessionTokenClient>(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_ibkrBaseUrl + "/v1/api/"),
            };
            return new LiveSessionTokenClient(httpClient);
        });

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
                sp.GetRequiredService<ILogger<TickleTimer>>()));

        // Internal session API client (signing only, no TokenRefreshHandler)
        services.AddTransient<OAuthSigningHandler>(sp =>
            new OAuthSigningHandler(
                sp.GetRequiredService<ISessionTokenProvider>(),
                credentials.ConsumerKey,
                credentials.AccessToken));

        services.AddRefitClient<IIbkrSessionApi>(provider: null, name: _sessionApiClientName)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler<OAuthSigningHandler>();

        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
            new SessionManager(
                sp.GetRequiredService<ISessionTokenProvider>(),
                sp.GetRequiredService<ITickleTimerFactory>(),
                sp.GetRequiredService<IIbkrSessionApi>(),
                clientOptions,
                sp.GetRequiredService<ILogger<SessionManager>>()));

        // Token refresh handler (for consumer API pipeline)
        services.AddTransient<TokenRefreshHandler>(sp =>
            new TokenRefreshHandler(
                sp.GetRequiredService<ISessionManager>()));

        // Consumer API signing handler (with session manager for lazy init)
        services.AddTransient(sp =>
        {
            var signingHandler = new OAuthSigningHandler(
                sp.GetRequiredService<ISessionTokenProvider>(),
                credentials.ConsumerKey,
                credentials.AccessToken,
                sp.GetRequiredService<ISessionManager>());
            return signingHandler;
        });

        // Consumer Refit client: TokenRefreshHandler -> OAuthSigningHandler -> HttpClient
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .AddHttpMessageHandler<TokenRefreshHandler>()
            .AddHttpMessageHandler<OAuthSigningHandler>();

        return services;
    }
}
```

- [ ] **Step 8: Run tests — expect all to pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "ServiceCollectionExtensionsTests" --configuration Release`

Expected: All 3 tests PASS.

- [ ] **Step 9: Run full build and test suite**

Run: `dotnet build --configuration Release`

Expected: Build succeeded with 0 warnings.

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 10: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 11: Commit**

```
feat: wire session lifecycle into HTTP pipeline

Updates OAuthSigningHandler to call EnsureInitializedAsync before signing.
Updates ServiceCollectionExtensions with two HTTP client pipelines:
consumer API (TokenRefreshHandler + OAuthSigningHandler) and internal
session API (OAuthSigningHandler only, avoiding recursive re-auth).
```

---

## Task 2.7: Integration Tests — Session Lifecycle

**Branch:** `feat/m2-session-lifecycle-integration-tests`
**PR scope:** WireMock-based integration tests validating full session lifecycle (init, tickle, 401 retry, shutdown).
**Depends on:** Task 2.6 (merged).

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Session/SessionLifecycleTests.cs`

- [ ] **Step 1: Write integration tests**

File: `tests/IbkrConduit.Tests.Integration/Session/SessionLifecycleTests.cs`

```csharp
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Session;

public class SessionLifecycleTests : IDisposable
{
    private readonly WireMockServer _server;

    public SessionLifecycleTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task FullInitialization_CallsEndpointsInCorrectOrder()
    {
        // Arrange: Mock all session endpoints
        SetupLstEndpoint();
        SetupSsodhInitEndpoint();
        SetupSuppressEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();

        var options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new System.Collections.Generic.List<string> { "o163" },
        };

        await using var provider = BuildServiceProvider(options);
        var api = provider.GetRequiredService<IIbkrPortfolioApi>();

        // Act: call portfolio endpoint (triggers lazy init)
        var accounts = await api.GetAccountsAsync();

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");

        // Verify endpoints were called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("ssodh/init"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("suppress"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("portfolio/accounts"));
    }

    [Fact]
    public async Task UnauthorizedApiCall_TriggersReauthAndRetries()
    {
        // Arrange
        SetupLstEndpoint();
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);

        var portfolioCallCount = 0;
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithCallback(req =>
                    {
                        portfolioCallCount++;
                        if (portfolioCallCount == 1)
                        {
                            return new WireMock.ResponseBuilders.ResponseMessage
                            {
                                StatusCode = 401,
                                BodyData = new WireMock.Types.BodyData
                                {
                                    BodyAsString = "Unauthorized",
                                    DetectedBodyType = WireMock.Types.BodyType.String,
                                },
                            };
                        }

                        return new WireMock.ResponseBuilders.ResponseMessage
                        {
                            StatusCode = 200,
                            Headers = new System.Collections.Generic.Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                ["Content-Type"] = new WireMock.Types.WireMockList<string>("application/json"),
                            },
                            BodyData = new WireMock.Types.BodyData
                            {
                                BodyAsString = """[{"id":"DU1234567","accountTitle":"Paper Trading","type":"INDIVIDUAL"}]""",
                                DetectedBodyType = WireMock.Types.BodyType.String,
                            },
                        };
                    }));

        await using var provider = BuildServiceProvider();
        var api = provider.GetRequiredService<IIbkrPortfolioApi>();

        // Act: First call gets 401, triggers re-auth, retries
        var accounts = await api.GetAccountsAsync();

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        portfolioCallCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CleanShutdown_CallsLogout()
    {
        // Arrange
        SetupLstEndpoint();
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();
        SetupLogoutEndpoint();

        var provider = BuildServiceProvider();
        var api = provider.GetRequiredService<IIbkrPortfolioApi>();

        // Initialize
        await api.GetAccountsAsync();

        // Act: Dispose
        await provider.DisposeAsync();

        // Assert: logout was called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("logout"));
    }

    [Fact(Skip = "Requires real IBKR paper account credentials in environment variables")]
    public async Task PaperAccount_FullLifecycle_InitializesAndShutdown()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient<IIbkrPortfolioApi>(creds, new IbkrClientOptions { Compete = true });

        await using var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IIbkrPortfolioApi>();

        var accounts = await api.GetAccountsAsync();

        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private ServiceProvider BuildServiceProvider(IbkrClientOptions? options = null)
    {
        var creds = CreateTestCredentials();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient<IIbkrPortfolioApi>(creds, options);

        // Override base URL to point to WireMock
        // We need to reconfigure the HttpClient base addresses
        // This is done by removing and re-adding the HTTP client configuration
        return services.BuildServiceProvider();
    }

    private void SetupLstEndpoint()
    {
        // The LST client uses a plain HttpClient, not the Refit pipeline.
        // For integration tests, the LST acquisition happens outside WireMock
        // unless we also intercept it. Since we're testing session lifecycle
        // (not LST acquisition which was tested in M1), we mock at a higher level.
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "diffie_hellman_response": "a1b2c3d4e5f6",
                            "live_session_token_signature": "abcdef0123456789",
                            "live_session_token_expiration": 9999999999999
                        }
                        """));
    }

    private void SetupSsodhInitEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"connected":true,"competing":false}"""));
    }

    private void SetupSuppressEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"status":"submitted"}"""));
    }

    private void SetupTickleEndpoint(bool authenticated)
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($$"""
                        {
                            "session": "abc123",
                            "iserver": {
                                "authStatus": {
                                    "authenticated": {{authenticated.ToString().ToLowerInvariant()}},
                                    "competing": false,
                                    "connected": true
                                }
                            }
                        }
                        """));
    }

    private void SetupPortfolioAccountsEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL"
                            }
                        ]
                        """));
    }

    private void SetupLogoutEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/logout")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"confirmed":true}"""));
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = System.Security.Cryptography.RSA.Create(2048);
        var encKey = System.Security.Cryptography.RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token",
            Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            sigKey, encKey, new System.Numerics.BigInteger(23));
    }
}
```

**Important note:** The integration tests for Task 2.7 are more complex than typical unit tests because they require the full DI pipeline to be wired and pointed at WireMock. The LST acquisition flow involves complex DH cryptography that is difficult to mock through WireMock alone. The practical approach is to:

1. Test the session lifecycle components (SessionManager, TokenRefreshHandler, TickleTimer) thoroughly in unit tests with fakes (Tasks 2.3-2.5).
2. For integration tests, create a modified pipeline that uses a `FakeTokenProvider` to skip the LST acquisition, while still testing the Refit + handler chain against WireMock.

Therefore, refactor the integration tests to use a manual pipeline construction (similar to M1's integration tests) instead of the full DI container:

Replace the file with this refined version:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Session;

public class SessionLifecycleTests : IAsyncDisposable
{
    private readonly WireMockServer _server;

    public SessionLifecycleTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task FullInitialization_CallsEndpointsInCorrectOrder()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupSuppressEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = new List<string> { "o163" },
        };

        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        await using var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Act: call portfolio endpoint (triggers lazy init)
        var accounts = await portfolioApi.GetAccountsAsync();

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");

        // Verify endpoints were called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("ssodh/init"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("suppress"));
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("portfolio/accounts"));
    }

    [Fact]
    public async Task UnauthorizedApiCall_TriggersReauthAndRetries()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);

        var portfolioCallCount = 0;
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"DU1234567","accountTitle":"Paper Trading","type":"INDIVIDUAL"}]"""));

        // Override: first call returns 401, subsequent return 200
        // WireMock scenarios handle this
        _server.ResetMappings();
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-retry")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("401-retry")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"DU1234567","accountTitle":"Paper Trading","type":"INDIVIDUAL"}]"""));

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions();
        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        await using var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Act
        var accounts = await portfolioApi.GetAccountsAsync();

        // Assert
        accounts.ShouldNotBeNull();
        accounts.Count.ShouldBe(1);
        accounts[0].Id.ShouldBe("DU1234567");
    }

    [Fact]
    public async Task CleanShutdown_CallsLogout()
    {
        // Arrange
        SetupSsodhInitEndpoint();
        SetupTickleEndpoint(authenticated: true);
        SetupPortfolioAccountsEndpoint();
        SetupLogoutEndpoint();

        var tokenProvider = CreateFakeTokenProvider();
        var sessionApi = CreateSessionApi(tokenProvider);
        var options = new IbkrClientOptions();
        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            NullLogger<SessionManager>.Instance);

        var portfolioApi = CreatePortfolioApi(tokenProvider, sessionManager);

        // Initialize by making an API call
        await portfolioApi.GetAccountsAsync();

        // Act: Dispose triggers shutdown
        await sessionManager.DisposeAsync();

        // Assert: logout was called
        var entries = _server.LogEntries.ToList();
        entries.ShouldContain(e => e.RequestMessage.Path!.Contains("logout"));
    }

    [Fact(Skip = "Requires real IBKR paper account credentials in environment variables")]
    public async Task PaperAccount_FullLifecycle_InitializesAndShutdown()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();

        using var lstHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.ibkr.com/v1/api/"),
        };
        var lstClient = new LiveSessionTokenClient(lstHttpClient);
        var tokenProvider = new SessionTokenProvider(creds, lstClient);

        var signingHandler = new OAuthSigningHandler(tokenProvider, creds.ConsumerKey, creds.AccessToken)
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var sessionHttpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };
        var sessionApi = Refit.RestService.For<IIbkrSessionApi>(sessionHttpClient);

        var options = new IbkrClientOptions { Compete = true };
        var tickleTimerFactory = new TickleTimerFactory(NullLogger<TickleTimer>.Instance);

        await using var sessionManager = new SessionManager(
            tokenProvider,
            tickleTimerFactory,
            sessionApi,
            options,
            NullLogger<SessionManager>.Instance);

        var consumerSigningHandler = new OAuthSigningHandler(
            tokenProvider, creds.ConsumerKey, creds.AccessToken, sessionManager)
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var consumerHttpClient = new HttpClient(consumerSigningHandler)
        {
            BaseAddress = new Uri("https://api.ibkr.com"),
        };
        var portfolioApi = Refit.RestService.For<IIbkrPortfolioApi>(consumerHttpClient);

        var accounts = await portfolioApi.GetAccountsAsync();

        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();
        accounts[0].Id.ShouldNotBeNullOrWhiteSpace();
    }

    public async ValueTask DisposeAsync()
    {
        _server.Dispose();
        await Task.CompletedTask;
    }

    private IIbkrPortfolioApi CreatePortfolioApi(
        FakeTokenProvider tokenProvider,
        SessionManager sessionManager)
    {
        var tokenRefreshHandler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = new OAuthSigningHandler(
                tokenProvider,
                "TESTKEY01",
                "mytoken",
                sessionManager)
            {
                InnerHandler = new HttpClientHandler(),
            },
        };

        var httpClient = new HttpClient(tokenRefreshHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<IIbkrPortfolioApi>(httpClient);
    }

    private IIbkrSessionApi CreateSessionApi(FakeTokenProvider tokenProvider)
    {
        var signingHandler = new OAuthSigningHandler(
            tokenProvider,
            "TESTKEY01",
            "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<IIbkrSessionApi>(httpClient);
    }

    private static FakeTokenProvider CreateFakeTokenProvider()
    {
        var lstBytes = new byte[] {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            0x11, 0x12, 0x13, 0x14,
        };
        return new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));
    }

    private void SetupSsodhInitEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"authenticated":true,"connected":true,"competing":false}"""));
    }

    private void SetupSuppressEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"status":"submitted"}"""));
    }

    private void SetupTickleEndpoint(bool authenticated)
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($$"""
                        {
                            "session": "abc123",
                            "iserver": {
                                "authStatus": {
                                    "authenticated": {{authenticated.ToString().ToLowerInvariant()}},
                                    "competing": false,
                                    "connected": true
                                }
                            }
                        }
                        """));
    }

    private void SetupPortfolioAccountsEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "DU1234567",
                                "accountTitle": "Paper Trading",
                                "type": "INDIVIDUAL"
                            }
                        ]
                        """));
    }

    private void SetupLogoutEndpoint()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/logout")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"confirmed":true}"""));
    }

    internal class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
```

- [ ] **Step 2: Add Refit PackageReference to integration test project if not present**

Verify `tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj` has:

```xml
<PackageReference Include="Refit" />
```

This should already be present from M1.

- [ ] **Step 3: Run integration tests**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --filter "SessionLifecycleTests" --configuration Release`

Expected: 3 non-skipped tests PASS (the paper account test is skipped).

- [ ] **Step 4: Run full test suite**

Run: `dotnet test --configuration Release`

Expected: All tests PASS.

- [ ] **Step 5: Run lint**

Run: `dotnet format --verify-no-changes`

Expected: No formatting issues.

- [ ] **Step 6: Commit**

```
test: add WireMock integration tests for session lifecycle

Tests full initialization flow, 401 retry with re-auth, and clean
shutdown with logout. Paper account E2E test included but skipped.
```

---

## Task 2.8: Extract ibind Question Suppression IDs

**Branch:** `feat/m2-ibind-suppression-ids`
**PR scope:** Research task — document known suppressible message IDs.
**Depends on:** Task 2.7 (merged).

This is a research/documentation task, not a code change.

**Files:**
- Create: `docs/ibkr-suppressible-message-ids.md`

### Research steps

- [ ] **Step 1: Search ibind source for suppression-related code**

Search the ibind Python library (https://github.com/Voyz/ibind) for:
- Strings like `suppress`, `messageId`, `o163`, `o451`
- Any configuration or constant files listing message IDs
- The `question` or `questions` endpoint handling logic

Key files to examine in ibind:
- `ibind/client/ibkr_client.py` — the main client that manages sessions
- `ibind/support/py_utils.py` — utility constants
- Any configuration or constants module

- [ ] **Step 2: Search IBKR official documentation**

Check the IBKR Client Portal API documentation for:
- Suppressible question/message ID reference
- The `/iserver/questions/suppress` endpoint documentation
- Known message IDs and their meanings

- [ ] **Step 3: Document findings**

File: `docs/ibkr-suppressible-message-ids.md`

Create a markdown file documenting:
- Each discovered message ID
- What question/prompt it suppresses
- Source (ibind, official docs, or empirical testing)
- Recommended defaults for `IbkrClientOptions.SuppressMessageIds`

Template:

```markdown
# IBKR Suppressible Message IDs

Reference of known message IDs that can be passed to `POST /iserver/questions/suppress`
to avoid interactive prompts during automated trading.

## Discovered IDs

| ID | Description | Source |
|---|---|---|
| `o163` | [description] | [ibind/docs/empirical] |
| `o451` | [description] | [ibind/docs/empirical] |
| ... | ... | ... |

## Recommended Defaults

For automated/headless usage, suppress these IDs:

```csharp
var options = new IbkrClientOptions
{
    SuppressMessageIds = new List<string> { /* discovered IDs */ },
};
```

## References

- ibind source: https://github.com/Voyz/ibind
- IBKR Client Portal API docs: https://www.interactivebrokers.com/api/doc.html
```

- [ ] **Step 4: Commit**

```
docs: document IBKR suppressible message IDs

Research findings from ibind source and IBKR documentation for
question/message IDs that should be suppressed in automated sessions.
```

---

## Summary

| Task | Branch | Files Created | Files Modified | Tests |
|---|---|---|---|---|
| 2.1 | `feat/m2-session-interfaces` | 8 source + 1 test | — | 9 unit |
| 2.2 | `feat/m2-token-refresh-support` | 1 test | 2 source + 2 test (FakeTokenProvider) | 3 unit |
| 2.3 | `feat/m2-tickle-timer` | 2 source + 1 test | 2 config | 4 unit |
| 2.4 | `feat/m2-session-manager` | 2 source + 1 test | 1 source (ITickleTimer.cs) | 11 unit |
| 2.5 | `feat/m2-token-refresh-handler` | 1 source + 1 test | — | 5 unit |
| 2.6 | `feat/m2-pipeline-wiring` | 1 test | 2 source | 4 unit |
| 2.7 | `feat/m2-session-lifecycle-integration-tests` | 1 test | — | 3 integration |
| 2.8 | `feat/m2-ibind-suppression-ids` | 1 doc | — | — |
| **Total** | | **14 source + 7 test + 1 doc** | **7 files** | **39 tests** |
