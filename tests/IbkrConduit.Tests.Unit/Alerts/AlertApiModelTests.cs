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
            Tif: "GTC",
            Conditions: new List<AlertCondition>
            {
                new(Type: 1, Conidex: "265598", Operator: ">=", TriggerMethod: "0", Value: "500"),
            });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"orderId\":0");
        json.ShouldContain("\"alertName\":\"Price Alert\"");
        json.ShouldContain("\"alertRepeatable\":1");
        json.ShouldContain("\"outsideRth\":0");
        json.ShouldContain("\"tif\":\"GTC\"");
        json.ShouldContain("\"conditions\"");
        json.ShouldContain("\"conidex\":\"265598\"");
        json.ShouldContain("\"operator\":");
    }

    [Fact]
    public void CreateAlertResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"request_id":1,"order_id":12345,"success":true,"text":"Alert created","order_status":"Submitted","warning_message":null}""";

        var response = JsonSerializer.Deserialize<CreateAlertResponse>(json);

        response.ShouldNotBeNull();
        response.RequestId.ShouldBe(1);
        response.OrderId.ShouldBe(12345);
        response.Success.ShouldBeTrue();
        response.Text.ShouldBe("Alert created");
        response.OrderStatus.ShouldBe("Submitted");
        response.WarningMessage.ShouldBeNull();
    }

    [Fact]
    public void AlertSummary_Deserializes_FromJsonCorrectly()
    {
        var json = """{"account":"DU1234567","order_id":12345,"alert_name":"Price Alert","alert_active":1,"alert_repeatable":0,"order_time":"20260403-14:30:00","alert_triggered":false}""";

        var response = JsonSerializer.Deserialize<AlertSummary>(json);

        response.ShouldNotBeNull();
        response.Account.ShouldBe("DU1234567");
        response.OrderId.ShouldBe(12345);
        response.AlertName.ShouldBe("Price Alert");
        response.AlertActive.ShouldBe(1);
        response.AlertRepeatable.ShouldBe(0);
        response.OrderTime.ShouldBe("20260403-14:30:00");
        response.AlertTriggered.ShouldBeFalse();
    }

    [Fact]
    public void AlertSummary_Deserializes_WithExtensionData()
    {
        var json = """{"account":"DU1","order_id":1,"alert_name":"Test","alert_active":0,"alert_repeatable":0,"unknown_field":"extra"}""";

        var response = JsonSerializer.Deserialize<AlertSummary>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("unknown_field");
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
            "tif": "GTC",
            "order_status": "PreSubmitted",
            "alert_triggered": false,
            "condition_size": 1,
            "condition_outside_rth": 0,
            "conditions": [
                {
                    "condition_type": 1,
                    "conidex": "265598",
                    "contract_description_1": "SPDR S&P 500 ETF Trust",
                    "condition_operator": ">=",
                    "condition_trigger_method": "0",
                    "condition_value": "500",
                    "condition_logic_bind": "a",
                    "condition_time_zone": "US/Eastern"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<AlertDetail>(json);

        response.ShouldNotBeNull();
        response.Account.ShouldBe("DU1234567");
        response.OrderId.ShouldBe(12345);
        response.AlertName.ShouldBe("Price Alert");
        response.AlertMessage.ShouldBe("SPY above 500");
        response.AlertActive.ShouldBe(1);
        response.AlertRepeatable.ShouldBe(1);
        response.Tif.ShouldBe("GTC");
        response.OrderStatus.ShouldBe("PreSubmitted");
        response.AlertTriggered.ShouldBeFalse();
        response.ConditionSize.ShouldBe(1);
        response.ConditionOutsideRth.ShouldBe(0);
        response.Conditions.Count.ShouldBe(1);
        response.Conditions[0].ConditionType.ShouldBe(1);
        response.Conditions[0].Conidex.ShouldBe("265598");
        response.Conditions[0].ContractDescription1.ShouldBe("SPDR S&P 500 ETF Trust");
        response.Conditions[0].ConditionOperator.ShouldBe(">=");
        response.Conditions[0].ConditionValue.ShouldBe("500");
        response.Conditions[0].ConditionLogicBind.ShouldBe("a");
    }

    [Fact]
    public void DeleteAlertResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"request_id":1,"order_id":12345,"success":true,"text":"Alert deleted","failure_list":null}""";

        var response = JsonSerializer.Deserialize<DeleteAlertResponse>(json);

        response.ShouldNotBeNull();
        response.RequestId.ShouldBe(1);
        response.OrderId.ShouldBe(12345);
        response.Success.ShouldBeTrue();
        response.Text.ShouldBe("Alert deleted");
        response.FailureList.ShouldBeNull();
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

    [Fact]
    public void AlertConditionDetail_Deserializes_FromJsonCorrectly()
    {
        var json = """{"condition_type":1,"conidex":"265598","contract_description_1":"SPY","condition_operator":">=","condition_trigger_method":"0","condition_value":"500","condition_logic_bind":"a","condition_time_zone":"US/Eastern"}""";

        var response = JsonSerializer.Deserialize<AlertConditionDetail>(json);

        response.ShouldNotBeNull();
        response.ConditionType.ShouldBe(1);
        response.Conidex.ShouldBe("265598");
        response.ContractDescription1.ShouldBe("SPY");
        response.ConditionOperator.ShouldBe(">=");
        response.ConditionTriggerMethod.ShouldBe("0");
        response.ConditionValue.ShouldBe("500");
        response.ConditionLogicBind.ShouldBe("a");
        response.ConditionTimeZone.ShouldBe("US/Eastern");
    }

    [Fact]
    public void AlertActivationRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new AlertActivationRequest(AlertId: 100, AlertActive: 1);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"alertId\":100");
        json.ShouldContain("\"alertActive\":1");
    }

    [Fact]
    public void AlertActivationResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"request_id":1,"order_id":100,"success":true,"text":"Activated","failure_list":null}""";

        var response = JsonSerializer.Deserialize<AlertActivationResponse>(json);

        response.ShouldNotBeNull();
        response.RequestId.ShouldBe(1);
        response.OrderId.ShouldBe(100);
        response.Success.ShouldBeTrue();
        response.Text.ShouldBe("Activated");
        response.FailureList.ShouldBeNull();
    }
}
