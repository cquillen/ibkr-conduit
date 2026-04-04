using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ErrorNormalizationHandlerTests
{
    private readonly ErrorNormalizationHandler _handler = new();
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
        _handler.InnerHandler = new StubInnerHandler(_response);
    }

    private void SetResponse(HttpStatusCode statusCode, string body, string path,
        (string Name, string Value) header, string contentType = "application/json")
    {
        SetResponse(statusCode, body, path, contentType);
        _response.Headers.TryAddWithoutValidation(header.Name, header.Value);
    }

    private Task<HttpResponseMessage> SendAsync()
    {
        var invoker = new HttpMessageInvoker(_handler);
        return invoker.SendAsync(_request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NonSuccess_500OnMarketDataUnsubscribe_ThrowsNotFound()
    {
        SetResponse(HttpStatusCode.InternalServerError, """{"error":"not found"}""",
            "/v1/api/iserver/marketdata/unsubscribe");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonSuccess_500OnSsodhInit_ThrowsBadRequest()
    {
        SetResponse(HttpStatusCode.InternalServerError, """{"error":"bad request"}""",
            "/v1/api/iserver/auth/ssodh/init");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonSuccess_500OnUnknownPath_ThrowsWithOriginalStatusCode()
    {
        SetResponse(HttpStatusCode.InternalServerError, """{"error":"server error"}""",
            "/v1/api/portfolio/accounts");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task NonSuccess_503OnDynacct_ThrowsForbidden()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable, """{"error":"forbidden"}""",
            "/v1/api/iserver/dynaccount");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonSuccess_503OnAccountSearch_ThrowsForbidden()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable, """{"error":"forbidden"}""",
            "/v1/api/iserver/account/search/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonSuccess_503OnOrderStatus_ThrowsNotFound()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable, """{"error":"not found"}""",
            "/v1/api/iserver/account/order/status/12345");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonSuccess_503OnReply_ThrowsGone()
    {
        SetResponse(HttpStatusCode.ServiceUnavailable, """{"error":"gone"}""",
            "/v1/api/iserver/reply/abc-123");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task NonSuccess_429WithRetryAfterHeader_ThrowsWithRateLimitError()
    {
        SetResponse(HttpStatusCode.TooManyRequests, """{"error":"rate limited"}""",
            "/v1/api/test", ("Retry-After", "30"));

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        var rle = ex.Error.ShouldBeOfType<IbkrRateLimitError>();
        rle.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task NonSuccess_429WithoutRetryAfter_ThrowsWithRateLimitErrorNullRetryAfter()
    {
        SetResponse(HttpStatusCode.TooManyRequests, """{"error":"rate limited"}""",
            "/v1/api/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        var rle = ex.Error.ShouldBeOfType<IbkrRateLimitError>();
        rle.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task NonSuccess_401_PassesThrough()
    {
        // 401 passes through ErrorNormalizationHandler so TokenRefreshHandler
        // (upstream) can intercept it and trigger re-authentication.
        SetResponse(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""",
            "/v1/api/test");

        var response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonSuccess_GenericError_ThrowsIbkrApiException()
    {
        SetResponse(HttpStatusCode.NotFound, """{"error":"not found"}""",
            "/v1/api/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Success_200WithTopLevelError_ThrowsWithOrderRejectedError()
    {
        SetResponse(HttpStatusCode.OK, """{"error":"insufficient funds"}""",
            "/v1/api/iserver/account/orders");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        var ore = ex.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        ore.RejectionMessage.ShouldBe("insufficient funds");
    }

    [Fact]
    public async Task Success_200WithSuccessFalse_ThrowsIbkrApiException()
    {
        SetResponse(HttpStatusCode.OK, """{"success":false,"failure_list":"reason"}""",
            "/v1/api/test");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ex.Error.Message.ShouldBe("reason");
    }

    [Fact]
    public async Task Success_200WithNullError_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, """{"error":null}""",
            "/v1/api/test");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithOrderConfirmation_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, """[{"id":"abc","message":["confirm?"]}]""",
            "/v1/api/iserver/account/orders");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithNormalBody_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, """[{"id":"DU123"}]""",
            "/v1/api/portfolio/accounts");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithPlainText_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, "Success",
            "/v1/api/test", contentType: "text/plain");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithEmptyBody_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, "",
            "/v1/api/test");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ContentTypePreservedAfterInspection()
    {
        SetResponse(HttpStatusCode.OK,
            """[{"id":"DU1234567"}]""",
            "/v1/api/portfolio/accounts");

        var response = await SendAsync();
        var contentType = response.Content.Headers.ContentType;
        contentType.ShouldNotBeNull();
        contentType.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task BodyPreservedAfterInspection()
    {
        var body = """[{"id":"DU123"}]""";
        SetResponse(HttpStatusCode.OK, body, "/v1/api/portfolio/accounts");

        var result = await SendAsync();

        var content = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldBe(body);
    }

    [Fact]
    public async Task NonJsonErrorBody_ThrowsWithNullErrorMessage()
    {
        SetResponse(HttpStatusCode.InternalServerError, "<html>Server Error</html>",
            "/v1/api/test", contentType: "text/html");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.Message.ShouldBeNull();
        ex.Error.RawBody.ShouldBe("<html>Server Error</html>");
    }

    [Fact]
    public async Task Success_200WithEmptyErrorString_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, """{"error":""}""",
            "/v1/api/test");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_200WithOrderSubmitted_PassesThrough()
    {
        SetResponse(HttpStatusCode.OK, """[{"order_id":"12345","order_status":"Submitted"}]""",
            "/v1/api/iserver/account/orders");

        var result = await SendAsync();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExceptionCarriesRequestPath()
    {
        SetResponse(HttpStatusCode.InternalServerError, """{"error":"fail"}""",
            "/v1/api/test?secret=abc123");

        var ex = await Should.ThrowAsync<IbkrApiException>(SendAsync());

        ex.Error.RequestPath.ShouldBe("/v1/api/test");
        ex.Error.RequestPath.ShouldNotContain("secret");
    }

    private class StubInnerHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
