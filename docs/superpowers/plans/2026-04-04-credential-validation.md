# Credential Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `IbkrConfigurationException` with friendly messages, wrap raw crypto/HTTP exceptions in `SessionManager`, and expose `ValidateConnectionAsync` on `IIbkrClient` for fail-fast startup validation.

**Architecture:** A new `IbkrConfigurationException` (extends `Exception`, not `IbkrApiException`) carries a `CredentialHint` property. `SessionManager` catches known exception types from LST acquisition and session init, wraps them in `IbkrConfigurationException` with actionable messages. `IbkrClient.ValidateConnectionAsync` delegates to `SessionManager.EnsureInitializedAsync`.

**Tech Stack:** C# / .NET 9, xUnit v3, Shouldly, WireMock.Net

---

## File Map

### New Files
| Path | Purpose |
|------|---------|
| `src/IbkrConduit/Errors/IbkrConfigurationException.cs` | New exception type with `CredentialHint` |
| `tests/IbkrConduit.Tests.Unit/Errors/IbkrConfigurationExceptionTests.cs` | Unit tests for the exception |
| `tests/IbkrConduit.Tests.Integration/Session/ValidateConnectionTests.cs` | Integration tests for ValidateConnectionAsync |

### Modified Files
| Path | Change |
|------|--------|
| `src/IbkrConduit/Session/SessionManager.cs` | Add `WrapCredentialException` helper, wrap calls in `EnsureInitializedAsync` and `ReauthenticateAsync` |
| `src/IbkrConduit/Client/IIbkrClient.cs` | Add `ValidateConnectionAsync` to interface |
| `src/IbkrConduit/Client/IbkrClient.cs` | Implement `ValidateConnectionAsync` |
| `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs` | Add exception wrapping tests, extend `FakeSessionTokenProvider` to support throwing |
| `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs` | Add `ValidateConnectionAsync` delegation test, extend `FakeSessionManager` |

---

### Task 1: Create IbkrConfigurationException

**Files:**
- Create: `src/IbkrConduit/Errors/IbkrConfigurationException.cs`
- Create: `tests/IbkrConduit.Tests.Unit/Errors/IbkrConfigurationExceptionTests.cs`

- [ ] **Step 1: Write the failing test for the three-arg constructor**

Create `tests/IbkrConduit.Tests.Unit/Errors/IbkrConfigurationExceptionTests.cs`:

```csharp
using System;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrConfigurationExceptionTests
{
    [Fact]
    public void Constructor_WithInnerException_CarriesAllProperties()
    {
        var inner = new InvalidOperationException("original");

        var ex = new IbkrConfigurationException(
            "Friendly message", "EncryptionPrivateKey", inner);

        ex.Message.ShouldBe("Friendly message");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithoutInnerException_CarriesMessageAndHint()
    {
        var ex = new IbkrConfigurationException(
            "Check config", "ConsumerKey, AccessToken");

        ex.Message.ShouldBe("Check config");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NullHint_Allowed()
    {
        var ex = new IbkrConfigurationException("msg", null, new Exception("x"));

        ex.CredentialHint.ShouldBeNull();
    }

    [Fact]
    public void IsNotIbkrApiException()
    {
        var ex = new IbkrConfigurationException("msg", "hint");

        ex.ShouldNotBeAssignableTo<IbkrApiException>();
        ex.ShouldBeAssignableTo<Exception>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~IbkrConfigurationExceptionTests" --configuration Release`
Expected: Build failure — `IbkrConfigurationException` does not exist yet.

- [ ] **Step 3: Write the implementation**

Create `src/IbkrConduit/Errors/IbkrConfigurationException.cs`:

```csharp
using System;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when session initialization fails due to misconfigured credentials or options.
/// Not an API error — this indicates a configuration problem on the consumer side.
/// </summary>
public class IbkrConfigurationException : Exception
{
    /// <summary>Suggests which credential or option field to check.</summary>
    public string? CredentialHint { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrConfigurationException"/> wrapping an inner exception.
    /// </summary>
    /// <param name="message">A friendly, actionable error message.</param>
    /// <param name="credentialHint">The credential or option field name(s) to check.</param>
    /// <param name="innerException">The original exception from the failed operation.</param>
    public IbkrConfigurationException(string message, string? credentialHint, Exception innerException)
        : base(message, innerException)
    {
        CredentialHint = credentialHint;
    }

    /// <summary>
    /// Creates a new <see cref="IbkrConfigurationException"/> without an inner exception.
    /// </summary>
    /// <param name="message">A friendly, actionable error message.</param>
    /// <param name="credentialHint">The credential or option field name(s) to check.</param>
    public IbkrConfigurationException(string message, string? credentialHint)
        : base(message)
    {
        CredentialHint = credentialHint;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~IbkrConfigurationExceptionTests" --configuration Release`
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Errors/IbkrConfigurationException.cs tests/IbkrConduit.Tests.Unit/Errors/IbkrConfigurationExceptionTests.cs
git commit -m "feat: add IbkrConfigurationException with CredentialHint property"
```

---

### Task 2: Add exception wrapping to SessionManager

**Files:**
- Modify: `src/IbkrConduit/Session/SessionManager.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`

This task has several sub-steps because we need to: (a) extend the test fakes to support throwing, (b) write failing tests for each exception type, (c) implement the wrapping.

- [ ] **Step 1: Extend FakeSessionTokenProvider to support configurable exceptions**

In `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`, modify the existing `FakeSessionTokenProvider` class. Replace the existing class (lines 336-358) with:

```csharp
    internal class FakeSessionTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token = new(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }

        /// <summary>If set, GetLiveSessionTokenAsync throws this exception.</summary>
        public Exception? GetException { get; set; }

        /// <summary>If set, RefreshAsync throws this exception.</summary>
        public Exception? RefreshException { get; set; }

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (GetException != null)
            {
                throw GetException;
            }

            return Task.FromResult(_token);
        }

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            if (RefreshException != null)
            {
                throw RefreshException;
            }

            return Task.FromResult(new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.AddHours(24)));
        }
    }
```

Also extend `FakeSessionApi` to support throwing from `InitializeBrokerageSessionAsync`. Add this property and modify the method. Replace the `InitializeBrokerageSessionAsync` method in `FakeSessionApi` (the existing one at lines 416-421):

```csharp
        /// <summary>If set, InitializeBrokerageSessionAsync throws this exception.</summary>
        public Exception? InitException { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            LastInitRequest = request;
            if (InitException != null)
            {
                throw InitException;
            }

            return Task.FromResult(new SsodhInitResponse(Authenticated: true, Connected: true, Competing: false, Established: true, Message: null, Mac: null, ServerInfo: null, HardwareInfo: null));
        }
```

- [ ] **Step 2: Run existing tests to verify fakes are backward-compatible**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~SessionManagerTests" --configuration Release`
Expected: All existing tests still pass.

- [ ] **Step 3: Write failing tests for CryptographicException wrapping (decrypt)**

Add these tests to `SessionManagerTests` in `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`. Add `using IbkrConduit.Errors;` and `using System.Security.Cryptography;` to the top of the file.

```csharp
    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Decrypt_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("Unable to decrypt data");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("decrypt");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~CryptographicException_Decrypt" --configuration Release`
Expected: FAIL — `CryptographicException` propagates unwrapped (no `IbkrConfigurationException` is thrown yet).

- [ ] **Step 5: Write failing tests for remaining exception types**

Add all remaining exception-wrapping tests to `SessionManagerTests`:

```csharp
    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Sign_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("Unable to sign data");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("signature");
        ex.CredentialHint.ShouldBe("SignaturePrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_CryptographicException_Generic_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new CryptographicException("The parameter is incorrect");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Cryptographic operation failed");
        ex.CredentialHint.ShouldBe("SignaturePrivateKey, EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_401_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("ConsumerKey");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_403_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("rejected");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_HttpRequestException_NetworkError_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new HttpRequestException("Connection refused");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("network");
        ex.CredentialHint.ShouldBe("BaseUrl");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_FormatException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new FormatException("Input string was not in a correct format");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Diffie-Hellman");
        ex.CredentialHint.ShouldBe("DhPrime");
        ex.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_InvalidOperationException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new InvalidOperationException("DH exchange failed");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Diffie-Hellman");
        ex.CredentialHint.ShouldBe("DhPrime");
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_JsonException_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new System.Text.Json.JsonException("Unexpected token");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Unexpected response format");
        ex.CredentialHint.ShouldBe("BaseUrl");
        ex.InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_InitApiThrows_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();
        deps.SessionApi.InitException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ReauthenticateAsync_TokenProviderThrows_WrapsInConfigurationException()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        // Initialize successfully first
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Now make refresh throw
        deps.TokenProvider.RefreshException = new CryptographicException("Unable to decrypt data");

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => manager.ReauthenticateAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("decrypt");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeOfType<CryptographicException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_OperationCanceledException_NotWrapped()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.GetException = new OperationCanceledException("Canceled");

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));
    }
```

- [ ] **Step 6: Run all new tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~SessionManagerTests" --configuration Release`
Expected: New tests FAIL, existing tests still PASS.

- [ ] **Step 7: Implement WrapCredentialException and add wrapping to SessionManager**

Modify `src/IbkrConduit/Session/SessionManager.cs`. Add the following usings at the top of the file (after the existing usings):

```csharp
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using IbkrConduit.Errors;
```

Add the `WrapCredentialException` private static helper method inside the `SessionManager` class, after the `CancelProactiveRefresh` method and before the `SessionState` enum:

```csharp
    private static IbkrConfigurationException WrapCredentialException(Exception ex) =>
        ex switch
        {
            CryptographicException ce when ce.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "Failed to decrypt access token secret — verify EncryptionPrivateKey matches the key registered in the IBKR portal",
                    "EncryptionPrivateKey", ce),

            CryptographicException ce when ce.Message.Contains("sign", StringComparison.OrdinalIgnoreCase) =>
                new IbkrConfigurationException(
                    "RSA signature failed — verify SignaturePrivateKey matches the key registered in the IBKR portal",
                    "SignaturePrivateKey", ce),

            CryptographicException ce =>
                new IbkrConfigurationException(
                    "Cryptographic operation failed during session initialization — verify SignaturePrivateKey and EncryptionPrivateKey",
                    "SignaturePrivateKey, EncryptionPrivateKey", ce),

            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } he =>
                new IbkrConfigurationException(
                    "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired",
                    "ConsumerKey, AccessToken", he),

            HttpRequestException { StatusCode: null } he =>
                new IbkrConfigurationException(
                    "Cannot reach IBKR API — check network connectivity and BaseUrl configuration",
                    "BaseUrl", he),

            HttpRequestException he =>
                new IbkrConfigurationException(
                    "LST acquisition rejected by IBKR — verify ConsumerKey and AccessToken are correct and not expired",
                    "ConsumerKey, AccessToken", he),

            FormatException fe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange produced invalid data — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", fe),

            InvalidOperationException ioe =>
                new IbkrConfigurationException(
                    "Diffie-Hellman key exchange failed — verify DhPrime is the correct RFC 3526 Group 14 prime",
                    "DhPrime", ioe),

            JsonException je =>
                new IbkrConfigurationException(
                    "Unexpected response format from IBKR LST endpoint — the API may be experiencing issues or the endpoint URL may be incorrect",
                    "BaseUrl", je),

            _ => new IbkrConfigurationException(
                $"Session initialization failed: {ex.Message}",
                null, ex),
        };
```

Now modify `EnsureInitializedAsync` to wrap the LST + ssodh/init calls. Replace lines 82-94 (from `_state = SessionState.Initializing;` through the suppress call) with:

```csharp
            _state = SessionState.Initializing;
            LogInitializing();

            try
            {
                _currentLst = await _sessionTokenProvider.GetLiveSessionTokenAsync(cancellationToken);

                await _sessionApi.InitializeBrokerageSessionAsync(
                    new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw WrapCredentialException(ex);
            }

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }
```

Similarly modify `ReauthenticateAsync`. Replace lines 136-145 (from `_currentLst = await _sessionTokenProvider.RefreshAsync` through the suppress call) with:

```csharp
            try
            {
                _currentLst = await _sessionTokenProvider.RefreshAsync(cancellationToken);

                await _sessionApi.InitializeBrokerageSessionAsync(
                    new SsodhInitRequest(Publish: true, Compete: _options.Compete), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw WrapCredentialException(ex);
            }

            if (_options.SuppressMessageIds.Count > 0)
            {
                await _sessionApi.SuppressQuestionsAsync(
                    new SuppressRequest(_options.SuppressMessageIds), cancellationToken);
            }
```

- [ ] **Step 8: Run all SessionManager tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~SessionManagerTests" --configuration Release`
Expected: All tests pass (existing + new).

- [ ] **Step 9: Run the full test suite to check nothing is broken**

Run: `dotnet test --configuration Release`
Expected: All tests pass. The `AuthFailureTests` in integration tests should still work because `TokenRefreshHandler` wraps whatever exception `ReauthenticateAsync` throws (now `IbkrConfigurationException` instead of raw exceptions), and it wraps it in `IbkrSessionException` either way.

- [ ] **Step 10: Commit**

```bash
git add src/IbkrConduit/Session/SessionManager.cs tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs
git commit -m "feat: wrap credential exceptions in SessionManager with friendly IbkrConfigurationException messages"
```

---

### Task 3: Add ValidateConnectionAsync to IIbkrClient

**Files:**
- Modify: `src/IbkrConduit/Client/IIbkrClient.cs`
- Modify: `src/IbkrConduit/Client/IbkrClient.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs`
- Create: `tests/IbkrConduit.Tests.Integration/Session/ValidateConnectionTests.cs`

- [ ] **Step 1: Write the failing unit test for ValidateConnectionAsync delegation**

Add this test to `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs`:

```csharp
    [Fact]
    public async Task ValidateConnectionAsync_DelegatesToSessionManager()
    {
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(sessionManager: sessionManager);

        await client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        sessionManager.EnsureInitializedCallCount.ShouldBe(1);
    }
```

Also update the `FakeSessionManager` class inside the same file to track calls:

```csharp
    private class FakeSessionManager : ISessionManager
    {
        public bool Disposed { get; private set; }
        public int EnsureInitializedCallCount { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            EnsureInitializedCallCount++;
            return Task.CompletedTask;
        }

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~ValidateConnectionAsync_DelegatesToSessionManager" --configuration Release`
Expected: Build failure — `ValidateConnectionAsync` does not exist on `IIbkrClient`.

- [ ] **Step 3: Add ValidateConnectionAsync to the interface**

Modify `src/IbkrConduit/Client/IIbkrClient.cs`. Add the following method declaration before the closing brace of the interface, after the `Allocations` property:

```csharp
    /// <summary>
    /// Validates that the configured credentials can establish a session with the IBKR API.
    /// Performs LST acquisition, session initialization, and auth status verification.
    /// Call at startup for fail-fast credential validation.
    /// Throws <see cref="IbkrConduit.Errors.IbkrConfigurationException"/> with a descriptive message if validation fails.
    /// </summary>
    Task ValidateConnectionAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Add implementation to IbkrClient**

Modify `src/IbkrConduit/Client/IbkrClient.cs`. Add the following method after the `Allocations` property and before the `DisposeAsync` method:

```csharp
    /// <inheritdoc />
    public async Task ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        await _sessionManager.EnsureInitializedAsync(cancellationToken);
    }
```

- [ ] **Step 5: Run the unit test to verify it passes**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --filter "FullyQualifiedName~ValidateConnectionAsync_DelegatesToSessionManager" --configuration Release`
Expected: PASS.

- [ ] **Step 6: Write the failing integration test for success case**

Create `tests/IbkrConduit.Tests.Integration/Session/ValidateConnectionTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Session;

public class ValidateConnectionTests : IAsyncDisposable
{
    private TestHarness? _harness;

    [Fact]
    public async Task ValidateConnectionAsync_ValidCredentials_Succeeds()
    {
        _harness = await TestHarness.CreateAsync();

        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task ValidateConnectionAsync_LstEndpointReturns401_ThrowsConfigurationException()
    {
        _harness = await TestHarness.CreateAsync();

        // Override the LST endpoint to return 401 at highest priority
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/oauth/live_session_token")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateConnectionAsync_SsodhInitReturns401_ThrowsConfigurationException()
    {
        _harness = await TestHarness.CreateAsync();

        // Override ssodh/init to return 401 at highest priority
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/auth/ssodh/init")
                .UsingPost())
            .AtPriority(-1)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        var ex = await Should.ThrowAsync<IbkrConfigurationException>(
            () => _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken));

        ex.CredentialHint.ShouldNotBeNull();
        ex.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateConnectionAsync_AlreadyInitialized_ReturnsImmediately()
    {
        _harness = await TestHarness.CreateAsync();

        // First call initializes
        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        // Second call should return immediately (idempotent) — no extra LST calls
        await _harness.Client.ValidateConnectionAsync(TestContext.Current.CancellationToken);

        // Only one LST handshake
        _harness.Server.FindLogEntries(
            Request.Create().WithPath("/v1/api/oauth/live_session_token").UsingPost())
            .Count.ShouldBe(1);
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness != null)
        {
            await _harness.DisposeAsync();
        }
    }
}
```

- [ ] **Step 7: Run the integration tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --filter "FullyQualifiedName~ValidateConnectionTests" --configuration Release`
Expected: All 4 tests pass.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 9: Run the linter**

Run: `dotnet format --verify-no-changes`
Expected: No formatting issues.

- [ ] **Step 10: Commit**

```bash
git add src/IbkrConduit/Client/IIbkrClient.cs src/IbkrConduit/Client/IbkrClient.cs tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs tests/IbkrConduit.Tests.Integration/Session/ValidateConnectionTests.cs
git commit -m "feat: add ValidateConnectionAsync to IIbkrClient for fail-fast startup validation"
```
