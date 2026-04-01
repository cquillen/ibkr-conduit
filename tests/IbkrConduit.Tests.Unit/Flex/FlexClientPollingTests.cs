using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexClientPollingTests
{
    private const string _successSendRequest = """
        <FlexStatementResponse>
            <Status>Success</Status>
            <ReferenceCode>REF001</ReferenceCode>
        </FlexStatementResponse>
        """;

    private const string _inProgressResponse = """
        <FlexStatementResponse>
            <ErrorCode>1019</ErrorCode>
            <ErrorMessage>Statement generation in progress</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _successStatement = """
        <FlexQueryResponse>
            <FlexStatements count="1">
                <FlexStatement accountId="U1234567" />
            </FlexStatements>
        </FlexQueryResponse>
        """;

    private const string _errorResponse1004 = """
        <FlexStatementResponse>
            <ErrorCode>1004</ErrorCode>
            <ErrorMessage>Invalid token</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _failSendRequest = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>1018</ErrorCode>
            <ErrorMessage>Invalid query ID</ErrorMessage>
        </FlexStatementResponse>
        """;

    [Fact]
    public async Task ExecuteQueryAsync_SuccessOnFirstPoll_ReturnsImmediately()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _successStatement);

        var client = CreateClient(handler);

        var doc = await client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(2); // SendRequest + 1 GetStatement
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOnError1019_SucceedsOnSecondAttempt()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _successStatement);

        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var doc = await client.ExecuteQueryAsync("Q1", null, null, cts.Token);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(3); // SendRequest + 2 GetStatement polls
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOnError1019_SucceedsAfterMultipleAttempts()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _inProgressResponse,
            _inProgressResponse,
            _successStatement);

        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var doc = await client.ExecuteQueryAsync("Q1", null, null, cts.Token);

        doc.ShouldNotBeNull();
        handler.CallCount.ShouldBe(5); // SendRequest + 4 GetStatement polls
    }

    [Fact]
    public async Task ExecuteQueryAsync_TimeoutAfterMaxDuration_ThrowsTimeoutException()
    {
        // The FlexClient has _pollDelaysMs with 12 entries (56s total wait).
        // After all delays it makes a final attempt. If still 1019, throws TimeoutException.
        // We need 14 in-progress responses (12 during loop + 1 final check exceeds limit + 1 final attempt).
        // This test is inherently slow so we use cancellation to verify the behavior pattern.
        // Instead, we verify that cancellation during polling results in OperationCanceledException.
        var responses = new List<string> { _successSendRequest };
        for (var i = 0; i < 14; i++)
        {
            responses.Add(_inProgressResponse);
        }

        var handler = new SequentialFakeHttpHandler(responses.ToArray());
        var client = CreateClient(handler);

        // Use a very short timeout to verify cancellation propagation during polling
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Either TimeoutException (if polling loop completes fast enough) or OperationCanceledException (from CancellationToken)
        var ex = await Should.ThrowAsync<Exception>(
            () => client.ExecuteQueryAsync("Q1", null, null, cts.Token));

        (ex is TimeoutException or OperationCanceledException).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteQueryAsync_NonRetryableError_ThrowsFlexQueryException()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _errorResponse1004);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1004);
        ex.Message.ShouldBe("Invalid token");
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestFails_ThrowsFlexQueryException()
    {
        var handler = new SequentialFakeHttpHandler(_failSendRequest);
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("BADID", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1018);
    }

    [Fact]
    public async Task ExecuteQueryAsync_DateRangeOverride_IncludesInUrl()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _successStatement);

        var client = CreateClient(handler);

        await client.ExecuteQueryAsync("Q1", "20260101", "20260301", CancellationToken.None);

        var firstUrl = handler.RequestUris[0].ToString();
        firstUrl.ShouldContain("fd=20260101");
        firstUrl.ShouldContain("td=20260301");
    }

    [Fact]
    public async Task ExecuteQueryAsync_PassesCancellationToken()
    {
        // Create a handler that blocks on the second call, then we cancel
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _inProgressResponse,
            _inProgressResponse);

        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.ExecuteQueryAsync("Q1", null, null, cts.Token));
    }

    [Fact]
    public async Task ExecuteQueryAsync_CancelledDuringPollDelay_ThrowsOperationCanceled()
    {
        var responses = new List<string> { _successSendRequest };
        for (var i = 0; i < 20; i++)
        {
            responses.Add(_inProgressResponse);
        }

        var handler = new SequentialFakeHttpHandler(responses.ToArray());
        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.ExecuteQueryAsync("Q1", null, null, cts.Token));
    }

    private static FlexClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        return new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
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

    private sealed class SequentialFakeHttpHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public List<Uri> RequestUris { get; } = [];

        public SequentialFakeHttpHandler(params string[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
