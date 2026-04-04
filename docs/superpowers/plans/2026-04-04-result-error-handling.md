# Result-Based Error Handling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace exception-driven error handling with `Result<T>` pattern at the facade layer, `IApiResponse<T>` at the Refit layer, and simplified error types.

**Architecture:** New `IbkrError` record hierarchy carries error details. `Result<T>` readonly struct wraps success/failure at the facade. `ResultFactory` converts `IApiResponse<T>` to `Result<T>`. Handlers simplified — `ErrorNormalizationHandler` and `ResilienceHandler` removed, `TokenRefreshHandler` modified to return response on repeated 401.

**Tech Stack:** C# 10, .NET 10, Refit (`IApiResponse<T>`), xUnit v3, Shouldly, WireMock.Net

**Spec:** `docs/superpowers/specs/2026-04-04-result-error-handling-design.md`

---

## File Map

### New files
| File | Purpose |
|------|---------|
| `src/IbkrConduit/Errors/IbkrError.cs` | Abstract base record + all subtypes |
| `src/IbkrConduit/Errors/Result.cs` | `Result<T>` readonly struct |
| `src/IbkrConduit/Errors/ResultFactory.cs` | `IApiResponse<T>` → `Result<T>` conversion |

### Modified files — Core
| File | Change |
|------|--------|
| `src/IbkrConduit/Errors/IbkrApiException.cs` | Simplify to single class wrapping `IbkrError` |
| `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs` | **Delete** |
| `src/IbkrConduit/Errors/IbkrRateLimitException.cs` | **Delete** |
| `src/IbkrConduit/Errors/IbkrSessionException.cs` | **Delete** |
| `src/IbkrConduit/Session/IbkrClientOptions.cs` | Add `ThrowOnApiError` property |
| `src/IbkrConduit/Session/TokenRefreshHandler.cs` | Return response on repeated 401 instead of throwing |
| `src/IbkrConduit/Http/ErrorNormalizationHandler.cs` | **Delete** |
| `src/IbkrConduit/Http/ResilienceHandler.cs` | **Delete** |
| `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` | Remove deleted handlers from pipeline |
| `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs` | Remove Polly resilience pipeline registration |
| `src/IbkrConduit/Http/IbkrErrorBody.cs` | Move to Errors/ (used by ResultFactory now) |

### Modified files — Refit interfaces (Task<T> → Task<IApiResponse<T>>)
| File | Methods |
|------|---------|
| `src/IbkrConduit/Accounts/IIbkrAccountApi.cs` | 10 methods |
| `src/IbkrConduit/Alerts/IIbkrAlertApi.cs` | 6 methods |
| `src/IbkrConduit/Contracts/IIbkrContractApi.cs` | 12 methods |
| `src/IbkrConduit/Fyi/IIbkrFyiApi.cs` | 12 methods |
| `src/IbkrConduit/MarketData/IIbkrMarketDataApi.cs` | 8 methods |
| `src/IbkrConduit/Orders/IIbkrOrderApi.cs` | 8 methods |
| `src/IbkrConduit/Portfolio/IIbkrPortfolioApi.cs` | 18 methods |
| `src/IbkrConduit/Watchlists/IIbkrWatchlistApi.cs` | 4 methods |

Note: `IIbkrSessionApi.cs` is **not** changed — session endpoints are internal and don't flow through the consumer pipeline.

### Modified files — Operations interfaces (Task<T> → Task<Result<T>>)
| File | Methods |
|------|---------|
| `src/IbkrConduit/Client/IAccountOperations.cs` | 10 methods |
| `src/IbkrConduit/Client/IAlertOperations.cs` | 6 methods |
| `src/IbkrConduit/Client/IContractOperations.cs` | 12 methods |
| `src/IbkrConduit/Client/IFyiOperations.cs` | 12 methods |
| `src/IbkrConduit/Client/IMarketDataOperations.cs` | 8 methods |
| `src/IbkrConduit/Client/IOrderOperations.cs` | 8 methods |
| `src/IbkrConduit/Client/IPortfolioOperations.cs` | 18 methods |
| `src/IbkrConduit/Client/IWatchlistOperations.cs` | 4 methods |

Note: `IFlexOperations.cs` and `IStreamingOperations.cs` are **not** changed — Flex uses custom FlexClient, Streaming uses WebSocket.

### Modified files — Operations implementations
| File | Change |
|------|--------|
| `src/IbkrConduit/Client/AccountOperations.cs` | Add `IbkrClientOptions`, use `ResultFactory` |
| `src/IbkrConduit/Client/AlertOperations.cs` | Same pattern |
| `src/IbkrConduit/Client/ContractOperations.cs` | Same pattern |
| `src/IbkrConduit/Client/FyiOperations.cs` | Same pattern |
| `src/IbkrConduit/Client/MarketDataOperations.cs` | Already has `IbkrClientOptions`, use `ResultFactory` |
| `src/IbkrConduit/Client/OrderOperations.cs` | Custom parsing for OneOf via `ResultFactory.FromResponse<T>(response, parser)` |
| `src/IbkrConduit/Client/PortfolioOperations.cs` | Add `IbkrClientOptions`, use `ResultFactory` |
| `src/IbkrConduit/Client/WatchlistOperations.cs` | Same pattern |
| `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` | Inject `IbkrClientOptions` into operations constructors |

### Test files — Delete
| File | Reason |
|------|--------|
| `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs` | Handler removed |
| `tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs` | Handler removed |
| `tests/IbkrConduit.Tests.Integration/Pipeline/ErrorNormalizationTests.cs` | Handler removed |
| `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs` | Handler removed |

### Test files — Modify
| File | Change |
|------|--------|
| `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs` | Rewrite for simplified exception |
| `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs` | Update for no-throw on repeated 401 |
| `tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs` | Update for Result pattern |
| All 7 category integration test files | Update assertions for `Result<T>` |
| `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs` | Update fakes for new return types |
| `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs` | Update fakes |
| All unit test files under `tests/IbkrConduit.Tests.Unit/*/` | Update for new operations signatures |

---

## Task 1: Create IbkrError record hierarchy

**Files:**
- Create: `src/IbkrConduit/Errors/IbkrError.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Errors/IbkrErrorTests.cs`

- [ ] **Step 1: Write failing tests for IbkrError types**

```csharp
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrErrorTests
{
    [Fact]
    public void IbkrApiError_CanBeCreated()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad input", "{}", "/test");
        error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.Message.ShouldBe("bad input");
        error.RawBody.ShouldBe("{}");
        error.RequestPath.ShouldBe("/test");
    }

    [Fact]
    public void IbkrSessionError_HasIsCompeting()
    {
        var error = new IbkrSessionError(HttpStatusCode.Unauthorized, "competing", "", "/auth", true);
        error.IsCompeting.ShouldBeTrue();
        (error is IbkrError).ShouldBeTrue();
    }

    [Fact]
    public void IbkrRateLimitError_HasRetryAfter()
    {
        var delay = TimeSpan.FromSeconds(30);
        var error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow down", "", "/orders", delay);
        error.RetryAfter.ShouldBe(delay);
    }

    [Fact]
    public void IbkrOrderRejectedError_HasRejectionMessage()
    {
        var error = new IbkrOrderRejectedError("insufficient funds", "{\"error\":\"insufficient funds\"}", "/orders");
        error.RejectionMessage.ShouldBe("insufficient funds");
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void IbkrHiddenError_HasMessage()
    {
        var error = new IbkrHiddenError("some error", "{\"error\":\"some error\"}", "/test");
        error.Message.ShouldBe("some error");
        error.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void IbkrError_PatternMatching_Works()
    {
        IbkrError error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow", "", "/test", TimeSpan.FromSeconds(5));
        var matched = error switch
        {
            IbkrRateLimitError { RetryAfter: var delay } => delay?.TotalSeconds,
            _ => null
        };
        matched.ShouldBe(5);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*IbkrErrorTests*"`
Expected: FAIL — types don't exist

- [ ] **Step 3: Implement IbkrError hierarchy**

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Base type for all IBKR API errors. Use pattern matching to discriminate subtypes.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record IbkrError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath);

/// <summary>
/// Generic API error for non-2xx responses that don't match a more specific subtype.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrApiError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Session/authentication error — competing session or expired credentials.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrSessionError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath,
    bool IsCompeting)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Rate limit error from IBKR server (HTTP 429).
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrRateLimitError(
    HttpStatusCode? StatusCode,
    string? Message,
    string? RawBody,
    string? RequestPath,
    TimeSpan? RetryAfter)
    : IbkrError(StatusCode, Message, RawBody, RequestPath);

/// <summary>
/// Order rejected — IBKR returned 200 OK with an error body on an order endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrOrderRejectedError(
    string RejectionMessage,
    string? RawBody,
    string? RequestPath)
    : IbkrError(HttpStatusCode.OK, RejectionMessage, RawBody, RequestPath);

/// <summary>
/// Hidden error — IBKR returned 200 OK with an error body on a non-order endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public record IbkrHiddenError(
    string? Message,
    string? RawBody,
    string? RequestPath)
    : IbkrError(HttpStatusCode.OK, Message, RawBody, RequestPath);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*IbkrErrorTests*"`
Expected: PASS — 6 tests

- [ ] **Step 5: Commit**

```
git add src/IbkrConduit/Errors/IbkrError.cs tests/IbkrConduit.Tests.Unit/Errors/IbkrErrorTests.cs
git commit -m "feat: add IbkrError record hierarchy for Result-based error handling"
```

---

## Task 2: Create Result\<T\> struct

**Files:**
- Create: `src/IbkrConduit/Errors/Result.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Errors/ResultTests.cs`

- [ ] **Step 1: Write failing tests for Result\<T\>**

```csharp
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_ReturnsTrue()
    {
        var result = Result<string>.Success("hello");
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public void Failure_IsSuccess_ReturnsFalse()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Success_AccessingError_Throws()
    {
        var result = Result<string>.Success("hello");
        Should.Throw<InvalidOperationException>(() => { _ = result.Error; });
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        Should.Throw<InvalidOperationException>(() => { _ = result.Value; });
    }

    [Fact]
    public void EnsureSuccess_OnSuccess_ReturnsSelf()
    {
        var result = Result<string>.Success("hello");
        var returned = result.EnsureSuccess();
        returned.Value.ShouldBe("hello");
    }

    [Fact]
    public void EnsureSuccess_OnFailure_ThrowsIbkrApiException()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        var ex = Should.Throw<IbkrApiException>(() => result.EnsureSuccess());
        ex.Error.ShouldBe(error);
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<int>.Success(42);
        var mapped = result.Map(v => v.ToString());
        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBe("42");
    }

    [Fact]
    public void Map_OnFailure_PreservesError()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var mapped = result.Map(v => v.ToString());
        mapped.IsSuccess.ShouldBeFalse();
        mapped.Error.ShouldBe(error);
    }

    [Fact]
    public void Match_OnSuccess_CallsSuccessFunc()
    {
        var result = Result<int>.Success(42);
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Message}");
        output.ShouldBe("ok:42");
    }

    [Fact]
    public void Match_OnFailure_CallsErrorFunc()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Message}");
        output.ShouldBe("err:bad");
    }

    [Fact]
    public void Switch_OnSuccess_CallsSuccessAction()
    {
        var result = Result<int>.Success(42);
        var called = false;
        result.Switch(v => called = true, e => { });
        called.ShouldBeTrue();
    }

    [Fact]
    public void Switch_OnFailure_CallsErrorAction()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var called = false;
        result.Switch(v => { }, e => called = true);
        called.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*ResultTests*"`
Expected: FAIL — `Result<T>` doesn't exist

- [ ] **Step 3: Implement Result\<T\>**

```csharp
namespace IbkrConduit.Errors;

/// <summary>
/// Represents the outcome of an IBKR API call — either a success value or an error.
/// Readonly struct for zero-allocation on the success path.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly IbkrError? _error;

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if the result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result. Check IsSuccess first.");

    /// <summary>
    /// The error details. Throws <see cref="InvalidOperationException"/> if the result is a success.
    /// </summary>
    public IbkrError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result. Check IsSuccess first.");

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(IbkrError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Creates a successful result.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result.</summary>
    public static Result<T> Failure(IbkrError error) => new(error);

    /// <summary>
    /// Returns this result if successful, or throws <see cref="IbkrApiException"/> wrapping the error.
    /// </summary>
    public Result<T> EnsureSuccess() => IsSuccess
        ? this
        : throw new IbkrApiException(_error!);

    /// <summary>Transforms the success value, preserving errors.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector) => IsSuccess
        ? Result<TOut>.Success(selector(_value!))
        : Result<TOut>.Failure(_error!);

    /// <summary>Calls the appropriate action based on success or failure.</summary>
    public void Switch(Action<T> onSuccess, Action<IbkrError> onError)
    {
        if (IsSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onError(_error!);
        }
    }

    /// <summary>Calls the appropriate function and returns its result.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IbkrError, TOut> onError) => IsSuccess
        ? onSuccess(_value!)
        : onError(_error!);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*ResultTests*"`
Expected: PASS — 12 tests

- [ ] **Step 5: Commit**

```
git add src/IbkrConduit/Errors/Result.cs tests/IbkrConduit.Tests.Unit/Errors/ResultTests.cs
git commit -m "feat: add Result<T> readonly struct"
```

---

## Task 3: Simplify IbkrApiException and add ThrowOnApiError option

**Files:**
- Modify: `src/IbkrConduit/Errors/IbkrApiException.cs`
- Delete: `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs`
- Delete: `src/IbkrConduit/Errors/IbkrRateLimitException.cs`
- Delete: `src/IbkrConduit/Errors/IbkrSessionException.cs`
- Modify: `src/IbkrConduit/Session/IbkrClientOptions.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs`

- [ ] **Step 1: Rewrite IbkrApiException to wrap IbkrError**

Replace the entire contents of `src/IbkrConduit/Errors/IbkrApiException.cs`:

```csharp
namespace IbkrConduit.Errors;

/// <summary>
/// Exception wrapping an <see cref="IbkrError"/>. Thrown by <see cref="Result{T}.EnsureSuccess"/>
/// and when <see cref="IbkrConduit.Session.IbkrClientOptions.ThrowOnApiError"/> is enabled.
/// Use pattern matching on <see cref="Error"/> to discriminate error subtypes.
/// </summary>
public class IbkrApiException : Exception
{
    /// <summary>The structured error details.</summary>
    public IbkrError Error { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/> wrapping the given error.
    /// </summary>
    /// <param name="error">The structured error.</param>
    public IbkrApiException(IbkrError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/> wrapping the given error with an inner exception.
    /// </summary>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    public IbkrApiException(IbkrError error, Exception innerException)
        : base(error.Message, innerException)
    {
        Error = error;
    }
}
```

- [ ] **Step 2: Delete old exception subclasses**

```
rm src/IbkrConduit/Errors/IbkrOrderRejectedException.cs
rm src/IbkrConduit/Errors/IbkrRateLimitException.cs
rm src/IbkrConduit/Errors/IbkrSessionException.cs
```

- [ ] **Step 3: Add ThrowOnApiError to IbkrClientOptions**

Add to `src/IbkrConduit/Session/IbkrClientOptions.cs`:

```csharp
/// <summary>
/// When true, facade methods call <see cref="Result{T}.EnsureSuccess"/> internally,
/// throwing <see cref="IbkrApiException"/> on API errors. Default false.
/// </summary>
public bool ThrowOnApiError { get; set; }
```

- [ ] **Step 4: Rewrite exception tests**

Replace `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs`:

```csharp
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrApiExceptionTests
{
    [Fact]
    public void Constructor_SetsErrorAndMessage()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad input", "{}", "/test");
        var ex = new IbkrApiException(error);
        ex.Error.ShouldBe(error);
        ex.Message.ShouldBe("bad input");
    }

    [Fact]
    public void Constructor_WithInnerException_SetsAll()
    {
        var error = new IbkrApiError(HttpStatusCode.InternalServerError, "fail", "", "/test");
        var inner = new InvalidOperationException("inner");
        var ex = new IbkrApiException(error, inner);
        ex.Error.ShouldBe(error);
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void PatternMatching_OnError_Works()
    {
        var error = new IbkrRateLimitError(HttpStatusCode.TooManyRequests, "slow", "", "/test", TimeSpan.FromSeconds(5));
        var ex = new IbkrApiException(error);
        var delay = ex.Error switch
        {
            IbkrRateLimitError rle => rle.RetryAfter,
            _ => null
        };
        delay.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
```

- [ ] **Step 5: Build to find all compilation errors from deleted exceptions**

Run: `dotnet build --configuration Release`
Expected: Compilation errors in handlers, operations, and tests referencing deleted types. Note all errors — they will be fixed in subsequent tasks.

- [ ] **Step 6: Commit (may not compile yet — foundation types only)**

```
git add -A
git commit -m "feat: simplify IbkrApiException, add ThrowOnApiError option, delete exception subclasses"
```

---

## Task 4: Create ResultFactory

**Files:**
- Create: `src/IbkrConduit/Errors/ResultFactory.cs`
- Move: `src/IbkrConduit/Http/IbkrErrorBody.cs` → `src/IbkrConduit/Errors/IbkrErrorBody.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Errors/ResultFactoryTests.cs`

- [ ] **Step 1: Move IbkrErrorBody to Errors namespace**

Move `src/IbkrConduit/Http/IbkrErrorBody.cs` to `src/IbkrConduit/Errors/IbkrErrorBody.cs` and update the namespace to `IbkrConduit.Errors`. Keep it `internal`.

- [ ] **Step 2: Write failing tests for ResultFactory**

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using IbkrConduit.Errors;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class ResultFactoryTests
{
    [Fact]
    public void FromResponse_Success_ReturnsSuccessResult()
    {
        var response = CreateApiResponse(HttpStatusCode.OK, "test-value", """{"field":"value"}""");
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("test-value");
    }

    [Fact]
    public void FromResponse_NonSuccess_ReturnsFailure()
    {
        var response = CreateApiResponse<string>(HttpStatusCode.BadRequest, null, """{"error":"bad input","statusCode":400}""");
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrApiError>();
        result.Error.Message.ShouldBe("bad input");
        result.Error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void FromResponse_429_ReturnsRateLimitError()
    {
        var response = CreateApiResponse<string>(HttpStatusCode.TooManyRequests, null, """{"error":"rate limited"}""", retryAfterSeconds: 30);
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        var rle = result.Error.ShouldBeOfType<IbkrRateLimitError>();
        rle.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void FromResponse_EmptyBody_ReturnsFailureWithStatusCode()
    {
        var response = CreateApiResponse<string>(HttpStatusCode.Unauthorized, null, "");
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        result.Error.RawBody.ShouldBe("");
    }

    [Fact]
    public void FromResponse_HtmlBody_ReturnsFailureWithRawBody()
    {
        var body = "<html><body><h1>Resource not found</h1></body></html>";
        var response = CreateApiResponse<string>(HttpStatusCode.NotFound, null, body);
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.RawBody.ShouldBe(body);
    }

    [Fact]
    public void FromResponse_200WithErrorBody_ReturnsHiddenError()
    {
        var response = CreateApiResponse<string>(HttpStatusCode.OK, null, """{"error":"something went wrong"}""");
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrHiddenError>();
        result.Error.Message.ShouldBe("something went wrong");
    }

    [Fact]
    public void FromResponse_200WithSuccessFalse_ReturnsHiddenError()
    {
        var response = CreateApiResponse<string>(HttpStatusCode.OK, null, """{"success":false,"failure_list":"validation failed"}""");
        var result = ResultFactory.FromResponse(response, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrHiddenError>();
    }

    [Fact]
    public void FromResponse_CustomParser_Success_UsesParser()
    {
        var rawResponse = CreateStringApiResponse(HttpStatusCode.OK, """{"order_id":"123"}""");
        var result = ResultFactory.FromResponse(rawResponse, body => $"parsed:{body}", "/test");
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldStartWith("parsed:");
    }

    [Fact]
    public void FromResponse_CustomParser_NonSuccess_ReturnsFailure()
    {
        var rawResponse = CreateStringApiResponse(HttpStatusCode.InternalServerError, """{"error":"server error"}""");
        var result = ResultFactory.FromResponse(rawResponse, body => body, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldBe("server error");
    }

    // Helper to create mock IApiResponse<T>
    private static IApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T? content, string body, int? retryAfterSeconds = null)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
        httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (retryAfterSeconds.HasValue)
        {
            httpResponse.Headers.Add("Retry-After", retryAfterSeconds.Value.ToString());
        }
        return new ApiResponse<T>(httpResponse, content, new RefitSettings(), null);
    }

    private static IApiResponse<string> CreateStringApiResponse(HttpStatusCode statusCode, string body)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
        httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var content = statusCode == HttpStatusCode.OK ? body : null;
        return new ApiResponse<string>(httpResponse, content, new RefitSettings(), null);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*ResultFactoryTests*"`
Expected: FAIL — `ResultFactory` doesn't exist

- [ ] **Step 4: Implement ResultFactory**

```csharp
using System.Net;
using System.Text.Json;
using Refit;

namespace IbkrConduit.Errors;

/// <summary>
/// Converts Refit <see cref="IApiResponse{T}"/> into <see cref="Result{T}"/>.
/// Handles all three IBKR error body formats: JSON error objects, empty bodies, and plain text/HTML.
/// </summary>
internal static class ResultFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Converts a Refit response with a pre-deserialized body into a Result.
    /// </summary>
    public static Result<T> FromResponse<T>(IApiResponse<T> response, string? requestPath = null)
    {
        var rawBody = response.Error?.Content ?? "";

        if (response.IsSuccessStatusCode)
        {
            if (response.Content is not null)
            {
                // Check for hidden errors in 200 OK responses
                var hiddenError = DetectHiddenError(rawBody, requestPath);
                if (hiddenError is not null)
                {
                    return Result<T>.Failure(hiddenError);
                }

                return Result<T>.Success(response.Content);
            }

            // 2xx but null content — unexpected
            return Result<T>.Failure(new IbkrApiError(
                response.StatusCode, "Response body could not be deserialized", rawBody, requestPath));
        }

        return Result<T>.Failure(ParseError(response.StatusCode, rawBody, response.Headers, requestPath));
    }

    /// <summary>
    /// Converts a Refit response with a raw string body into a Result using a custom parser.
    /// </summary>
    public static Result<T> FromResponse<T>(IApiResponse<string> response, Func<string, T> parser, string? requestPath = null)
    {
        var rawBody = response.Content ?? response.Error?.Content ?? "";

        if (response.IsSuccessStatusCode)
        {
            // Check for hidden errors in 200 OK responses
            var hiddenError = DetectHiddenError(rawBody, requestPath);
            if (hiddenError is not null)
            {
                return Result<T>.Failure(hiddenError);
            }

            return Result<T>.Success(parser(rawBody));
        }

        return Result<T>.Failure(ParseError(response.StatusCode, rawBody, response.Headers, requestPath));
    }

    private static IbkrError ParseError(
        HttpStatusCode statusCode, string rawBody,
        System.Net.Http.Headers.HttpResponseHeaders? headers, string? requestPath)
    {
        // 429 — rate limit
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            TimeSpan? retryAfter = null;
            if (headers?.RetryAfter?.Delta is not null)
            {
                retryAfter = headers.RetryAfter.Delta;
            }
            else if (headers is not null)
            {
                // Try raw header parsing
                if (headers.TryGetValues("Retry-After", out var values))
                {
                    var raw = values.FirstOrDefault();
                    if (raw is not null && int.TryParse(raw, out var seconds))
                    {
                        retryAfter = TimeSpan.FromSeconds(seconds);
                    }
                }
            }

            var msg = TryParseErrorMessage(rawBody);
            return new IbkrRateLimitError(statusCode, msg ?? "Rate limited", rawBody, requestPath, retryAfter);
        }

        // Try to parse JSON error body
        var errorMessage = TryParseErrorMessage(rawBody);
        return new IbkrApiError(statusCode, errorMessage, rawBody, requestPath);
    }

    private static IbkrError? DetectHiddenError(string rawBody, string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        var trimmed = rawBody.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return null;
        }

        IbkrErrorBody? errorBody;
        try
        {
            errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (errorBody is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(errorBody.Error))
        {
            return new IbkrHiddenError(errorBody.Error, rawBody, requestPath);
        }

        if (errorBody.Success == false)
        {
            return new IbkrHiddenError(errorBody.FailureList ?? "Operation failed", rawBody, requestPath);
        }

        return null;
    }

    private static string? TryParseErrorMessage(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            var errorBody = JsonSerializer.Deserialize<IbkrErrorBody>(rawBody, JsonOptions);
            return errorBody?.Error;
        }
        catch (JsonException)
        {
            return rawBody;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release --filter-class "*ResultFactoryTests*"`
Expected: PASS — 9 tests

- [ ] **Step 6: Commit**

```
git add -A
git commit -m "feat: add ResultFactory for IApiResponse to Result conversion"
```

---

## Task 5: Modify TokenRefreshHandler and remove ErrorNormalizationHandler + ResilienceHandler

**Files:**
- Modify: `src/IbkrConduit/Session/TokenRefreshHandler.cs`
- Delete: `src/IbkrConduit/Http/ErrorNormalizationHandler.cs`
- Delete: `src/IbkrConduit/Http/ResilienceHandler.cs`
- Modify: `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`
- Modify: `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs`
- Delete: `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs`
- Delete: `tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs`
- Delete: `tests/IbkrConduit.Tests.Integration/Pipeline/ErrorNormalizationTests.cs`
- Delete: `tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs`

- [ ] **Step 1: Modify TokenRefreshHandler — return response on repeated 401**

In `src/IbkrConduit/Session/TokenRefreshHandler.cs`, replace the block at lines 80-91 that throws `IbkrSessionException` on repeated 401:

Replace:
```csharp
        // If retry also returns 401, credentials are fundamentally invalid — do not loop
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            retryResponse.Dispose();
            throw new IbkrSessionException(
                false,
                null,
                HttpStatusCode.Unauthorized,
                "Re-authentication succeeded but request still unauthorized — credentials may be invalidated",
                null,
                request.RequestUri?.AbsolutePath);
        }
```

With:
```csharp
        // If retry also returns 401, this is likely IBKR returning 401 for a
        // non-auth reason (e.g., invalid account ID). Return the response as-is
        // so the facade can interpret it via ResultFactory.
```

Also update the re-auth failure catch block (lines 63-69) to throw `IbkrApiException` wrapping `IbkrSessionError` instead of `IbkrSessionException`:

Replace:
```csharp
        catch (Exception ex)
        {
            response.Dispose();
            throw new IbkrSessionException(
                "Re-authentication failed — credentials may be invalidated", ex);
        }
```

With:
```csharp
        catch (Exception ex)
        {
            response.Dispose();
            throw new IbkrApiException(
                new IbkrSessionError(
                    HttpStatusCode.Unauthorized,
                    "Re-authentication failed — credentials may be invalidated",
                    "",
                    request.RequestUri?.AbsolutePath,
                    false),
                ex);
        }
```

Update the using directives: remove `IbkrConduit.Errors` references to deleted types, add `using IbkrConduit.Errors;` if not present.

- [ ] **Step 2: Delete ErrorNormalizationHandler and ResilienceHandler**

```
rm src/IbkrConduit/Http/ErrorNormalizationHandler.cs
rm src/IbkrConduit/Http/ResilienceHandler.cs
```

- [ ] **Step 3: Remove handlers from ConsumerPipelineRegistration**

In `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`, remove:
- The `.AddHttpMessageHandler` call for `ErrorNormalizationHandler`
- The `.AddHttpMessageHandler` call for `ResilienceHandler`
- The `using Polly;` import
- Update the comment about pipeline order
- Update the consumer count comment from "8 consumer Refit clients" to reflect the simplified pipeline

- [ ] **Step 4: Remove Polly resilience pipeline from RateLimitingAndResilienceRegistration**

In `src/IbkrConduit/Http/RateLimitingAndResilienceRegistration.cs`, remove:
- The `ResiliencePipeline<HttpResponseMessage>` registration
- The `CreateResiliencePipeline` method
- The Polly using directives
- Keep the rate limiter registrations (global + endpoint)

- [ ] **Step 5: Delete handler test files**

```
rm tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs
rm tests/IbkrConduit.Tests.Unit/Http/ResilienceHandlerTests.cs
rm tests/IbkrConduit.Tests.Integration/Pipeline/ErrorNormalizationTests.cs
rm tests/IbkrConduit.Tests.Integration/Pipeline/ResilienceTests.cs
```

- [ ] **Step 6: Update TokenRefreshHandler tests**

In `tests/IbkrConduit.Tests.Unit/Session/TokenRefreshHandlerTests.cs`:
- Update the test that verifies repeated 401 behavior: assert it returns the 401 response instead of throwing
- Update the test that verifies re-auth failure: assert it throws `IbkrApiException` with `IbkrSessionError` instead of `IbkrSessionException`

- [ ] **Step 7: Build to check progress**

Run: `dotnet build --configuration Release`
Note remaining errors — should be mostly in operations implementations and integration tests referencing old exception types.

- [ ] **Step 8: Commit**

```
git add -A
git commit -m "refactor: remove ErrorNormalization/Resilience handlers, fix TokenRefreshHandler repeated 401"
```

---

## Task 6: Convert Refit interfaces to IApiResponse\<T\>

**Files:**
- Modify: All 8 Refit interface files listed in file map

- [ ] **Step 1: Convert all Refit interfaces**

For every method in every Refit interface (except `IIbkrSessionApi`), change:
- `Task<T>` → `Task<IApiResponse<T>>`
- `Task` (void returns like `InvalidatePortfolioCacheAsync`, `DeleteDeviceAsync`) → `Task<IApiResponse<HttpContent>>`

Add `using Refit;` to any file that doesn't already have it.

Note: `IIbkrOrderApi.ReplyAsync` already returns `Task<IApiResponse<string>>` — leave it as-is.

Do all 8 files in one step — this is a mechanical find-and-replace.

- [ ] **Step 2: Build to check progress**

Run: `dotnet build --configuration Release`
Expected: Operations implementations won't compile — they expect `T` not `IApiResponse<T>`. This is expected and will be fixed in the next task.

- [ ] **Step 3: Commit**

```
git add -A
git commit -m "refactor: convert all Refit interfaces to IApiResponse<T> return types"
```

---

## Task 7: Convert Operations interfaces and implementations

**Files:**
- Modify: All 8 Operations interface files
- Modify: All 8 Operations implementation files
- Modify: `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs` (inject IbkrClientOptions)

This is the largest task. Each operations class follows the same pattern:
1. Interface: `Task<T>` → `Task<Result<T>>`
2. Implementation: add `IbkrClientOptions` to constructor, use `ResultFactory.FromResponse`, apply `ThrowOnApiError`

- [ ] **Step 1: Convert all Operations interfaces**

For every method in every `I*Operations.cs` interface:
- `Task<T>` → `Task<Result<T>>`
- `Task` (void returns like `InvalidatePortfolioCacheAsync`) → `Task<Result<bool>>` (or appropriate sentinel)
- Order operations returning `OneOf<A, B>` → `Task<Result<OneOf<A, B>>>`
- Add `using IbkrConduit.Errors;` to each file

Note: `IFlexOperations` and `IStreamingOperations` are **not** changed.

- [ ] **Step 2: Convert all Operations implementations**

For each `*Operations.cs` implementation:

a. Add `IbkrClientOptions _options` to constructor (except `MarketDataOperations` which already has it).

b. Convert each method to the facade pattern:

```csharp
public async Task<Result<T>> MethodAsync(CancellationToken cancellationToken = default)
{
    using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Category.Method");
    var response = await _api.MethodAsync(cancellationToken);
    var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
    return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
}
```

For `OrderOperations` — the `PlaceOrderAsync`, `ModifyOrderAsync`, and `ReplyAsync` methods use the custom parser overload:

```csharp
var response = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
var result = ResultFactory.FromResponse(response, ParseOrderResponse, response.RequestMessage?.RequestUri?.AbsolutePath);
return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
```

The existing `ParseOrderResponse` private method (or equivalent) stays but is adapted to work as `Func<string, OneOf<OrderSubmitted, OrderConfirmationRequired>>`.

For void-returning Refit methods (like `InvalidatePortfolioCacheAsync`), the facade returns `Result<bool>`:

```csharp
var response = await _api.InvalidatePortfolioCacheAsync(accountId, cancellationToken);
var result = response.IsSuccessStatusCode
    ? Result<bool>.Success(true)
    : Result<bool>.Failure(ResultFactory.ParseError(response));
return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
```

- [ ] **Step 3: Update DI registration to inject IbkrClientOptions**

In `src/IbkrConduit/Http/ConsumerPipelineRegistration.cs`, update operations registrations:

```csharp
// Before
services.AddSingleton<IAccountOperations, AccountOperations>();

// After — operations that now need IbkrClientOptions
services.AddSingleton<IAccountOperations>(sp =>
    new AccountOperations(sp.GetRequiredService<IIbkrAccountApi>(), clientOptions));
```

Apply this pattern to all 8 operations registrations. `MarketDataOperations` already receives `IbkrClientOptions` — verify it's passed through correctly.

- [ ] **Step 4: Build to check progress**

Run: `dotnet build --configuration Release`
Expected: Core library should compile. Test projects will have errors from old return types and fake implementations.

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "refactor: convert all Operations to Result<T> return types with ResultFactory"
```

---

## Task 8: Update unit tests

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Client/IbkrClientTests.cs`
- Modify: All unit test files under `tests/IbkrConduit.Tests.Unit/*/` that reference old types
- Modify: `tests/IbkrConduit.Tests.Integration_Old/Orders/OrderManagementTests.cs`

- [ ] **Step 1: Update FakeOperations stubs in IbkrClientTests**

All Fake*Operations classes need updated return types: `Task<T>` → `Task<Result<T>>`. Since these are stubs that throw `NotImplementedException`, change them to:

```csharp
public Task<Result<IserverAccountsResponse>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();
```

- [ ] **Step 2: Update FakeOperations stubs in OrderManagementTests**

Same pattern as step 1.

- [ ] **Step 3: Update any unit tests that assert on old exception types**

Search for references to `IbkrSessionException`, `IbkrRateLimitException`, `IbkrOrderRejectedException` in test files and replace with `IbkrApiException` + pattern matching on `.Error`.

- [ ] **Step 4: Update operations unit tests**

Each `*OperationsTests.cs` file tests delegation behavior. These need to:
- Mock the Refit interface to return `IApiResponse<T>` instead of `T`
- Assert the result is `Result<T>.IsSuccess` instead of asserting the DTO directly

- [ ] **Step 5: Build and run unit tests**

Run: `dotnet build --configuration Release`
Run: `dotnet test --project tests/IbkrConduit.Tests.Unit --configuration Release`
Fix any remaining compilation or test failures.

- [ ] **Step 6: Commit**

```
git add -A
git commit -m "test: update unit tests for Result<T> return types"
```

---

## Task 9: Update integration tests

**Files:**
- Modify: All 7 category integration test files
- Modify: `tests/IbkrConduit.Tests.Integration/Pipeline/AuthFailureTests.cs`
- Delete or modify: `tests/IbkrConduit.Tests.Integration_Old/Http/ErrorNormalizationPipelineTests.cs`

- [ ] **Step 1: Update success test assertions**

In every integration test file, change:

```csharp
// Before
var result = await _harness.Client.Accounts.GetAccountsAsync(ct);
result.Accounts.ShouldNotBeEmpty();

// After
var result = await _harness.Client.Accounts.GetAccountsAsync(ct);
result.IsSuccess.ShouldBeTrue();
result.Value.Accounts.ShouldNotBeEmpty();
```

Apply to all success tests across all 7 category files.

- [ ] **Step 2: Update 401 recovery test assertions**

401 recovery tests verify that re-auth happens and the retry succeeds. The pattern stays the same but assertions change:

```csharp
// Before
var result = await _harness.Client.Accounts.GetAccountsAsync(ct);
result.Accounts.ShouldNotBeEmpty();

// After
var result = await _harness.Client.Accounts.GetAccountsAsync(ct);
result.IsSuccess.ShouldBeTrue();
result.Value.Accounts.ShouldNotBeEmpty();
```

The WireMock scenario setup stays identical — first call returns 401, handler re-auths, second call returns 200.

- [ ] **Step 3: Update AuthFailureTests**

These tests verify behavior when auth completely fails. Update to expect `IbkrApiException` with `IbkrSessionError` instead of `IbkrSessionException`.

- [ ] **Step 4: Handle Integration_Old error tests**

Delete or update `tests/IbkrConduit.Tests.Integration_Old/Http/ErrorNormalizationPipelineTests.cs` — the handler it tests no longer exists.

For `tests/IbkrConduit.Tests.Integration_Old/Http/RateLimitingAndResilienceTests.cs` — remove resilience-related tests, keep rate limiting tests.

- [ ] **Step 5: Build and run all integration tests**

Run: `dotnet build --configuration Release`
Run: `dotnet test --project tests/IbkrConduit.Tests.Integration --configuration Release`
Fix any remaining failures.

- [ ] **Step 6: Run full test suite**

Run: `dotnet test --configuration Release`
Verify unit tests and integration tests all pass. Integration_Old may have some pre-existing failures — note but don't block on them.

- [ ] **Step 7: Run format check**

Run: `dotnet format --verify-no-changes`

- [ ] **Step 8: Commit**

```
git add -A
git commit -m "test: update integration tests for Result<T> pattern"
```

---

## Task 10: Final cleanup and verification

- [ ] **Step 1: Remove any remaining references to deleted types**

Search for: `IbkrSessionException`, `IbkrRateLimitException`, `IbkrOrderRejectedException`, `ErrorNormalizationHandler`, `ResilienceHandler` across the entire codebase. Remove any stale using directives or references.

- [ ] **Step 2: Remove Polly package if no longer used**

Check if any code still references Polly. If not, remove from `Directory.Packages.props` and relevant `.csproj` files.

- [ ] **Step 3: Full build and test**

Run: `dotnet build --configuration Release`
Run: `dotnet test --configuration Release`
Run: `dotnet format --verify-no-changes`

- [ ] **Step 4: Final commit**

```
git add -A
git commit -m "chore: remove stale references and unused Polly dependency"
```
