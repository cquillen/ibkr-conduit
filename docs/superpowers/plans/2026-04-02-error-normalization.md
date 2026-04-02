# Error Normalization Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Abstract IBKR's inconsistent error responses into a well-typed exception hierarchy and OneOf result types so consumers never see raw HTTP quirks.

**Architecture:** Single `ErrorNormalizationHandler` (DelegatingHandler) positioned between `TokenRefreshHandler` and `ResilienceHandler`. Shallow exception hierarchy (`IbkrApiException` base + 3 subclasses). `OneOf<OrderSubmitted, OrderConfirmationRequired>` result types on 3 order methods.

**Tech Stack:** C# / .NET, OneOf NuGet, Refit, xUnit v3, Shouldly, WireMock.Net, NSubstitute

**Spec:** `docs/superpowers/specs/2026-04-02-error-normalization-design.md`

---

### Task 1: Exception Hierarchy

**Files:**
- Create: `src/IbkrConduit/Errors/IbkrApiException.cs`
- Create: `src/IbkrConduit/Errors/IbkrRateLimitException.cs`
- Create: `src/IbkrConduit/Errors/IbkrSessionException.cs`
- Create: `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs`

- [ ] **Step 1: Write failing tests for all exception types**

Create `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs`:

```csharp
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrApiExceptionTests
{
    [Fact]
    public void IbkrApiException_CarriesAllProperties()
    {
        var ex = new IbkrApiException(
            HttpStatusCode.BadRequest, "bad param", """{"error":"bad param"}""", "/v1/api/test");

        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.ErrorMessage.ShouldBe("bad param");
        ex.RawResponseBody.ShouldBe("""{"error":"bad param"}""");
        ex.RequestUri.ShouldBe("/v1/api/test");
        ex.Message.ShouldContain("bad param");
    }

    [Fact]
    public void IbkrApiException_NullErrorMessage_StillConstructs()
    {
        var ex = new IbkrApiException(HttpStatusCode.InternalServerError, null, "<html>error</html>", "/v1/api/test");

        ex.ErrorMessage.ShouldBeNull();
        ex.RawResponseBody.ShouldBe("<html>error</html>");
    }

    [Fact]
    public void IbkrRateLimitException_InheritsFromBase()
    {
        var ex = new IbkrRateLimitException(
            TimeSpan.FromSeconds(5), "rate limited", """{"error":"rate limited"}""", "/v1/api/test");

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(5));
        ex.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void IbkrRateLimitException_NullRetryAfter_Allowed()
    {
        var ex = new IbkrRateLimitException(
            null, "rate limited", """{"error":"rate limited"}""", "/v1/api/test");

        ex.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public void IbkrSessionException_CarriesExtraProperties()
    {
        var ex = new IbkrSessionException(
            true, "competing session", HttpStatusCode.Unauthorized, "session dead",
            """{"authenticated":false}""", "/v1/api/iserver/auth/status");

        ex.IsCompeting.ShouldBeTrue();
        ex.Reason.ShouldBe("competing session");
        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void IbkrOrderRejectedException_CarriesRejectionMessage()
    {
        var ex = new IbkrOrderRejectedException(
            "insufficient funds", """{"error":"insufficient funds"}""", "/v1/api/iserver/account/DU123/orders");

        ex.RejectionMessage.ShouldBe("insufficient funds");
        ex.StatusCode.ShouldBe(HttpStatusCode.OK);
        ex.ShouldBeAssignableTo<IbkrApiException>();
    }

    [Fact]
    public void AllExceptions_CatchableAsIbkrApiException()
    {
        var exceptions = new IbkrApiException[]
        {
            new(HttpStatusCode.BadRequest, "test", "{}", "/test"),
            new IbkrRateLimitException(null, "test", "{}", "/test"),
            new IbkrSessionException(false, null, HttpStatusCode.Unauthorized, "test", "{}", "/test"),
            new IbkrOrderRejectedException("test", "{}", "/test"),
        };

        foreach (var ex in exceptions)
        {
            ex.ShouldBeAssignableTo<IbkrApiException>();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~IbkrApiExceptionTests"`
Expected: Build failure — `IbkrConduit.Errors` namespace does not exist.

- [ ] **Step 3: Implement the exception types**

Create `src/IbkrConduit/Errors/IbkrApiException.cs`:

```csharp
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Base exception for all IBKR API errors after normalization.
/// The <see cref="StatusCode"/> reflects the normalized (remapped) status, not the original.
/// </summary>
public class IbkrApiException : Exception
{
    /// <summary>The normalized HTTP status code (after remapping).</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Parsed error message from the response body, if available.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Full response body for diagnostics and logging.</summary>
    public string? RawResponseBody { get; }

    /// <summary>The request URI path that triggered the error.</summary>
    public string? RequestUri { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/>.
    /// </summary>
    public IbkrApiException(
        HttpStatusCode statusCode, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(errorMessage ?? $"IBKR API returned {(int)statusCode}")
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        RawResponseBody = rawResponseBody;
        RequestUri = requestUri;
    }
}
```

Create `src/IbkrConduit/Errors/IbkrRateLimitException.cs`:

```csharp
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when IBKR returns 429 Too Many Requests.
/// </summary>
public class IbkrRateLimitException : IbkrApiException
{
    /// <summary>Retry delay from the Retry-After header, if present.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrRateLimitException"/>.
    /// </summary>
    public IbkrRateLimitException(
        TimeSpan? retryAfter, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(HttpStatusCode.TooManyRequests, errorMessage, rawResponseBody, requestUri)
    {
        RetryAfter = retryAfter;
    }
}
```

Create `src/IbkrConduit/Errors/IbkrSessionException.cs`:

```csharp
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when the error indicates the session is dead and requires re-initialization.
/// </summary>
public class IbkrSessionException : IbkrApiException
{
    /// <summary>True if another session took over.</summary>
    public bool IsCompeting { get; }

    /// <summary>Reason from the auth status "fail" field, if available.</summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrSessionException"/>.
    /// </summary>
    public IbkrSessionException(
        bool isCompeting, string? reason,
        HttpStatusCode statusCode, string? errorMessage, string? rawResponseBody, string? requestUri)
        : base(statusCode, errorMessage, rawResponseBody, requestUri)
    {
        IsCompeting = isCompeting;
        Reason = reason;
    }
}
```

Create `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs`:

```csharp
using System.Net;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when an order is rejected by IBKR. These arrive as 200 OK with
/// <c>{"error": "..."}</c> in the body — the handler detects and throws this.
/// </summary>
public class IbkrOrderRejectedException : IbkrApiException
{
    /// <summary>The rejection reason from IBKR.</summary>
    public string RejectionMessage { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrOrderRejectedException"/>.
    /// </summary>
    public IbkrOrderRejectedException(
        string rejectionMessage, string? rawResponseBody, string? requestUri)
        : base(HttpStatusCode.OK, rejectionMessage, rawResponseBody, requestUri)
    {
        RejectionMessage = rejectionMessage;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~IbkrApiExceptionTests"`
Expected: All 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Errors/ tests/IbkrConduit.Tests.Unit/Errors/
git commit -m "feat: add IbkrApiException hierarchy for error normalization"
```

---

### Task 2: Error Detection Model and ErrorNormalizationHandler

**Files:**
- Create: `src/IbkrConduit/Http/IbkrErrorBody.cs`
- Create: `src/IbkrConduit/Http/ErrorNormalizationHandler.cs`
- Test: `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs`

- [ ] **Step 1: Write failing tests for the handler**

Create `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ErrorNormalizationHandlerTests
{
    private readonly ErrorNormalizationHandler _handler;

    public ErrorNormalizationHandlerTests()
    {
        _handler = new ErrorNormalizationHandler
        {
            InnerHandler = new TestInnerHandler()
        };
    }

    [Fact]
    public async Task NonSuccess_500OnMarketDataUnsubscribe_ThrowsNotFound()
    {
        SetResponse(HttpStatusCode.InternalServerError,
            """{"error":"unknown"}""",
            "/v1/api/iserver/marketdata/unsubscribe");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ex.ErrorMessage.ShouldBe("unknown");
    }

    [Fact]
    public async Task NonSuccess_500OnSsodhInit_ThrowsBadRequest()
    {
        SetResponse(HttpStatusCode.InternalServerError,
            """{"error":"invalid parameter"}""",
            "/v1/api/iserver/auth/ssodh/init");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonSuccess_500OnUnknownPath_ThrowsWithOriginalStatusCode()
    {
        SetResponse(HttpStatusCode.InternalServerError,
            """{"error":"something broke"}""",
            "/v1/api/portfolio/accounts");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task NonSuccess_503OnDynacct_ThrowsForbidden()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable,
            """{"error":"Details currently unavailable","statusCode":503}""",
            "/v1/api/iserver/dynaccount");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonSuccess_503OnAccountSearch_ThrowsForbidden()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable,
            """{"error":"Details currently unavailable","statusCode":503}""",
            "/v1/api/iserver/account/search/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonSuccess_503OnOrderStatus_ThrowsNotFound()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable,
            """{"error":"no data"}""",
            "/v1/api/iserver/account/order/status/12345");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonSuccess_503OnReply_ThrowsGone()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable,
            """{"error":"timeout"}""",
            "/v1/api/iserver/reply/abc-123");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task NonSuccess_429WithRetryAfterHeader_ThrowsRateLimitException()
    {
        SetResponse(HttpStatusCode.TooManyRequests,
            """{"error":"rate limited"}""",
            "/v1/api/iserver/marketdata/history",
            ("Retry-After", "30"));

        var ex = await Should.ThrowAsync<IbkrRateLimitException>(SendAsync);
        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        ex.ErrorMessage.ShouldBe("rate limited");
    }

    [Fact]
    public async Task NonSuccess_429WithoutRetryAfter_ThrowsRateLimitExceptionWithNullRetryAfter()
    {
        SetResponse(HttpStatusCode.TooManyRequests,
            """{"error":"too many requests"}""",
            "/v1/api/iserver/marketdata/history");

        var ex = await Should.ThrowAsync<IbkrRateLimitException>(SendAsync);
        ex.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task NonSuccess_401_ThrowsSessionException()
    {
        SetResponse(HttpStatusCode.Unauthorized,
            """{"error":"not authenticated"}""",
            "/v1/api/portfolio/accounts");

        var ex = await Should.ThrowAsync<IbkrSessionException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonSuccess_GenericError_ThrowsIbkrApiException()
    {
        SetResponse(HttpStatusCode.NotFound,
            """{"error":"not found"}""",
            "/v1/api/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ex.ErrorMessage.ShouldBe("not found");
        ex.RawResponseBody.ShouldBe("""{"error":"not found"}""");
        ex.RequestUri.ShouldBe("/v1/api/test");
    }

    [Fact]
    public async Task Success_200WithTopLevelError_ThrowsOrderRejectedException()
    {
        SetResponse(HttpStatusCode.OK,
            """{"error":"We cannot accept an order at the limit price"}""",
            "/v1/api/iserver/account/DU123/orders");

        var ex = await Should.ThrowAsync<IbkrOrderRejectedException>(SendAsync);
        ex.RejectionMessage.ShouldBe("We cannot accept an order at the limit price");
    }

    [Fact]
    public async Task Success_200WithSuccessFalse_ThrowsIbkrApiException()
    {
        SetResponse(HttpStatusCode.OK,
            """{"success":false,"failure_list":"Alert activation failed"}""",
            "/v1/api/iserver/account/DU123/alert/activate");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.ErrorMessage.ShouldBe("Alert activation failed");
    }

    [Fact]
    public async Task Success_200WithNullError_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            """{"error":null,"rules":{"someField":"value"}}""",
            "/v1/api/iserver/contract/rules");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithOrderConfirmation_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            """[{"id":"reply-abc","message":["Are you sure?"],"isSuppressed":false,"messageIds":["o163"]}]""",
            "/v1/api/iserver/account/DU123/orders");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithNormalBody_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            """[{"id":"DU1234567","accountId":"DU1234567"}]""",
            "/v1/api/portfolio/accounts");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithPlainText_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            "Success",
            "/v1/api/iserver/notification",
            contentType: "text/plain");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithEmptyBody_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            "",
            "/v1/api/fyi/deliveryoptions/device123");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BodyPreservedAfterInspection()
    {
        var originalBody = """[{"id":"DU1234567"}]""";
        SetResponse(HttpStatusCode.OK, originalBody, "/v1/api/portfolio/accounts");

        var response = await SendAsync();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe(originalBody);
    }

    [Fact]
    public async Task NonJsonErrorBody_ThrowsWithNullErrorMessage()
    {
        SetResponse(HttpStatusCode.InternalServerError,
            "<html>Internal Server Error</html>",
            "/v1/api/test",
            contentType: "text/html");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.ErrorMessage.ShouldBeNull();
        ex.RawResponseBody.ShouldBe("<html>Internal Server Error</html>");
    }

    [Fact]
    public async Task Success_200WithEmptyErrorString_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            """{"error":""}""",
            "/v1/api/iserver/account/DU123/orders");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithOrderSubmitted_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK,
            """[{"order_id":"12345","order_status":"Submitted"}]""",
            "/v1/api/iserver/account/DU123/orders");

        var response = await SendAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExceptionCarriesRequestUri()
    {
        SetResponse(HttpStatusCode.BadRequest,
            """{"error":"bad"}""",
            "/v1/api/some/endpoint?secret=hidden");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync);
        ex.RequestUri.ShouldBe("/v1/api/some/endpoint");
    }

    #region Test Infrastructure

    private HttpRequestMessage _request = new(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
    private HttpResponseMessage _response = new(HttpStatusCode.OK);

    private void SetResponse(HttpStatusCode statusCode, string body, string path,
        string contentType = "application/json")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        _request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ibkr.com{path}");
    }

    private void SetResponse(HttpStatusCode statusCode, string body, string path,
        (string Name, string Value) header, string contentType = "application/json")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        _response.Headers.TryAddWithoutValidation(header.Name, header.Value);
        _request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ibkr.com{path}");
    }

    private Task<HttpResponseMessage> SendAsync()
    {
        var invoker = new HttpMessageInvoker(_handler);
        return invoker.SendAsync(_request, TestContext.Current.CancellationToken);
    }

    private class TestInnerHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Options.TryGetValue(
                new HttpRequestOptionsKey<HttpResponseMessage>("test-response"), out var r)
                ? r : new HttpResponseMessage(HttpStatusCode.OK));
    }

    #endregion
}
```

Wait — the `TestInnerHandler` approach above won't work because we need the response to come from the inner handler. Let me use a simpler approach: a `DelegatingHandler` subclass that captures the response we want to return.

Replace the test infrastructure region with:

```csharp
    #region Test Infrastructure

    private HttpRequestMessage _request = new(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");

    private void SetResponse(HttpStatusCode statusCode, string body, string path,
        string contentType = "application/json")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        _request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ibkr.com{path}");
        UpdateInnerHandler();
    }

    private void SetResponse(HttpStatusCode statusCode, string body, string path,
        (string Name, string Value) header, string contentType = "application/json")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        _response.Headers.TryAddWithoutValidation(header.Name, header.Value);
        _request = new HttpRequestMessage(HttpMethod.Get, $"https://api.ibkr.com{path}");
        UpdateInnerHandler();
    }

    private void UpdateInnerHandler()
    {
        _handler.InnerHandler = new StubInnerHandler(_response);
    }

    private Task<HttpResponseMessage> SendAsync()
    {
        var invoker = new HttpMessageInvoker(_handler);
        return invoker.SendAsync(_request, TestContext.Current.CancellationToken);
    }

    private HttpResponseMessage _response = new(HttpStatusCode.OK);

    private class StubInnerHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    #endregion
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~ErrorNormalizationHandlerTests"`
Expected: Build failure — `ErrorNormalizationHandler` does not exist.

- [ ] **Step 3: Implement the error detection model**

Create `src/IbkrConduit/Http/IbkrErrorBody.cs`:

```csharp
using System.Text.Json.Serialization;

namespace IbkrConduit.Http;

/// <summary>
/// Internal model for detecting error patterns in IBKR API response bodies.
/// </summary>
internal record IbkrErrorBody(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("success")] bool? Success,
    [property: JsonPropertyName("failure_list")] string? FailureList,
    [property: JsonPropertyName("statusCode")] int? StatusCode);
```

- [ ] **Step 4: Implement the ErrorNormalizationHandler**

Create `src/IbkrConduit/Http/ErrorNormalizationHandler.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IbkrConduit.Errors;

namespace IbkrConduit.Http;

/// <summary>
/// Inspects IBKR API responses to normalize misused HTTP status codes and detect
/// error indicators in 200 OK response bodies. Positioned between TokenRefreshHandler
/// and ResilienceHandler so that the resilience layer sees corrected status codes.
/// </summary>
public class ErrorNormalizationHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var pathWithoutQuery = path;

        // Read body for inspection
        var body = response.Content is not null
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        if (!response.IsSuccessStatusCode)
        {
            HandleNonSuccess(response, body, pathWithoutQuery);
        }
        else
        {
            HandleSuccess(response, body, pathWithoutQuery);
        }

        // Replace content so downstream (Refit) can still read it
        if (response.Content is not null)
        {
            var contentType = response.Content.Headers.ContentType;
            response.Content = new StringContent(body, Encoding.UTF8);
            if (contentType is not null)
            {
                response.Content.Headers.ContentType = contentType;
            }
        }

        return response;
    }

    private static void HandleNonSuccess(
        HttpResponseMessage response, string body, string path)
    {
        var errorBody = TryParseErrorBody(body);
        var errorMessage = errorBody?.Error;
        var statusCode = response.StatusCode;

        statusCode = response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => throw CreateRateLimitException(response, errorMessage, body, path),
            HttpStatusCode.Unauthorized => throw new IbkrSessionException(
                false, null, HttpStatusCode.Unauthorized, errorMessage, body, path),
            HttpStatusCode.InternalServerError => RemapInternalServerError(path, statusCode),
            HttpStatusCode.ServiceUnavailable => RemapServiceUnavailable(path, statusCode),
            _ => statusCode,
        };

        throw new IbkrApiException(statusCode, errorMessage, body, path);
    }

    private static void HandleSuccess(
        HttpResponseMessage response, string body, string path)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var contentType = response.Content?.Headers.ContentType?.MediaType;
        if (contentType is not null && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Arrays pass through (order confirmations, normal list responses)
        var trimmed = body.AsSpan().Trim();
        if (trimmed.Length == 0 || trimmed[0] == '[')
        {
            return;
        }

        var errorBody = TryParseErrorBody(body);
        if (errorBody is null)
        {
            return;
        }

        // Top-level {"error": "non-null, non-empty string"} → order rejection
        if (!string.IsNullOrEmpty(errorBody.Error))
        {
            throw new IbkrOrderRejectedException(errorBody.Error, body, path);
        }

        // {"success": false} → alert/allocation failure
        if (errorBody.Success == false)
        {
            var failureMessage = errorBody.FailureList;
            throw new IbkrApiException(HttpStatusCode.BadRequest, failureMessage, body, path);
        }
    }

    private static HttpStatusCode RemapInternalServerError(string path, HttpStatusCode original)
    {
        if (path.Contains("/iserver/marketdata/unsubscribe", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.NotFound;
        }

        if (path.Contains("/iserver/auth/ssodh/init", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.BadRequest;
        }

        return original;
    }

    private static HttpStatusCode RemapServiceUnavailable(string path, HttpStatusCode original)
    {
        if (path.Contains("/iserver/dynaccount", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/iserver/account/search", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.Forbidden;
        }

        if (path.Contains("/iserver/account/order/status", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.NotFound;
        }

        if (path.Contains("/iserver/reply/", StringComparison.OrdinalIgnoreCase))
        {
            return HttpStatusCode.Gone;
        }

        return original;
    }

    private static IbkrRateLimitException CreateRateLimitException(
        HttpResponseMessage response, string? errorMessage, string body, string path)
    {
        TimeSpan? retryAfter = null;
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            retryAfter = delta;
        }
        else if (response.Headers.RetryAfter?.Date is { } date)
        {
            retryAfter = date - DateTimeOffset.UtcNow;
        }

        return new IbkrRateLimitException(retryAfter, errorMessage, body, path);
    }

    private static IbkrErrorBody? TryParseErrorBody(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<IbkrErrorBody>(body, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release --filter "FullyQualifiedName~ErrorNormalizationHandlerTests"`
Expected: All 22 tests pass.

- [ ] **Step 6: Run full test suite to check for regressions**

Run: `dotnet test tests/IbkrConduit.Tests.Unit --configuration Release`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/IbkrConduit/Http/IbkrErrorBody.cs src/IbkrConduit/Http/ErrorNormalizationHandler.cs tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs
git commit -m "feat: add ErrorNormalizationHandler with status code remapping and 200-error detection"
```

---

### Task 3: Wire ErrorNormalizationHandler into DI Pipeline

**Files:**
- Modify: `src/IbkrConduit/Http/ServiceCollectionExtensions.cs:199-227`
- Test: `tests/IbkrConduit.Tests.Integration/Http/ErrorNormalizationPipelineTests.cs`

- [ ] **Step 1: Write failing integration tests**

Create `tests/IbkrConduit.Tests.Integration/Http/ErrorNormalizationPipelineTests.cs`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Http;

public class ErrorNormalizationPipelineTests : IDisposable
{
    private readonly WireMockServer _server;

    public ErrorNormalizationPipelineTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task Pipeline_200WithErrorBody_ThrowsOrderRejectedException()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/DU123/orders").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"insufficient funds"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrOrderRejectedException>(async () =>
            await client.PostAsync(
                $"{_server.Url}/v1/api/iserver/account/DU123/orders",
                new StringContent("{}"),
                TestContext.Current.CancellationToken));

        ex.RejectionMessage.ShouldBe("insufficient funds");
    }

    [Fact]
    public async Task Pipeline_200WithConfirmation_PassesThrough()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/DU123/orders").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"abc","message":["confirm?"],"isSuppressed":false,"messageIds":["o163"]}]"""));

        using var client = CreatePipelinedClient();

        var response = await client.PostAsync(
            $"{_server.Url}/v1/api/iserver/account/DU123/orders",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pipeline_500Remapped_NotRetried()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/marketdata/unsubscribe").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"unknown"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrApiException>(async () =>
            await client.PostAsync(
                $"{_server.Url}/v1/api/iserver/marketdata/unsubscribe",
                new StringContent("{}"),
                TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // Should NOT have been retried — only 1 request should have been made
        _server.LogEntries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Pipeline_429_ThrowsRateLimitException()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/marketdata/history").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Retry-After", "60")
                    .WithBody("""{"error":"too many requests"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrRateLimitException>(async () =>
            await client.GetAsync(
                $"{_server.Url}/v1/api/iserver/marketdata/history",
                TestContext.Current.CancellationToken));

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
    }

    private HttpClient CreatePipelinedClient()
    {
        // Build a minimal pipeline: ErrorNormalizationHandler -> HttpClientHandler
        // No resilience handler here — we're testing normalization only
        var handler = new ErrorNormalizationHandler
        {
            InnerHandler = new HttpClientHandler()
        };
        return new HttpClient(handler);
    }

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass (handler already exists)**

Run: `dotnet test tests/IbkrConduit.Tests.Integration --configuration Release --filter "FullyQualifiedName~ErrorNormalizationPipelineTests"`
Expected: All 4 tests pass (handler code exists from Task 2).

- [ ] **Step 3: Wire handler into RegisterConsumerRefitClient**

Modify `src/IbkrConduit/Http/ServiceCollectionExtensions.cs`. In the `RegisterConsumerRefitClient<TApi>` method, add `ErrorNormalizationHandler` between `TokenRefreshHandler` and `ResilienceHandler`:

```csharp
    private static void RegisterConsumerRefitClient<TApi>(
        IServiceCollection services,
        IbkrOAuthCredentials credentials) where TApi : class
    {
        services.AddRefitClient<TApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(_ibkrBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler(sp =>
                new TokenRefreshHandler(
                    sp.GetRequiredService<ISessionManager>()))
            .AddHttpMessageHandler(_ =>
                new ErrorNormalizationHandler())
            .AddHttpMessageHandler(sp =>
                new ResilienceHandler(
                    sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>()))
            .AddHttpMessageHandler(sp =>
                new GlobalRateLimitingHandler(
                    sp.GetRequiredService<RateLimiter>()))
            .AddHttpMessageHandler(sp =>
                new EndpointRateLimitingHandler(
                    sp.GetRequiredService<IReadOnlyDictionary<string, RateLimiter>>()))
            .AddHttpMessageHandler(sp =>
                new OAuthSigningHandler(
                    sp.GetRequiredService<ISessionTokenProvider>(),
                    credentials.ConsumerKey,
                    credentials.AccessToken,
                    sp.GetRequiredService<ISessionManager>()));
    }
```

Also update the XML doc comment on `AddIbkrClient` (lines 38-41) to reflect the new pipeline order:

```csharp
    /// Consumer pipeline: Refit -> TokenRefreshHandler -> ErrorNormalizationHandler ->
    /// ResilienceHandler -> GlobalRateLimitingHandler -> EndpointRateLimitingHandler ->
    /// OAuthSigningHandler -> HttpClient.
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass (unit + integration). The existing tests should not break because the handler only throws on error conditions, and existing WireMock tests return clean 200 responses.

- [ ] **Step 5: Commit**

```bash
git add src/IbkrConduit/Http/ServiceCollectionExtensions.cs tests/IbkrConduit.Tests.Integration/Http/ErrorNormalizationPipelineTests.cs
git commit -m "feat: wire ErrorNormalizationHandler into consumer DI pipeline"
```

---

### Task 4: Add OneOf Dependency and Order Result Types

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/IbkrConduit/IbkrConduit.csproj`
- Create: `src/IbkrConduit/Orders/OrderSubmitted.cs`
- Create: `src/IbkrConduit/Orders/OrderConfirmationRequired.cs`
- Modify: `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` (remove `OrderResult`)

- [ ] **Step 1: Add OneOf package version to Directory.Packages.props**

Add to the `<!-- HTTP client and DI -->` section of `Directory.Packages.props`:

```xml
    <!-- Result types -->
    <PackageVersion Include="OneOf" Version="3.0.271" />
```

- [ ] **Step 2: Add OneOf PackageReference to IbkrConduit.csproj**

Add to the `<ItemGroup>` containing other PackageReferences:

```xml
    <PackageReference Include="OneOf" />
```

- [ ] **Step 3: Create OrderSubmitted record**

Create `src/IbkrConduit/Orders/OrderSubmitted.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Orders;

/// <summary>
/// Confirms the order was accepted by IBKR.
/// </summary>
/// <param name="OrderId">The IBKR order identifier.</param>
/// <param name="OrderStatus">The status of the placed order (e.g., "Submitted", "PreSubmitted").</param>
[ExcludeFromCodeCoverage]
public sealed record OrderSubmitted(string OrderId, string OrderStatus);
```

- [ ] **Step 4: Create OrderConfirmationRequired record**

Create `src/IbkrConduit/Orders/OrderConfirmationRequired.cs`:

```csharp
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Orders;

/// <summary>
/// IBKR requires confirmation before proceeding with the order.
/// Caller must decide whether to confirm via <c>ReplyAsync</c>.
/// </summary>
/// <param name="ReplyId">The identifier to pass to ReplyAsync.</param>
/// <param name="Messages">Warning messages from IBKR explaining why confirmation is needed.</param>
/// <param name="MessageIds">IBKR message type identifiers (e.g., "o163", "o354").</param>
[ExcludeFromCodeCoverage]
public sealed record OrderConfirmationRequired(
    string ReplyId,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> MessageIds);
```

- [ ] **Step 5: Remove OrderResult from IIbkrOrderApiModels.cs**

Remove the `OrderResult` record (lines 41-47 of `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs`):

```csharp
// DELETE these lines:
/// <summary>
/// Successful order placement result.
/// </summary>
/// <param name="OrderId">The IBKR order identifier.</param>
/// <param name="OrderStatus">The status of the placed order.</param>
[ExcludeFromCodeCoverage]
public record OrderResult(string OrderId, string OrderStatus);
```

- [ ] **Step 6: Verify build compiles (expect failures in OrderOperations and tests)**

Run: `dotnet build src/IbkrConduit --configuration Release`
Expected: Build failures referencing `OrderResult` in `OrderOperations.cs` and `IOrderOperations.cs`. This is expected — we fix those in Task 5.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/IbkrConduit/IbkrConduit.csproj src/IbkrConduit/Orders/OrderSubmitted.cs src/IbkrConduit/Orders/OrderConfirmationRequired.cs src/IbkrConduit/Orders/IIbkrOrderApiModels.cs
git commit -m "feat: add OneOf dependency and order result types (OrderSubmitted, OrderConfirmationRequired)"
```

---

### Task 5: Update IOrderOperations Interface and OrderOperations Implementation

**Files:**
- Modify: `src/IbkrConduit/Client/IOrderOperations.cs`
- Modify: `src/IbkrConduit/Client/OrderOperations.cs`

- [ ] **Step 1: Update IOrderOperations interface**

Replace the full contents of `src/IbkrConduit/Client/IOrderOperations.cs`:

```csharp
using IbkrConduit.Orders;
using OneOf;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations on the IBKR API.
/// </summary>
public interface IOrderOperations
{
    /// <summary>
    /// Places an order for the specified account. Returns either a confirmed submission
    /// or a confirmation-required response that the caller must handle via <see cref="ReplyAsync"/>.
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves live orders for the current session.
    /// </summary>
    Task<List<LiveOrder>> GetLiveOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed trades for the current session.
    /// </summary>
    Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies an existing order. Returns either a confirmed submission
    /// or a confirmation-required response that the caller must handle via <see cref="ReplyAsync"/>.
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replies to an order confirmation question. Returns either a confirmed submission
    /// or another confirmation-required response (IBKR can chain confirmations).
    /// </summary>
    Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the commission and margin impact of an order without placing it.
    /// </summary>
    Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed status for a single order.
    /// </summary>
    Task<OrderStatus> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Update OrderOperations implementation**

Replace the full contents of `src/IbkrConduit/Client/OrderOperations.cs`:

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using IbkrConduit.Diagnostics;
using IbkrConduit.Orders;
using Microsoft.Extensions.Logging;
using OneOf;

namespace IbkrConduit.Client;

/// <summary>
/// Order management operations with OneOf result types for polymorphic IBKR responses.
/// Uses per-account semaphore serialization to prevent concurrent order submissions.
/// </summary>
public partial class OrderOperations : IOrderOperations
{
    private static readonly Histogram<double> _submissionDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.order.submission.duration", "ms");

    private static readonly Counter<long> _submissionCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.submission.count");

    private static readonly Counter<long> _cancelCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.cancel.count");

    private static readonly Counter<long> _questionCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.order.question.count");

    private readonly IIbkrOrderApi _orderApi;
    private readonly ILogger<OrderOperations> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

    /// <summary>
    /// Creates a new <see cref="OrderOperations"/> instance.
    /// </summary>
    /// <param name="orderApi">The Refit order API client.</param>
    /// <param name="logger">The logger instance.</param>
    public OrderOperations(IIbkrOrderApi orderApi, ILogger<OrderOperations> logger)
    {
        _orderApi = orderApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> PlaceOrderAsync(
        string accountId, OrderRequest order, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Place");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);
        activity?.SetTag(LogFields.Side, order.Side);
        activity?.SetTag(LogFields.OrderType, order.OrderType);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            var wireModel = ToWireModel(order);
            var payload = new OrdersPayload([wireModel]);
            var responses = await _orderApi.PlaceOrderAsync(accountId, payload, cancellationToken);
            var result = ClassifyResponse(responses[0]);

            _submissionDuration.Record(sw.Elapsed.TotalMilliseconds);
            _submissionCount.Add(1,
                new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CancelOrderResponse> CancelOrderAsync(
        string accountId, string orderId, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Cancel");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.OrderId, orderId);
        _cancelCount.Add(1);
        return await _orderApi.CancelOrderAsync(accountId, orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<LiveOrder>> GetLiveOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetLiveOrders");
        var response = await _orderApi.GetLiveOrdersAsync(cancellationToken);
        return response.Orders ?? [];
    }

    /// <inheritdoc />
    public async Task<List<Trade>> GetTradesAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetTrades");
        return await _orderApi.GetTradesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ModifyOrderAsync(
        string accountId, string orderId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Modify");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.OrderId, orderId);
        activity?.SetTag(LogFields.Conid, order.Conid);
        activity?.SetTag(LogFields.Side, order.Side);
        activity?.SetTag(LogFields.OrderType, order.OrderType);

        var semaphore = _accountLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            var wireModel = ToWireModel(order);
            var payload = new OrdersPayload([wireModel]);
            var responses = await _orderApi.ModifyOrderAsync(accountId, orderId, payload, cancellationToken);
            var result = ClassifyResponse(responses[0]);

            _submissionDuration.Record(sw.Elapsed.TotalMilliseconds);
            _submissionCount.Add(1,
                new KeyValuePair<string, object?>(LogFields.Side, order.Side),
                new KeyValuePair<string, object?>(LogFields.OrderType, order.OrderType));
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<OneOf<OrderSubmitted, OrderConfirmationRequired>> ReplyAsync(
        string replyId, bool confirmed, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.Reply");
        activity?.SetTag("replyId", replyId);
        activity?.SetTag("confirmed", confirmed);

        _questionCount.Add(1);
        LogReplyAttempt(replyId, confirmed);

        var replyApiResponse = await _orderApi.ReplyAsync(
            replyId, new ReplyRequest(confirmed), cancellationToken);

        if (!replyApiResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"IBKR reply endpoint returned HTTP {(int)replyApiResponse.StatusCode}: {replyApiResponse.Error?.Content}");
        }

        LogReplyRawContent(replyApiResponse.Content ?? string.Empty);
        var replyResponses = DeserializeReplyResponse(replyApiResponse.Content!);
        return ClassifyResponse(replyResponses[0]);
    }

    /// <inheritdoc />
    public async Task<WhatIfResponse> WhatIfOrderAsync(
        string accountId, OrderRequest order,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.WhatIf");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag(LogFields.Conid, order.Conid);

        var wireModel = ToWireModel(order);
        var payload = new OrdersPayload([wireModel]);
        return await _orderApi.WhatIfOrderAsync(accountId, payload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderStatus> GetOrderStatusAsync(
        string orderId, CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Order.GetStatus");
        activity?.SetTag(LogFields.OrderId, orderId);
        return await _orderApi.GetOrderStatusAsync(orderId, cancellationToken);
    }

    /// <summary>
    /// Classifies an IBKR order submission response into either OrderSubmitted or OrderConfirmationRequired.
    /// </summary>
    internal static OneOf<OrderSubmitted, OrderConfirmationRequired> ClassifyResponse(
        OrderSubmissionResponse response)
    {
        if (response.OrderId is not null)
        {
            return new OrderSubmitted(response.OrderId, response.OrderStatus ?? string.Empty);
        }

        if (response.Id is not null && response.Message is not null)
        {
            return new OrderConfirmationRequired(
                response.Id,
                response.Message.AsReadOnly(),
                (response.MessageIds ?? []).AsReadOnly());
        }

        throw new InvalidOperationException(
            "Unexpected order submission response: no order ID and no question message.");
    }

    /// <summary>
    /// Deserializes an IBKR reply response that may be either a JSON array or a bare JSON object.
    /// </summary>
    internal static List<OrderSubmissionResponse> DeserializeReplyResponse(string content)
    {
        var trimmed = content.AsSpan().Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException(
                "IBKR reply endpoint returned an empty response body.");
        }

        if (trimmed[0] == '[')
        {
            return JsonSerializer.Deserialize<List<OrderSubmissionResponse>>(trimmed)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as array: {content}");
        }

        if (trimmed[0] == '{')
        {
            var single = JsonSerializer.Deserialize<OrderSubmissionResponse>(trimmed)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize IBKR reply response as object: {content}");
            return [single];
        }

        throw new InvalidOperationException(
            $"IBKR reply endpoint returned unexpected content: {content}");
    }

    private static OrderWireModel ToWireModel(OrderRequest order) =>
        new(order.Conid, order.Side, order.Quantity, order.OrderType,
            order.Price, order.AuxPrice, order.Tif, order.ManualIndicator);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replying to IBKR order question {ReplyId} with confirmed={Confirmed}")]
    private partial void LogReplyAttempt(string replyId, bool confirmed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "IBKR reply raw content: {Content}")]
    private partial void LogReplyRawContent(string content);
}
```

- [ ] **Step 3: Verify build compiles**

Run: `dotnet build src/IbkrConduit --configuration Release`
Expected: Build succeeds. Test projects will still fail because they reference `OrderResult` and the old signatures.

- [ ] **Step 4: Commit**

```bash
git add src/IbkrConduit/Client/IOrderOperations.cs src/IbkrConduit/Client/OrderOperations.cs
git commit -m "feat: update IOrderOperations to use OneOf result types and expose ReplyAsync"
```

---

### Task 6: Update Existing Order Tests for New Signatures

**Files:**
- Modify: `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsModifyTests.cs`
- Modify: `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsReplyTests.cs`

This task updates existing tests to work with the new `OneOf<OrderSubmitted, OrderConfirmationRequired>` return types. The `FakeOrderApi` stays the same since the Refit interface is unchanged — only the assertions on `OrderOperations` return values change.

- [ ] **Step 1: Update OrderOperationsTests.cs**

In all tests that call `PlaceOrderAsync` and assert on `result.OrderId`, change the assertion pattern from:

```csharp
var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);
result.OrderId.ShouldBe("12345");
result.OrderStatus.ShouldBe("PreSubmitted");
```

to:

```csharp
var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);
var submitted = result.AsT0;
submitted.OrderId.ShouldBe("12345");
submitted.OrderStatus.ShouldBe("PreSubmitted");
```

Apply this pattern to these tests:
- `PlaceOrderAsync_DirectConfirmation_ReturnsOrderResult` → rename to `PlaceOrderAsync_DirectConfirmation_ReturnsOrderSubmitted`
- `PlaceOrderAsync_WithQuestion_AutoConfirmsAndReturnsOrderResult` → this test's behavior changes. Since `PlaceOrderAsync` no longer auto-confirms, a question response should return `OrderConfirmationRequired`. Change:

```csharp
    [Fact]
    public async Task PlaceOrderAsync_WithQuestionResponse_ReturnsOrderConfirmationRequired()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                ["Are you sure you want to submit this order?"],
                false,
                ["msg-id-1"],
                null,
                null),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 50,
            OrderType = "LMT",
            Price = 150.00m,
            Tif = "GTC",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);
        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("reply-id-1");
        confirmation.Messages.ShouldContain("Are you sure you want to submit this order?");
        confirmation.MessageIds.ShouldContain("msg-id-1");
    }
```

Remove these tests that tested auto-confirm loop behavior (no longer applicable — the loop is removed):
- `PlaceOrderAsync_MultipleQuestions_ConfirmsAllAndReturnsOrderResult`
- `PlaceOrderAsync_ExceedsMaxIterations_ThrowsInvalidOperationException`
- `PlaceOrderAsync_ReplyThrows_ExceptionPropagates`
- `PlaceOrderAsync_EmptyMessageArray_EntersQuestionBranch`

Update `PlaceOrderAsync_OrderIdPresent_IgnoresMessageField`:

```csharp
    [Fact]
    public async Task PlaceOrderAsync_OrderIdPresent_IgnoresMessageField()
    {
        _fakeApi.PlaceOrderResponses.Enqueue(
        [
            new OrderSubmissionResponse(
                "reply-id-1",
                ["Some question"],
                false,
                ["msg-id-1"],
                "77777",
                "PreSubmitted"),
        ]);

        var order = new OrderRequest
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "MKT",
        };

        var result = await _sut.PlaceOrderAsync("DU1234567", order, TestContext.Current.CancellationToken);
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("77777");
        submitted.OrderStatus.ShouldBe("PreSubmitted");
    }
```

Update `PlaceOrderAsync_DifferentAccounts_RunInParallel`:

```csharp
        var results = await Task.WhenAll(task1, task2);
        results[0].AsT0.OrderId.ShouldBe("order-ACCT1");
        results[1].AsT0.OrderId.ShouldBe("order-ACCT2");
```

Update `PlaceOrderAsync_SerializesPerAccount` — the `BlockingOrderApi` results use `OrderSubmitted` via `ClassifyResponse`, so the return type changes. Assertions stay the same shape since we just verify call ordering.

Update `PlaceOrderAsync_MessagePresentButIdNull_ThrowsInvalidOperation` — this test remains valid as `ClassifyResponse` still throws.

- [ ] **Step 2: Update OrderOperationsModifyTests.cs**

Apply the same `AsT0` / `AsT1` assertion pattern to modify tests. Read the file first to identify exact tests.

- [ ] **Step 3: Add tests for ClassifyResponse and ReplyAsync**

Add to `OrderOperationsReplyTests.cs` or a new test file:

```csharp
    [Fact]
    public void ClassifyResponse_OrderIdPresent_ReturnsOrderSubmitted()
    {
        var response = new OrderSubmissionResponse(null, null, null, null, "12345", "Submitted");
        var result = OrderOperations.ClassifyResponse(response);
        var submitted = result.AsT0;
        submitted.OrderId.ShouldBe("12345");
        submitted.OrderStatus.ShouldBe("Submitted");
    }

    [Fact]
    public void ClassifyResponse_QuestionPresent_ReturnsOrderConfirmationRequired()
    {
        var response = new OrderSubmissionResponse(
            "reply-1", ["Are you sure?"], false, ["o163"], null, null);
        var result = OrderOperations.ClassifyResponse(response);
        var confirmation = result.AsT1;
        confirmation.ReplyId.ShouldBe("reply-1");
        confirmation.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public void ClassifyResponse_NeitherPresent_Throws()
    {
        var response = new OrderSubmissionResponse(null, null, null, null, null, null);
        Should.Throw<InvalidOperationException>(
            () => OrderOperations.ClassifyResponse(response));
    }
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/IbkrConduit.Tests.Unit/Orders/
git commit -m "test: update order tests for OneOf return types and ClassifyResponse"
```

---

### Task 7: Update Remaining Consumers and Run Full Validation

**Files:**
- Modify: Any E2E or integration tests that reference `OrderResult` or use the old `PlaceOrderAsync` signature
- Verify: `dotnet build --configuration Release`
- Verify: `dotnet test --configuration Release`
- Verify: `dotnet format --verify-no-changes`

- [ ] **Step 1: Search for remaining references to OrderResult**

Run: `grep -r "OrderResult" --include="*.cs" .` to find any remaining usages across the codebase.

- [ ] **Step 2: Fix any remaining references**

Update any files found in Step 1 to use `OrderSubmitted` instead of `OrderResult`. E2E tests that call `PlaceOrderAsync` need to handle the `OneOf` result.

- [ ] **Step 3: Run full build**

Run: `dotnet build --configuration Release`
Expected: Zero warnings, zero errors.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests pass.

- [ ] **Step 5: Run lint**

Run: `dotnet format --verify-no-changes`
Expected: No formatting issues.

- [ ] **Step 6: Commit any remaining fixes**

```bash
git add -A
git commit -m "chore: fix remaining OrderResult references after OneOf migration"
```

---

## File Structure Summary

### New Files
| File | Responsibility |
|---|---|
| `src/IbkrConduit/Errors/IbkrApiException.cs` | Base exception |
| `src/IbkrConduit/Errors/IbkrRateLimitException.cs` | Rate limit exception |
| `src/IbkrConduit/Errors/IbkrSessionException.cs` | Session death exception |
| `src/IbkrConduit/Errors/IbkrOrderRejectedException.cs` | Order rejection exception |
| `src/IbkrConduit/Http/IbkrErrorBody.cs` | Internal error body detection model |
| `src/IbkrConduit/Http/ErrorNormalizationHandler.cs` | DelegatingHandler for error normalization |
| `src/IbkrConduit/Orders/OrderSubmitted.cs` | Order accepted result type |
| `src/IbkrConduit/Orders/OrderConfirmationRequired.cs` | Confirmation required result type |
| `tests/IbkrConduit.Tests.Unit/Errors/IbkrApiExceptionTests.cs` | Exception hierarchy tests |
| `tests/IbkrConduit.Tests.Unit/Http/ErrorNormalizationHandlerTests.cs` | Handler unit tests |
| `tests/IbkrConduit.Tests.Integration/Http/ErrorNormalizationPipelineTests.cs` | WireMock pipeline tests |

### Modified Files
| File | Change |
|---|---|
| `Directory.Packages.props` | Add OneOf version |
| `src/IbkrConduit/IbkrConduit.csproj` | Add OneOf reference |
| `src/IbkrConduit/Http/ServiceCollectionExtensions.cs` | Insert handler in pipeline |
| `src/IbkrConduit/Client/IOrderOperations.cs` | OneOf return types, add ReplyAsync |
| `src/IbkrConduit/Client/OrderOperations.cs` | Implement new signatures, remove auto-confirm loop |
| `src/IbkrConduit/Orders/IIbkrOrderApiModels.cs` | Remove OrderResult record |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsTests.cs` | Update for OneOf assertions |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsModifyTests.cs` | Update for OneOf assertions |
| `tests/IbkrConduit.Tests.Unit/Orders/OrderOperationsReplyTests.cs` | Add ClassifyResponse tests |
