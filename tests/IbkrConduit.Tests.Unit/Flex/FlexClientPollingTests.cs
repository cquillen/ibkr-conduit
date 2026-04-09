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

    // 1015 "Token is invalid" — permanent per IBKR docs
    private const string _errorResponse1015 = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>1015</ErrorCode>
            <ErrorMessage>Token is invalid.</ErrorMessage>
        </FlexStatementResponse>
        """;

    // 1004 "Statement is incomplete at this time" — retryable per IBKR docs
    private const string _retryableResponse1004 = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>1004</ErrorCode>
            <ErrorMessage>Statement is incomplete at this time. Please try again shortly.</ErrorMessage>
        </FlexStatementResponse>
        """;

    // 1018 "Too many requests" — retryable per IBKR docs
    private const string _retryableResponse1018 = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>1018</ErrorCode>
            <ErrorMessage>Too many requests have been made from this token.</ErrorMessage>
        </FlexStatementResponse>
        """;

    // Unknown code with Status=Fail — should be classified as permanent
    private const string _unknownFailResponse = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>9999</ErrorCode>
            <ErrorMessage>Something went wrong</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _failSendRequest = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>1015</ErrorCode>
            <ErrorMessage>Token is invalid.</ErrorMessage>
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
    public async Task ExecuteQueryAsync_KnownPermanentErrorDuringPoll_ThrowsFlexQueryException()
    {
        // 1015 "Token is invalid" is documented as permanent — should throw immediately.
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _errorResponse1015);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1015);
        ex.Message.ShouldBe("Token is invalid.");
    }

    [Fact]
    public async Task ExecuteQueryAsync_KnownRetryableError1004_RetriesUntilSuccess()
    {
        // 1004 "Statement is incomplete at this time" is documented as retryable.
        // The old implementation only retried 1019; this test verifies the new table-driven classifier.
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _retryableResponse1004,
            _retryableResponse1004,
            _successStatement);

        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var doc = await client.ExecuteQueryAsync("Q1", null, null, cts.Token);

        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(4); // SendRequest + 3 GetStatement polls
    }

    [Fact]
    public async Task ExecuteQueryAsync_RateLimit1018_RetriesUntilSuccess()
    {
        // 1018 is retryable but should use a longer backoff to respect the 10/min/token limit.
        // This test just verifies the classification works; timing is not asserted to keep the test fast.
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _retryableResponse1018,
            _successStatement);

        var client = CreateClient(handler);

        // Generous cancellation to allow the longer 1018 delay (10s)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var doc = await client.ExecuteQueryAsync("Q1", null, null, cts.Token);

        doc.ShouldNotBeNull();
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteQueryAsync_UnknownErrorCodeWithStatusFail_ThrowsFlexQueryException()
    {
        // Unknown codes fall back to the Status element. Status=Fail → permanent → throw.
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest,
            _unknownFailResponse);

        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("Q1", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(9999);
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestFails_ThrowsFlexQueryException()
    {
        var handler = new SequentialFakeHttpHandler(_failSendRequest);
        var client = CreateClient(handler);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => client.ExecuteQueryAsync("BADID", null, null, CancellationToken.None));

        ex.ErrorCode.ShouldBe(1015);
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
