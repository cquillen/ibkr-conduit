using System;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Orders;

/// <summary>
/// RPD-02: end-to-end coverage (full DI stack) that <c>GET /iserver/account/orders</c> surfaces the
/// bracket/OCA parent-child linkage fields <c>parentId</c> and <c>ocaGroupId</c> as typed properties
/// on <see cref="IbkrConduit.Orders.LiveOrder"/>, across both observed <c>ocaGroupId</c> shapes, and
/// that they survive a 401-driven re-authentication + retry.
/// </summary>
public class Rpd02BracketLinkageTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetLiveOrders_BracketAndOca_MapsParentIdAndBothOcaGroupIdShapes()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/orders",
            FixtureLoader.LoadBody("Orders", "GET-live-orders-bracket-oca"));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        snapshot.ShouldNotBeNull();
        snapshot.IsSnapshot.ShouldBeTrue("the fixture is a primed read (snapshot:true)");
        var orders = snapshot.Orders;
        orders.Count.ShouldBe(5);

        // The bracket parent: no parent linkage, no OCA membership.
        var parent = orders.Single(o => o.OrderId == 46184813);
        parent.ParentId.ShouldBeNull();
        parent.OcaGroupId.ShouldBeNull();

        // The two bracket exit legs: parentId echoes the parent's server orderId (an integer), and the
        // ocaGroupId is the BARE integer-valued string equal to the parent's orderId (no "oco-" prefix).
        foreach (var childId in new[] { 46184814, 46184815 })
        {
            var child = orders.Single(o => o.OrderId == childId);
            child.ParentId.ShouldBe(46184813);
            child.OcaGroupId.ShouldBe("46184813");
            // Grouping must key on child.ParentId matching the parent's OrderId.
            child.ParentId.ShouldBe(parent.OrderId);
        }

        // The explicit OCA group: prefixed "oco-<orderId>" ocaGroupId, no parent linkage.
        foreach (var ocaId in new[] { 636441077, 636441078 })
        {
            var leg = orders.Single(o => o.OrderId == ocaId);
            leg.ParentId.ShouldBeNull();
            leg.OcaGroupId.ShouldBe("oco-636441077");
        }

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetLiveOrders_BracketOca_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .InScenario("rpd02-live-orders-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/orders")
                .UsingGet())
            .InScenario("rpd02-live-orders-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Orders", "GET-live-orders-bracket-oca")));

        var snapshot = (await _harness.Client.Orders.GetLiveOrdersAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        // The linkage fields survive the re-auth + retry — proving they map on the replayed response.
        var child = snapshot.Orders.Single(o => o.OrderId == 46184814);
        child.ParentId.ShouldBe(46184813);
        child.OcaGroupId.ShouldBe("46184813");

        _harness.VerifyReauthenticationOccurred();
    }

    public async ValueTask DisposeAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
