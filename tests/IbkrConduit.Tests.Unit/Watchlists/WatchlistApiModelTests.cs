using System.Collections.Generic;
using System.Text.Json;
using IbkrConduit.Watchlists;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Watchlists;

public class WatchlistApiModelTests
{
    [Fact]
    public void CreateWatchlistRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new CreateWatchlistRequest(
            Id: "My Watchlist",
            Name: "My Watchlist",
            Rows: new List<WatchlistRow>
            {
                new(C: 265598, H: "AAPL"),
                new(C: 272093, H: "MSFT"),
            });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"id\":\"My Watchlist\"");
        json.ShouldContain("\"name\":\"My Watchlist\"");
        json.ShouldContain("\"rows\"");
        json.ShouldContain("\"C\":265598");
        json.ShouldContain("\"H\":\"AAPL\"");
    }

    [Fact]
    public void CreateWatchlistResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"id":"99999","hash":"1775319529009","name":"Capture Test","readOnly":false,"instruments":[]}""";

        var response = JsonSerializer.Deserialize<CreateWatchlistResponse>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("99999");
        response.Hash.ShouldBe("1775319529009");
        response.Name.ShouldBe("Capture Test");
        response.ReadOnly.ShouldBeFalse();
        response.Instruments.ShouldBeEmpty();
    }

    [Fact]
    public void WatchlistSummary_Deserializes_FromJsonCorrectly()
    {
        var json = """{"id":"wl1","name":"Tech Stocks","modified":1700000000,"is_open":false,"read_only":false,"type":"watchlist"}""";

        var response = JsonSerializer.Deserialize<WatchlistSummary>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("wl1");
        response.Name.ShouldBe("Tech Stocks");
        response.Modified.ShouldBe(1700000000);
        response.IsOpen.ShouldBeFalse();
        response.ReadOnly.ShouldBeFalse();
        response.Type.ShouldBe("watchlist");
    }

    [Fact]
    public void WatchlistSummary_Deserializes_WithExtensionData()
    {
        var json = """{"id":"wl1","name":"Test","modified":0,"is_open":false,"read_only":false,"type":"watchlist","isDefault":true}""";

        var response = JsonSerializer.Deserialize<WatchlistSummary>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("isDefault");
    }

    [Fact]
    public void GetWatchlistsResponse_Deserializes_WithUserLists()
    {
        var json = """
        {
            "data": {
                "scanners_only": false,
                "show_scanners": false,
                "bulk_delete": false,
                "user_lists": [
                    { "is_open": false, "read_only": false, "name": "Capture Test", "modified": 1775319529009, "id": "99999", "type": "watchlist" }
                ]
            },
            "action": "content",
            "MID": "1"
        }
        """;

        var response = JsonSerializer.Deserialize<GetWatchlistsResponse>(json);

        response.ShouldNotBeNull();
        response.Action.ShouldBe("content");
        response.Mid.ShouldBe("1");
        response.Data.ShouldNotBeNull();
        response.Data.ScannersOnly.ShouldBeFalse();
        response.Data.UserLists.Count.ShouldBe(1);
        response.Data.UserLists[0].Id.ShouldBe("99999");
        response.Data.UserLists[0].Name.ShouldBe("Capture Test");
    }

    [Fact]
    public void WatchlistDetail_Deserializes_WithInstruments()
    {
        var json = """
        {
            "id": "99999",
            "hash": "1775319529009",
            "name": "Capture Test",
            "readOnly": false,
            "instruments": [
                { "ST": "STK", "C": "756733", "conid": 756733, "name": "SS SPDR S&P 500 ETF TRUST-US", "fullName": "SPY", "assetClass": "STK", "ticker": "SPY", "chineseName": "test" }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<WatchlistDetail>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("99999");
        response.Hash.ShouldBe("1775319529009");
        response.Name.ShouldBe("Capture Test");
        response.ReadOnly.ShouldBeFalse();
        response.Instruments.Count.ShouldBe(1);
        response.Instruments[0].St.ShouldBe("STK");
        response.Instruments[0].C.ShouldBe("756733");
        response.Instruments[0].Conid.ShouldBe(756733);
        response.Instruments[0].FullName.ShouldBe("SPY");
        response.Instruments[0].Ticker.ShouldBe("SPY");
    }

    [Fact]
    public void WatchlistInstrument_Deserializes_WithNullChineseName()
    {
        var json = """{"ST":"STK","C":"265598","conid":265598,"name":"APPLE INC","fullName":"AAPL","assetClass":"STK","ticker":"AAPL","chineseName":null}""";

        var response = JsonSerializer.Deserialize<WatchlistInstrument>(json);

        response.ShouldNotBeNull();
        response.Conid.ShouldBe(265598);
        response.Ticker.ShouldBe("AAPL");
        response.ChineseName.ShouldBeNull();
    }

    [Fact]
    public void DeleteWatchlistResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"data":{"deleted":"99999"},"action":"context","MID":"2"}""";

        var response = JsonSerializer.Deserialize<DeleteWatchlistResponse>(json);

        response.ShouldNotBeNull();
        response.Data.ShouldNotBeNull();
        response.Data.Deleted.ShouldBe("99999");
        response.Action.ShouldBe("context");
        response.Mid.ShouldBe("2");
    }

    [Fact]
    public void WatchlistRow_Serializes_CorrectJsonPropertyNames()
    {
        var row = new WatchlistRow(C: 265598, H: "AAPL");

        var json = JsonSerializer.Serialize(row);

        json.ShouldContain("\"C\":265598");
        json.ShouldContain("\"H\":\"AAPL\"");
    }

    [Fact]
    public void WatchlistRow_Serializes_WithNullH()
    {
        var row = new WatchlistRow(C: 265598);

        var json = JsonSerializer.Serialize(row);

        json.ShouldContain("\"C\":265598");
    }
}
