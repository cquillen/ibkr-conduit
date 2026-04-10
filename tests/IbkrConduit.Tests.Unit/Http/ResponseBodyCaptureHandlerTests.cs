using System;
using System.Net;
using System.Net.Http;
using System.Text;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ResponseBodyCaptureHandlerTests
{
    [Fact]
    public async Task SendAsync_CapturesResponseBodyInRequestOptions()
    {
        var body = """{"accounts":["U1234567"]}""";
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, body),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>(ResponseBodyCaptureHandler.RawBodyOptionKey),
            out var captured).ShouldBeTrue();
        captured.ShouldBe(body);
    }

    [Fact]
    public async Task SendAsync_CapturesErrorResponseBody()
    {
        var errorBody = """{"error":"insufficient funds"}""";
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.BadRequest, errorBody),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>(ResponseBodyCaptureHandler.RawBodyOptionKey),
            out var captured).ShouldBeTrue();
        captured.ShouldBe(errorBody);
    }

    [Fact]
    public async Task SendAsync_Captures200WithHiddenError()
    {
        // This is the core bug scenario: IBKR returns 200 OK with an error body
        var hiddenError = """{"error":"Order rejected"}""";
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, hiddenError),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>(ResponseBodyCaptureHandler.RawBodyOptionKey),
            out var captured).ShouldBeTrue();
        captured.ShouldBe(hiddenError);
    }

    [Fact]
    public async Task SendAsync_PreservesResponseBodyForDownstream()
    {
        var body = """{"key":"value"}""";
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, body),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Body should still be readable after capture
        var downstream = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        downstream.ShouldBe(body);
    }

    [Fact]
    public async Task SendAsync_PreservesContentType()
    {
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, "{}"),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task SendAsync_EmptyContent_CapturesEmptyString()
    {
        var handler = new ResponseBodyCaptureHandler
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, ""),
        };

        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ibkr.com/v1/api/test");
        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>(ResponseBodyCaptureHandler.RawBodyOptionKey),
            out var captured).ShouldBeTrue();
        captured.ShouldBe("");
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }

    private sealed class NullContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                RequestMessage = request,
            });
    }
}
