using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Polly;
using Polly.Retry;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Http;

public class RateLimitingAndResilienceTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly TokenBucketRateLimiter _globalLimiter;
    private readonly Dictionary<string, RateLimiter> _endpointLimiters;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public RateLimitingAndResilienceTests()
    {
        _server = WireMockServer.Start();

        _globalLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 100,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        _endpointLimiters = new Dictionary<string, RateLimiter>();
        _pipeline = CreateTestResiliencePipeline();
    }

    [Fact]
    public async Task TransientError_IsRetriedTransparently()
    {
        // First request returns 503, second returns 200
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id": "DU1234567"}]"""));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("DU1234567");
    }

    [Fact]
    public async Task TooManyRequests429_IsRetriedWithBackoff()
    {
        // First request returns 429, second returns 200
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("throttle")
            .WillSetStateTo("allowed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithBody("Too Many Requests"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("throttle")
            .WhenStateIs("allowed")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id": "DU7654321"}]"""));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("DU7654321");
    }

    [Fact]
    public async Task NonRetryableError_PassesThroughImmediately()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(400)
                    .WithBody("Bad Request"));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Verify only one request was made (no retry)
        _server.LogEntries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ForbiddenError403_NotRetried()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(403)
                    .WithBody("Forbidden"));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Verify only one request was made (no retry)
        _server.LogEntries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InternalServerError500_IsRetried()
    {
        // First request returns 500, second returns 200
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry500")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithBody("Internal Server Error"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry500")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id": "DU1111111"}]"""));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("DU1111111");
    }

    [Fact]
    public async Task BadGateway502_IsRetried()
    {
        // First request returns 502, second returns 200
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry502")
            .WillSetStateTo("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(502)
                    .WithBody("Bad Gateway"));

        _server.Given(
            Request.Create()
                .WithPath("/v1/api/portfolio/accounts")
                .UsingGet())
            .InScenario("retry502")
            .WhenStateIs("retried")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id": "DU2222222"}]"""));

        using var client = CreatePipelinedClient(_globalLimiter, _endpointLimiters, _pipeline);
        var response = await client.GetAsync($"{_server.Url}/v1/api/portfolio/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("DU2222222");
    }

    [Fact]
    public async Task RateLimitQueueFull_ThrowsRateLimitRejectedException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/test")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithBody("OK"));

        // Use a very tight limiter: 1 token, no queue, no replenishment
        using var tightLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            TokensPerPeriod = 1,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

        var emptyEndpointLimiters = new Dictionary<string, RateLimiter>();

        using var client = CreatePipelinedClient(tightLimiter, emptyEndpointLimiters, _pipeline);

        // First request consumes the token
        var firstResponse = await client.GetAsync($"{_server.Url}/v1/api/test", TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Subsequent requests should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync($"{_server.Url}/v1/api/test", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Creates an HttpClient with the full resilience + rate limiting pipeline.
    /// </summary>
    private static HttpClient CreatePipelinedClient(
        RateLimiter globalLimiter,
        IReadOnlyDictionary<string, RateLimiter> endpointLimiters,
        ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        var endpointHandler = new EndpointRateLimitingHandler(endpointLimiters)
        {
            InnerHandler = new HttpClientHandler(),
        };

        var globalHandler = new GlobalRateLimitingHandler(globalLimiter)
        {
            InnerHandler = endpointHandler,
        };

        var resilienceHandler = new ResilienceHandler(pipeline)
        {
            InnerHandler = globalHandler,
        };

        return new HttpClient(resilienceHandler);
    }

    /// <summary>
    /// Creates a resilience pipeline with zero delay for fast test execution.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> CreateTestResiliencePipeline() =>
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

    public void Dispose()
    {
        _globalLimiter.Dispose();
        _server.Dispose();
    }
}
