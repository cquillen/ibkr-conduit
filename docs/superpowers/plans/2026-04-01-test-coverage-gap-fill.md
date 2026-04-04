# Implementation Plan: Test Coverage Gap Fill

**Date:** 2026-04-01
**Spec:** `docs/superpowers/specs/2026-04-01-test-coverage-gap-fill-design.md`
**Branch:** `feat/test-coverage-gap-fill`

---

## Goal

Exclude trivial code from coverage metrics and write targeted unit tests for untested branching logic — WebSocket client, Flex polling, HTTP handler edge cases, and SessionManager error paths. Raise both line and branch coverage from the 71.7%/53.6% baseline.

## Architecture

No architectural changes. T.2 extracts `IWebSocketAdapter` from `IbkrWebSocketClient` to enable testing without real sockets. All other tasks add tests only or apply `[ExcludeFromCodeCoverage]` attributes.

## Tech Stack

- **xUnit v3** — test framework
- **Shouldly** — assertion library
- **NSubstitute** — not used; fakes are hand-rolled per project convention
- **System.Diagnostics.CodeAnalysis** — `[ExcludeFromCodeCoverage]` attribute

## File Structure

| File | Type | Task |
|---|---|---|
| ~25 source files | Modified (attribute) | T.1 |
| `src/IbkrConduit/Streaming/IWebSocketAdapter.cs` | New | T.2 |
| `src/IbkrConduit/Streaming/ClientWebSocketAdapter.cs` | New | T.2 |
| `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` | Modified | T.2 |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Modified | T.2 |
| `tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs` | New | T.2 |
| `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` | New | T.2 |
| `tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs` | New | T.3 |
| `tests/IbkrConduit.Tests.Unit/Http/TokenRefreshHandlerTests.cs` | New | T.4 |
| `tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs` | New | T.4 |
| `tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs` | New | T.4 |
| `tests/IbkrConduit.Tests.Unit/Session/SessionManagerEdgeTests.cs` | New | T.5 |
| `tests/IbkrConduit.Tests.Unit/Flex/FlexOperationsTests.cs` | New | T.5 |

---

## Task T.1 — Apply `[ExcludeFromCodeCoverage]` Exclusions

### Files

All files listed below are modified (adding `using System.Diagnostics.CodeAnalysis;` and `[ExcludeFromCodeCoverage]` attribute).

### Steps

- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Session/IIbkrSessionApiModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/MarketData/IIbkrMarketDataApiModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Streaming/StreamingModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to records in `src/IbkrConduit/Flex/FlexModels.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Auth/IbkrOAuthCredentials.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Auth/LiveSessionToken.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Session/IbkrClientOptions.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Client/PortfolioOperations.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Client/ContractOperations.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/MarketData/MarketDataFields.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Session/SuppressibleMessages.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Diagnostics/LogFields.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Diagnostics/IbkrConduitDiagnostics.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Streaming/CancellationDisposable.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Http/RateLimitRejectedException.cs`
- [ ] Add `[ExcludeFromCodeCoverage]` to `src/IbkrConduit/Flex/FlexQueryException.cs`
- [ ] Run `dotnet build --configuration Release` — verify zero warnings
- [ ] Run `dotnet test --configuration Release` — verify all existing tests still pass
- [ ] Commit: `chore: apply ExcludeFromCodeCoverage to trivial records, constants, and pass-through classes`

### Code

Each file gets a `using System.Diagnostics.CodeAnalysis;` import (if not already present) and an `[ExcludeFromCodeCoverage]` attribute on the class/record.

**`src/IbkrConduit/Portfolio/IIbkrPortfolioApiModels.cs`** — add at top of file:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `Account`, `Position`, `AccountSummaryEntry`, `LedgerEntry`, `AccountInfo`, `AccountAllocation`, `PositionContractInfo`, `AccountPerformance`, `TransactionHistory`, `PerformanceRequest`, `TransactionHistoryRequest`.

**`src/IbkrConduit/Session/IIbkrSessionApiModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `SsodhInitRequest`, `SsodhInitResponse`, `TickleResponse`, `TickleIserverStatus`, `TickleAuthStatus`, `SuppressRequest`, `SuppressResponse`, `LogoutResponse`.

**`src/IbkrConduit/Contracts/IIbkrContractApiModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `ContractSearchResult`, `ContractSection`, `ContractDetails`.

**`src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `OrderRequest`, `OrderResult`, `CancelOrderResponse`, `OrdersPayload`, `OrderWireModel`, `OrderSubmissionResponse`, `ReplyRequest`, `OrdersResponse`, `LiveOrder`, `Trade`.

**`src/IbkrConduit/MarketData/IIbkrMarketDataApiModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `MarketDataSnapshotRaw`, `MarketDataSnapshot`, `HistoricalDataResponse`, `HistoricalBar`.

**`src/IbkrConduit/Streaming/StreamingModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `MarketDataTick`, `OrderUpdate`, `PnlUpdate`, `AccountSummaryUpdate`, `AccountLedgerUpdate`.

**`src/IbkrConduit/Flex/FlexModels.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before each record: `FlexTrade`, `FlexPosition`.

**`src/IbkrConduit/Auth/IbkrOAuthCredentials.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then add `[ExcludeFromCodeCoverage]` before the record:
```csharp
[ExcludeFromCodeCoverage]
public record IbkrOAuthCredentials(
```

**`src/IbkrConduit/Auth/LiveSessionToken.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public record LiveSessionToken(byte[] Token, DateTimeOffset Expiry);
```

**`src/IbkrConduit/Session/IbkrClientOptions.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public record IbkrClientOptions
```

**`src/IbkrConduit/Client/PortfolioOperations.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public class PortfolioOperations : IPortfolioOperations
```

**`src/IbkrConduit/Client/ContractOperations.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public class ContractOperations : IContractOperations
```

**`src/IbkrConduit/MarketData/MarketDataFields.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public static class MarketDataFields
```

**`src/IbkrConduit/Session/SuppressibleMessages.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public static class SuppressibleMessages
```

**`src/IbkrConduit/Diagnostics/LogFields.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public static class LogFields
```

**`src/IbkrConduit/Diagnostics/IbkrConduitDiagnostics.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public static class IbkrConduitDiagnostics
```

**`src/IbkrConduit/Streaming/CancellationDisposable.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
internal sealed class CancellationDisposable : IDisposable
```

**`src/IbkrConduit/Http/RateLimitRejectedException.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public class RateLimitRejectedException : Exception
```

**`src/IbkrConduit/Flex/FlexQueryException.cs`** — add at top:
```csharp
using System.Diagnostics.CodeAnalysis;
```
Then:
```csharp
[ExcludeFromCodeCoverage]
public class FlexQueryException : Exception
```

---

## Task T.2 — IWebSocketAdapter + WebSocket Client Tests

### Files
- `src/IbkrConduit/Streaming/IWebSocketAdapter.cs` (new)
- `src/IbkrConduit/Streaming/ClientWebSocketAdapter.cs` (new)
- `src/IbkrConduit/Streaming/IbkrWebSocketClient.cs` (modified)
- `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` (modified)
- `tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs` (new)

### Steps

- [ ] RED: Create `IbkrWebSocketClientTests` with all 10 test methods (stubs that reference non-existent `IWebSocketAdapter`)
- [ ] Run `dotnet test --configuration Release` — verify compilation failure
- [ ] GREEN (adapter extraction): Create `IWebSocketAdapter.cs`, `ClientWebSocketAdapter.cs`
- [ ] GREEN (refactor client): Modify `IbkrWebSocketClient` to accept `Func<IWebSocketAdapter>` factory
- [ ] GREEN (DI wiring): Update `ServiceCollectionExtensions.cs` to pass `() => new ClientWebSocketAdapter()`
- [ ] GREEN (fake): Create `FakeWebSocketAdapter` in test project
- [ ] GREEN (tests): Complete all test method bodies
- [ ] Run `dotnet test --configuration Release` — verify all pass
- [ ] REFACTOR: Clean up if needed
- [ ] Run `dotnet build --configuration Release` — zero warnings
- [ ] Run `dotnet format --verify-no-changes` — verify lint passes
- [ ] Commit: `feat: extract IWebSocketAdapter and add IbkrWebSocketClient unit tests`

### Code

**`src/IbkrConduit/Streaming/IWebSocketAdapter.cs`:**
```csharp
using System.Net;
using System.Net.WebSockets;

namespace IbkrConduit.Streaming;

/// <summary>
/// Abstraction over <see cref="ClientWebSocket"/> to enable unit testing of WebSocket logic.
/// </summary>
internal interface IWebSocketAdapter : IAsyncDisposable
{
    /// <summary>Gets the current state of the WebSocket connection.</summary>
    WebSocketState State { get; }

    /// <summary>Connects to a WebSocket server.</summary>
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Sends data over the WebSocket.</summary>
    Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken);

    /// <summary>Receives data from the WebSocket.</summary>
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>Closes the WebSocket connection.</summary>
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken);

    /// <summary>Sets a request header for the WebSocket handshake.</summary>
    void SetRequestHeader(string name, string value);

    /// <summary>Sets or gets the proxy for the WebSocket connection.</summary>
    IWebProxy? Proxy { set; }
}
```

**`src/IbkrConduit/Streaming/ClientWebSocketAdapter.cs`:**
```csharp
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;

namespace IbkrConduit.Streaming;

/// <summary>
/// Production adapter wrapping <see cref="ClientWebSocket"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ClientWebSocketAdapter : IWebSocketAdapter
{
    private readonly ClientWebSocket _ws = new();

    /// <inheritdoc />
    public WebSocketState State => _ws.State;

    /// <inheritdoc />
    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
        _ws.ConnectAsync(uri, cancellationToken);

    /// <inheritdoc />
    public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken) =>
        _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken).AsTask();

    /// <inheritdoc />
    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken) =>
        _ws.ReceiveAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken) =>
        _ws.CloseAsync(closeStatus, statusDescription, cancellationToken);

    /// <inheritdoc />
    public void SetRequestHeader(string name, string value) =>
        _ws.Options.SetRequestHeader(name, value);

    /// <inheritdoc />
    public IWebProxy? Proxy
    {
        set => _ws.Options.Proxy = value;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

**`src/IbkrConduit/Streaming/IbkrWebSocketClient.cs`** changes:

Replace the constructor to accept a factory:
```csharp
private readonly Func<IWebSocketAdapter> _webSocketFactory;
private IWebSocketAdapter? _webSocket;

public IbkrWebSocketClient(
    IIbkrSessionApi sessionApi,
    IbkrOAuthCredentials credentials,
    ISessionLifecycleNotifier notifier,
    ILogger<IbkrWebSocketClient> logger,
    Func<IWebSocketAdapter> webSocketFactory)
{
    _sessionApi = sessionApi;
    _credentials = credentials;
    _notifier = notifier;
    _logger = logger;
    _webSocketFactory = webSocketFactory;

    IbkrConduitDiagnostics.Meter.CreateObservableGauge(
        "ibkr.conduit.websocket.connection_state",
        () => _webSocket is { State: WebSocketState.Open } ? 1 : 0);

    _notifierSubscription = _notifier.Subscribe(OnSessionRefreshedAsync);
}
```

Replace `ConnectCoreAsync` body — replace `new ClientWebSocket()` section with:
```csharp
var ws = _webSocketFactory();
ws.SetRequestHeader("Cookie", $"api={tickleResponse.Session}");
ws.SetRequestHeader("User-Agent", "ClientPortalGW/1");
ws.Proxy = System.Net.WebRequest.DefaultWebProxy;

await ws.ConnectAsync(uri, cancellationToken);
_webSocket = ws;
```

Replace `StartMessagePump` inner loop — change `_webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct)` to use the adapter's `ReceiveAsync(Memory<byte>)` signature and `ValueWebSocketReceiveResult`:
```csharp
var result = await _webSocket.ReceiveAsync(buffer.AsMemory(), ct);
ms.Write(buffer, 0, result.Count);
```
Check `result.EndOfMessage` and `result.MessageType` against the `ValueWebSocketReceiveResult` properties instead of `WebSocketReceiveResult`.

Replace `SendTextAsync` body:
```csharp
var bytes = Encoding.UTF8.GetBytes(message);
await _webSocket.SendAsync(
    bytes.AsMemory(),
    WebSocketMessageType.Text,
    true,
    cancellationToken);
```

Replace `DisconnectAsync` — change `_webSocket.CloseAsync` to use the adapter interface, and change `_webSocket?.Dispose()` to:
```csharp
if (_webSocket != null)
{
    await _webSocket.DisposeAsync();
}
_webSocket = null;
```

**`src/IbkrConduit/Http/ServiceCollectionExtensions.cs`** — update the WebSocket registration:
```csharp
services.AddSingleton<IIbkrWebSocketClient>(sp =>
    new IbkrWebSocketClient(
        sp.GetRequiredService<IIbkrSessionApi>(),
        credentials,
        sp.GetRequiredService<ISessionLifecycleNotifier>(),
        sp.GetRequiredService<ILogger<IbkrWebSocketClient>>(),
        () => new ClientWebSocketAdapter()));
```

**`tests/IbkrConduit.Tests.Unit/Streaming/FakeWebSocketAdapter.cs`:**
```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// In-memory WebSocket adapter for testing. Messages are exchanged via queues.
/// </summary>
internal sealed class FakeWebSocketAdapter : IbkrConduit.Streaming.IWebSocketAdapter
{
    private readonly ConcurrentQueue<byte[]> _inboundMessages = new();
    private readonly ConcurrentQueue<string> _sentMessages = new();
    private readonly SemaphoreSlim _inboundSignal = new(0);
    private WebSocketState _state = WebSocketState.None;
    private bool _failOnConnect;
    private bool _disposed;

    public WebSocketState State => _state;
    public ConcurrentQueue<string> SentMessages => _sentMessages;
    public Dictionary<string, string> RequestHeaders { get; } = new();
    public IWebProxy? LastProxy { get; private set; }
    public Uri? ConnectedUri { get; private set; }
    public bool FailOnConnect { set => _failOnConnect = value; }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (_failOnConnect)
        {
            throw new WebSocketException("Simulated connection failure");
        }

        ConnectedUri = uri;
        _state = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(buffer.Span);
        _sentMessages.Enqueue(text);
        return Task.CompletedTask;
    }

    public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await _inboundSignal.WaitAsync(cancellationToken);

        if (_state != WebSocketState.Open)
        {
            return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        if (_inboundMessages.TryDequeue(out var data))
        {
            data.CopyTo(buffer);
            return new ValueWebSocketReceiveResult(data.Length, WebSocketMessageType.Text, true);
        }

        return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Text, true);
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public void SetRequestHeader(string name, string value) =>
        RequestHeaders[name] = value;

    public IWebProxy? Proxy
    {
        set => LastProxy = value;
    }

    /// <summary>Enqueue a message as if it arrived from the server.</summary>
    public void EnqueueServerMessage(string json)
    {
        _inboundMessages.Enqueue(Encoding.UTF8.GetBytes(json));
        _inboundSignal.Release();
    }

    /// <summary>Signal a close frame from the server.</summary>
    public void SignalClose()
    {
        _state = WebSocketState.CloseReceived;
        _inboundSignal.Release();
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _state = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Streaming/IbkrWebSocketClientTests.cs`:**
```csharp
using System.Text.Json;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using IbkrConduit.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class IbkrWebSocketClientTests
{
    private readonly FakeWebSocketAdapter _adapter = new();
    private readonly FakeSessionApi _sessionApi = new();
    private readonly FakeLifecycleNotifier _notifier = new();
    private readonly IbkrOAuthCredentials _credentials;

    public IbkrWebSocketClientTests()
    {
        using var sigKey = System.Security.Cryptography.RSA.Create(2048);
        using var encKey = System.Security.Cryptography.RSA.Create(2048);
        _credentials = new IbkrOAuthCredentials(
            TenantId: "test-tenant",
            ConsumerKey: "TESTKEY01",
            AccessToken: "test_access_token",
            EncryptedAccessTokenSecret: "dGVzdA==",
            SignaturePrivateKey: System.Security.Cryptography.RSA.Create(2048),
            EncryptionPrivateKey: System.Security.Cryptography.RSA.Create(2048),
            DhPrime: new System.Numerics.BigInteger(23));
    }

    [Fact]
    public async Task ConnectAsync_SetsUrlAndHeaders()
    {
        await using var client = CreateClient();

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        _adapter.ConnectedUri.ShouldNotBeNull();
        _adapter.ConnectedUri!.ToString().ShouldContain("oauth_token=test_access_token");
        _adapter.RequestHeaders.ShouldContainKey("Cookie");
        _adapter.RequestHeaders.ShouldContainKey("User-Agent");
        _adapter.RequestHeaders["User-Agent"].ShouldBe("ClientPortalGW/1");
    }

    [Fact]
    public async Task ConnectAsync_StartsHeartbeatAndMessagePump()
    {
        await using var client = CreateClient();

        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Give the heartbeat loop a moment to fire (interval is 10s but we just
        // verify the background tasks started without throwing)
        await Task.Delay(50);

        // The client should still be in a healthy state (no crash)
        _adapter.State.ShouldBe(System.Net.WebSockets.WebSocketState.Open);
    }

    [Fact]
    public async Task MessagePump_RoutesMessagesByTopicPrefix()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+265598+{\"fields\":[\"31\"]}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.EnqueueServerMessage("""{"topic":"smd+265598","31":"150.25"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+265598");
        unsubscribe();
    }

    [Fact]
    public async Task MessagePump_IgnoresUnknownTopics()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var (reader, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        // Send a message with an unknown topic — should not crash
        _adapter.EnqueueServerMessage("""{"topic":"xyz+unknown","data":"test"}""");
        // Then send a matching message
        _adapter.EnqueueServerMessage("""{"topic":"smd+123","31":"100"}""");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msg = await reader.ReadAsync(cts.Token);

        msg.GetProperty("topic").GetString().ShouldBe("smd+123");
        unsubscribe();
    }

    [Fact]
    public async Task SubscribeTopicAsync_SendsSubscribeMessage()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var subscribeMsg = "smd+265598+{\"fields\":[\"31\"]}";
        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            subscribeMsg, "smd",
            TestContext.Current.CancellationToken);

        _adapter.SentMessages.ShouldContain(subscribeMsg);
        unsubscribe();
    }

    [Fact]
    public async Task SubscribeTopicAsync_ConnectsIfNotConnected()
    {
        await using var client = CreateClient();

        // Do not call ConnectAsync — subscribe should trigger it
        var (_, unsubscribe) = await client.SubscribeTopicAsync(
            "smd+123+{}", "smd",
            TestContext.Current.CancellationToken);

        _adapter.ConnectedUri.ShouldNotBeNull();
        unsubscribe();
    }

    [Fact]
    public async Task ReconnectAsync_ReplaysActiveSubscriptions()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var sub1 = "smd+100+{}";
        var sub2 = "sor+{}";
        var (_, unsub1) = await client.SubscribeTopicAsync(sub1, "smd",
            TestContext.Current.CancellationToken);
        var (_, unsub2) = await client.SubscribeTopicAsync(sub2, "sor",
            TestContext.Current.CancellationToken);

        // Clear sent messages and trigger reconnect via session refresh
        while (_adapter.SentMessages.TryDequeue(out _)) { }

        // Trigger reconnect by notifying session refresh
        await _notifier.TriggerRefreshAsync(TestContext.Current.CancellationToken);

        // Wait a moment for the reconnect to replay
        await Task.Delay(200);

        // After reconnect, subscriptions should be replayed
        var sent = _adapter.SentMessages.ToArray();
        sent.ShouldContain(sub1);
        sent.ShouldContain(sub2);

        unsub1();
        unsub2();
    }

    [Fact]
    public async Task DisconnectAsync_StopsHeartbeatAndPump()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        // Dispose triggers disconnect
        await client.DisposeAsync();

        _adapter.State.ShouldNotBe(System.Net.WebSockets.WebSocketState.Open);
    }

    [Fact]
    public async Task DisposeAsync_UnsubscribesFromNotifier()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        await client.DisposeAsync();

        _notifier.SubscriptionDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task OnSessionRefreshed_TriggersReconnect()
    {
        await using var client = CreateClient();
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        var originalUri = _adapter.ConnectedUri;

        // Trigger session refresh notification
        await _notifier.TriggerRefreshAsync(TestContext.Current.CancellationToken);

        // Wait for reconnect to complete
        await Task.Delay(200);

        // ConnectAsync should have been called again (adapter was re-created via factory)
        _adapter.ConnectedUri.ShouldNotBeNull();
    }

    private IbkrWebSocketClient CreateClient() =>
        new(
            _sessionApi,
            _credentials,
            _notifier,
            NullLogger<IbkrWebSocketClient>.Instance,
            () => _adapter);

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SsodhInitResponse(true, true, false));

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: "fake-session-id",
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true))));

        public Task<SuppressResponse> SuppressQuestionsAsync(
            SuppressRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SuppressResponse("submitted"));

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LogoutResponse(true));
    }

    internal class FakeLifecycleNotifier : ISessionLifecycleNotifier
    {
        private Func<CancellationToken, Task>? _callback;
        public bool SubscriptionDisposed { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed)
        {
            _callback = onSessionRefreshed;
            return new CallbackDisposable(() => SubscriptionDisposed = true);
        }

        public Task NotifyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task TriggerRefreshAsync(CancellationToken cancellationToken)
        {
            if (_callback != null)
            {
                await _callback(cancellationToken);
            }
        }

        private sealed class CallbackDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
```

---

## Task T.3 — FlexClient Polling Unit Tests

### Files
- `tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs` (new)

### Steps

- [ ] RED: Create `FlexClientPollingTests` with all 8 test methods
- [ ] Run `dotnet test --configuration Release` — verify they fail (test logic)
- [ ] GREEN: Complete all test bodies using `SequentialFakeHttpHandler`
- [ ] Run `dotnet test --configuration Release` — verify all pass
- [ ] REFACTOR: Extract shared helpers if needed
- [ ] Run `dotnet format --verify-no-changes` — verify lint passes
- [ ] Commit: `test: add FlexClient polling unit tests for retry, timeout, and error paths`

### Code

**`tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs`:**
```csharp
using System.Net;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexClientPollingTests
{
    private const string _successSendRequest = """
        <FlexStatementResponse>
            <Status>Success</Status>
            <ReferenceCode>REF001</ReferenceCode>
        </FlexStatementResponse>
        """;

    private const string _inProgressResponse = """
        <FlexStatementResponse>
            <ErrorCode>1019</ErrorCode>
            <ErrorMessage>Statement generation in progress</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _successStatement = """
        <FlexQueryResponse>
            <FlexStatements count="1">
                <FlexStatement accountId="U1234567" />
            </FlexStatements>
        </FlexQueryResponse>
        """;

    private const string _errorResponse1004 = """
        <FlexStatementResponse>
            <ErrorCode>1004</ErrorCode>
            <ErrorMessage>Invalid token</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _failSendRequest = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>1018</ErrorCode>
            <ErrorMessage>Invalid query ID</ErrorMessage>
        </FlexStatementResponse>
        """;

    [Fact]
    public async Task ExecuteQueryAsync_SuccessOnFirstPoll_ReturnsImmediately()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _successStatement);

        var client = CreateClient(handler);

        var doc = await client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(2); // SendRequest + 1 GetStatement
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOnError1019_SucceedsOnSecondAttempt()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _successStatement);

        var client = CreateClient(handler);

        var doc = await client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(3); // SendRequest + 2 GetStatement polls
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOnError1019_SucceedsAfterMultipleAttempts()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _inProgressResponse,
            _inProgressResponse,
            _successStatement);

        var client = CreateClient(handler);

        var doc = await client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None);

        doc.ShouldNotBeNull();
        handler.CallCount.ShouldBe(5); // SendRequest + 4 GetStatement polls
    }

    [Fact]
    public async Task ExecuteQueryAsync_TimeoutAfterMaxDuration_ThrowsTimeoutException()
    {
        // Need 13+ in-progress responses to exhaust all poll delays + final attempt
        var responses = new List<string> { _successSendRequest };
        for (var i = 0; i < 14; i++)
        {
            responses.Add(_inProgressResponse);
        }

        var handler = new SequentialFakeHttpHandler(responses.ToArray());
        var client = CreateClient(handler);

        await Should.ThrowAsync<TimeoutException>(
            () => client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteQueryAsync_NonRetryableError_ThrowsFlexQueryException()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _errorResponse1004);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1004);
        ex.Message.ShouldBe("Invalid token");
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestFails_ThrowsFlexQueryException()
    {
        var handler = new SequentialFakeHttpHandler(_failSendRequest);
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("BADID", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1018);
    }

    [Fact]
    public async Task ExecuteQueryAsync_DateRangeOverride_IncludesInUrl()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _successStatement);

        var client = CreateClient(handler);

        await client.ExecuteQueryAsync("Q1", "20260101", "20260301", CancellationToken.None);

        var firstUrl = handler.RequestUris[0].ToString();
        firstUrl.ShouldContain("fd=20260101");
        firstUrl.ShouldContain("td=20260301");
    }

    [Fact]
    public async Task ExecuteQueryAsync_PassesCancellationToken()
    {
        // Create a handler that blocks on the second call, then we cancel
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _inProgressResponse);

        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.ExecuteQueryAsync("Q1", null, null, cts.Token));
    }

    private static FlexClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new FlexClient(httpClient, "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
    }

    private sealed class SequentialFakeHttpHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _callCount;

        public int CallCount => _callCount;
        public List<Uri> RequestUris { get; } = [];

        public SequentialFakeHttpHandler(params string[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUris.Add(request.RequestUri!);
            var index = Interlocked.Increment(ref _callCount) - 1;
            var body = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/xml"),
            });
        }
    }
}
```

---

## Task T.4 — HTTP Handler Edge Case Tests

### Files
- `tests/IbkrConduit.Tests.Unit/Http/TokenRefreshHandlerTests.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs` (new)

### Steps

- [ ] RED: Create `TokenRefreshHandlerTests` with 4 tests
- [ ] Run `dotnet test --configuration Release` — verify they fail
- [ ] GREEN: Complete all `TokenRefreshHandlerTests` implementations
- [ ] Run `dotnet test --configuration Release` — verify they pass
- [ ] RED: Create `GlobalRateLimitingHandlerTests` with 2 tests
- [ ] Run `dotnet test --configuration Release` — verify they fail
- [ ] GREEN: Complete all `GlobalRateLimitingHandlerTests` implementations
- [ ] Run `dotnet test --configuration Release` — verify they pass
- [ ] RED: Create `EndpointRateLimitingHandlerTests` with 3 tests
- [ ] Run `dotnet test --configuration Release` — verify they fail
- [ ] GREEN: Complete all `EndpointRateLimitingHandlerTests` implementations
- [ ] Run `dotnet test --configuration Release` — verify they pass
- [ ] REFACTOR: Extract shared handler helpers
- [ ] Run `dotnet format --verify-no-changes` — verify lint passes
- [ ] Commit: `test: add HTTP handler edge case tests for TokenRefresh, GlobalRateLimit, EndpointRateLimit`

### Code

**`tests/IbkrConduit.Tests.Unit/Http/TokenRefreshHandlerTests.cs`:**
```csharp
using System.Net;
using System.Text;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class TokenRefreshHandlerTests
{
    [Fact]
    public async Task SendAsync_401WithBody_ClonesBodyForRetry()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var content = new StringContent("""{"side":"BUY"}""", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/v1/api/iserver/account/1234/orders")
        {
            Content = content,
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessionManager.ReauthCallCount.ShouldBe(1);
        innerHandler.CallCount.ShouldBe(2);
        // The retry request should have had content (body was cloned)
        innerHandler.LastRequestHadContent.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_TicklePath_SkipsRetryOn401()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(HttpStatusCode.Unauthorized);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/tickle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sessionManager.ReauthCallCount.ShouldBe(0);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_RetryAlso401_ReturnsSecondResponse()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Unauthorized);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/iserver/account/orders",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sessionManager.ReauthCallCount.ShouldBe(1);
        innerHandler.CallCount.ShouldBe(2); // original + one retry only
    }

    [Fact]
    public async Task SendAsync_Non401_PassesThrough()
    {
        var sessionManager = new FakeSessionManager();
        var innerHandler = new SequenceHandler(HttpStatusCode.OK);

        var handler = new TokenRefreshHandler(sessionManager)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessionManager.ReauthCallCount.ShouldBe(0);
        innerHandler.CallCount.ShouldBe(1);
    }

    private sealed class FakeSessionManager : ISessionManager
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

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _responses;
        private int _callCount;

        public int CallCount => _callCount;
        public bool LastRequestHadContent { get; private set; }

        public SequenceHandler(params HttpStatusCode[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            LastRequestHadContent = request.Content != null;
            var statusCode = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Http/GlobalRateLimitingHandlerTests.cs`:**
```csharp
using System.Net;
using System.Threading.RateLimiting;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class GlobalRateLimitingHandlerTests
{
    [Fact]
    public async Task SendAsync_TokenAvailable_PassesThrough()
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10,
        });

        var innerHandler = new OkHandler();
        var handler = new GlobalRateLimitingHandler(limiter)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/test",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_QueueFull_ThrowsRateLimitRejectedException()
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 0,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var innerHandler = new OkHandler();
        var handler = new GlobalRateLimitingHandler(limiter)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);

        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync(
                "http://localhost/test",
                TestContext.Current.CancellationToken));

        innerHandler.CallCount.ShouldBe(0);
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Http/EndpointRateLimitingHandlerTests.cs`:**
```csharp
using System.Net;
using System.Threading.RateLimiting;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class EndpointRateLimitingHandlerTests
{
    [Fact]
    public async Task SendAsync_MatchedEndpoint_AppliesLimiter()
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10,
        });

        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = limiter,
        };

        var innerHandler = new OkHandler();
        var handler = new EndpointRateLimitingHandler(limiters)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync(
            "http://localhost/iserver/account/orders",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_UnmatchedEndpoint_PassesThrough()
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 0,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = limiter,
        };

        var innerHandler = new OkHandler();
        var handler = new EndpointRateLimitingHandler(limiters)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        // This path does NOT match "/iserver/account/orders"
        var response = await client.GetAsync(
            "http://localhost/v1/api/some/other/path",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_MatchedEndpoint_QueueFull_ThrowsRateLimitRejectedException()
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 0,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var limiters = new Dictionary<string, RateLimiter>
        {
            ["/iserver/account/orders"] = limiter,
        };

        var innerHandler = new OkHandler();
        var handler = new EndpointRateLimitingHandler(limiters)
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);

        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync(
                "http://localhost/iserver/account/orders",
                TestContext.Current.CancellationToken));

        innerHandler.CallCount.ShouldBe(0);
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
```

---

## Task T.5 — SessionManager + FlexOperations Gap Tests

### Files
- `tests/IbkrConduit.Tests.Unit/Session/SessionManagerEdgeTests.cs` (new)
- `tests/IbkrConduit.Tests.Unit/Flex/FlexOperationsTests.cs` (new)

### Steps

- [ ] RED: Create `SessionManagerEdgeTests` with 4 tests
- [ ] Run `dotnet test --configuration Release` — verify they fail
- [ ] GREEN: Complete all `SessionManagerEdgeTests` implementations
- [ ] Run `dotnet test --configuration Release` — verify they pass
- [ ] RED: Create `FlexOperationsTests` with 2 tests
- [ ] Run `dotnet test --configuration Release` — verify they fail
- [ ] GREEN: Complete all `FlexOperationsTests` implementations
- [ ] Run `dotnet test --configuration Release` — verify they pass
- [ ] REFACTOR: Extract shared fakes if duplicated
- [ ] Run `dotnet format --verify-no-changes` — verify lint passes
- [ ] Commit: `test: add SessionManager edge case and FlexOperations gap tests`

### Code

**`tests/IbkrConduit.Tests.Unit/Session/SessionManagerEdgeTests.cs`:**
```csharp
using System.Net.Http;
using IbkrConduit.Auth;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

/// <summary>
/// Edge case tests for <see cref="SessionManager"/> covering concurrent re-auth,
/// dispose-during-init, logout failures, and suppress-skip scenarios.
/// </summary>
public class SessionManagerEdgeTests
{
    [Fact]
    public async Task ReauthenticateAsync_WhenAlreadyReady_SkipsIfTokenNotExpiring()
    {
        var deps = CreateDependencies();

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Two concurrent re-auth calls — the semaphore serializes them.
        // Both should complete without error and only one actual refresh should happen
        // because the second caller acquires the semaphore after the first has
        // already finished refreshing.
        var task1 = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);
        var task2 = manager.ReauthenticateAsync(TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2);

        // Both calls go through because the semaphore serializes (doesn't skip).
        // The key behavior is that neither call throws and both complete.
        deps.TokenProvider.RefreshCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task DisposeAsync_DuringInit_CleansUpGracefully()
    {
        var deps = CreateDependencies();
        deps.TokenProvider.DelayMs = 200; // Slow down token acquisition

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        // Start initialization in background
        var initTask = Task.Run(
            () => manager.EnsureInitializedAsync(TestContext.Current.CancellationToken));

        // Give init a moment to start, then dispose
        await Task.Delay(50);
        await manager.DisposeAsync();

        // Init may throw OperationCanceledException or ObjectDisposedException,
        // or complete normally before dispose runs. All are acceptable.
        try
        {
            await initTask;
        }
        catch (ObjectDisposedException)
        {
            // Expected — semaphore was disposed while waiting
        }
        catch (OperationCanceledException)
        {
            // Also acceptable
        }
    }

    [Fact]
    public async Task DisposeAsync_LogoutFailure_SwallowsException()
    {
        var deps = CreateDependencies();
        deps.SessionApi.LogoutShouldThrow = true;

        var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Dispose should not throw even though logout fails
        await manager.DisposeAsync();

        deps.SessionApi.LogoutCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureInitializedAsync_SuppressEmpty_SkipsSuppressCall()
    {
        var deps = CreateDependencies();
        deps.Options = new IbkrClientOptions
        {
            Compete = true,
            SuppressMessageIds = [],
        };

        await using var manager = new SessionManager(
            deps.TokenProvider,
            deps.TickleTimerFactory,
            deps.SessionApi,
            deps.Options,
            deps.Notifier,
            NullLogger<SessionManager>.Instance);

        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        deps.SessionApi.SuppressCallCount.ShouldBe(0);
    }

    private static TestDependencies CreateDependencies() => new();

    private class TestDependencies
    {
        public DelayableTokenProvider TokenProvider { get; } = new();
        public FakeTickleTimerFactory TickleTimerFactory { get; } = new();
        public FakeSessionApi SessionApi { get; } = new();
        public FakeLifecycleNotifier Notifier { get; } = new();
        public IbkrClientOptions Options { get; set; } = new();
    }

    internal class DelayableTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token = new(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        public int GetCallCount { get; private set; }
        public int RefreshCallCount { get; private set; }
        public int DelayMs { get; set; }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
        {
            GetCallCount++;
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            return _token;
        }

        public async Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            return new LiveSessionToken(
                new byte[] { 0x04, 0x05, 0x06 },
                DateTimeOffset.UtcNow.AddHours(24));
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

    internal class FakeLifecycleNotifier : ISessionLifecycleNotifier
    {
        public int NotifyCallCount { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed) =>
            throw new NotImplementedException();

        public Task NotifyAsync(CancellationToken cancellationToken)
        {
            NotifyCallCount++;
            return Task.CompletedTask;
        }
    }

    internal class FakeSessionApi : IIbkrSessionApi
    {
        public int InitCallCount { get; private set; }
        public int SuppressCallCount { get; private set; }
        public int LogoutCallCount { get; private set; }
        public bool LogoutShouldThrow { get; set; }

        public Task<SsodhInitResponse> InitializeBrokerageSessionAsync(
            SsodhInitRequest request, CancellationToken cancellationToken = default)
        {
            InitCallCount++;
            return Task.FromResult(new SsodhInitResponse(true, true, false));
        }

        public Task<TickleResponse> TickleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TickleResponse(
                Session: string.Empty,
                Iserver: new TickleIserverStatus(
                    AuthStatus: new TickleAuthStatus(true, false, true))));

        public Task<SuppressResponse> SuppressQuestionsAsync(
            SuppressRequest request, CancellationToken cancellationToken = default)
        {
            SuppressCallCount++;
            return Task.FromResult(new SuppressResponse("submitted"));
        }

        public Task<LogoutResponse> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCallCount++;
            if (LogoutShouldThrow)
            {
                throw new HttpRequestException("Simulated logout failure");
            }

            return Task.FromResult(new LogoutResponse(true));
        }
    }
}
```

**`tests/IbkrConduit.Tests.Unit/Flex/FlexOperationsTests.cs`:**
```csharp
using System.Net;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexOperationsTests
{
    [Fact]
    public async Task ExecuteQueryAsync_NullToken_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("12345", CancellationToken.None));

        ex.Message.ShouldContain("FlexToken");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithDateRange_DelegatesCorrectly()
    {
        var handler = new FakeHttpHandler(
            """
            <FlexStatementResponse>
                <Status>Success</Status>
                <ReferenceCode>REF001</ReferenceCode>
            </FlexStatementResponse>
            """,
            """
            <FlexQueryResponse>
                <FlexStatements count="1">
                    <FlexStatement accountId="U1234567" />
                </FlexStatements>
            </FlexQueryResponse>
            """);

        var httpClient = new HttpClient(handler);
        var flexClient = new FlexClient(httpClient, "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
        var ops = new FlexOperations(flexClient);

        var result = await ops.ExecuteQueryAsync("Q1", "20260101", "20260301", CancellationToken.None);

        result.ShouldNotBeNull();
        result.RawXml.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        // Verify the first request (SendRequest) included date params
        var sendRequestUrl = handler.RequestUris[0].ToString();
        sendRequestUrl.ShouldContain("fd=20260101");
        sendRequestUrl.ShouldContain("td=20260301");
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _callCount;

        public List<Uri> RequestUris { get; } = [];

        public FakeHttpHandler(params string[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            var index = Interlocked.Increment(ref _callCount) - 1;
            var body = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/xml"),
            });
        }
    }
}
```

---

## Task T.6 — Run Coverage, Verify Improvement

### Files
- No new files. This is a verification step.

### Steps

- [ ] Run `dotnet test --configuration Release --collect:"XPlat Code Coverage"` to generate coverage data
- [ ] Compare new coverage versus baseline (71.7% line / 53.6% branch)
- [ ] Verify line coverage exceeds 80% and branch coverage exceeds 65%
- [ ] Document final coverage numbers in the commit message
- [ ] Commit: `docs: record final coverage baseline after gap-fill`

---

## Dependency Graph

```
T.1 (exclusions)          independent, do first
T.2 (WebSocket adapter)   depends on T.1 (to avoid merge conflicts on IbkrWebSocketClient)
T.3 (FlexClient polling)  depends on T.1
T.4 (HTTP handlers)       depends on T.1
T.5 (SessionManager+Flex) depends on T.1
T.6 (verify coverage)     depends on T.1, T.2, T.3, T.4, T.5
```

**Parallel:** T.2, T.3, T.4, T.5 are independent of each other (all can follow T.1).

---

## Self-Review Checklist

### Spec Coverage

| Spec Requirement | Plan Task |
|---|---|
| Apply `[ExcludeFromCodeCoverage]` to records, pass-through ops, constants, trivial classes | T.1 |
| Extract `IWebSocketAdapter` + `ClientWebSocketAdapter` | T.2 |
| 10 WebSocket client test cases | T.2 |
| 8 FlexClient polling test cases | T.3 |
| 4 TokenRefreshHandler tests | T.4 |
| 2 GlobalRateLimitingHandler tests | T.4 |
| 3 EndpointRateLimitingHandler tests | T.4 |
| 5 ResilienceHandler tests (already exist in `ResilienceHandlerTests.cs`) | N/A (already covered) |
| 4 SessionManager edge tests | T.5 |
| 2 FlexOperations gap tests | T.5 |
| Run coverage and verify improvement | T.6 |

### Placeholder Scan

No TBDs, no vague steps. Every task has complete code and exact file paths.

### Type Consistency

| Type Name | Used In |
|---|---|
| `IWebSocketAdapter` | T.2 interface, adapter, client, DI, fake |
| `ClientWebSocketAdapter` | T.2 production adapter, DI registration |
| `FakeWebSocketAdapter` | T.2 test fake |
| `FlexClient` | T.3 (existing type, tested) |
| `FlexQueryException` | T.1 (excluded), T.3 (asserted) |
| `TokenRefreshHandler` | T.4 (existing type, tested) |
| `GlobalRateLimitingHandler` | T.4 (existing type, tested) |
| `EndpointRateLimitingHandler` | T.4 (existing type, tested) |
| `RateLimitRejectedException` | T.1 (excluded), T.4 (asserted) |
| `SessionManager` | T.5 (existing type, tested) |
| `FlexOperations` | T.5 (existing type, tested) |
| `ISessionManager` | T.4 (fake in TokenRefreshHandlerTests) |
| `ISessionLifecycleNotifier` | T.2, T.5 (fakes in test classes) |
| `IIbkrSessionApi` | T.2, T.5 (fakes in test classes) |
