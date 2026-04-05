using System;
using System.Net;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests verifying that the full pipeline correctly returns <c>Result.Failure</c>
/// with the appropriate <see cref="IbkrError"/> subtype for various error response patterns.
/// </summary>
public class ErrorResultTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task JsonErrorBody_Non2xx_ReturnsIbkrApiError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Invalid account ID","statusCode":500}"""));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        error.Message.ShouldBe("Invalid account ID");
        error.RawBody.ShouldNotBeNullOrEmpty();
        error.RawBody.ShouldContain("Invalid account ID");
    }

    [Fact]
    public async Task EmptyBody_Non2xx_ReturnsIbkrApiError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(400));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlainTextBody_Non2xx_ReturnsIbkrApiError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(403)
                    .WithHeader("Content-Type", "text/plain")
                    .WithBody("Error 403 - Access Denied"));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        error.RawBody.ShouldContain("Access Denied");
    }

    [Fact]
    public async Task HtmlBody_Non2xx_ReturnsIbkrApiError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(404)
                    .WithHeader("Content-Type", "text/html")
                    .WithBody("<html><body><h1>Resource not found</h1></body></html>"));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HiddenError_200WithErrorField_ReturnsIbkrHiddenError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alerts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"MTA alert tool ID is wrong =0"}"""));

        var result = await _harness.Client.Alerts.GetAlertsAsync(
            "DU9999999", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrHiddenError>();
        error.Message.ShouldBe("MTA alert tool ID is wrong =0");
    }

    [Fact]
    public async Task HiddenError_200WithSuccessFalse_ReturnsIbkrHiddenError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alerts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":false,"failure_list":"validation failed"}"""));

        var result = await _harness.Client.Alerts.GetAlertsAsync(
            "DU9999999", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrHiddenError>();
        error.Message.ShouldBe("validation failed");
    }

    [Fact]
    public async Task RateLimit_429WithRetryAfter_ReturnsIbkrRateLimitError()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Retry-After", "30")
                    .WithBody("""{"error":"rate limited"}"""));

        var result = await _harness.Client.Accounts.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrRateLimitError>();
        error.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task OrderError_200WithRejection_ReturnsFailure()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/*/orders")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Your order is not accepted. Reason: insufficient funds"}"""));

        var order = new OrderRequest
        {
            Conid = 756733,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
        };

        var result = await _harness.Client.Orders.PlaceOrderAsync(
            "U1234567", order, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("insufficient funds");
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
