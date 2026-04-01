using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexOperationsTests
{
    [Fact]
    public async Task ExecuteQueryAsync_NullToken_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("12345", CancellationToken.None));

        ex.Message.ShouldContain("FlexToken");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithDateRange_DelegatesCorrectly()
    {
        var handler = new FakeHttpHandler(
            """
            <FlexStatementResponse>
                <Status>Success</Status>
                <ReferenceCode>REF001</ReferenceCode>
            </FlexStatementResponse>
            """,
            """
            <FlexQueryResponse>
                <FlexStatements count="1">
                    <FlexStatement accountId="U1234567" />
                </FlexStatements>
            </FlexQueryResponse>
            """);

        var factory = new FakeHttpClientFactory(handler);
        var flexClient = new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
        var ops = new FlexOperations(flexClient);

        var result = await ops.ExecuteQueryAsync("Q1", "20260101", "20260301", CancellationToken.None);

        result.ShouldNotBeNull();
        result.RawXml.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        // Verify the first request (SendRequest) included date params
        var sendRequestUrl = handler.RequestUris[0].ToString();
        sendRequestUrl.ShouldContain("fd=20260101");
        sendRequestUrl.ShouldContain("td=20260301");
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) =>
            new HttpClient(_handler, disposeHandler: false);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _callCount;

        public List<Uri> RequestUris { get; } = [];

        public FakeHttpHandler(params string[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            var index = Interlocked.Increment(ref _callCount) - 1;
            var body = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }
    }
}
