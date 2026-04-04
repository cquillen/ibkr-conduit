# M1: Session/Accounts + Pipeline Auth Failures — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build integration tests for session and accounts endpoints with fixture-based DTO validation, 401 recovery tests for accounts, and fix `TokenRefreshHandler` for pipeline-level auth failure scenarios.

**Architecture:** Full DI stack via `AddIbkrClient` with WireMock. Session endpoints are tested by resolving `IIbkrSessionApi` directly from DI (internal pipeline, no `TokenRefreshHandler`). Accounts endpoints use `Client.Accounts` (consumer pipeline with `TokenRefreshHandler`). Auth failure tests override stubs after session init to force failure modes.

**Tech Stack:** xUnit v3, Shouldly, WireMock.Net, Microsoft.Extensions.DependencyInjection, System.Text.Json

---

## File Map

### New Files
| Path | Purpose |
|---|---|
| `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-ssodh-init.json` | Sanitized ssodh/init fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-tickle.json` | Sanitized tickle fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Session/GET-auth-status.json` | Sanitized auth status fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-suppress.json` | Sanitized suppress fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-suppress-reset.json` | Sanitized suppress reset fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Accounts/GET-iserver-accounts.json` | Sanitized iserver accounts fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Fixtures/Accounts/POST-switch-account.json` | Sanitized switch account fixture from recording |
| `tests/IbkrConduit.Tests.Integration/Session/SessionTests.cs` | Session endpoint integration tests |
| `tests/IbkrConduit.Tests.Integration/Accounts/AccountsTests.cs` | Accounts endpoint integration tests |
| `tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs` | Pipeline-level 401 failure scenario tests |

### Modified Files
| Path | Change |
|---|---|
| `tests/IbkrConduit.Tests.Integration/TestHarness.cs` | Add `GetRequiredService<T>()` for resolving internal DI services |
| `src/IbkrConduit/Session/IIbkrSessionApiModels.cs` | Add missing fields: `Established`, `Message`, `Mac`, `ServerInfo`, `HardwareInfo` + `[JsonExtensionData]` on all response DTOs |
| `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs` | Add missing fields to `IserverAccountsResponse`, fix `SwitchAccountResponse` to match wire format |
| `src/IbkrConduit/Errors/IbkrApiException.cs` | Add constructor overload accepting inner exception |
| `src/IbkrConduit/Errors/IbkrSessionException.cs` | Add constructor overload accepting message + inner exception |
| `src/IbkrConduit/Session/TokenRefreshHandler.cs` | Add retry limit (one max), catch re-auth failures, wrap in `IbkrSessionException` |

---

## Task 1: Create Fixture Files from Recordings

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-ssodh-init.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-tickle.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Session/GET-auth-status.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-suppress.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Session/POST-suppress-reset.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Accounts/GET-iserver-accounts.json`
- Create: `tests/IbkrConduit.Tests.Integration/Fixtures/Accounts/POST-switch-account.json`

Sanitize recordings by replacing sensitive values:
- Account IDs: `DUO873728` → `U1234567`
- MAC: `06:05:9B:DE:2D:8B` → `AA:BB:CC:DD:EE:FF`
- hardware_info: `d12054d0|06:05:9B:DE:2D:8B` → `test1234|AA:BB:CC:DD:EE:FF`
- serverName: `JifN20047` / `JifN16068` → `TestServer01`
- Account names: `Robert C Quillen` → `Test User`
- Session IDs: `69cdf4fa.00000051` → `test-session-001`
- DH challenge: keep `session` hash value as-is (not PII)

- [ ] **Step 1: Create `Fixtures/Session/POST-ssodh-init.json`**

```json
{
  "Request": {
    "Path": "/v1/api/iserver/auth/ssodh/init",
    "Methods": ["POST"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "authenticated": true,
      "established": true,
      "competing": false,
      "connected": true,
      "message": "",
      "MAC": "AA:BB:CC:DD:EE:FF",
      "serverInfo": {
        "serverName": "TestServer01",
        "serverVersion": "Build 10.44.1d, Mar 3, 2026 1:55:32 PM"
      },
      "hardware_info": "test1234|AA:BB:CC:DD:EE:FF"
    }
  }
}
```

- [ ] **Step 2: Create `Fixtures/Session/POST-tickle.json`**

```json
{
  "Request": {
    "Path": "/v1/api/tickle",
    "Methods": ["POST"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "session": "abc123def456",
      "hmds": {
        "error": "no bridge"
      },
      "iserver": {
        "authStatus": {
          "authenticated": true,
          "established": true,
          "competing": false,
          "connected": true,
          "message": "",
          "MAC": "AA:BB:CC:DD:EE:FF",
          "serverInfo": {
            "serverName": "TestServer01",
            "serverVersion": "Build 10.44.1d, Mar 3, 2026 1:55:32 PM"
          },
          "hardware_info": "test1234|AA:BB:CC:DD:EE:FF"
        }
      }
    }
  }
}
```

- [ ] **Step 3: Create `Fixtures/Session/GET-auth-status.json`**

```json
{
  "Request": {
    "Path": "/v1/api/iserver/auth/status",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "authenticated": true,
      "established": true,
      "competing": false,
      "connected": true,
      "message": "",
      "MAC": "AA:BB:CC:DD:EE:FF",
      "serverInfo": {
        "serverName": "TestServer01",
        "serverVersion": "Build 10.44.1d, Mar 3, 2026 1:55:32 PM"
      },
      "hardware_info": "test1234|AA:BB:CC:DD:EE:FF",
      "fail": ""
    }
  }
}
```

- [ ] **Step 4: Create `Fixtures/Session/POST-suppress.json`**

```json
{
  "Request": {
    "Path": "/v1/api/iserver/questions/suppress",
    "Methods": ["POST"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "status": "submitted"
    }
  }
}
```

- [ ] **Step 5: Create `Fixtures/Session/POST-suppress-reset.json`**

```json
{
  "Request": {
    "Path": "/v1/api/iserver/questions/suppress/reset",
    "Methods": ["POST"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "status": "submitted"
    }
  }
}
```

- [ ] **Step 6: Create `Fixtures/Accounts/GET-iserver-accounts.json`**

```json
{
  "Request": {
    "Path": "/v1/api/iserver/accounts",
    "Methods": ["GET"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "accounts": ["U1234567"],
      "acctProps": {
        "U1234567": {
          "hasChildAccounts": false,
          "supportsCashQty": true,
          "liteUnderPro": false,
          "noFXConv": false,
          "isProp": false,
          "supportsFractions": true,
          "allowCustomerTime": false,
          "autoFx": false
        }
      },
      "aliases": {
        "U1234567": "U1234567"
      },
      "allowFeatures": {
        "showGFIS": true,
        "showEUCostReport": false,
        "allowEventContract": true,
        "allowFXConv": true,
        "allowFinancialLens": false,
        "allowMTA": true,
        "allowTypeAhead": true,
        "allowEventTrading": true,
        "snapshotRefreshTimeout": 30,
        "liteUser": false,
        "showWebNews": true,
        "research": true,
        "debugPnl": true,
        "showTaxOpt": true,
        "showImpactDashboard": true,
        "allowDynAccount": false,
        "allowCrypto": true,
        "allowFA": false,
        "allowLiteUnderPro": false,
        "allowedAssetTypes": "STK,CFD,OPT,FOP,WAR,FUT,BAG,PDC,CASH,IND,BOND,BILL,FUND,SLB,News,CMDTY,IOPT,ICU,ICS,PHYSS,CRYPTO",
        "restrictTradeSubscription": false,
        "showUkUserLabels": false,
        "sideBySide": true
      },
      "chartPeriods": {
        "STK": ["*"],
        "CFD": ["*"],
        "OPT": ["2h", "1d", "2d", "1w", "1m"],
        "FOP": ["2h", "1d", "2d", "1w", "1m"],
        "WAR": ["*"],
        "IOPT": ["*"],
        "FUT": ["*"],
        "CASH": ["*"],
        "IND": ["*"],
        "BOND": ["*"],
        "FUND": ["*"],
        "CMDTY": ["*"],
        "PHYSS": ["*"],
        "CRYPTO": ["*"]
      },
      "groups": [],
      "profiles": [],
      "selectedAccount": "U1234567",
      "serverInfo": {
        "serverName": "TestServer01",
        "serverVersion": "Build 10.44.1d, Mar 3, 2026 1:55:32 PM"
      },
      "sessionId": "test-session-001",
      "isFT": false,
      "isPaper": true
    }
  }
}
```

- [ ] **Step 7: Create `Fixtures/Accounts/POST-switch-account.json`**

The recording shows `{"success": "Account already set"}` — this is the real wire format. The current `SwitchAccountResponse` DTO (`set`/`selectedAccount`) is wrong and will be fixed in Task 4.

```json
{
  "Request": {
    "Path": "/v1/api/iserver/account",
    "Methods": ["POST"]
  },
  "Response": {
    "StatusCode": 200,
    "Headers": { "Content-Type": "application/json; charset=utf-8" },
    "Body": {
      "success": "Account already set"
    }
  }
}
```

- [ ] **Step 8: Verify fixtures load correctly**

Run: `dotnet build tests/IbkrConduit.Tests.Integration --configuration Release`
Expected: BUILD SUCCEEDED (fixtures are copied via `<None Include="Fixtures\**\*.json" CopyToOutputDirectory="PreserveNewest" />`)

- [ ] **Step 9: Commit fixture files**

```bash
git add tests/IbkrConduit.Tests.Integration/Fixtures/Session/ tests/IbkrConduit.Tests.Integration/Fixtures/Accounts/
git commit -m "test: add session and accounts fixture files from recordings

Sanitized fixtures for ssodh/init, tickle, auth status, suppress,
suppress/reset, iserver accounts, and switch account."
```

---

## Task 2: Expose DI Services from TestHarness

**Files:**
- Modify: `tests/IbkrConduit.Tests.Integration/TestHarness.cs`

Session endpoints use the internal `IIbkrSessionApi` Refit interface (internal pipeline, no `TokenRefreshHandler`). To call these in tests, we need to resolve them from the DI container. `InternalsVisibleTo` is already configured for `IbkrConduit.Tests.Integration` in `IbkrConduit.csproj`.

- [ ] **Step 1: Add `GetRequiredService<T>()` to TestHarness**

In `tests/IbkrConduit.Tests.Integration/TestHarness.cs`, add this method after the `StubAuthenticatedPost` method:

```csharp
/// <summary>
/// Resolves a service from the DI container. Use for accessing internal services
/// like <see cref="IIbkrSessionApi"/> or <see cref="ISessionManager"/> in tests.
/// </summary>
public T GetRequiredService<T>() where T : notnull =>
    _provider!.GetRequiredService<T>();
```

Add this using directive at the top of the file (if not already present):

```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 2: Verify build succeeds**

Run: `dotnet build tests/IbkrConduit.Tests.Integration --configuration Release`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/TestHarness.cs
git commit -m "feat: add GetRequiredService<T> to TestHarness for internal API access"
```

---

## Task 3: Session Integration Tests + DTO Updates

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Session/SessionTests.cs`
- Modify: `src/IbkrConduit/Session/IIbkrSessionApiModels.cs`

Session endpoints are on the internal pipeline (no `TokenRefreshHandler`), so there are no 401 recovery tests. Tests resolve `IIbkrSessionApi` from DI and call endpoints directly after initializing the session via `ISessionManager.EnsureInitializedAsync()`.

**Important:** TestHarness already stubs `/iserver/auth/ssodh/init` and `/tickle` with minimal responses. For DTO validation, register fixture-based stubs at higher priority (`AtPriority(-1)`) BEFORE calling `EnsureInitializedAsync`. For auth status, suppress, and suppress/reset — TestHarness doesn't stub these, so standard stubs work.

### Sub-task 3a: Write failing session tests

- [ ] **Step 1: Create `SessionTests.cs` with all 5 tests**

Create `tests/IbkrConduit.Tests.Integration/Session/SessionTests.cs`:

```csharp
using System;
using System.Text.Json;
using System.Threading.Tasks;
using IbkrConduit.Session;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

public class SessionTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;
    private IIbkrSessionApi _sessionApi = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();

        // Override ssodh/init stub with full fixture response (higher priority than TestHarness default)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-ssodh-init")));

        // Override tickle stub with full fixture response
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/tickle")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-tickle")));

        // Stub auth status (not stubbed by TestHarness)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/status")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "GET-auth-status")));

        // Stub suppress with fixture response (higher priority than TestHarness default which uses a simple response)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-suppress")));

        // Stub suppress/reset (not stubbed by TestHarness)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/questions/suppress/reset")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Session", "POST-suppress-reset")));

        // Initialize session to acquire LST (needed for OAuthSigningHandler on the internal pipeline)
        var sessionManager = _harness.GetRequiredService<ISessionManager>();
        await sessionManager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        _sessionApi = _harness.GetRequiredService<IIbkrSessionApi>();
    }

    [Fact]
    public async Task GetAuthStatus_ReturnsAllFields()
    {
        var result = await _sessionApi.GetAuthStatusAsync(TestContext.Current.CancellationToken);

        result.Authenticated.ShouldBeTrue();
        result.Established.ShouldBeTrue();
        result.Competing.ShouldBeFalse();
        result.Connected.ShouldBeTrue();
        result.Message.ShouldBe("");
        result.Fail.ShouldBe("");
        result.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.ServerInfo.ShouldNotBeNull();
        result.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.ServerInfo!.ServerVersion.ShouldBe("Build 10.44.1d, Mar 3, 2026 1:55:32 PM");
        result.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    [Fact]
    public async Task Tickle_ReturnsAllFields()
    {
        var result = await _sessionApi.TickleAsync(TestContext.Current.CancellationToken);

        result.Session.ShouldBe("abc123def456");
        result.Hmds.ShouldNotBeNull();
        result.Hmds!.GetProperty("error").GetString().ShouldBe("no bridge");
        result.Iserver.ShouldNotBeNull();
        result.Iserver!.AuthStatus.ShouldNotBeNull();
        result.Iserver!.AuthStatus!.Authenticated.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Established.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Competing.ShouldBeFalse();
        result.Iserver!.AuthStatus!.Connected.ShouldBeTrue();
        result.Iserver!.AuthStatus!.Message.ShouldBe("");
        result.Iserver!.AuthStatus!.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.Iserver!.AuthStatus!.ServerInfo.ShouldNotBeNull();
        result.Iserver!.AuthStatus!.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.Iserver!.AuthStatus!.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    [Fact]
    public async Task Suppress_ReturnsStatus()
    {
        var result = await _sessionApi.SuppressQuestionsAsync(
            new SuppressRequest(["o163"]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe("submitted");
    }

    [Fact]
    public async Task SuppressReset_ReturnsStatus()
    {
        var result = await _sessionApi.ResetSuppressedQuestionsAsync(TestContext.Current.CancellationToken);

        result.Status.ShouldBe("submitted");
    }

    [Fact]
    public async Task SsodhInit_ReturnsAllFields()
    {
        var result = await _sessionApi.InitializeBrokerageSessionAsync(
            new SsodhInitRequest(Publish: true, Compete: true), TestContext.Current.CancellationToken);

        result.Authenticated.ShouldBeTrue();
        result.Established.ShouldBeTrue();
        result.Competing.ShouldBeFalse();
        result.Connected.ShouldBeTrue();
        result.Message.ShouldBe("");
        result.Mac.ShouldBe("AA:BB:CC:DD:EE:FF");
        result.ServerInfo.ShouldNotBeNull();
        result.ServerInfo!.ServerName.ShouldBe("TestServer01");
        result.ServerInfo!.ServerVersion.ShouldBe("Build 10.44.1d, Mar 3, 2026 1:55:32 PM");
        result.HardwareInfo.ShouldBe("test1234|AA:BB:CC:DD:EE:FF");

        _harness.VerifyUserAgentOnAllRequests();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~SessionTests"`
Expected: FAIL — properties like `Established`, `Mac`, `ServerInfo`, `HardwareInfo`, `Hmds` don't exist on DTOs yet.

### Sub-task 3b: Update session DTOs

- [ ] **Step 3: Add `ServerInfo` record and update `SsodhInitResponse`**

In `src/IbkrConduit/Session/IIbkrSessionApiModels.cs`, add the `ServerInfo` record and update `SsodhInitResponse`:

Add `ServerInfo` record (before `SsodhInitRequest`):

```csharp
/// <summary>
/// Server identification returned in auth and tickle responses.
/// </summary>
/// <param name="ServerName">Server hostname identifier.</param>
/// <param name="ServerVersion">Server build version string.</param>
[ExcludeFromCodeCoverage]
public record ServerInfo(
    [property: JsonPropertyName("serverName")] string? ServerName,
    [property: JsonPropertyName("serverVersion")] string? ServerVersion);
```

Replace the existing `SsodhInitResponse` with:

```csharp
/// <summary>
/// Response from POST /iserver/auth/ssodh/init.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Established">Whether the session has been fully established.</param>
/// <param name="Message">Optional status message from the server.</param>
/// <param name="Mac">MAC address of the server.</param>
/// <param name="ServerInfo">Server identification details.</param>
/// <param name="HardwareInfo">Hardware identifier string.</param>
[ExcludeFromCodeCoverage]
public record SsodhInitResponse(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("established")] bool Established,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("MAC")] string? Mac,
    [property: JsonPropertyName("serverInfo")] ServerInfo? ServerInfo,
    [property: JsonPropertyName("hardware_info")] string? HardwareInfo)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 4: Update `TickleResponse`, `TickleIserverStatus`, and `TickleAuthStatus`**

Replace the existing `TickleResponse`:

```csharp
/// <summary>
/// Response from POST /tickle. Contains session status and optional iserver auth status.
/// </summary>
/// <param name="Session">The session identifier.</param>
/// <param name="Hmds">Historical market data service status (may contain error field).</param>
/// <param name="Iserver">Optional iserver status including authentication state.</param>
[ExcludeFromCodeCoverage]
public record TickleResponse(
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("hmds")] JsonElement? Hmds,
    [property: JsonPropertyName("iserver")] TickleIserverStatus? Iserver)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

Replace `TickleIserverStatus` (add `[JsonExtensionData]`):

```csharp
/// <summary>
/// Iserver status block within a tickle response.
/// </summary>
/// <param name="AuthStatus">Authentication status of the iserver connection.</param>
[ExcludeFromCodeCoverage]
public record TickleIserverStatus(
    [property: JsonPropertyName("authStatus")] TickleAuthStatus? AuthStatus)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

Replace `TickleAuthStatus` with all fields from recording:

```csharp
/// <summary>
/// Authentication status details from a tickle response.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Established">Whether the session has been fully established.</param>
/// <param name="Message">Optional status message from the server.</param>
/// <param name="Mac">MAC address of the server.</param>
/// <param name="ServerInfo">Server identification details.</param>
/// <param name="HardwareInfo">Hardware identifier string.</param>
[ExcludeFromCodeCoverage]
public record TickleAuthStatus(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("established")] bool Established,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("MAC")] string? Mac,
    [property: JsonPropertyName("serverInfo")] ServerInfo? ServerInfo,
    [property: JsonPropertyName("hardware_info")] string? HardwareInfo)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 5: Update `AuthStatusResponse`**

Replace the existing `AuthStatusResponse`:

```csharp
/// <summary>
/// Response from GET /iserver/auth/status.
/// </summary>
/// <param name="Authenticated">Whether the session is authenticated.</param>
/// <param name="Competing">Whether this session is competing with another.</param>
/// <param name="Connected">Whether the session is connected to the backend.</param>
/// <param name="Established">Whether the session has been fully established.</param>
/// <param name="Fail">Failure reason, if any.</param>
/// <param name="Message">Optional status message.</param>
/// <param name="Mac">MAC address of the server.</param>
/// <param name="ServerInfo">Server identification details.</param>
/// <param name="HardwareInfo">Hardware identifier string.</param>
/// <param name="Prompts">Optional prompts from the server.</param>
[ExcludeFromCodeCoverage]
public record AuthStatusResponse(
    [property: JsonPropertyName("authenticated")] bool Authenticated,
    [property: JsonPropertyName("competing")] bool Competing,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("established")] bool Established,
    [property: JsonPropertyName("fail")] string? Fail,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("MAC")] string? Mac,
    [property: JsonPropertyName("serverInfo")] ServerInfo? ServerInfo,
    [property: JsonPropertyName("hardware_info")] string? HardwareInfo,
    [property: JsonPropertyName("prompts")] List<string>? Prompts)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 6: Add `[JsonExtensionData]` to `SuppressResponse` and `SsodhInitRequest`**

`SuppressResponse` currently lacks `[JsonExtensionData]`. Add it:

```csharp
[ExcludeFromCodeCoverage]
public record SuppressResponse(
    [property: JsonPropertyName("status")] string Status)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

Note: `SsodhInitRequest` and `SuppressRequest` are request DTOs — they don't need `[JsonExtensionData]`.

- [ ] **Step 7: Fix any compilation errors in existing code**

The `SsodhInitResponse` constructor changed from 3 to 8 parameters. Check if any code constructs it directly (e.g., in unit tests or stubs). If so, update those call sites to include the new parameters with default values.

Run: `dotnet build --configuration Release`
Expected: BUILD SUCCEEDED (or compilation errors from call sites — fix them by adding default values for new parameters)

- [ ] **Step 8: Run session tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~SessionTests"`
Expected: 5 PASSED

- [ ] **Step 9: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass (existing tests unbroken by DTO changes)

- [ ] **Step 10: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Session/SessionTests.cs src/IbkrConduit/Session/IIbkrSessionApiModels.cs
git commit -m "feat: add session integration tests + update DTOs to match wire format

Add ServerInfo record, update SsodhInitResponse, TickleResponse,
TickleAuthStatus, and AuthStatusResponse with all fields from
recorded API responses. Add [JsonExtensionData] to all response DTOs."
```

---

## Task 4: Accounts Integration Tests + DTO Updates

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Accounts/AccountsTests.cs`
- Modify: `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs`

Accounts endpoints are on the consumer pipeline (`TokenRefreshHandler` is active), so they get both success and 401 recovery tests. The switch account DTO has a wire format mismatch that TDD will expose.

### Sub-task 4a: Write failing accounts tests

- [ ] **Step 1: Create `AccountsTests.cs`**

Create `tests/IbkrConduit.Tests.Integration/Accounts/AccountsTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Accounts;

public class AccountsTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetAccounts_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts"));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.Accounts.ShouldNotBeEmpty();
        result.Accounts[0].ShouldBe("U1234567");
        result.SelectedAccount.ShouldBe("U1234567");
        result.SessionId.ShouldBe("test-session-001");
        result.IsFt.ShouldBeFalse();
        result.IsPaper.ShouldBeTrue();
        result.ServerInfo.ShouldNotBeNull();
        result.AcctProps.ShouldNotBeNull();
        result.Aliases.ShouldNotBeNull();
        result.AllowFeatures.ShouldNotBeNull();
        result.ChartPeriods.ShouldNotBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAccounts_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("accounts-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("accounts-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.Accounts.ShouldNotBeEmpty();
        result.Accounts[0].ShouldBe("U1234567");
        result.SelectedAccount.ShouldBe("U1234567");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SwitchAccount_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account",
            FixtureLoader.LoadBody("Accounts", "POST-switch-account"));

        var result = await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

        result.Success.ShouldBe("Account already set");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SwitchAccount_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .InScenario("switch-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account")
                .UsingPost())
            .InScenario("switch-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "POST-switch-account")));

        var result = await _harness.Client.Accounts.SwitchAccountAsync(
            "U1234567", TestContext.Current.CancellationToken);

        result.Success.ShouldBe("Account already set");

        _harness.VerifyReauthenticationOccurred();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~AccountsTests"`
Expected: FAIL — `SessionId`, `IsFt`, `IsPaper`, `ServerInfo`, `AcctProps`, `Aliases`, `AllowFeatures`, `ChartPeriods` don't exist on `IserverAccountsResponse`. `Success` doesn't exist on `SwitchAccountResponse`.

### Sub-task 4b: Update accounts DTOs

- [ ] **Step 3: Update `IserverAccountsResponse`**

In `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs`, replace `IserverAccountsResponse`:

```csharp
/// <summary>
/// Response from GET /iserver/accounts.
/// </summary>
/// <param name="Accounts">List of account identifiers.</param>
/// <param name="SelectedAccount">The currently selected account.</param>
/// <param name="AcctProps">Per-account property flags (keyed by account ID).</param>
/// <param name="Aliases">Per-account alias mappings (keyed by account ID).</param>
/// <param name="AllowFeatures">Feature flags for the session.</param>
/// <param name="ChartPeriods">Allowed chart periods per asset class.</param>
/// <param name="Groups">Account groups.</param>
/// <param name="Profiles">Account profiles.</param>
/// <param name="ServerInfo">Server identification details.</param>
/// <param name="SessionId">Current session identifier.</param>
/// <param name="IsFt">Whether this is a financial tools session.</param>
/// <param name="IsPaper">Whether this is a paper trading account.</param>
[ExcludeFromCodeCoverage]
public record IserverAccountsResponse(
    [property: JsonPropertyName("accounts")] List<string> Accounts,
    [property: JsonPropertyName("selectedAccount")] string SelectedAccount,
    [property: JsonPropertyName("acctProps")] JsonElement? AcctProps,
    [property: JsonPropertyName("aliases")] JsonElement? Aliases,
    [property: JsonPropertyName("allowFeatures")] JsonElement? AllowFeatures,
    [property: JsonPropertyName("chartPeriods")] JsonElement? ChartPeriods,
    [property: JsonPropertyName("groups")] JsonElement? Groups,
    [property: JsonPropertyName("profiles")] JsonElement? Profiles,
    [property: JsonPropertyName("serverInfo")] JsonElement? ServerInfo,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("isFT")] bool? IsFt,
    [property: JsonPropertyName("isPaper")] bool? IsPaper)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 4: Fix `SwitchAccountResponse`**

The recording shows `{"success": "Account already set"}`, not `{"set": true, "selectedAccount": "..."}`. Replace:

```csharp
/// <summary>
/// Response from POST /iserver/account.
/// </summary>
/// <param name="Success">Success message describing the switch result.</param>
[ExcludeFromCodeCoverage]
public record SwitchAccountResponse(
    [property: JsonPropertyName("success")] string? Success)
{
    /// <summary>
    /// Additional undocumented fields from the API response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}
```

- [ ] **Step 5: Fix compilation errors from `SwitchAccountResponse` change**

The old DTO had `Set` and `SelectedAccount` properties. Search for usages and update them.
`AccountOperations.SwitchAccountAsync` returns this type — callers may reference `.Set` or `.SelectedAccount`. Update any references to use `.Success` instead.

Run: `dotnet build --configuration Release`
Expected: BUILD SUCCEEDED (fix any remaining compilation errors)

- [ ] **Step 6: Run accounts tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~AccountsTests"`
Expected: 4 PASSED

- [ ] **Step 7: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass

- [ ] **Step 8: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Accounts/AccountsTests.cs src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs
git commit -m "feat: add accounts integration tests + fix DTOs to match wire format

Update IserverAccountsResponse with all fields from recording.
Fix SwitchAccountResponse: wire format returns {success: string},
not {set: bool, selectedAccount: string}."
```

---

## Task 5: Pipeline Auth Failure Tests + TokenRefreshHandler Fix

**Files:**
- Create: `tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs`
- Modify: `src/IbkrConduit/Errors/IbkrApiException.cs`
- Modify: `src/IbkrConduit/Errors/IbkrSessionException.cs`
- Modify: `src/IbkrConduit/Session/TokenRefreshHandler.cs`

Three auth failure scenarios from the spec. All use the consumer pipeline via `Client.Accounts.GetAccountsAsync()` to trigger `TokenRefreshHandler` behavior. Each test creates its own `TestHarness` and makes a warm-up call to initialize the session before overriding stubs with higher-priority failure stubs.

**Scenario 3:** 401 → re-auth succeeds → retry gets 401 again → throw `IbkrSessionException`
**Scenario 4:** 401 → re-auth fails (LST acquisition throws) → throw `IbkrSessionException` wrapping inner
**Scenario 5:** 401 → re-auth fails (ssodh/init returns error) → throw `IbkrSessionException` wrapping inner

### Sub-task 5a: Write failing auth failure tests

- [ ] **Step 1: Create `AuthFailureTests.cs`**

Create `tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

public class AuthFailureTests : IDisposable
{
    private TestHarness? _harness;

    private async Task<TestHarness> CreateInitializedHarnessAsync()
    {
        var harness = await TestHarness.CreateAsync();

        // Stub accounts endpoint for warm-up call
        harness.StubAuthenticatedGet(
            "/v1/api/iserver/accounts",
            FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts"));

        // Make warm-up call to initialize session (LST + ssodh/init)
        await harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        return harness;
    }

    [Fact]
    public async Task Request_401AfterReauth_ThrowsSessionException()
    {
        // Scenario 3: 401 → re-auth succeeds → retry gets 401 again
        // Credentials invalidated (e.g., new PEM uploaded to portal while client running)
        _harness = await CreateInitializedHarnessAsync();

        // Override accounts endpoint to always return 401 (higher priority than warm-up stub)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Re-authentication succeeded but request still unauthorized");
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task Request_ReauthLstFails_ThrowsSessionException()
    {
        // Scenario 4: 401 → re-auth fails (LST acquisition throws)
        // DH exchange fails (e.g., access token regenerated in portal)
        _harness = await CreateInitializedHarnessAsync();

        // Override LST endpoint to fail (higher priority than MockLstServer callback)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithBody("DH exchange validation failed"));

        // Override accounts endpoint to return 401
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task Request_ReauthSsodhInitFails_ThrowsSessionException()
    {
        // Scenario 5: 401 → re-auth fails (ssodh/init returns error)
        // Session init rejected by server
        _harness = await CreateInitializedHarnessAsync();

        // Override ssodh/init to fail (higher priority than TestHarness stub)
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Session rejected"}"""));

        // Override accounts endpoint to return 401
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrSessionException>(
            _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldNotBeNull();
    }

    public void Dispose()
    {
        _harness?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~AuthFailureTests"`
Expected: FAIL
- Scenario 3: No `IbkrSessionException` thrown — handler returns raw 401 response, Refit throws `ApiException` instead
- Scenario 4: Unhandled exception from `ReauthenticateAsync` — not wrapped in `IbkrSessionException`
- Scenario 5: Same as scenario 4

### Sub-task 5b: Add inner exception constructors

- [ ] **Step 3: Add inner exception constructor to `IbkrApiException`**

In `src/IbkrConduit/Errors/IbkrApiException.cs`, add a second constructor:

```csharp
/// <summary>
/// Creates a new <see cref="IbkrApiException"/> with an inner exception.
/// </summary>
public IbkrApiException(
    HttpStatusCode statusCode, string? errorMessage, Exception innerException)
    : base(errorMessage ?? $"IBKR API returned {(int)statusCode}", innerException)
{
    StatusCode = statusCode;
    ErrorMessage = errorMessage;
    RawResponseBody = null;
    RequestUri = null;
}
```

- [ ] **Step 4: Add inner exception constructor to `IbkrSessionException`**

In `src/IbkrConduit/Errors/IbkrSessionException.cs`, add a second constructor:

```csharp
/// <summary>
/// Creates a new <see cref="IbkrSessionException"/> wrapping a re-authentication failure.
/// </summary>
public IbkrSessionException(string message, Exception innerException)
    : base(HttpStatusCode.Unauthorized, message, innerException)
{
    IsCompeting = false;
    Reason = message;
}
```

- [ ] **Step 5: Verify build succeeds**

Run: `dotnet build --configuration Release`
Expected: BUILD SUCCEEDED

### Sub-task 5c: Fix TokenRefreshHandler

- [ ] **Step 6: Update `TokenRefreshHandler.SendAsync` with retry limit and error handling**

In `src/IbkrConduit/Session/TokenRefreshHandler.cs`, add a using directive at the top:

```csharp
using IbkrConduit.Errors;
```

Replace the `SendAsync` method body (after the content buffering and first `base.SendAsync` call) with:

```csharp
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
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Http.TokenRefreshRetry");
    activity?.SetTag("original_status_code", (int)response.StatusCode);

    try
    {
        await _sessionManager.ReauthenticateAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        response.Dispose();
        throw new IbkrSessionException(
            "Re-authentication failed — credentials may be invalidated", ex);
    }

    // Clone the request for retry
    using var retryRequest = CloneRequest(request, bufferedContent, contentType);

    // Dispose the original 401 response
    response.Dispose();

    var retryResponse = await base.SendAsync(retryRequest, cancellationToken);

    // If retry also returns 401, credentials are fundamentally invalid — do not loop
    if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
    {
        retryResponse.Dispose();
        throw new IbkrSessionException(
            "Re-authentication succeeded but request still unauthorized — credentials may be invalidated",
            HttpStatusCode.Unauthorized, null, null, request.RequestUri?.AbsolutePath);
    }

    return retryResponse;
}
```

- [ ] **Step 7: Verify build succeeds**

Run: `dotnet build --configuration Release`
Expected: BUILD SUCCEEDED

- [ ] **Step 8: Run auth failure tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~AuthFailureTests"`
Expected: 3 PASSED

- [ ] **Step 9: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass (existing 401 recovery tests still work — they use scenarios where retry succeeds)

- [ ] **Step 10: Commit**

```bash
git add tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs src/IbkrConduit/Errors/IbkrApiException.cs src/IbkrConduit/Errors/IbkrSessionException.cs src/IbkrConduit/Session/TokenRefreshHandler.cs
git commit -m "fix: add retry limit and error handling to TokenRefreshHandler

- One retry max after re-auth; double-401 throws IbkrSessionException
- Re-auth exceptions caught and wrapped in IbkrSessionException
- Add inner exception constructors to IbkrApiException/IbkrSessionException
- Integration tests for all three auth failure scenarios"
```

---

## Verification Checklist

After all tasks complete:

- [ ] `dotnet build --configuration Release` — zero warnings
- [ ] `dotnet test --configuration Release` — all tests pass
- [ ] `dotnet format --verify-no-changes` — formatting clean
- [ ] New tests: ~12 total (5 session + 4 accounts + 3 auth failure)
- [ ] All response DTOs have `[JsonExtensionData]`
- [ ] `SwitchAccountResponse` matches wire format (`success` string, not `set`/`selectedAccount`)
- [ ] `TokenRefreshHandler` has one-retry-max behavior with proper exception wrapping
- [ ] No secrets or real account IDs in fixture files
