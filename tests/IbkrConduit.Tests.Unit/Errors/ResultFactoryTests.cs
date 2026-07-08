using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Unit.TestHelpers;
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
        // Hidden errors in 200 OK are detected via the string overload (custom parser path)
        var response = CreateStringApiResponse(HttpStatusCode.OK, """{"error":"something went wrong"}""");
        var result = ResultFactory.FromResponse(response, body => body, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrHiddenError>();
        result.Error.Message.ShouldBe("something went wrong");
    }

    [Fact]
    public void FromResponse_200WithSuccessFalse_ReturnsHiddenError()
    {
        // Hidden errors in 200 OK are detected via the string overload (custom parser path)
        var response = CreateStringApiResponse(HttpStatusCode.OK, """{"success":false,"failure_list":"validation failed"}""");
        var result = ResultFactory.FromResponse(response, body => body, "/test");
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrHiddenError>();
    }

    [Fact]
    public void FromResponse_OrderMutating200WithErrorBody_ReturnsOrderRejectedError()
    {
        // ERR-4 / §9.9: a 200-with-error on an order-mutating endpoint classifies as the order-rejection
        // subtype, NOT the generic hidden-error subtype (the request path is known to be an order endpoint).
        var response = CreateStringApiResponse(HttpStatusCode.OK, """{"error":"We cannot accept an order at the limit price you selected."}""");
        var result = ResultFactory.FromResponse(response, body => body, "/v1/api/iserver/reply/abc", orderMutating: true);
        result.IsSuccess.ShouldBeFalse();
        var rejected = result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldContain("cannot accept an order");
    }

    [Fact]
    public void FromResponse_OrderMutating200WithSuccessFalse_ReturnsOrderRejectedError()
    {
        // ERR-4 / §9.9: the success:false shape on an order-mutating endpoint is also an order rejection.
        var response = CreateStringApiResponse(HttpStatusCode.OK, """{"success":false,"failure_list":"order refused"}""");
        var result = ResultFactory.FromResponse(response, body => body, "/v1/api/iserver/account/DU1/orders", orderMutating: true);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrOrderRejectedError>().RejectionMessage.ShouldBe("order refused");
    }

    [Fact]
    public void FromResponse_NonOrder200WithErrorBody_StillReturnsHiddenError()
    {
        // ERR-4 / §9.9: the generic hidden-error subtype is preserved for NON-order surfaces (default).
        var response = CreateStringApiResponse(HttpStatusCode.OK, """{"error":"something went wrong"}""");
        var result = ResultFactory.FromResponse(response, body => body, "/v1/api/iserver/account/DU1/alerts");
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

    [Fact]
    public void FromResponse_SendFailureCancellation_Propagates()
    {
        var response = FakeApiResponse.SendFailure<string>(new OperationCanceledException("cancelled"));
        Should.Throw<OperationCanceledException>(() => ResultFactory.FromResponse(response, "/test"));
    }

    [Fact]
    public void FromResponse_SendFailureSchemaViolation_PropagatesAsIbkrException()
    {
        var inner = new IbkrSchemaViolationException("/test", typeof(string), new[] { "extra" }, System.Array.Empty<string>());
        var response = FakeApiResponse.SendFailure<string>(inner);
        Should.Throw<IbkrSchemaViolationException>(() => ResultFactory.FromResponse(response, "/test"));
    }

    // Helper to create mock IApiResponse<T>
    private static async Task<IApiResponse<T>> CreateApiResponseAsync<T>(HttpStatusCode statusCode, T? content, string body, int? retryAfterSeconds = null)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
            // Refit 12's ApiResponse<T> constructor requires an associated request message.
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/test"),
        };
        httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (retryAfterSeconds.HasValue)
        {
            httpResponse.Headers.Add("Retry-After", retryAfterSeconds.Value.ToString());
        }

        // Create ApiException for non-success responses (Refit populates Error.Content with raw body)
        ApiException? exception = null;
        if (!httpResponse.IsSuccessStatusCode)
        {
            exception = await ApiException.Create(
                new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/test"),
                HttpMethod.Get,
                httpResponse,
                new RefitSettings());
        }

        return new ApiResponse<T>(httpResponse, content, new RefitSettings(), exception);
    }

    private static IApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T? content, string body, int? retryAfterSeconds = null) =>
        CreateApiResponseAsync(statusCode, content, body, retryAfterSeconds).GetAwaiter().GetResult();

    private static async Task<IApiResponse<string>> CreateStringApiResponseAsync(HttpStatusCode statusCode, string body)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
            // Refit 12's ApiResponse<T> constructor requires an associated request message.
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/test"),
        };
        httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var content = statusCode == HttpStatusCode.OK ? body : null;

        ApiException? exception = null;
        if (!httpResponse.IsSuccessStatusCode)
        {
            exception = await ApiException.Create(
                new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/test"),
                HttpMethod.Get,
                httpResponse,
                new RefitSettings());
        }

        return new ApiResponse<string>(httpResponse, content, new RefitSettings(), exception);
    }

    private static IApiResponse<string> CreateStringApiResponse(HttpStatusCode statusCode, string body) =>
        CreateStringApiResponseAsync(statusCode, body).GetAwaiter().GetResult();
}
