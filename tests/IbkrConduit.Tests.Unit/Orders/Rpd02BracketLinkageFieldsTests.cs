using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IbkrConduit.Orders;
using IbkrConduit.Serialization;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// RPD-02: pins the bracket/OCA parent-child linkage fields <see cref="LiveOrder.ParentId"/> and
/// <see cref="LiveOrder.OcaGroupId"/> — promoted from <c>AdditionalData</c> to typed nullable
/// properties. Covers both observed <c>ocaGroupId</c> shapes (prefixed <c>"oco-…"</c> and bare
/// integer-string) and the <c>parentId</c> request/response type asymmetry (integer on the wire,
/// tolerant of the integer-string form). Deserializes through the exact serializer every Refit
/// client in the library uses.
/// </summary>
public class Rpd02BracketLinkageFieldsTests
{
    private static Task<T?> DeserializeAsync<T>(string json)
    {
        var serializer = IbkrRefitSettings.Create().ContentSerializer;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return serializer.FromHttpContentAsync<T>(content, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LiveOrder_ParentId_DeserializesFromJsonNumber()
    {
        // The observed wire shape: parentId is an UNQUOTED JSON integer equal to the parent's orderId.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184814,"side":"BUY","status":"PreSubmitted","parentId":46184813}""");

        result.ShouldNotBeNull();
        result!.ParentId.ShouldBe(46184813);
    }

    [Fact]
    public async Task LiveOrder_ParentId_DeserializesFromIntegerValuedString()
    {
        // The request/response asymmetry hedge: tolerate the integer-valued string form IBKR uses on
        // its other order surfaces, normalizing to the same int? value the number form yields.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184814,"side":"BUY","status":"PreSubmitted","parentId":"46184813"}""");

        result.ShouldNotBeNull();
        result!.ParentId.ShouldBe(46184813);
    }

    [Fact]
    public async Task LiveOrder_ParentIdAbsent_MapsToNull()
    {
        // A non-child order (the bracket parent, or a standalone order) carries no parentId.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184813,"side":"BUY","status":"PreSubmitted"}""");

        result.ShouldNotBeNull();
        result!.ParentId.ShouldBeNull();
    }

    [Fact]
    public async Task LiveOrder_OcaGroupId_PrefixedShape_KeptRawWithPrefix()
    {
        // An explicit OCA group leg: prefixed "oco-<orderId>" string — kept exactly, prefix intact.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":636441078,"side":"BUY","status":"Cancelled","ocaGroupId":"oco-636441077"}""");

        result.ShouldNotBeNull();
        result!.OcaGroupId.ShouldBe("oco-636441077");
    }

    [Fact]
    public async Task LiveOrder_OcaGroupId_BareIntegerShape_KeptRawWithoutPrefix()
    {
        // A bracket exit leg: a BARE integer-valued string equal to the parent's orderId — no "oco-"
        // prefix. Must be kept verbatim, never prefix-normalized or reinterpreted as a number.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184814,"side":"BUY","status":"PreSubmitted","ocaGroupId":"46184813"}""");

        result.ShouldNotBeNull();
        result!.OcaGroupId.ShouldBe("46184813");
    }

    [Fact]
    public async Task LiveOrder_OcaGroupIdAbsent_MapsToNull()
    {
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184813,"side":"BUY","status":"PreSubmitted"}""");

        result.ShouldNotBeNull();
        result!.OcaGroupId.ShouldBeNull();
    }

    [Fact]
    public async Task LiveOrder_BracketChildLeg_MapsBothFields_NotIntoAdditionalData()
    {
        // False-green guard: with the fields correctly mapped by their exact JSON names, neither
        // parentId nor ocaGroupId spills into the extension bucket. If either JsonPropertyName is
        // wrong (or the fields are absent), the value lands in AdditionalData and this fails.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":46184814,"side":"BUY","status":"PreSubmitted","parentId":46184813,"ocaGroupId":"46184813"}""");

        result.ShouldNotBeNull();
        result!.ParentId.ShouldBe(46184813);
        result.OcaGroupId.ShouldBe("46184813");
        // Fully mapped — nothing spills into AdditionalData.
        result.AdditionalData.ShouldBeNull();
    }
}
