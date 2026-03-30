using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Polly;
using Polly.Retry;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ResilienceHandlerTests
{
    private static ResiliencePipeline<HttpResponseMessage> CreateTestPipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout)
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
            })
            .Build();

    [Fact]
    public async Task SendAsync_OnTransient503ThenSuccess_RetriesAndReturnsSuccess()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_On429ThenSuccess_RetriesAndReturnsSuccess()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_On408ThenSuccess_RetriesAndReturnsSuccess()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_On400_DoesNotRetry()
    {
        var innerHandler = new SequenceHandler(HttpStatusCode.BadRequest);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_On404_DoesNotRetry()
    {
        var innerHandler = new SequenceHandler(HttpStatusCode.NotFound);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_PassesThroughDirectly()
    {
        var innerHandler = new SequenceHandler(HttpStatusCode.OK);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_AllRetriesExhausted_ReturnsLastResponse()
    {
        var innerHandler = new SequenceHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable);

        var handler = new ResilienceHandler(CreateTestPipeline())
        {
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("http://localhost/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        innerHandler.CallCount.ShouldBe(4); // 1 initial + 3 retries
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _responses;
        private int _callCount;

        public int CallCount => _callCount;

        public SequenceHandler(params HttpStatusCode[] responses) =>
            _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            var statusCode = index < _responses.Length
                ? _responses[index]
                : _responses[^1];

            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
