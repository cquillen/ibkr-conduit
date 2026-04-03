using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

public class ResilienceTests : IAsyncDisposable
{
    private TestHarness? _harness;

    /// <summary>
    /// Creates a zero-delay Polly pipeline for fast test execution.
    /// Same retry logic as production (5xx, 408, 429, max 3 retries) but no backoff delay.
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> CreateZeroDelayPipeline() =>
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

    private Task<TestHarness> CreateHarnessWithZeroDelayResilience() =>
        TestHarness.CreateAsync(configureServices: services =>
        {
            services.AddSingleton(CreateZeroDelayPipeline());
        });

    [Fact]
    public async Task Request_503ThenSuccess_RetriesAndReturnsResult()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // First two calls return 503, third returns 200
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WillSetStateTo("first-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WhenStateIs("first-retry")
            .WillSetStateTo("second-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("503-retry")
            .WhenStateIs("second-retry")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    [Fact]
    public async Task Request_503AllRetriesExhausted_ThrowsIbkrApiException()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // All calls return 503 — retries will be exhausted
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(503)
                    .WithBody("Service Unavailable"));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.Accounts.GetAccountsAsync(
                TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Request_500WithHtmlBody_ThrowsIbkrApiExceptionWithoutCrashing()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        var htmlBody = "<html><body><h1>Service Unavailable</h1></body></html>";

        // All calls return 500 with HTML — not JSON
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "text/html")
                    .WithBody(htmlBody));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.Accounts.GetAccountsAsync(
                TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        ex.ErrorMessage.ShouldBeNull();
        ex.RawResponseBody.ShouldContain("Service Unavailable");
    }

    [Fact]
    public async Task Request_ConnectionTimeout_ThrowsCleanException()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // Server responds with a 30-second delay — longer than our cancellation timeout
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"accounts":["U1234567"]}""")
                    .WithDelay(TimeSpan.FromSeconds(30)));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var ex = await Should.ThrowAsync<TaskCanceledException>(
            _harness.Client.Accounts.GetAccountsAsync(cts.Token));

        // Verify it's a clean cancellation, not an obscure internal error
        ex.ShouldNotBeNull();
        ex.CancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Request_500NotReadyThenSuccess_RetriesAndReturnsResult()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // First call returns 500 "Not ready"
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("not-ready")
            .WillSetStateTo("warmed-up")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Not ready"}"""));

        // Second call succeeds
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .InScenario("not-ready")
            .WhenStateIs("warmed-up")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Accounts", "GET-iserver-accounts")));

        var result = await _harness.Client.Accounts.GetAccountsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.ShouldContain("U1234567");
    }

    [Fact]
    public async Task Request_500NotReadyAllRetriesExhausted_ThrowsWithErrorMessage()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // All calls return 500 "Not ready"
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Not ready"}"""));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.Accounts.GetAccountsAsync(
                TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        ex.ErrorMessage.ShouldBe("Not ready");
        ex.RawResponseBody.ShouldContain("Not ready");
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
