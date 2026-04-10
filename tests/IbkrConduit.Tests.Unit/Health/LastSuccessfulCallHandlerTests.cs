using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Health;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Health;

public class LastSuccessfulCallHandlerTests
{
    [Fact]
    public async Task SuccessResponse_UpdatesTimestamp()
    {
        var handler = new LastSuccessfulCallHandler
        {
            InnerHandler = new FakeHandler(HttpStatusCode.OK),
        };
        using var client = new HttpClient(handler);

        handler.LastSuccessfulCall.ShouldBeNull();

        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        handler.LastSuccessfulCall.ShouldNotBeNull();
    }

    [Fact]
    public async Task NonSuccessResponse_DoesNotUpdateTimestamp()
    {
        var handler = new LastSuccessfulCallHandler
        {
            InnerHandler = new FakeHandler(HttpStatusCode.InternalServerError),
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        handler.LastSuccessfulCall.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleSuccessResponses_UpdateToLatest()
    {
        var handler = new LastSuccessfulCallHandler
        {
            InnerHandler = new FakeHandler(HttpStatusCode.OK),
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/first", TestContext.Current.CancellationToken);
        var first = handler.LastSuccessfulCall;
        first.ShouldNotBeNull();

        await Task.Delay(10, TestContext.Current.CancellationToken);

        await client.GetAsync("http://localhost/second", TestContext.Current.CancellationToken);
        var second = handler.LastSuccessfulCall;
        second.ShouldNotBeNull();

        second!.Value.ShouldBeGreaterThanOrEqualTo(first!.Value);
    }

    [Fact]
    public async Task NonSuccessAfterSuccess_DoesNotOverwrite()
    {
        var switchableHandler = new SwitchableHandler(HttpStatusCode.OK);
        var handler = new LastSuccessfulCallHandler
        {
            InnerHandler = switchableHandler,
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/ok", TestContext.Current.CancellationToken);
        var afterSuccess = handler.LastSuccessfulCall;
        afterSuccess.ShouldNotBeNull();

        switchableHandler.StatusCode = HttpStatusCode.BadRequest;
        await client.GetAsync("http://localhost/fail", TestContext.Current.CancellationToken);

        handler.LastSuccessfulCall.ShouldBe(afterSuccess);
    }

    private sealed class FakeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class SwitchableHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(StatusCode));
    }
}
