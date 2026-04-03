using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Http;

public class ErrorNormalizationPipelineTests : IDisposable
{
    private readonly WireMockServer _server;

    public ErrorNormalizationPipelineTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task Pipeline_200WithErrorBody_ThrowsOrderRejectedException()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/DU123/orders").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"insufficient funds"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrOrderRejectedException>(async () =>
            await client.PostAsync(
                $"{_server.Url}/v1/api/iserver/account/DU123/orders",
                new StringContent("{}"),
                TestContext.Current.CancellationToken));

        ex.RejectionMessage.ShouldBe("insufficient funds");
    }

    [Fact]
    public async Task Pipeline_200WithConfirmation_PassesThrough()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/account/DU123/orders").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"abc","message":["confirm?"],"isSuppressed":false,"messageIds":["o163"]}]"""));

        using var client = CreatePipelinedClient();

        var response = await client.PostAsync(
            $"{_server.Url}/v1/api/iserver/account/DU123/orders",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pipeline_500Remapped_NotRetried()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/marketdata/unsubscribe").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"unknown"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrApiException>(async () =>
            await client.PostAsync(
                $"{_server.Url}/v1/api/iserver/marketdata/unsubscribe",
                new StringContent("{}"),
                TestContext.Current.CancellationToken));

        ex.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // Handler sits alone — only 1 request should have been made
        _server.LogEntries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Pipeline_429_ThrowsRateLimitException()
    {
        _server.Given(
            Request.Create().WithPath("/v1/api/iserver/marketdata/history").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Retry-After", "60")
                    .WithBody("""{"error":"too many requests"}"""));

        using var client = CreatePipelinedClient();

        var ex = await Should.ThrowAsync<IbkrRateLimitException>(async () =>
            await client.GetAsync(
                $"{_server.Url}/v1/api/iserver/marketdata/history",
                TestContext.Current.CancellationToken));

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
    }

    private HttpClient CreatePipelinedClient()
    {
        var handler = new ErrorNormalizationHandler
        {
            InnerHandler = new HttpClientHandler()
        };
        return new HttpClient(handler);
    }

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
