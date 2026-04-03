using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for <see cref="IbkrConduit.Http.ErrorNormalizationHandler"/>
/// exercised through the full DI pipeline.
/// </summary>
public class ErrorNormalizationTests : IAsyncDisposable
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

    /// <summary>
    /// Verifies that a 200 OK with an error body on order placement
    /// throws <see cref="IbkrOrderRejectedException"/>.
    /// </summary>
    [Fact]
    public async Task OrderResponse_200WithError_ThrowsOrderRejectedException()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // Stub order placement returning 200 with error body
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/U1234567/orders")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"insufficient funds"}"""));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            OrderType = "MKT",
            Quantity = 100,
        };

        var ex = await Should.ThrowAsync<IbkrOrderRejectedException>(
            _harness.Client.Orders.PlaceOrderAsync(
                "U1234567", order, TestContext.Current.CancellationToken));

        ex.RejectionMessage.ShouldBe("insufficient funds");
        ex.StatusCode.ShouldBe(HttpStatusCode.OK);
        ex.RawResponseBody.ShouldContain("insufficient funds");
    }

    /// <summary>
    /// Verifies that a 200 OK with a confirmation array passes through
    /// without throwing (ErrorNormalizationHandler skips arrays).
    /// </summary>
    [Fact]
    public async Task OrderResponse_200WithConfirmation_PassesThrough()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // Stub order placement returning 200 with confirmation (array response)
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/U1234567/orders",
            FixtureLoader.LoadBody("Orders", "POST-place-order-confirmation"));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            OrderType = "LMT",
            Quantity = 1,
            Price = 600.0m,
        };

        // Should NOT throw — confirmation arrays are passed through
        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsT1.ShouldBeTrue("Expected OrderConfirmationRequired (T1), not OrderSubmitted (T0)");
    }

    /// <summary>
    /// Verifies that a persistent 500 on /iserver/marketdata/unsubscribe
    /// is remapped to 404 by ErrorNormalizationHandler after retries exhaust.
    /// </summary>
    [Fact]
    public async Task UnsubscribeResponse_500Persistent_RemappedTo404_ThrowsApiException()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // Persistent 500 on unsubscribe — ResilienceHandler retries (all fail),
        // then ErrorNormalizationHandler remaps 500 -> 404 for /iserver/marketdata/unsubscribe
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/unsubscribe")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"unknown"}"""));

        var ex = await Should.ThrowAsync<IbkrApiException>(
            _harness.Client.MarketData.UnsubscribeAsync(
                999999999, TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ex.ErrorMessage.ShouldBe("unknown");
    }

    /// <summary>
    /// Verifies that a persistent 429 with Retry-After header
    /// throws <see cref="IbkrRateLimitException"/> with the correct retry delay.
    /// </summary>
    [Fact]
    public async Task HistoryResponse_429WithRetryAfter_ThrowsRateLimitException()
    {
        _harness = await CreateHarnessWithZeroDelayResilience();

        // Persistent 429 — ResilienceHandler retries (all fail),
        // then ErrorNormalizationHandler throws IbkrRateLimitException
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/history")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Retry-After", "60")
                    .WithBody("""{"error":"Too many requests"}"""));

        var ex = await Should.ThrowAsync<IbkrRateLimitException>(
            _harness.Client.MarketData.GetHistoryAsync(
                756733, "1d", "1min",
                cancellationToken: TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ex.RetryAfter.ShouldNotBeNull();
        ex.RetryAfter!.Value.TotalSeconds.ShouldBe(60);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
