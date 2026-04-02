using System.Text.Json;
using IbkrConduit.Accounts;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Accounts;

public class AccountApiModelTests
{
    [Fact]
    public void IserverAccountsResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "accounts": ["DU1234567", "DU7654321"],
            "selectedAccount": "DU1234567"
        }
        """;

        var response = JsonSerializer.Deserialize<IserverAccountsResponse>(json);

        response.ShouldNotBeNull();
        response.Accounts.Count.ShouldBe(2);
        response.Accounts[0].ShouldBe("DU1234567");
        response.SelectedAccount.ShouldBe("DU1234567");
    }

    [Fact]
    public void IserverAccountsResponse_Deserializes_WithExtensionData()
    {
        var json = """{"accounts":["DU1"],"selectedAccount":"DU1","serverInfo":"v123","aliases":{"DU1":"My Account"}}""";

        var response = JsonSerializer.Deserialize<IserverAccountsResponse>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("serverInfo");
        response.AdditionalData.ShouldContainKey("aliases");
    }

    [Fact]
    public void SwitchAccountRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new SwitchAccountRequest(AcctId: "DU1234567");

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"acctId\":\"DU1234567\"");
    }

    [Fact]
    public void SwitchAccountResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"set":true,"selectedAccount":"DU1234567"}""";

        var response = JsonSerializer.Deserialize<SwitchAccountResponse>(json);

        response.ShouldNotBeNull();
        response.Set.ShouldBeTrue();
        response.SelectedAccount.ShouldBe("DU1234567");
    }

}
