using System.Text.Json;
using IbkrConduit.MarketData;
using Shouldly;

namespace IbkrConduit.Tests.Unit.MarketData;

public class MarketDataApiModelTests
{
    [Fact]
    public void UnsubscribeRequest_SerializesCorrectly()
    {
        var request = new UnsubscribeRequest(265598);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"conid\"");
        json.ShouldContain("265598");
    }

    [Fact]
    public void UnsubscribeResponse_DeserializesFromJson()
    {
        var json = """{ "success": true }""";

        var response = JsonSerializer.Deserialize<UnsubscribeResponse>(json);

        response.ShouldNotBeNull();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void UnsubscribeAllResponse_DeserializesFromJson()
    {
        var json = """{ "unsubscribed": true }""";

        var response = JsonSerializer.Deserialize<UnsubscribeAllResponse>(json);

        response.ShouldNotBeNull();
        response.Unsubscribed.ShouldBeTrue();
    }

    [Fact]
    public void ScannerRequest_SerializesCorrectly()
    {
        var request = new ScannerRequest("STK", "TOP_TRADE_COUNT", "STK.US.MAJOR",
            [new ScannerFilter("priceAbove", 5)]);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"instrument\"");
        json.ShouldContain("\"STK\"");
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"TOP_TRADE_COUNT\"");
        json.ShouldContain("\"location\"");
        json.ShouldContain("\"STK.US.MAJOR\"");
        json.ShouldContain("\"filter\"");
        json.ShouldContain("\"code\"");
        json.ShouldContain("\"priceAbove\"");
    }

    [Fact]
    public void ScannerResponse_DeserializesFromJson()
    {
        var json = """
            {
                "contracts": [
                    {
                        "server_id": "0",
                        "symbol": "AMD",
                        "conidex": "4391",
                        "con_id": 4391,
                        "available_chart_periods": "#R|1",
                        "company_name": "ADVANCED MICRO DEVICES",
                        "scan_data": "163.773K",
                        "contract_description_1": "AMD",
                        "listing_exchange": "NASDAQ.NMS",
                        "sec_type": "STK"
                    }
                ],
                "scan_data_column_name": "Trades"
            }
            """;

        var response = JsonSerializer.Deserialize<ScannerResponse>(json);

        response.ShouldNotBeNull();
        response.Contracts.ShouldNotBeNull();
        response.Contracts!.Count.ShouldBe(1);
        response.Contracts[0].Symbol.ShouldBe("AMD");
        response.Contracts[0].ConId.ShouldBe(4391);
        response.Contracts[0].CompanyName.ShouldBe("ADVANCED MICRO DEVICES");
        response.Contracts[0].ListingExchange.ShouldBe("NASDAQ.NMS");
        response.Contracts[0].SecType.ShouldBe("STK");
        response.ScanDataColumnName.ShouldBe("Trades");
    }

    [Fact]
    public void ScannerContract_CapturesExtensionData()
    {
        var json = """
            {
                "server_id": "0",
                "symbol": "AMD",
                "con_id": 4391,
                "unknown_field": "extra_value"
            }
            """;

        var contract = JsonSerializer.Deserialize<ScannerContract>(json);

        contract.ShouldNotBeNull();
        contract.AdditionalData.ShouldNotBeNull();
        contract.AdditionalData!.ShouldContainKey("unknown_field");
    }

    [Fact]
    public void HmdsScannerRequest_SerializesCorrectly()
    {
        var request = new HmdsScannerRequest("BOND", "BOND.US",
            "HIGH_BOND_ASK_YIELD_ALL", "BOND", 25, []);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"instrument\"");
        json.ShouldContain("\"BOND\"");
        json.ShouldContain("\"locations\"");
        json.ShouldContain("\"BOND.US\"");
        json.ShouldContain("\"scanCode\"");
        json.ShouldContain("\"HIGH_BOND_ASK_YIELD_ALL\"");
        json.ShouldContain("\"secType\"");
        json.ShouldContain("\"maxItems\"");
    }

    [Fact]
    public void HmdsScannerResponse_DeserializesFromJson()
    {
        var json = """
            {
                "total": "17262",
                "size": "250",
                "offset": "0",
                "scanTime": "20231214-18:55:25",
                "id": "scanner1",
                "Contracts": {
                    "Contract": [
                        {
                            "inScanTime": "20231214-18:55:25",
                            "contractID": "431424315"
                        }
                    ]
                }
            }
            """;

        var response = JsonSerializer.Deserialize<HmdsScannerResponse>(json);

        response.ShouldNotBeNull();
        response.Total.ShouldBe("17262");
        response.Size.ShouldBe("250");
        response.Offset.ShouldBe("0");
        response.ScanTime.ShouldBe("20231214-18:55:25");
        response.Id.ShouldBe("scanner1");
        response.Contracts.ShouldNotBeNull();
        response.Contracts!.Contract.ShouldNotBeNull();
        response.Contracts.Contract!.Count.ShouldBe(1);
        response.Contracts.Contract[0].InScanTime.ShouldBe("20231214-18:55:25");
        response.Contracts.Contract[0].ContractId.ShouldBe("431424315");
    }

    [Fact]
    public void ScannerParameters_DeserializesFromJson()
    {
        var json = """
            {
                "scan_type_list": [
                    {
                        "display_name": "Top % Gainers",
                        "code": "TOP_PERC_GAIN",
                        "instruments": ["STK", "ETF"]
                    }
                ],
                "instrument_list": [
                    {
                        "display_name": "US Stocks",
                        "type": "STK",
                        "filters": ["priceAbove", "priceBelow"]
                    }
                ],
                "filter_list": [
                    {
                        "group": "price",
                        "display_name": "Price Above",
                        "code": "priceAbove",
                        "type": "int"
                    }
                ],
                "location_tree": [
                    {
                        "display_name": "US Stocks",
                        "type": "STK",
                        "locations": [
                            {
                                "display_name": "US Major",
                                "type": "STK.US.MAJOR",
                                "locations": []
                            }
                        ]
                    }
                ]
            }
            """;

        var parameters = JsonSerializer.Deserialize<ScannerParameters>(json);

        parameters.ShouldNotBeNull();
        parameters.ScanTypeList.ShouldNotBeNull();
        parameters.ScanTypeList!.Count.ShouldBe(1);
        parameters.ScanTypeList[0].DisplayName.ShouldBe("Top % Gainers");
        parameters.ScanTypeList[0].Code.ShouldBe("TOP_PERC_GAIN");
        parameters.InstrumentList.ShouldNotBeNull();
        parameters.InstrumentList!.Count.ShouldBe(1);
        parameters.InstrumentList[0].DisplayName.ShouldBe("US Stocks");
        parameters.FilterList.ShouldNotBeNull();
        parameters.FilterList!.Count.ShouldBe(1);
        parameters.FilterList[0].Code.ShouldBe("priceAbove");
        parameters.LocationTree.ShouldNotBeNull();
        parameters.LocationTree!.Count.ShouldBe(1);
        parameters.LocationTree[0].Locations.ShouldNotBeNull();
        parameters.LocationTree[0].Locations!.Count.ShouldBe(1);
        parameters.LocationTree[0].Locations[0].Type.ShouldBe("STK.US.MAJOR");
    }
}
