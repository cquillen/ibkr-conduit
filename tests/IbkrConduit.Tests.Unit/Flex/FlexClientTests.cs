using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexClientTests
{
    [Fact]
    public async Task SendRequestAsync_SuccessResponse_ReturnsReferenceCode()
    {
        var handler = new FakeHttpHandler("""
            <FlexStatementResponse timestamp="12345">
                <Status>Success</Status>
                <ReferenceCode>9876543210</ReferenceCode>
                <Url>https://example.com</Url>
            </FlexStatementResponse>
            """);

        var client = CreateClient(handler);

        var result = await client.SendRequestAsync("12345", null, null, CancellationToken.None);

        result.ShouldBe("9876543210");
    }

    [Fact]
    public async Task SendRequestAsync_ErrorResponse_ThrowsFlexQueryException()
    {
        var handler = new FakeHttpHandler("""
            <FlexStatementResponse timestamp="12345">
                <Status>Fail</Status>
                <ErrorCode>1004</ErrorCode>
                <ErrorMessage>Invalid token</ErrorMessage>
            </FlexStatementResponse>
            """);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.SendRequestAsync("12345", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1004);
        ex.Message.ShouldBe("Invalid token");
    }

    [Fact]
    public async Task SendRequestAsync_WithDateRange_IncludesDatesInUrl()
    {
        var handler = new FakeHttpHandler("""
            <FlexStatementResponse>
                <Status>Success</Status>
                <ReferenceCode>ABC123</ReferenceCode>
            </FlexStatementResponse>
            """);

        var client = CreateClient(handler);

        await client.SendRequestAsync("Q1", "20260101", "20260301", CancellationToken.None);

        var requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.ShouldContain("fd=20260101");
        requestUrl.ShouldContain("td=20260301");
    }

    [Fact]
    public async Task PollForStatementAsync_ImmediateSuccess_ReturnsDocument()
    {
        var handler = new FakeHttpHandler("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567" />
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var client = CreateClient(handler);

        var doc = await client.PollForStatementAsync("REF123", CancellationToken.None);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
    }

    [Fact]
    public async Task PollForStatementAsync_ErrorResponse_ThrowsFlexQueryException()
    {
        var handler = new FakeHttpHandler("""
            <FlexStatementResponse>
                <ErrorCode>1004</ErrorCode>
                <ErrorMessage>Invalid token</ErrorMessage>
            </FlexStatementResponse>
            """);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.PollForStatementAsync("REF123", CancellationToken.None));

        ex.ErrorCode.ShouldBe(1004);
    }

    [Fact]
    public void FlexQueryException_PreservesErrorCodeAndMessage()
    {
        var ex = new FlexQueryException(1003, "Too many requests");

        ex.ErrorCode.ShouldBe(1003);
        ex.Message.ShouldBe("Too many requests");
    }

    [Fact]
    public async Task FlexOperations_NullFlexClient_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("12345", CancellationToken.None));

        ex.Message.ShouldContain("FlexToken");
    }

    [Fact]
    public async Task FlexOperations_NullFlexClient_WithDateRange_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("12345", "20260101", "20260301", CancellationToken.None));

        ex.Message.ShouldContain("FlexToken");
    }

    private static FlexClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new FlexClient(httpClient, "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
    }

    private class FakeHttpHandler : HttpMessageHandler
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
            var body = _responses.Count > 0 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/xml"),
            });
        }
    }
}
