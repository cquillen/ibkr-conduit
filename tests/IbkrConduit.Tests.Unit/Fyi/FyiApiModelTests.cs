using System.Text.Json;
using IbkrConduit.Fyi;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Fyi;

public class FyiApiModelTests
{
    [Fact]
    public void UnreadBulletinCountResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"BN":4}""";

        var response = JsonSerializer.Deserialize<UnreadBulletinCountResponse>(json);

        response.ShouldNotBeNull();
        response.BN.ShouldBe(4);
    }

    [Fact]
    public void UnreadBulletinCountResponse_Deserializes_WithExtensionData()
    {
        var json = """{"BN":4,"extra":"value"}""";

        var response = JsonSerializer.Deserialize<UnreadBulletinCountResponse>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("extra");
    }

    [Fact]
    public void FyiSettingItem_Deserializes_FromJsonCorrectly()
    {
        var json = """{"FC":"M8","H":0,"A":1,"FD":"Notify me about 871(m) trades.","FN":"871(m) Trades"}""";

        var response = JsonSerializer.Deserialize<FyiSettingItem>(json);

        response.ShouldNotBeNull();
        response.FC.ShouldBe("M8");
        response.FN.ShouldBe("871(m) Trades");
        response.FD.ShouldBe("Notify me about 871(m) trades.");
        response.H.ShouldBe(0);
        response.A.ShouldBe(1);
    }

    [Fact]
    public void FyiSettingUpdateRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new FyiSettingUpdateRequest(Enabled: true);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"enabled\":true");
    }

    [Fact]
    public void FyiAcknowledgementResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"V":1,"T":10}""";

        var response = JsonSerializer.Deserialize<FyiAcknowledgementResponse>(json);

        response.ShouldNotBeNull();
        response.V.ShouldBe(1);
        response.T.ShouldBe(10);
    }

    [Fact]
    public void FyiDisclaimerResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"FC":"SM","DT":"This is a disclaimer message."}""";

        var response = JsonSerializer.Deserialize<FyiDisclaimerResponse>(json);

        response.ShouldNotBeNull();
        response.FC.ShouldBe("SM");
        response.DT.ShouldBe("This is a disclaimer message.");
    }

    [Fact]
    public void FyiDeliveryOptionsResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "E": [
                {
                    "NM": "iPhone",
                    "I": "apn://device1",
                    "UI": "apn://device1",
                    "A": 1
                }
            ],
            "M": 1
        }
        """;

        var response = JsonSerializer.Deserialize<FyiDeliveryOptionsResponse>(json);

        response.ShouldNotBeNull();
        response.M.ShouldBe(1);
        response.E.Count.ShouldBe(1);
        response.E[0].NM.ShouldBe("iPhone");
        response.E[0].I.ShouldBe("apn://device1");
        response.E[0].A.ShouldBe(1);
    }

    [Fact]
    public void FyiDeviceRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new FyiDeviceRequest(
            DeviceName: "iPhone",
            DeviceId: "apn://device1",
            UiName: "apn://device1",
            Enabled: true);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"deviceName\":\"iPhone\"");
        json.ShouldContain("\"deviceId\":\"apn://device1\"");
        json.ShouldContain("\"uiName\":\"apn://device1\"");
        json.ShouldContain("\"enabled\":true");
    }

    [Fact]
    public void FyiNotification_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "R": 0,
            "D": "1702469440.0",
            "MS": "IBKR FYI: Option Expiration",
            "MD": "One or more options expiring.",
            "ID": "2023121370119463",
            "HT": 0,
            "FC": "OE"
        }
        """;

        var response = JsonSerializer.Deserialize<FyiNotification>(json);

        response.ShouldNotBeNull();
        response.R.ShouldBe(0);
        response.D.ShouldBe("1702469440.0");
        response.MS.ShouldBe("IBKR FYI: Option Expiration");
        response.MD.ShouldBe("One or more options expiring.");
        response.ID.ShouldBe("2023121370119463");
        response.HT.ShouldBe(0);
        response.FC.ShouldBe("OE");
    }

    [Fact]
    public void FyiNotificationReadResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"V":1,"T":5,"P":{"R":1,"ID":"12345678901234567"}}""";

        var response = JsonSerializer.Deserialize<FyiNotificationReadResponse>(json);

        response.ShouldNotBeNull();
        response.V.ShouldBe(1);
        response.T.ShouldBe(5);
        response.P.ShouldNotBeNull();
        response.P!.R.ShouldBe(1);
        response.P.ID.ShouldBe("12345678901234567");
    }

    [Fact]
    public void FyiNotification_Deserializes_WithExtensionData()
    {
        var json = """{"R":0,"D":"123","MS":"Title","MD":"Content","ID":"1","HT":0,"FC":"SM","extra":"val"}""";

        var response = JsonSerializer.Deserialize<FyiNotification>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("extra");
    }
}
