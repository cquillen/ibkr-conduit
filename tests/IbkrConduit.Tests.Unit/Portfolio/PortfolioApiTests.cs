using System.Text.Json;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

public class PortfolioApiTests
{
    [Fact]
    public void Account_DeserializesFromJson()
    {
        var json = """
            {
                "id": "U1234567",
                "accountTitle": "Paper Trading Account",
                "type": "INDIVIDUAL"
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var account = JsonSerializer.Deserialize<Account>(json, options);

        account.ShouldNotBeNull();
        account.Id.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Paper Trading Account");
        account.Type.ShouldBe("INDIVIDUAL");
    }

    [Fact]
    public void ComboPosition_DeserializesFromJson()
    {
        var json = """
            {
                "name": "CP.CP66a00d50",
                "description": "1*708474422-1*710225103",
                "legs": [
                    { "conid": "708474422", "ratio": 1 },
                    { "conid": "710225103", "ratio": -1 }
                ],
                "positions": [
                    {
                        "acctId": "U1234567",
                        "conid": 708474422,
                        "contractDesc": "SPX AUG2024 5555 P",
                        "position": 1.0,
                        "mktPrice": 59.66,
                        "mktValue": 5966.0,
                        "avgCost": 6011.71,
                        "avgPrice": 60.12,
                        "realizedPnl": 0.0,
                        "unrealizedPnl": -45.99,
                        "currency": "USD",
                        "name": "SPX AUG2024 5555 P",
                        "assetClass": "OPT",
                        "sector": null,
                        "ticker": "SPX",
                        "multiplier": null,
                        "isUS": true
                    }
                ]
            }
            """;

        var combo = JsonSerializer.Deserialize<ComboPosition>(json);

        combo.ShouldNotBeNull();
        combo.Name.ShouldBe("CP.CP66a00d50");
        combo.Description.ShouldBe("1*708474422-1*710225103");
        combo.Legs.ShouldNotBeNull();
        combo.Legs!.Count.ShouldBe(2);
        combo.Legs[0].Conid.ShouldBe("708474422");
        combo.Legs[0].Ratio.ShouldBe(1);
        combo.Legs[1].Conid.ShouldBe("710225103");
        combo.Legs[1].Ratio.ShouldBe(-1);
        combo.Positions.ShouldNotBeNull();
        combo.Positions!.Count.ShouldBe(1);
        combo.Positions[0].AccountId.ShouldBe("U1234567");
        combo.Positions[0].AssetClass.ShouldBe("OPT");
    }

    [Fact]
    public void ComboLeg_DeserializesFromJson()
    {
        var json = """{ "conid": "708474422", "ratio": 1 }""";

        var leg = JsonSerializer.Deserialize<ComboLeg>(json);

        leg.ShouldNotBeNull();
        leg.Conid.ShouldBe("708474422");
        leg.Ratio.ShouldBe(1);
    }

    [Fact]
    public void SubAccount_DeserializesFromJson()
    {
        var json = """
            {
                "id": "U1234567",
                "accountId": "U1234567",
                "accountTitle": "Paper Trading",
                "type": "INDIVIDUAL",
                "desc": "U1234567",
                "brokerageAccess": false
            }
            """;

        var sub = JsonSerializer.Deserialize<SubAccount>(json);

        sub.ShouldNotBeNull();
        sub.Id.ShouldBe("U1234567");
        sub.AccountId.ShouldBe("U1234567");
        sub.AccountTitle.ShouldBe("Paper Trading");
        sub.AccountType.ShouldBe("INDIVIDUAL");
        sub.Description.ShouldBe("U1234567");
        sub.AdditionalData.ShouldNotBeNull();
        sub.AdditionalData!.ShouldContainKey("brokerageAccess");
    }

    [Fact]
    public void AllPeriodsPerformance_DeserializesFromJson()
    {
        var json = """
            {
                "currencyType": "base",
                "rc": 0,
                "view": ["U1234567"],
                "nd": 366,
                "id": "getPerformanceAllPeriods",
                "pm": "TWR"
            }
            """;

        var perf = JsonSerializer.Deserialize<AllPeriodsPerformance>(json);

        perf.ShouldNotBeNull();
        perf.CurrencyType.ShouldBe("base");
        perf.Rc.ShouldBe(0);
        perf.AdditionalData.ShouldNotBeNull();
        perf.AdditionalData!.ShouldContainKey("view");
        perf.AdditionalData.ShouldContainKey("nd");
        perf.AdditionalData.ShouldContainKey("pm");
    }

    [Fact]
    public void PartitionedPnl_DeserializesFromJson()
    {
        var json = """
            {
                "upnl": {
                    "U1234567.Core": {
                        "rowType": 1,
                        "dpl": 15.7,
                        "nl": 10000.0,
                        "upl": 607.0,
                        "el": 10000.0,
                        "mv": 0.0
                    }
                }
            }
            """;

        var pnl = JsonSerializer.Deserialize<PartitionedPnl>(json);

        pnl.ShouldNotBeNull();
        pnl.Upnl.ShouldNotBeNull();
        pnl.Upnl!.ShouldContainKey("U1234567.Core");
        var entry = pnl.Upnl["U1234567.Core"];
        entry.RowType.ShouldBe(1);
        entry.Dpl.ShouldBe(15.7m);
        entry.Nl.ShouldBe(10000.0m);
        entry.Upl.ShouldBe(607.0m);
        entry.El.ShouldBe(10000.0m);
        entry.Mv.ShouldBe(0.0m);
    }

    [Fact]
    public void PnlEntry_DeserializesFromJson()
    {
        var json = """
            {
                "rowType": 1,
                "dpl": 15.7,
                "nl": 10000.0,
                "upl": 607.0,
                "el": 10000.0,
                "mv": 0.0,
                "customField": "extra"
            }
            """;

        var entry = JsonSerializer.Deserialize<PnlEntry>(json);

        entry.ShouldNotBeNull();
        entry.RowType.ShouldBe(1);
        entry.Dpl.ShouldBe(15.7m);
        entry.AdditionalData.ShouldNotBeNull();
        entry.AdditionalData!.ShouldContainKey("customField");
    }

    [Fact]
    public void ConsolidatedAllocationRequest_SerializesCorrectly()
    {
        var request = new ConsolidatedAllocationRequest(["U1234567", "U4567890"]);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"acctIds\"");
        json.ShouldContain("U1234567");
        json.ShouldContain("U4567890");
    }

    [Fact]
    public void AllPeriodsRequest_SerializesCorrectly()
    {
        var request = new AllPeriodsRequest(["U1234567"]);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"acctIds\"");
        json.ShouldContain("U1234567");
    }
}
