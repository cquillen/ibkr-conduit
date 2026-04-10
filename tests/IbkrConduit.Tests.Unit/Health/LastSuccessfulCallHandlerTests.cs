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
    private readonly LastSuccessfulCallTracker _tracker = new();

    [Fact]
    public async Task SuccessResponse_UpdatesTimestamp()
    {
        var handler = new LastSuccessfulCallHandler(_tracker)
        {
            InnerHandler = new FakeHandler(HttpStatusCode.OK),
        };
        using var client = new HttpClient(handler);

        _tracker.LastSuccessfulCall.ShouldBeNull();

        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        _tracker.LastSuccessfulCall.ShouldNotBeNull();
    }

    [Fact]
    public async Task NonSuccessResponse_DoesNotUpdateTimestamp()
    {
        var handler = new LastSuccessfulCallHandler(_tracker)
        {
            InnerHandler = new FakeHandler(HttpStatusCode.InternalServerError),
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        _tracker.LastSuccessfulCall.ShouldBeNull();
    }

    [Fact]
    public async Task MultipleSuccessResponses_UpdateToLatest()
    {
        var handler = new LastSuccessfulCallHandler(_tracker)
        {
            InnerHandler = new FakeHandler(HttpStatusCode.OK),
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/first", TestContext.Current.CancellationToken);
        var first = _tracker.LastSuccessfulCall;
        first.ShouldNotBeNull();

        await Task.Delay(10, TestContext.Current.CancellationToken);

        await client.GetAsync("http://localhost/second", TestContext.Current.CancellationToken);
        var second = _tracker.LastSuccessfulCall;
        second.ShouldNotBeNull();

        second!.Value.ShouldBeGreaterThanOrEqualTo(first!.Value);
    }

    [Fact]
    public async Task NonSuccessAfterSuccess_DoesNotOverwrite()
    {
        var switchableHandler = new SwitchableHandler(HttpStatusCode.OK);
        var handler = new LastSuccessfulCallHandler(_tracker)
        {
            InnerHandler = switchableHandler,
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/ok", TestContext.Current.CancellationToken);
        var afterSuccess = _tracker.LastSuccessfulCall;
        afterSuccess.ShouldNotBeNull();

        switchableHandler.StatusCode = HttpStatusCode.BadRequest;
        await client.GetAsync("http://localhost/fail", TestContext.Current.CancellationToken);

        _tracker.LastSuccessfulCall.ShouldBe(afterSuccess);
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
