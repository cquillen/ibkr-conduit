using System.Text.Json;
using IbkrConduit.Orders;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

public class OrderPlacementFieldsTests
{
    [Fact]
    public void OrderWireModel_ParentOrderWithCoid_SerializesCoidAndIsSingleGroup()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null)
        {
            CustomerOrderId = "parent-1",
            IsSingleGroup = true,
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"cOID\":\"parent-1\"");
        json.ShouldContain("\"isSingleGroup\":true");
    }

    [Fact]
    public void OrderWireModel_WithOutsideRth_SerializesOutsideRthName()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null)
        {
            OutsideRth = true,
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"outsideRTH\":true");
    }

    [Fact]
    public void OrderWireModel_WithIsSingleGroupFalse_SerializesExplicitFalse()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null)
        {
            IsSingleGroup = false,
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"isSingleGroup\":false");
    }

    [Fact]
    public void OrderWireModel_ChildWithParentId_SerializesParentId()
    {
        var wire = new OrderWireModel(265598, "SELL", 50m, "STP", 157.30m, null, "GTC", null)
        {
            ParentId = "Parent",
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"parentId\":\"Parent\"");
    }

    [Fact]
    public void OrderWireModel_WithExtOperator_SerializesExtOperator()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null)
        {
            ExtOperator = "person1234",
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"extOperator\":\"person1234\"");
    }

    [Fact]
    public void OrderWireModel_WithoutExtOperator_OmitsIt()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null);

        var json = JsonSerializer.Serialize(wire);

        json.ShouldNotContain("extOperator");
    }

    [Fact]
    public void OrderWireModel_WithoutBracketFields_OmitsThem()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null);

        var json = JsonSerializer.Serialize(wire);

        json.ShouldNotContain("cOID");
        json.ShouldNotContain("parentId");
        json.ShouldNotContain("isSingleGroup");
        json.ShouldNotContain("outsideRTH");
        json.ShouldNotContain("extOperator");
    }

    [Fact]
    public void OrderWireModel_WithTrailingParams_SerializesTrailingAmtAndType()
    {
        // Pins the probe-accepted shape (2026-07-07: trailingAmt:50, trailingType:"amt").
        var wire = new OrderWireModel(756733, "SELL", 1m, "TRAIL", null, null, "GTC", null)
        {
            TrailingAmt = 50m,
            TrailingType = "amt",
        };

        var json = JsonSerializer.Serialize(wire);

        json.ShouldContain("\"trailingAmt\":50");
        json.ShouldContain("\"trailingType\":\"amt\"");
    }

    [Fact]
    public void OrderWireModel_WithoutTrailingParams_OmitsThem()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null);

        var json = JsonSerializer.Serialize(wire);

        json.ShouldNotContain("trailingAmt");
        json.ShouldNotContain("trailingType");
    }

    [Fact]
    public void OrderSubmissionResponse_WithGroupFields_DeserializesLocalAndOcaGroupId()
    {
        var json = """{"order_id":"636441077","order_status":"PreSubmitted","local_order_id":"conduit-oca-a","oca_group_id":"oco-636441077","encrypt_message":"1"}""";

        var response = JsonSerializer.Deserialize<OrderSubmissionResponse>(json);

        response.ShouldNotBeNull();
        response.LocalOrderId.ShouldBe("conduit-oca-a");
        response.OcaGroupId.ShouldBe("oco-636441077");
    }

    [Fact]
    public void LiveOrder_WithOrderRef_DeserializesTypedProperty()
    {
        var json = """
        {"conid":265598,"orderId":111,"side":"BUY","status":"Filled",
         "order_ref":"Parent","filledQuantity":1,"remainingQuantity":0,"totalSize":1}
        """;

        var order = JsonSerializer.Deserialize<LiveOrder>(json);

        order.ShouldNotBeNull();
        order.OrderRef.ShouldBe("Parent");
        order.AdditionalData?.ContainsKey("order_ref").ShouldNotBe(true);
    }

    [Fact]
    public void OrderUpdate_WithOrderRef_DeserializesTypedProperty()
    {
        var json = """{"orderId":"111","conid":265598,"symbol":"SPY","side":"BUY","order_ref":"Parent"}""";

        var update = JsonSerializer.Deserialize<OrderUpdate>(json);

        update.ShouldNotBeNull();
        update.OrderRef.ShouldBe("Parent");
        update.AdditionalData?.ContainsKey("order_ref").ShouldNotBe(true);
    }
}
