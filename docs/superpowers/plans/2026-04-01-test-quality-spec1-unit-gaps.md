# Test Quality Spec 1: Unit Test Gap Fill — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fill all unit test coverage gaps — add ~65 tests for operations delegation, edge cases in OrderOperations, SessionManager, LiveSessionTokenClient, OAuthSigningHandler, and cancellation scenarios.

**Architecture:** Add NSubstitute as test-only dependency. New delegation tests use NSubstitute mocks; existing hand-written fakes stay untouched. Remove [ExcludeFromCodeCoverage] from 6 operations classes.

**Tech Stack:** xUnit v3, Shouldly, NSubstitute 5.3.0

---

## Dependency Graph

```
Task 0 (NSubstitute dependency)
         │
         ├──────────────────────────────────────┐
         ▼                                      ▼
Tasks 1-6 (delegation tests)          Tasks 7-11 (edge case tests)
         │                                      │
         ▼                                      ▼
      [independent — can run in parallel]    [independent — can run in parallel]
```

All of Tasks 1-6 are independent of each other (can run in parallel after Task 0).
All of Tasks 7-11 are independent of each other and of Tasks 1-6 (can run in parallel after Task 0).

---

## Task 0 — Add NSubstitute Dependency

**Branch:** `test/unit-test-gap-fill` (single branch for all tasks)

### Files Modified

- `Directory.Packages.props` — add `<PackageVersion Include="NSubstitute" Version="5.3.0" />`
- `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj` — add `<PackageReference Include="NSubstitute" />`

### Steps

- [ ] Add `<PackageVersion Include="NSubstitute" Version="5.3.0" />` to `Directory.Packages.props` inside the `<ItemGroup>` with other test packages
- [ ] Add `<PackageReference Include="NSubstitute" />` to `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj`
- [ ] Run: `dotnet build /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release`
- [ ] Verify build succeeds with zero warnings
- [ ] Commit: `chore: add NSubstitute 5.3.0 as test-only dependency`

---

## Task 1 — AccountOperations Delegation Tests (5 tests)

### Files Modified

- `src/IbkrConduit/Client/AccountOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Accounts/AccountOperationsTests.cs`

### Important

Before writing tests, read the model record definitions in `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs` to get exact constructor signatures for `IserverAccountsResponse`, `SwitchAccountResponse`, `DynAccountResponse`, `AccountSearchResult`, and `IserverAccountInfo`.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `GetAccountsAsync_DelegatesToApi` | Calls `_api.GetAccountsAsync`, returns result unchanged, forwards CancellationToken |
| 2 | `SwitchAccountAsync_DelegatesToApi` | Wraps accountId in `SwitchAccountRequest`, calls `_api.SwitchAccountAsync`, returns result |
| 3 | `SetDynAccountAsync_DelegatesToApi` | Wraps accountId in `DynAccountRequest`, calls `_api.SetDynAccountAsync`, returns result |
| 4 | `SearchAccountsAsync_DelegatesToApi` | Passes pattern through to `_api.SearchAccountsAsync`, returns result |
| 5 | `GetAccountInfoAsync_DelegatesToApi` | Passes accountId through to `_api.GetAccountInfoAsync`, returns result |

### Pattern

Each test follows this structure:

```csharp
[Fact]
public async Task MethodName_DelegatesToApi()
{
    var expected = /* construct return value using actual record constructor */;
    _api.MethodAsync(Arg.Any<...>(), Arg.Any<CancellationToken>()).Returns(expected);

    var result = await _sut.MethodAsync(/* args */, TestContext.Current.CancellationToken);

    result.ShouldBeSameAs(expected);
    await _api.Received(1).MethodAsync(/* exact arg matchers */, TestContext.Current.CancellationToken);
}
```

For methods that wrap parameters (SwitchAccountAsync, SetDynAccountAsync), use `Arg.Is<T>(r => r.Property == value)` to verify the wrapper was constructed correctly.

### Steps

- [ ] Read `src/IbkrConduit/Accounts/IIbkrAccountApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Accounts/AccountOperationsTests.cs` with all 5 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~AccountOperationsTests"`
- [ ] Verify all 5 tests pass (implementation already exists)
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/AccountOperations.cs`
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~AccountOperationsTests"`
- [ ] Verify still passing
- [ ] Commit: `test: add AccountOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 2 — AlertOperations Delegation Tests (4 tests)

### Files Modified

- `src/IbkrConduit/Client/AlertOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Alerts/AlertOperationsTests.cs`

### Important

Read `src/IbkrConduit/Alerts/IIbkrAlertApiModels.cs` for exact record constructors of `CreateAlertResponse`, `CreateAlertRequest`, `AlertSummary`, `AlertDetail`, `DeleteAlertResponse`.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `CreateOrModifyAlertAsync_DelegatesToApi` | Passes accountId and request body to `_api.CreateOrModifyAlertAsync`, returns result |
| 2 | `GetAlertsAsync_DelegatesToApi` | Calls `_api.GetAlertsAsync`, returns result unchanged |
| 3 | `GetAlertDetailAsync_DelegatesToApi` | Passes alertId to `_api.GetAlertDetailAsync`, returns result |
| 4 | `DeleteAlertAsync_DelegatesToApi` | Passes accountId and alertId to `_api.DeleteAlertAsync`, returns result |

### Steps

- [ ] Read `src/IbkrConduit/Alerts/IIbkrAlertApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Alerts/AlertOperationsTests.cs` with all 4 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~AlertOperationsTests"`
- [ ] Verify all 4 tests pass
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/AlertOperations.cs`
- [ ] Run tests again, verify still passing
- [ ] Commit: `test: add AlertOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 3 — FyiOperations Delegation Tests (12 tests)

### Files Modified

- `src/IbkrConduit/Client/FyiOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Fyi/FyiOperationsTests.cs`

### Important

1. Read `src/IbkrConduit/Fyi/IIbkrFyiApiModels.cs` for exact record constructors.
2. The `SetEmailDeliveryAsync(bool enabled)` method in `FyiOperations` converts the boolean to a lowercase string via `enabled.ToString().ToLowerInvariant()` before calling `_api.SetEmailDeliveryAsync(string, CancellationToken)`. Two tests MUST verify this conversion for both `true` and `false`.
3. The `UpdateSettingAsync(string typecode, bool enabled)` method wraps the bool in `FyiSettingUpdateRequest(enabled)`.
4. `DeleteDeviceAsync` returns `Task` (void), not a value.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `GetUnreadCountAsync_DelegatesToApi` | Calls `_api.GetUnreadCountAsync`, returns result |
| 2 | `GetSettingsAsync_DelegatesToApi` | Calls `_api.GetSettingsAsync`, returns result |
| 3 | `UpdateSettingAsync_DelegatesToApi` | Wraps enabled bool in `FyiSettingUpdateRequest`, passes typecode and request |
| 4 | `GetDisclaimerAsync_DelegatesToApi` | Passes typecode to `_api.GetDisclaimerAsync`, returns result |
| 5 | `MarkDisclaimerReadAsync_DelegatesToApi` | Passes typecode to `_api.MarkDisclaimerReadAsync`, returns result |
| 6 | `GetDeliveryOptionsAsync_DelegatesToApi` | Calls `_api.GetDeliveryOptionsAsync`, returns result |
| 7 | `SetEmailDeliveryAsync_True_PassesTrueAsLowercaseString` | Calls with `enabled=true`, verifies `_api.SetEmailDeliveryAsync("true", ct)` |
| 8 | `SetEmailDeliveryAsync_False_PassesFalseAsLowercaseString` | Calls with `enabled=false`, verifies `_api.SetEmailDeliveryAsync("false", ct)` |
| 9 | `RegisterDeviceAsync_DelegatesToApi` | Passes request body to `_api.RegisterDeviceAsync`, returns result |
| 10 | `DeleteDeviceAsync_DelegatesToApi` | Passes deviceId to `_api.DeleteDeviceAsync`, verifies Received(1) |
| 11 | `GetNotificationsAsync_DelegatesToApi` | Passes max param to `_api.GetNotificationsAsync`, returns result |
| 12 | `MarkNotificationReadAsync_DelegatesToApi` | Passes notificationId to `_api.MarkNotificationReadAsync`, returns result |

Note: `GetMoreNotificationsAsync` is also present (12 methods total in the operations class). Include it:

| 13 | `GetMoreNotificationsAsync_DelegatesToApi` | Passes id to `_api.GetMoreNotificationsAsync`, returns result |

Correction: FyiOperations has 12 methods, not 13. Count: GetUnreadCount, GetSettings, UpdateSetting, GetDisclaimer, MarkDisclaimerRead, GetDeliveryOptions, SetEmailDelivery, RegisterDevice, DeleteDevice, GetNotifications, GetMoreNotifications, MarkNotificationRead = 12 methods. With SetEmailDelivery needing 2 tests (true/false), that's 13 tests total.

### Steps

- [ ] Read `src/IbkrConduit/Fyi/IIbkrFyiApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Fyi/FyiOperationsTests.cs` with all 13 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~FyiOperationsTests"`
- [ ] Verify all 13 tests pass
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/FyiOperations.cs`
- [ ] Run tests again, verify still passing
- [ ] Commit: `test: add FyiOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 4 — AllocationOperations Delegation Tests (8 tests)

### Files Modified

- `src/IbkrConduit/Client/AllocationOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Allocation/AllocationOperationsTests.cs`

### Important

1. Read `src/IbkrConduit/Allocation/IIbkrAllocationApiModels.cs` for exact record constructors.
2. `GetGroupAsync(string name)` wraps name in `AllocationGroupNameRequest(name)` before calling the API.
3. `DeleteGroupAsync(string name)` also wraps name in `AllocationGroupNameRequest(name)`.
4. `AddGroupAsync` and `ModifyGroupAsync` take `AllocationGroupRequest` directly (no wrapping).
5. `SetPresetsAsync` takes `AllocationPresetsRequest` directly.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `GetAccountsAsync_DelegatesToApi` | Calls `_api.GetAccountsAsync`, returns result |
| 2 | `GetGroupsAsync_DelegatesToApi` | Calls `_api.GetGroupsAsync`, returns result |
| 3 | `AddGroupAsync_DelegatesToApi` | Passes request to `_api.AddGroupAsync`, returns result |
| 4 | `GetGroupAsync_WrapsNameInRequest` | Wraps name in `AllocationGroupNameRequest`, verifies `_api.GetGroupAsync` received it |
| 5 | `DeleteGroupAsync_WrapsNameInRequest` | Wraps name in `AllocationGroupNameRequest`, verifies `_api.DeleteGroupAsync` received it |
| 6 | `ModifyGroupAsync_DelegatesToApi` | Passes request to `_api.ModifyGroupAsync`, returns result |
| 7 | `GetPresetsAsync_DelegatesToApi` | Calls `_api.GetPresetsAsync`, returns result |
| 8 | `SetPresetsAsync_DelegatesToApi` | Passes request to `_api.SetPresetsAsync`, returns result |

### Steps

- [ ] Read `src/IbkrConduit/Allocation/IIbkrAllocationApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Allocation/AllocationOperationsTests.cs` with all 8 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~AllocationOperationsTests"`
- [ ] Verify all 8 tests pass
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/AllocationOperations.cs`
- [ ] Run tests again, verify still passing
- [ ] Commit: `test: add AllocationOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 5 — WatchlistOperations Delegation Tests (4 tests)

### Files Modified

- `src/IbkrConduit/Client/WatchlistOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Watchlists/WatchlistOperationsTests.cs`

### Important

Read `src/IbkrConduit/Watchlists/IIbkrWatchlistApiModels.cs` for exact record constructors of `CreateWatchlistRequest`, `CreateWatchlistResponse`, `WatchlistSummary`, `WatchlistDetail`, `DeleteWatchlistResponse`.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `CreateWatchlistAsync_DelegatesToApi` | Passes request to `_api.CreateWatchlistAsync`, returns result |
| 2 | `GetWatchlistsAsync_DelegatesToApi` | Calls `_api.GetWatchlistsAsync`, returns result |
| 3 | `GetWatchlistAsync_DelegatesToApi` | Passes id to `_api.GetWatchlistAsync`, returns result |
| 4 | `DeleteWatchlistAsync_DelegatesToApi` | Passes id to `_api.DeleteWatchlistAsync`, returns result |

### Steps

- [ ] Read `src/IbkrConduit/Watchlists/IIbkrWatchlistApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Watchlists/WatchlistOperationsTests.cs` with all 4 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~WatchlistOperationsTests"`
- [ ] Verify all 4 tests pass
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/WatchlistOperations.cs`
- [ ] Run tests again, verify still passing
- [ ] Commit: `test: add WatchlistOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 6 — ContractOperations Delegation Tests (12 tests)

### Files Modified

- `src/IbkrConduit/Client/ContractOperations.cs` — remove `[ExcludeFromCodeCoverage]`

### Files Created

- `tests/IbkrConduit.Tests.Unit/Contracts/ContractOperationsTests.cs`

### Important

1. Read `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs` for exact record constructors.
2. `GetTradingRulesAsync` takes a `TradingRulesRequest` body directly (no wrapping).
3. `GetSecurityDefinitionInfoAsync` has many optional params (`exchange`, `strike`, `right`, `issuerId`). Test with required params only; optional params are passed straight through.
4. `GetTradingScheduleAsync` has optional `exchange` and `exchangeFilter` params.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `SearchBySymbolAsync_DelegatesToApi` | Passes symbol to `_api.SearchBySymbolAsync`, returns result |
| 2 | `GetContractDetailsAsync_DelegatesToApi` | Passes conid to `_api.GetContractDetailsAsync`, returns result |
| 3 | `GetSecurityDefinitionInfoAsync_DelegatesToApi` | Passes conid, sectype, month to `_api.GetSecurityDefinitionInfoAsync`, returns result |
| 4 | `GetOptionStrikesAsync_DelegatesToApi` | Passes conid, sectype, month to `_api.GetOptionStrikesAsync`, returns result |
| 5 | `GetTradingRulesAsync_DelegatesToApi` | Passes request body to `_api.GetTradingRulesAsync`, returns result |
| 6 | `GetSecurityDefinitionsByConidAsync_DelegatesToApi` | Passes conids string to `_api.GetSecurityDefinitionsByConidAsync`, returns result |
| 7 | `GetAllConidsByExchangeAsync_DelegatesToApi` | Passes exchange to `_api.GetAllConidsByExchangeAsync`, returns result |
| 8 | `GetFuturesBySymbolAsync_DelegatesToApi` | Passes symbols to `_api.GetFuturesBySymbolAsync`, returns result |
| 9 | `GetStocksBySymbolAsync_DelegatesToApi` | Passes symbols to `_api.GetStocksBySymbolAsync`, returns result |
| 10 | `GetTradingScheduleAsync_DelegatesToApi` | Passes assetClass, symbol, conid to `_api.GetTradingScheduleAsync`, returns result |
| 11 | `GetCurrencyPairsAsync_DelegatesToApi` | Passes currency to `_api.GetCurrencyPairsAsync`, returns result |
| 12 | `GetExchangeRateAsync_DelegatesToApi` | Passes source, target to `_api.GetExchangeRateAsync`, returns result |

### Steps

- [ ] Read `src/IbkrConduit/Contracts/IIbkrContractApiModels.cs` for exact record constructor signatures
- [ ] Create `tests/IbkrConduit.Tests.Unit/Contracts/ContractOperationsTests.cs` with all 12 tests
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ContractOperationsTests"`
- [ ] Verify all 12 tests pass
- [ ] Remove `[ExcludeFromCodeCoverage]` from `src/IbkrConduit/Client/ContractOperations.cs`
- [ ] Run tests again, verify still passing
- [ ] Commit: `test: add ContractOperations delegation tests and remove ExcludeFromCodeCoverage`

---

## Task 7 — OrderOperations Question/Reply Loop Edge Cases (4 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs` — add 4 tests to existing file

### Important

Read the `HandleQuestionReplyLoopAsync` method in `src/IbkrConduit/Client/OrderOperations.cs` (lines 189-220). The branching logic is:

1. Line 196: `if (response.OrderId is not null)` — returns immediately with OrderResult
2. Line 201: `if (response.Message is not null && response.Id is not null)` — enters question/reply branch
3. Line 211-215: `else` — throws `InvalidOperationException("Unexpected order submission response...")`

The `OrderSubmissionResponse` record has this signature: `OrderSubmissionResponse(string? Id, List<string>? Message, bool? IsSuppress, List<string>? MessageIds, string? OrderId, string? OrderStatus)`.

Use the existing `FakeOrderApi` class in the test file. Do NOT add NSubstitute to this file.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `PlaceOrderAsync_EmptyMessageArray_EntersQuestionBranch` | Response has `Message` as empty list (not null) and `Id` not null. `Message is not null` is true for an empty list, so the question branch is entered and `ReplyAsync` is called. Enqueue a reply with OrderId to succeed. |
| 2 | `PlaceOrderAsync_OrderIdPresent_IgnoresMessageField` | Response has both OrderId (non-null) AND Message (non-null). Since OrderId check comes first (line 196), it returns immediately without calling ReplyAsync. Verify `ReplyCallCount == 0`. |
| 3 | `PlaceOrderAsync_ReplyThrows_ExceptionPropagates` | Question response with valid Id and Message, but no reply enqueued. The `ReplyResponses.Dequeue()` throws `InvalidOperationException` (empty queue). Verify exception propagates to caller. |
| 4 | `PlaceOrderAsync_NullReplyId_ThrowsInvalidOperation` | Response has `Message` (non-null) but `Id` is null. Fails the second condition (`response.Id is not null`), falls to else branch, throws `InvalidOperationException` with "Unexpected order submission response". |

### Steps

- [ ] Read existing `OrderOperationsTests.cs` to understand the FakeOrderApi pattern and test style
- [ ] Add 4 new test methods to `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~OrderOperationsTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add OrderOperations question/reply loop edge case tests`

---

## Task 8 — SessionManager Edge Cases (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Session/SessionManagerEdgeTests.cs` — add tests to existing file

### Important

Read `src/IbkrConduit/Session/SessionManager.cs` carefully, specifically:

- `ScheduleProactiveRefresh()` (lines 235-271): Returns early if `_currentLst == null` (line 237) or if `timeUntilRefresh <= TimeSpan.Zero` (line 245).
- `ReauthenticateAsync()` (lines 113-163): Checks `_state == SessionState.ShuttingDown` at line 121 after acquiring semaphore.
- The proactive refresh `Task.Run` catches `OperationCanceledException` (expected on shutdown) and other exceptions (logged via `LogProactiveRefreshFailed`).

Use the existing `TestDependencies`, `DelayableTokenProvider`, `FakeTickleTimerFactory`, `FakeSessionApi`, `FakeLifecycleNotifier` classes already in `SessionManagerEdgeTests.cs`. Do NOT use NSubstitute in this file.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `ScheduleProactiveRefresh_NearZeroExpiry_SkipsScheduling` | Create a `DelayableTokenProvider` that returns a token expiring within 30 minutes (less than the 1-hour buffer). After `EnsureInitializedAsync`, the proactive refresh task should NOT be scheduled. Verify by disposing immediately — no errors from proactive refresh. Also verify `RefreshCallCount == 0` after a short delay. |
| 2 | `ReauthenticateAsync_WhileShuttingDown_ReturnsEarly` | Initialize, then start a `ReauthenticateAsync` on a separate thread while calling `DisposeAsync` (which sets state to ShuttingDown). Due to semaphore serialization, the second operation waits for the first. If DisposeAsync runs first, it sets ShuttingDown and disposes the semaphore — ReauthenticateAsync would throw ObjectDisposedException. If ReauthenticateAsync runs first, it completes normally, then DisposeAsync runs. Both outcomes are acceptable. Test with `DelayableTokenProvider.DelayMs = 200` for RefreshAsync to ensure overlap. |
| 3 | `ProactiveRefresh_CallbackThrows_ExceptionLogged` | Create a custom `ISessionTokenProvider` where `RefreshAsync` throws on the second call. Initialize with a token expiring in 1 hour + a few seconds (so proactive refresh fires quickly). Wait for the proactive refresh to trigger. The exception should be caught and logged, not thrown to any caller. Verify the session is still usable after the failed proactive refresh. |

Note: Test 3 is complex and timing-sensitive. An alternative simpler approach: use a token expiring in `now + 1 hour + 100ms`, so `timeUntilRefresh = 100ms`. After 200ms, the proactive refresh fires and calls `ReauthenticateAsync`. If `RefreshAsync` throws, the catch block logs it. Verify no unhandled exception propagates. Since the test can't easily observe the log, verify that the SessionManager is still functional (e.g., `EnsureInitializedAsync` still returns without error because state is still Ready from before the failed refresh).

### Steps

- [ ] Read existing `SessionManagerEdgeTests.cs` to understand the fake infrastructure
- [ ] For Test 1: Modify `DelayableTokenProvider` to support custom expiry (or create a subclass). The token returned by `GetLiveSessionTokenAsync` should have `Expiry = DateTimeOffset.UtcNow.AddMinutes(30)`. After init + dispose, no errors should occur.
- [ ] For Test 2: Use `DelayableTokenProvider.DelayMs = 200` for both Get and Refresh. Start init, start reauth and dispose concurrently after init completes. Accept any combination of outcomes (ObjectDisposedException, OperationCanceledException, or clean completion).
- [ ] For Test 3: Create a custom token provider that returns a token expiring in `UtcNow + 1h + 200ms` on first call and throws on RefreshAsync. Wait 500ms after init. Verify no unhandled exceptions.
- [ ] Add all 3 tests to `tests/IbkrConduit.Tests.Unit/Session/SessionManagerEdgeTests.cs`
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~SessionManagerEdgeTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add SessionManager proactive refresh and shutdown edge case tests`

---

## Task 9 — LiveSessionTokenClient Error Handling (5 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Auth/LiveSessionTokenClientTests.cs` — add 5 tests to existing file

### Important

Read `src/IbkrConduit/Auth/LiveSessionTokenClient.cs` lines 77-87. The error paths are:

1. Line 78: `response.EnsureSuccessStatusCode()` — throws `HttpRequestException` for non-2xx
2. Line 82: `JsonDocument.Parse(json)` — throws `JsonException` for malformed JSON
3. Line 85: `root.GetProperty("diffie_hellman_response")` — throws `KeyNotFoundException` if property missing
4. Line 86: `root.GetProperty("live_session_token_signature")` — throws `KeyNotFoundException` if property missing
5. Line 90: `BigInteger.Parse("0" + dhResponseHex, ...)` — throws `FormatException` if hex is invalid (but the actual exception for invalid base64 would come from elsewhere). Actually, `dhResponseHex` comes from `GetString()!` — if it's not valid hex, `BigInteger.Parse` throws. Test with a non-hex value.

Use the existing `FakeHttpHandler` and `FakeHttpClientFactory` classes in the test file. Each test needs valid `IbkrOAuthCredentials` (use the same RSA key pattern from the existing `GetLiveSessionTokenAsync_ValidResponse_ReturnsValidatedToken` test).

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `GetLiveSessionTokenAsync_Non2xxResponse_ThrowsHttpRequestException` | FakeHttpHandler returns 401. `EnsureSuccessStatusCode()` throws. |
| 2 | `GetLiveSessionTokenAsync_MalformedJson_ThrowsJsonException` | FakeHttpHandler returns 200 with body `"not json"`. `JsonDocument.Parse` throws. |
| 3 | `GetLiveSessionTokenAsync_MissingDhResponse_ThrowsKeyNotFoundException` | FakeHttpHandler returns valid JSON missing `diffie_hellman_response`. `GetProperty` throws. |
| 4 | `GetLiveSessionTokenAsync_MissingSignature_ThrowsKeyNotFoundException` | FakeHttpHandler returns JSON with `diffie_hellman_response` but missing `live_session_token_signature`. |
| 5 | `GetLiveSessionTokenAsync_InvalidHexDhResponse_ThrowsFormatException` | FakeHttpHandler returns JSON with `diffie_hellman_response` set to `"ZZZZ"` (invalid hex). `BigInteger.Parse` throws. |

### Steps

- [ ] Read existing `LiveSessionTokenClientTests.cs` to understand the FakeHttpHandler/FakeHttpClientFactory pattern
- [ ] Create a helper method to construct test `IbkrOAuthCredentials` (reuse the RSA key generation pattern from the valid test)
- [ ] Add 5 new test methods
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~LiveSessionTokenClientTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add LiveSessionTokenClient error handling edge case tests`

---

## Task 10 — OAuthSigningHandler Edge Cases (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs` — add 3 tests to existing file

### Important

Read `src/IbkrConduit/Auth/OAuthSigningHandler.cs`:

1. Lines 59-62: If `_sessionManager != null`, calls `EnsureInitializedAsync`. If it throws, the exception propagates to the caller.
2. Lines 80-83: If `request.Headers.UserAgent.Count == 0`, adds default User-Agent. Otherwise leaves existing header.
3. Lines 86-109: `_activeRequests.Add(1)` before `base.SendAsync`, `_activeRequests.Add(-1)` in `finally`. If `base.SendAsync` throws, the finally block still runs.

Use the existing `FakeSessionManager`, `FakeTokenProvider`, and `FakeInnerHandler` classes in the test file.

### Test Inventory

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| 1 | `SendAsync_EnsureInitializedThrows_ExceptionPropagates` | Create `FakeSessionManager` that throws `InvalidOperationException` from `EnsureInitializedAsync`. Verify the exception reaches the caller. |
| 2 | `SendAsync_ExistingUserAgent_NotReplaced` | Set `User-Agent` on the request before sending. Verify the captured request still has the original User-Agent (not replaced with default). |
| 3 | `SendAsync_InnerHandlerThrows_ExceptionPropagates` | Create `FakeInnerHandler` that throws `HttpRequestException`. Verify it propagates to caller (active request counter is handled in finally). |

### Steps

- [ ] Read existing `OAuthSigningHandlerTests.cs` to understand the fake infrastructure
- [ ] For Test 1: Create a new `ThrowingSessionManager` class (or modify `FakeSessionManager` with a flag) that throws from `EnsureInitializedAsync`
- [ ] For Test 2: Add `User-Agent` to the request using `httpClient.DefaultRequestHeaders.UserAgent.Add(...)` or set it on the individual request
- [ ] For Test 3: Create `FakeInnerHandler` that throws `HttpRequestException`
- [ ] Add all 3 tests to `tests/IbkrConduit.Tests.Unit/Auth/OAuthSigningHandlerTests.cs`
- [ ] Run: `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~OAuthSigningHandlerTests"`
- [ ] Verify all tests pass (existing + new)
- [ ] Commit: `test: add OAuthSigningHandler edge case tests`

---

## Task 11 — Cancellation and Timeout Edge Cases (3 tests)

### Files Modified

- `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs` — add 1 cancellation test
- `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs` — add 1 cancellation test
- `tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs` — add 1 timeout boundary test

### Important

1. **SessionManager cancellation**: `EnsureInitializedAsync` calls `_semaphore.WaitAsync(cancellationToken)` at line 73. A pre-cancelled token should throw `OperationCanceledException` immediately.

2. **OrderOperations cancellation**: `PlaceOrderAsync` calls `semaphore.WaitAsync(cancellationToken)` at line 56. A pre-cancelled token should throw `OperationCanceledException`.

3. **FlexClient timeout**: The polling loop in `FlexClient` checks `if (totalWaited >= _maxTotalWaitMs)` before each delay. The `_maxTotalWaitMs` is 60000. The `_pollDelaysMs` array has 12 entries summing to 56s. After all delays, if still 1019, it throws `TimeoutException`. This test should verify that after exhausting all poll delays with continuous 1019 responses, a `TimeoutException` is thrown. The existing test `ExecuteQueryAsync_TimeoutAfterMaxDuration_ThrowsTimeoutException` already tests this but uses a CancellationToken short-circuit. Add a test that specifically verifies the `totalWaited >= _maxTotalWaitMs` path by using a custom handler that tracks timing.

Actually, re-reading the existing timeout test more carefully: it sets a 500ms CancellationToken timeout, so it races between TimeoutException and OperationCanceledException. A better test would verify that when all 12 poll attempts return 1019 (with the `_pollDelaysMs` summing to 56s), the final check after the loop throws `TimeoutException`. But this would take 56 seconds to run, which is too slow.

Alternative approach: Test that a cancelled token during the semaphore wait in OrderOperations throws immediately. This is fast and deterministic.

### Test Inventory

| # | Test Name | File | What It Verifies |
|---|-----------|------|-----------------|
| 1 | `EnsureInitializedAsync_PreCancelledToken_ThrowsOperationCanceled` | `SessionManagerTests.cs` | Create a pre-cancelled `CancellationToken`. Call `EnsureInitializedAsync`. Verify `OperationCanceledException` is thrown without initializing the session. |
| 2 | `PlaceOrderAsync_PreCancelledToken_ThrowsOperationCanceled` | `OrderOperationsTests.cs` | Create a pre-cancelled `CancellationToken`. Call `PlaceOrderAsync`. Verify `OperationCanceledException` is thrown without calling the API. |
| 3 | `ExecuteQueryAsync_CancelledDuringPoll_ThrowsOperationCanceled` | `FlexClientPollingTests.cs` | Set up handler returning 1019 forever. Use a `CancellationTokenSource` with a short timeout (100ms). Verify `OperationCanceledException` is thrown during the polling delay. |

Note: Test 3 may already be covered by the existing `ExecuteQueryAsync_PassesCancellationToken` test. If so, make the new test focus on a different aspect: cancellation DURING the `Task.Delay` in the poll loop (not during the HTTP call). Actually, the existing test already covers this. Instead, make Test 3 about the exact timeout boundary:

| 3 | `ExecuteQueryAsync_AllPollsExhausted_ThrowsTimeoutException` | `FlexClientPollingTests.cs` | Override the delays to be 0ms (or use a custom FlexClient subclass). Actually, we cannot because `_pollDelaysMs` is `static readonly`. Instead, verify that after enough 1019 responses, the exception message contains the reference code. This is a refinement of the existing test — make it deterministic by NOT using a CancellationToken timeout. Use `CancellationToken.None` and provide exactly 13 responses (1 send + 12 polls, all 1019 after send). But this takes 56 seconds due to real delays. |

Given the 56-second wait is impractical, the best alternative for Test 3 is to test cancellation propagation through the `Task.Delay` specifically, which the existing test already does. Let's refine the test to be more targeted: verify that cancellation during `Task.Delay` in the poll loop surfaces as `OperationCanceledException` (not swallowed).

### Steps

- [ ] Add `EnsureInitializedAsync_PreCancelledToken_ThrowsOperationCanceled` to `tests/IbkrConduit.Tests.Unit/Session/SessionManagerTests.cs`:
  ```
  Create CancellationTokenSource, cancel immediately, pass token to EnsureInitializedAsync.
  Should throw OperationCanceledException. Verify SessionApi.InitCallCount == 0.
  ```
- [ ] Add `PlaceOrderAsync_PreCancelledToken_ThrowsOperationCanceled` to `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`:
  ```
  Create pre-cancelled token, call PlaceOrderAsync. Should throw OperationCanceledException.
  Verify no PlaceOrderResponses were dequeued.
  ```
- [ ] Add `ExecuteQueryAsync_CancelledDuringPollDelay_ThrowsOperationCanceled` to `tests/IbkrConduit.Tests.Unit/Flex/FlexClientPollingTests.cs`:
  ```
  Handler returns success send request, then 1019 forever.
  CancellationTokenSource with 200ms timeout.
  Verify OperationCanceledException is thrown during poll delay.
  Verify handler.CallCount >= 2 (at least send + 1 poll).
  ```
- [ ] Run all three test files:
  - `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~SessionManagerTests"`
  - `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~OrderOperationsTests"`
  - `dotnet test /workspace/ibkr-conduit/tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~FlexClientPollingTests"`
- [ ] Verify all tests pass
- [ ] Commit: `test: add cancellation and timeout edge case tests`

---

## Final Verification

After all tasks are committed:

- [ ] Run full check: `dotnet build /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet test /workspace/ibkr-conduit --configuration Release`
- [ ] Run full check: `dotnet format /workspace/ibkr-conduit --verify-no-changes`
- [ ] Verify total new test count is approximately 52-55 tests across all tasks
- [ ] Verify all 6 operations classes no longer have `[ExcludeFromCodeCoverage]`

### Test Count Summary

| Task | Tests |
|------|-------|
| Task 1: AccountOperations | 5 |
| Task 2: AlertOperations | 4 |
| Task 3: FyiOperations | 13 |
| Task 4: AllocationOperations | 8 |
| Task 5: WatchlistOperations | 4 |
| Task 6: ContractOperations | 12 |
| Task 7: OrderOperations edge cases | 4 |
| Task 8: SessionManager edge cases | 3 |
| Task 9: LiveSessionTokenClient errors | 5 |
| Task 10: OAuthSigningHandler edge cases | 3 |
| Task 11: Cancellation/timeout | 3 |
| **Total** | **64** |
