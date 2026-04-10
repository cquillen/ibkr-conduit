using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Flex;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexOperationsPollingTests
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

    private const string _errorResponse1015 = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>1015</ErrorCode>
            <ErrorMessage>Token is invalid.</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _retryableResponse1004 = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>1004</ErrorCode>
            <ErrorMessage>Statement is incomplete at this time. Please try again shortly.</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _retryableResponse1018 = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>1018</ErrorCode>
            <ErrorMessage>Too many requests have been made from this token.</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _unknownFailResponse = """
        <FlexStatementResponse>
            <Status>Fail</Status>
            <ErrorCode>9999</ErrorCode>
            <ErrorMessage>Something went wrong</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _unknownWarnResponse = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>8888</ErrorCode>
            <ErrorMessage>Unknown warning</ErrorMessage>
        </FlexStatementResponse>
        """;

    private const string _retryableSendRequest1018 = """
        <FlexStatementResponse>
            <Status>Warn</Status>
            <ErrorCode>1018</ErrorCode>
            <ErrorMessage>Too many requests have been made from this token.</ErrorMessage>
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
    public async Task ExecuteQueryAsync_SuccessOnFirstPoll_ReturnsSuccessResult()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RawXml.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOn1019_EventuallySucceeds()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest, _inProgressResponse, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteQueryAsync_RetryOn1004_EventuallySucceeds()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest, _retryableResponse1004, _retryableResponse1004, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.CallCount.ShouldBe(4);
    }

    [Fact]
    public async Task ExecuteQueryAsync_RateLimit1018_EventuallySucceeds()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest, _retryableResponse1018, _successStatement);
        // Use a long enough timeout to accommodate the 10s rate-limit delay.
        var ops = CreateOperations(handler, new IbkrClientOptions { FlexPollTimeout = TimeSpan.FromSeconds(30) });

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteQueryAsync_PermanentError1015_ReturnsFlexErrorFailure()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _errorResponse1015);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1015);
        err.IsRetryable.ShouldBeFalse();
        err.Message.ShouldBe("Token is invalid.");
    }

    [Fact]
    public async Task ExecuteQueryAsync_UnknownCodeWithStatusWarn_TreatedAsTransient()
    {
        var handler = new SequentialFakeHttpHandler(
            _successSendRequest, _unknownWarnResponse, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteQueryAsync_UnknownCodeWithStatusFail_TreatedAsPermanent()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _unknownFailResponse);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(9999);
        err.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestReturnsFail_ReturnsFailureWithoutPolling()
    {
        var handler = new SequentialFakeHttpHandler(_failSendRequest);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("BADID", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1015);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteQueryAsync_TimeoutExhausted_ReturnsRetryableFlexError()
    {
        // 20 in-progress responses — will always return 1019. Poll timeout is 500ms,
        // well short of the first delay (1000ms), so we will exit after one poll.
        var responses = new List<string> { _successSendRequest };
        for (var i = 0; i < 20; i++)
        {
            responses.Add(_inProgressResponse);
        }

        var handler = new SequentialFakeHttpHandler(responses.ToArray());
        var ops = CreateOperations(handler, new IbkrClientOptions { FlexPollTimeout = TimeSpan.FromMilliseconds(500) });

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(0);
        err.IsRetryable.ShouldBeTrue();
        err.Message.ShouldContain("did not complete");
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
        var ops = CreateOperations(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Should.ThrowAsync<OperationCanceledException>(
            () => ops.ExecuteQueryAsync("Q1", cts.Token));
    }

    [Fact]
    public async Task ExecuteQueryAsync_DateRangeOverride_IncludesInUrl()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", "20260101", "20260301", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var firstUrl = handler.RequestUris[0].ToString();
        firstUrl.ShouldContain("fd=20260101");
        firstUrl.ShouldContain("td=20260301");
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowOnApiError_PermanentError_ThrowsIbkrApiException()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _errorResponse1015);
        var ops = CreateOperations(handler, new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(60),
            ThrowOnApiError = true,
        });

        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken));

        var err = ex.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1015);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowOnApiErrorFalse_PermanentError_ReturnsFailureResult()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _errorResponse1015);
        var ops = CreateOperations(handler, new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(60),
            ThrowOnApiError = false,
        });

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeOfType<IbkrFlexError>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_NullFlexClient_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(
            null,
            new IbkrClientOptions(),
            NullLogger<FlexOperations>.Instance);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("FlexToken");
    }

    [Fact]
    public async Task GetCashTransactionsAsync_NoQueryIdConfigured_ThrowsInvalidOperationException()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _successStatement);
        var ops = CreateOperations(handler);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.GetCashTransactionsAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("CashTransactionsQueryId");
        ex.Message.ShouldContain("Reports");
    }

    [Fact]
    public async Task GetTradeConfirmationsAsync_NoQueryIdConfigured_ThrowsInvalidOperationException()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _successStatement);
        var ops = CreateOperations(handler);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.GetTradeConfirmationsAsync(
                new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9),
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("TradeConfirmationsQueryId");
        ex.Message.ShouldContain("Trade Confirmation");
    }

    [Fact]
    public async Task GetTradeConfirmationsAsync_FormatsDateAsYyyyMMdd()
    {
        var handler = new SequentialFakeHttpHandler(_successSendRequest, _successStatement);
        var options = new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(60),
            FlexQueries = new FlexQueryOptions { TradeConfirmationsQueryId = "TCFQ" },
        };
        var ops = CreateOperations(handler, options);

        var result = await ops.GetTradeConfirmationsAsync(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var firstUrl = handler.RequestUris[0].ToString();
        firstUrl.ShouldContain("fd=20260401");
        firstUrl.ShouldContain("td=20260409");
        firstUrl.ShouldContain("q=TCFQ");
    }

    [Fact]
    public async Task GetCashTransactionsAsync_WithConfiguredIdAndSuccessResponse_ReturnsTypedResult()
    {
        var fixtureXml = LoadFixture("cash-transactions.xml");
        var handler = new SequentialFakeHttpHandler(_successSendRequest, fixtureXml);
        var options = new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(60),
            FlexQueries = new FlexQueryOptions { CashTransactionsQueryId = "CASHQ" },
        };
        var ops = CreateOperations(handler, options);

        var result = await ops.GetCashTransactionsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.QueryName.ShouldNotBeNullOrEmpty();
        result.Value.CashTransactions.ShouldNotBeNull();
        result.Value.RawXml.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
        var firstUrl = handler.RequestUris[0].ToString();
        firstUrl.ShouldContain("q=CASHQ");
    }

    [Fact]
    public async Task GetTradeConfirmationsAsync_WithConfiguredIdAndSuccessResponse_ReturnsTypedResult()
    {
        var fixtureXml = LoadFixture("trade-confirmations.xml");
        var handler = new SequentialFakeHttpHandler(_successSendRequest, fixtureXml);
        var options = new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(60),
            FlexQueries = new FlexQueryOptions { TradeConfirmationsQueryId = "TCFQ" },
        };
        var ops = CreateOperations(handler, options);

        var result = await ops.GetTradeConfirmationsAsync(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TradeConfirmations.ShouldNotBeNull();
        result.Value.SymbolSummaries.ShouldNotBeNull();
        result.Value.Orders.ShouldNotBeNull();
        result.Value.RawXml.Root!.Name.LocalName.ShouldBe("FlexQueryResponse");
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestRetryableError_ThenSuccess_ReturnsSuccess()
    {
        // First SendRequest returns retryable 1018, second returns success with ref code,
        // then GetStatement returns success immediately.
        var handler = new SequentialFakeHttpHandler(
            _retryableSendRequest1018, _successSendRequest, _successStatement);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.CallCount.ShouldBe(3); // 2 SendRequest + 1 GetStatement
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestPermanentError_FailsImmediately()
    {
        // First SendRequest returns permanent error 1015 — no retry.
        var handler = new SequentialFakeHttpHandler(_failSendRequest);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1015);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteQueryAsync_SendRequestAllAttemptsRetryable_FailsWithAttemptCount()
    {
        // All 3 SendRequest attempts return retryable 1018.
        var handler = new SequentialFakeHttpHandler(
            _retryableSendRequest1018, _retryableSendRequest1018, _retryableSendRequest1018);
        var ops = CreateOperations(handler);

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.Message.ShouldContain("did not succeed after 3 attempts");
        err.Message.ShouldContain("1018");
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteQueryAsync_TimeoutMessage_ContainsAttemptCountAndLastError()
    {
        // Stub GetStatement to always return 1019 with a short timeout.
        var responses = new List<string> { _successSendRequest };
        for (var i = 0; i < 20; i++)
        {
            responses.Add(_inProgressResponse);
        }

        var handler = new SequentialFakeHttpHandler(responses.ToArray());
        var ops = CreateOperations(handler, new IbkrClientOptions { FlexPollTimeout = TimeSpan.FromMilliseconds(500) });

        var result = await ops.ExecuteQueryAsync("Q1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.Message.ShouldContain("Attempted");
        err.Message.ShouldContain("polls");
        err.Message.ShouldContain("1019");
        err.Message.ShouldContain("Breakout by Day");
    }

    [Fact]
    public void ApplyJitter_ProducesNonDeterministicDelays()
    {
        var results = new HashSet<int>();
        for (var i = 0; i < 100; i++)
        {
            results.Add(FlexOperations.ApplyJitter(5000));
        }

        // Should not all be the same value (non-deterministic)
        results.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void ApplyJitter_StaysWithinBounds()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = FlexOperations.ApplyJitter(5000);
            result.ShouldBeGreaterThanOrEqualTo(4000); // 5000 - 20%
            result.ShouldBeLessThanOrEqualTo(6000); // 5000 + 20%
        }
    }

    [Fact]
    public void ApplyJitter_RespectsMinimumFloor()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = FlexOperations.ApplyJitter(100);
            result.ShouldBeGreaterThanOrEqualTo(100);
        }
    }

    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Flex", "Fixtures", name));

    private static FlexOperations CreateOperations(
        HttpMessageHandler handler, IbkrClientOptions? options = null)
    {
        var factory = new FakeHttpClientFactory(handler);
        var flexClient = new FlexClient(factory, "test-flex", "FAKE_TOKEN", NullLogger<FlexClient>.Instance);
        return new FlexOperations(
            flexClient,
            options ?? new IbkrClientOptions { FlexPollTimeout = TimeSpan.FromSeconds(60) },
            NullLogger<FlexOperations>.Instance);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false);
    }

    private sealed class SequentialFakeHttpHandler : HttpMessageHandler
    {
        private readonly string[] _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public List<Uri> RequestUris { get; } = [];

        public SequentialFakeHttpHandler(params string[] responses) => _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUris.Add(request.RequestUri!);
            var index = Interlocked.Increment(ref _callCount) - 1;
            var body = index < _responses.Length ? _responses[index] : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }
    }
}
