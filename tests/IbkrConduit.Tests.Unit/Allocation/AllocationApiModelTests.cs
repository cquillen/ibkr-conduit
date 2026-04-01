using System.Text.Json;
using IbkrConduit.Allocation;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Allocation;

public class AllocationApiModelTests
{
    [Fact]
    public void AllocationAccountsResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "accounts": [
                {
                    "data": [
                        {"value": "2677.89", "key": "NetLiquidation"},
                        {"value": "2134.76", "key": "AvailableEquity"}
                    ],
                    "name": "U123456"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<AllocationAccountsResponse>(json);

        response.ShouldNotBeNull();
        response.Accounts.Count.ShouldBe(1);
        response.Accounts[0].Name.ShouldBe("U123456");
        response.Accounts[0].Data.Count.ShouldBe(2);
        response.Accounts[0].Data[0].Key.ShouldBe("NetLiquidation");
        response.Accounts[0].Data[0].Value.ShouldBe("2677.89");
    }

    [Fact]
    public void AllocationGroupListResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "data": [
                {
                    "allocation_method": "N",
                    "size": 10,
                    "name": "Group_1_NetLiq"
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<AllocationGroupListResponse>(json);

        response.ShouldNotBeNull();
        response.Data.Count.ShouldBe(1);
        response.Data[0].Name.ShouldBe("Group_1_NetLiq");
        response.Data[0].AllocationMethod.ShouldBe("N");
        response.Data[0].Size.ShouldBe(10);
    }

    [Fact]
    public void AllocationGroupDetail_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "name": "Group_1_NetLiq",
            "accounts": [
                {"amount": 1, "name": "DU1234567"},
                {"amount": 5, "name": "DU9876543"}
            ],
            "default_method": "R"
        }
        """;

        var response = JsonSerializer.Deserialize<AllocationGroupDetail>(json);

        response.ShouldNotBeNull();
        response.Name.ShouldBe("Group_1_NetLiq");
        response.DefaultMethod.ShouldBe("R");
        response.Accounts.Count.ShouldBe(2);
        response.Accounts[0].Name.ShouldBe("DU1234567");
        response.Accounts[0].Amount.ShouldBe(1m);
        response.Accounts[1].Name.ShouldBe("DU9876543");
        response.Accounts[1].Amount.ShouldBe(5m);
    }

    [Fact]
    public void AllocationGroupRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new AllocationGroupRequest(
            Name: "TestGroup",
            Accounts: [new AllocationGroupAccount("U123", 10)],
            DefaultMethod: "N");

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"name\":\"TestGroup\"");
        json.ShouldContain("\"default_method\":\"N\"");
        json.ShouldContain("\"accounts\":");
    }

    [Fact]
    public void AllocationGroupRequest_WithPrevName_Serializes_CorrectJsonPropertyNames()
    {
        var request = new AllocationGroupRequest(
            Name: "NewName",
            Accounts: [new AllocationGroupAccount("U123", 10)],
            DefaultMethod: "A",
            PrevName: "OldName");

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"prev_name\":\"OldName\"");
    }

    [Fact]
    public void AllocationGroupNameRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new AllocationGroupNameRequest(Name: "Group_1_NetLiq");

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"name\":\"Group_1_NetLiq\"");
    }

    [Fact]
    public void AllocationPresetsResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """
        {
            "group_auto_close_positions": false,
            "default_method_for_all": "N",
            "profiles_auto_close_positions": false,
            "strict_credit_check": false,
            "group_proportional_allocation": false
        }
        """;

        var response = JsonSerializer.Deserialize<AllocationPresetsResponse>(json);

        response.ShouldNotBeNull();
        response.GroupAutoClosePositions.ShouldBeFalse();
        response.DefaultMethodForAll.ShouldBe("N");
        response.ProfilesAutoClosePositions.ShouldBeFalse();
        response.StrictCreditCheck.ShouldBeFalse();
        response.GroupProportionalAllocation.ShouldBeFalse();
    }

    [Fact]
    public void AllocationPresetsRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new AllocationPresetsRequest(
            DefaultMethodForAll: "E",
            GroupAutoClosePositions: true,
            ProfilesAutoClosePositions: true,
            StrictCreditCheck: false,
            GroupProportionalAllocation: false);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"default_method_for_all\":\"E\"");
        json.ShouldContain("\"group_auto_close_positions\":true");
        json.ShouldContain("\"profiles_auto_close_positions\":true");
        json.ShouldContain("\"strict_credit_check\":false");
        json.ShouldContain("\"group_proportional_allocation\":false");
    }

    [Fact]
    public void AllocationSuccessResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"success":true}""";

        var response = JsonSerializer.Deserialize<AllocationSuccessResponse>(json);

        response.ShouldNotBeNull();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void AllocationAccountsResponse_Deserializes_WithExtensionData()
    {
        var json = """{"accounts":[],"extra":"val"}""";

        var response = JsonSerializer.Deserialize<AllocationAccountsResponse>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("extra");
    }
}
