# Spec 1: Unit Test Gap Fill

## Goal

Fill all unit test coverage gaps in the IbkrConduit library. Remove `[ExcludeFromCodeCoverage]` from operations classes that have testable logic, add per-method delegation tests using NSubstitute, and add edge case tests for complex components (SessionManager, OrderOperations, LiveSessionTokenClient, OAuthSigningHandler).

## Context

A test quality audit identified ~25-30 coverage gaps. This is the first of three specs:

- **Spec 1 (this):** Unit test gaps — operations delegation, edge cases for complex components
- **Spec 2:** Concurrency and resilience — semaphore tests, concurrent 401s, error response integration tests
- **Spec 3:** WebSocket and resource management — reconnect edge cases, message pump errors, dispose paths, TokenRefreshHandler

## New Dependency

Add `NSubstitute` as a test-only dependency:

- Add `<PackageVersion Include="NSubstitute" Version="5.3.0" />` to `Directory.Packages.props`
- Add `<PackageReference Include="NSubstitute" />` to `IbkrConduit.Tests.Unit.csproj`

Existing hand-written fakes (e.g., `FakePortfolioApi` in `PortfolioOperationsTests`) stay as-is. No retroactive conversion — new tests use NSubstitute, existing tests keep their pattern.

## Tasks

### Task 1: Operations Class Delegation Tests

Remove `[ExcludeFromCodeCoverage]` from these six classes and add a corresponding test file using NSubstitute:

| Source File | Methods | New Test File |
|---|---|---|
| `Client/AccountOperations.cs` | 5 | `Client/AccountOperationsTests.cs` |
| `Client/AlertOperations.cs` | 4 | `Alerts/AlertOperationsTests.cs` |
| `Client/FyiOperations.cs` | 12 | `Fyi/FyiOperationsTests.cs` |
| `Client/AllocationOperations.cs` | 7 | `Allocation/AllocationOperationsTests.cs` |
| `Client/WatchlistOperations.cs` | 4 | `Watchlists/WatchlistOperationsTests.cs` |
| `Client/ContractOperations.cs` | 12 | `Contracts/ContractOperationsTests.cs` |

Each test verifies three things:

1. **Delegation** — the correct Refit method was called with the correct arguments
2. **Pass-through** — the return value from the Refit call is returned unchanged
3. **CancellationToken forwarding** — the token is passed through to the Refit method

Example pattern using NSubstitute:

```csharp
[Fact]
public async Task GetAlertsAsync_DelegatesToApi()
{
    var api = Substitute.For<IIbkrAlertApi>();
    var expected = new List<Alert>();
    api.GetAlertsAsync(Arg.Any<CancellationToken>()).Returns(expected);
    var sut = new AlertOperations(api);

    var result = await sut.GetAlertsAsync(TestContext.Current.CancellationToken);

    result.ShouldBeSameAs(expected);
    await api.Received(1).GetAlertsAsync(TestContext.Current.CancellationToken);
}
```

~44 new tests total.

### Task 2: OrderOperations Question/Reply Loop Edge Cases

Add tests to `Orders/OrderOperationsTests.cs`:

| Test | Behavior |
|---|---|
| `PlaceOrderAsync_EmptyMessageArray_ReturnsResult` | Question response with empty `Message` array — loop terminates |
| `PlaceOrderAsync_BothOrderIdAndMessage_OrderIdWins` | Response has both `OrderId` (non-null) and `Message` — treated as success |
| `PlaceOrderAsync_ReplyAsyncThrows_ExceptionPropagates` | `ReplyAsync` throws mid-loop — exception is not swallowed |
| `PlaceOrderAsync_NullReplyId_ThrowsInvalidOperation` | Question response with null `Id` field — cannot construct reply |

~4 new tests.

### Task 3: SessionManager Proactive Refresh Edge Cases

Add tests to `Session/SessionManagerEdgeTests.cs`:

| Test | Behavior |
|---|---|
| `ProactiveRefresh_CallbackThrows_ExceptionLogged` | Exception in refresh callback is logged, not thrown to caller |
| `ProactiveRefresh_NearZeroExpiry_SkipsScheduling` | `timeUntilRefresh <= 0` skips the `Task.Delay` and refreshes immediately or skips |
| `ProactiveRefresh_NullLst_Skips` | `_currentLst` is null — `ScheduleProactiveRefresh` returns without scheduling |
| `ReauthenticateAsync_WhileShuttingDown_ReturnsEarly` | State is `ShuttingDown` — re-auth is a no-op |
| `EnsureInitializedAsync_WhileReauthenticating_WaitsForCompletion` | Caller blocks on semaphore until re-auth finishes |
| `RapidTickleFailures_OnlyOneReauthRuns` | Multiple tickle failures in quick succession — semaphore serializes re-auth |

~6 new tests.

### Task 4: LiveSessionTokenClient Error Handling

Add tests to `Auth/LiveSessionTokenClientTests.cs`:

| Test | Behavior |
|---|---|
| `AcquireAsync_Non2xxResponse_ThrowsHttpRequestException` | 401, 403, 500 responses throw `HttpRequestException` |
| `AcquireAsync_MalformedJson_ThrowsJsonException` | Response body is not valid JSON |
| `AcquireAsync_MissingDhResponse_Throws` | JSON missing `diffie_hellman_response` property |
| `AcquireAsync_MissingSignature_Throws` | JSON missing `live_session_token_signature` property |
| `AcquireAsync_InvalidBase64DhResponse_ThrowsFormatException` | DH response is not valid base64 |

~5 new tests.

### Task 5: OAuthSigningHandler Edge Cases

Add tests to `Auth/OAuthSigningHandlerTests.cs`:

| Test | Behavior |
|---|---|
| `SendAsync_EnsureInitializedThrows_ExceptionPropagates` | SessionManager.EnsureInitializedAsync throws — caller sees the exception |
| `SendAsync_EmptyUserAgent_NotReplaced` | Request already has empty User-Agent header — handler does not overwrite |
| `SendAsync_InnerHandlerThrows_ActiveRequestCounterDecrements` | Exception in base.SendAsync — metrics counter still decremented in finally |

~3 new tests.

### Task 6: Cancellation and Timeout Edge Cases

Tests added to relevant existing test files:

| Test | File | Behavior |
|---|---|---|
| `EnsureInitializedAsync_CancelledToken_ThrowsOperationCanceled` | `SessionManagerTests.cs` | Pre-cancelled token throws immediately |
| `PollForStatementAsync_ExactTimeout_ThrowsTimeoutException` | `FlexClientPollingTests.cs` | Polling at boundary of `_maxTotalWaitMs` throws |
| `PlaceOrderAsync_CancelledWhileWaitingForSemaphore_ThrowsOperationCanceled` | `OrderOperationsTests.cs` | Token cancelled while blocked on account semaphore |

~3 new tests.

## Testing Conventions

All new tests follow existing project rules:

- xUnit v3, Shouldly assertions (no `Assert`)
- Naming: `MethodName_Scenario_ExpectedResult`
- `CancellationToken` passed via `TestContext.Current.CancellationToken` where applicable
- NSubstitute for new tests; hand-written fakes untouched in existing tests
- No `[ExcludeFromCodeCoverage]` on any test code

## Out of Scope

- Concurrency stress tests (Spec 2)
- WireMock error response integration tests (Spec 2)
- WebSocket reconnect and message pump tests (Spec 3)
- TokenRefreshHandler tests (Spec 3)
- Resource cleanup / dispose tests (Spec 3)

## Success Criteria

- All 6 operations classes have `[ExcludeFromCodeCoverage]` removed and per-method delegation tests
- Edge case tests cover the identified gaps in OrderOperations, SessionManager, LiveSessionTokenClient, and OAuthSigningHandler
- ~65 new passing tests
- `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes` passes clean
