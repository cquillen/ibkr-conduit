using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexClientTests
{
    private const string _validXml = """
        <FlexStatementResponse timestamp="12345">
            <Status>Success</Status>
            <ReferenceCode>REF123</ReferenceCode>
        </FlexStatementResponse>
        """;

    [Fact]
    public async Task SendRequestAsync_SuccessXml_ReturnsSuccessResult()
    {
        var handler = new FakeHttpHandler(_validXml);
        var client = CreateClient(handler);

        var result = await client.SendRequestAsync("12345", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root!.Name.LocalName.ShouldBe("FlexStatementResponse");
    }

    [Fact]
    public async Task SendRequestAsync_HttpRequestException_ReturnsFailureResult()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("boom", inner: null, HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var result = await client.SendRequestAsync("12345", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrApiError>();
        err.RequestPath.ShouldBe("flex/SendRequest?q=12345");
    }

    [Fact]
    public async Task SendRequestAsync_MalformedXml_ReturnsFailureResultWithRawBody()
    {
        var handler = new FakeHttpHandler("not xml at all <<<");
        var client = CreateClient(handler);

        var result = await client.SendRequestAsync("12345", null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrApiError>();
        err.RawBody.ShouldBe("not xml at all <<<");
        err.Message.ShouldContain("not valid XML");
    }

    [Fact]
    public async Task SendRequestAsync_WithDateRange_IncludesFdTdInUrl()
    {
        var handler = new FakeHttpHandler(_validXml);
        var client = CreateClient(handler);

        await client.SendRequestAsync("Q1", "20260101", "20260301", TestContext.Current.CancellationToken);

        var url = handler.LastRequestUri!.ToString();
        url.ShouldContain("fd=20260101");
        url.ShouldContain("td=20260301");
    }

    [Fact]
    public async Task SendRequestAsync_WithNullDates_DoesNotIncludeFdTdInUrl()
    {
        var handler = new FakeHttpHandler(_validXml);
        var client = CreateClient(handler);

        await client.SendRequestAsync("Q1", null, null, TestContext.Current.CancellationToken);

        var url = handler.LastRequestUri!.ToString();
        url.ShouldNotContain("fd=");
        url.ShouldNotContain("td=");
    }

    [Fact]
    public async Task GetStatementAsync_SuccessXml_ReturnsSuccessResult()
    {
        var handler = new FakeHttpHandler("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1" />
              </FlexStatements>
            </FlexQueryResponse>
            """);
        var client = CreateClient(handler);

        var result = await client.GetStatementAsync("REF123", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
    }

    [Fact]
    public async Task GetStatementAsync_HttpRequestException_ReturnsFailureResult()
    {
        var handler = new ThrowingHttpHandler(new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var result = await client.GetStatementAsync("REF123", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrApiError>();
        err.RequestPath.ShouldBe("flex/GetStatement?q=REF123");
    }

    [Fact]
    public async Task GetStatementAsync_MalformedXml_ReturnsFailureResultWithRawBody()
    {
        var handler = new FakeHttpHandler("<<<broken>");
        var client = CreateClient(handler);

        var result = await client.GetStatementAsync("REF123", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrApiError>();
        err.RawBody.ShouldBe("<<<broken>");
    }

    private static FlexClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        return new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public Uri? LastRequestUri { get; private set; }

        public FakeHttpHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var body = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw _exception;
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false);
    }
}
