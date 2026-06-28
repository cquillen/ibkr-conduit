using System.Text.Json;
using IbkrConduit.Orders;
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
    public void OrderWireModel_WithoutBracketFields_OmitsThem()
    {
        var wire = new OrderWireModel(265598, "BUY", 50m, "MKT", null, null, "GTC", null);

        var json = JsonSerializer.Serialize(wire);

        json.ShouldNotContain("cOID");
        json.ShouldNotContain("parentId");
        json.ShouldNotContain("isSingleGroup");
        json.ShouldNotContain("outsideRTH");
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
}
