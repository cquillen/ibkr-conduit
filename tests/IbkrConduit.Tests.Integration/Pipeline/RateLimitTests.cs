using System;
using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for the global rate limiting handler.
/// </summary>
public class RateLimitTests : IDisposable
{
    private readonly WireMockServer _server;

    public RateLimitTests()
    {
        _server = WireMockServer.Start();
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

        var globalHandler = new GlobalRateLimitingHandler(tightLimiter, NullLogger<GlobalRateLimitingHandler>.Instance)
        {
            InnerHandler = new HttpClientHandler(),
        };

        using var client = new HttpClient(globalHandler);

        // First request consumes the token
        var firstResponse = await client.GetAsync($"{_server.Url}/v1/api/test", TestContext.Current.CancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Subsequent requests should be rejected
        await Should.ThrowAsync<RateLimitRejectedException>(
            () => client.GetAsync($"{_server.Url}/v1/api/test", TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
