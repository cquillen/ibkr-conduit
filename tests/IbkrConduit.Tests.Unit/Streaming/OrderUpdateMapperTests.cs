using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class OrderUpdateMapperTests
{
    // Real sor frame captured live: order(s) are wrapped in an args array, and orderId
    // arrives as a JSON number rather than a quoted string.
    private const string _realOrderFrame = """
        {"topic":"sor","args":[{"acct":"DUO873728","conidex":"756733","conid":756733,"orderId":656804954,"isEventTrading":"0"}]}
        """;

    // Real sor frame captured live for a market order: "price" arrives as an empty string
    // (no limit price on a market order), and the symbol/size live under "ticker"/"totalSize"
    // rather than "symbol"/"size". Before the empty-tolerant converters, deserializing
    // "price":"" threw a JsonException that killed the whole sor subscription.
    private const string _realMarketOrderFrame = """
        {"topic":"sor","args":[{"acct":"DUO873728","conid":320227571,"orderId":196655192,"orderDesc":"Buy 1 QQQ Market, Day","ticker":"QQQ","secType":"STK","remainingQuantity":1.0,"filledQuantity":0.0,"totalSize":1.0,"companyName":"INVESCO QQQ TRUST SERIES 1","status":"Inactive","orderType":"Market","order_ref":"submit-143824-5264","price":"","side":"BUY"}]}
        """;

    [Fact]
    public void MapMany_RealMarketOrderFrameWithEmptyPrice_YieldsOrderWithNullPriceAndCorrectFields()
    {
        var frame = JsonDocument.Parse(_realMarketOrderFrame).RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(1);
        var order = orders[0];
        order.Price.ShouldBeNull();
        order.Symbol.ShouldBe("QQQ");
        order.Size.ShouldBe(1m);
        order.Side.ShouldBe("BUY");
        order.Status.ShouldBe("Inactive");
        order.OrderRef.ShouldBe("submit-143824-5264");
        order.FilledQuantity.ShouldBe(0m);
        order.RemainingQuantity.ShouldBe(1m);
        order.OrderId.ShouldBe("196655192");
        order.Conid.ShouldBe(320227571);
    }

    [Fact]
    public void MapMany_SparseFrame_OmittedFieldsAreNull()
    {
        // WIR-1 (critical): a sparse sor delta omits fields wholesale. Every wire-optional field
        // the frame does not carry must deserialize to null — never a fabricated 0m / "" — so a
        // consumer's sparse-delta merge can tell "absent from this frame" from a genuine zero /
        // empty and never regresses a partially-filled order to unfilled.
        var frame = JsonDocument.Parse(_realOrderFrame).RootElement;

        var order = OrderUpdateMapper.MapMany(frame).Single();

        order.OrderId.ShouldBe("656804954"); // the dedupe key is present on every frame
        order.Conid.ShouldBe(756733);        // present on this frame
        order.Size.ShouldBeNull();
        order.FilledQuantity.ShouldBeNull();
        order.RemainingQuantity.ShouldBeNull();
        order.Status.ShouldBeNull();
        order.Side.ShouldBeNull();
        order.Symbol.ShouldBeNull();
        order.OrderType.ShouldBeNull();
    }

    [Fact]
    public void MapMany_FrameOmittingConid_ConidIsNull()
    {
        // conid is wire-optional on a sor delta; its absence must be null, not a fabricated 0.
        var frame = JsonDocument.Parse("""{"topic":"sor","args":[{"orderId":42}]}""").RootElement;

        OrderUpdateMapper.MapMany(frame).Single().Conid.ShouldBeNull();
    }

    [Fact]
    public void MapMany_RealOrderFrame_YieldsOneOrderWithNumericOrderIdCoercedToString()
    {
        var frame = JsonDocument.Parse(_realOrderFrame).RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(1);
        orders[0].OrderId.ShouldBe("656804954");
        orders[0].Conid.ShouldBe(756733);
    }

    [Fact]
    public void MapMany_TwoOrdersInArgs_YieldsBoth()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sor","args":[{"orderId":1},{"orderId":2}]}""").RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(2);
        orders[0].OrderId.ShouldBe("1");
        orders[1].OrderId.ShouldBe("2");
    }

    [Fact]
    public void MapMany_MissingArgs_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"sor"}""").RootElement;

        OrderUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_ArgsNotAnArray_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"sor","args":{"orderId":1}}""").RootElement;

        OrderUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }
}
