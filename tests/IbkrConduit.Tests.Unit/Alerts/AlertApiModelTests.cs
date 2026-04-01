using System.Collections.Generic;
using System.Text.Json;
using IbkrConduit.Alerts;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Alerts;

public class AlertApiModelTests
{
    [Fact]
    public void CreateAlertRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new CreateAlertRequest(
            OrderId: 0,
            AlertName: "Price Alert",
            AlertMessage: "SPY above 500",
            AlertRepeatable: 1,
            OutsideRth: 0,
            Conditions: new List<AlertCondition>
            {
                new(Type: 1, Conidex: "265598", Operator: ">=", TriggerMethod: "0", Value: "500"),
            });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"orderId\":0");
        json.ShouldContain("\"alertName\":\"Price Alert\"");
        json.ShouldContain("\"alertRepeatable\":1");
        json.ShouldContain("\"outsideRth\":0");
        json.ShouldContain("\"conditions\"");
        json.ShouldContain("\"conidex\":\"265598\"");
        json.ShouldContain("\"operator\":");
    }

    [Fact]
    public void CreateAlertResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"request_id":1,"order_id":12345,"order_status":"Submitted","text":"Alert created"}""";

        var response = JsonSerializer.Deserialize<CreateAlertResponse>(json);

        response.ShouldNotBeNull();
        response.RequestId.ShouldBe(1);
        response.OrderId.ShouldBe(12345);
        response.OrderStatus.ShouldBe("Submitted");
        response.Text.ShouldBe("Alert created");
    }

    [Fact]
    public void AlertSummary_Deserializes_FromJsonCorrectly()
    {
        var json = """{"account":"DU1234567","order_id":12345,"alert_name":"Price Alert","alert_active":1,"order_status":"Submitted"}""";

        var response = JsonSerializer.Deserialize<AlertSummary>(json);

        response.ShouldNotBeNull();
        response.AccountId.ShouldBe("DU1234567");
        response.OrderId.ShouldBe(12345);
        response.AlertName.ShouldBe("Price Alert");
        response.AlertActive.ShouldBe(1);
        response.OrderStatus.ShouldBe("Submitted");
    }

    [Fact]
    public void AlertSummary_Deserializes_WithExtensionData()
    {
        var json = """{"account":"DU1","order_id":1,"alert_name":"Test","alert_active":0,"order_status":"Inactive","tif":"GTC"}""";

        var response = JsonSerializer.Deserialize<AlertSummary>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("tif");
    }

    [Fact]
    public void AlertDetail_Deserializes_WithConditions()
    {
        var json = """
        {
            "account": "DU1234567",
            "order_id": 12345,
            "alert_name": "Price Alert",
            "alert_message": "SPY above 500",
            "alert_active": 1,
            "alert_repeatable": 1,
            "conditions": [
                {
                    "type": 1,
                    "conidex": "265598",
                    "operator": ">=",
                    "triggerMethod": "0",
                    "value": "500"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<AlertDetail>(json);

        response.ShouldNotBeNull();
        response.AccountId.ShouldBe("DU1234567");
        response.OrderId.ShouldBe(12345);
        response.AlertName.ShouldBe("Price Alert");
        response.AlertMessage.ShouldBe("SPY above 500");
        response.AlertActive.ShouldBe(1);
        response.AlertRepeatable.ShouldBe(1);
        response.Conditions.Count.ShouldBe(1);
        response.Conditions[0].Type.ShouldBe(1);
        response.Conditions[0].Conidex.ShouldBe("265598");
        response.Conditions[0].Operator.ShouldBe(">=");
        response.Conditions[0].Value.ShouldBe("500");
    }

    [Fact]
    public void DeleteAlertResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"request_id":1,"order_id":12345,"msg":"Alert deleted","text":null}""";

        var response = JsonSerializer.Deserialize<DeleteAlertResponse>(json);

        response.ShouldNotBeNull();
        response.RequestId.ShouldBe(1);
        response.OrderId.ShouldBe(12345);
        response.Msg.ShouldBe("Alert deleted");
        response.Text.ShouldBeNull();
    }

    [Fact]
    public void AlertCondition_Deserializes_WithExtensionData()
    {
        var json = """{"type":1,"conidex":"265598","operator":">=","triggerMethod":"0","value":"500","logicBind":"a"}""";

        var response = JsonSerializer.Deserialize<AlertCondition>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("logicBind");
    }
}
