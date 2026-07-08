using System;
using System.Threading.Tasks;
using IbkrConduit.Errors;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests for response integrity on the 401-reauth-retry leg (PVR-11 / ERR-1).
/// Verifies that hidden-error detection (a 200 carrying an <c>{"error":...}</c> body) still fires on
/// the retried request — the leg where the captured raw body used to be lost because the retry
/// response pointed at the internal clone whose <see cref="System.Net.Http.HttpRequestMessage.Options"/>
/// never carried <c>ResponseBodyCaptureHandler</c>'s stash.
/// <para>
/// The reproduction uses <c>GetOrderStatusAsync</c> → <see cref="IbkrConduit.Orders.OrderStatus"/>, a
/// positional record with no required members: the tolerant serializer deserializes an error body into
/// a non-null all-default instance, so <c>response.Error</c> is null and the Refit-error fallback never
/// fires — only <c>ResultFactory.GetCapturedBody</c> can surface the hidden error. A collection-shaped
/// endpoint would mask the defect via that fallback and produce a false green.
/// </para>
/// </summary>
public class RetryLegIntegrityTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetOrderStatus_401ThenHiddenErrorBodyOnRetry_ClassifiesAsHiddenError()
    {
        // ERR-1: the first send 401s, TokenRefreshHandler re-authenticates and replays the GET, and
        // IBKR answers the retry leg with a 200-with-error-body. Hidden-error detection MUST fire on
        // the retried response — before the fix the captured body was unreachable (stashed on the
        // original request while the response pointed at the clone), so this surfaced as a silent
        // success wrapping a degenerate all-default OrderStatus on a money-adjacent read path.
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/order/status/473740665")
                .UsingGet())
            .InScenario("order-status-401-then-hidden-error")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/order/status/473740665")
                .UsingGet())
            .InScenario("order-status-401-then-hidden-error")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Order status lookup failed"}"""));

        var result = await _harness.Client.Orders.GetOrderStatusAsync(
            "473740665", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse(
            "a hidden-error body returned on the 401-retry leg must not surface as a silent success");
        var error = result.Error.ShouldBeOfType<IbkrHiddenError>();
        error.Message.ShouldBe("Order status lookup failed");
        error.RawBody.ShouldContain("Order status lookup failed");

        _harness.VerifyReauthenticationOccurred();
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
