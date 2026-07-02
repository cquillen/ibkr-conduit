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
