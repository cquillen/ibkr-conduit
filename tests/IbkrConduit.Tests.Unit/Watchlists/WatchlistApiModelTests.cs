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
            Rows: new List<WatchlistRow>
            {
                new(C: 265598, H: "AAPL"),
                new(C: 272093, H: "MSFT"),
            });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"id\":\"My Watchlist\"");
        json.ShouldContain("\"rows\"");
        json.ShouldContain("\"C\":265598");
        json.ShouldContain("\"H\":\"AAPL\"");
    }

    [Fact]
    public void CreateWatchlistResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"id":"my-watchlist-123"}""";

        var response = JsonSerializer.Deserialize<CreateWatchlistResponse>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("my-watchlist-123");
    }

    [Fact]
    public void WatchlistSummary_Deserializes_FromJsonCorrectly()
    {
        var json = """{"id":"wl1","name":"Tech Stocks","modified":1700000000,"instruments":5}""";

        var response = JsonSerializer.Deserialize<WatchlistSummary>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("wl1");
        response.Name.ShouldBe("Tech Stocks");
        response.Modified.ShouldBe(1700000000);
        response.Instruments.ShouldBe(5);
    }

    [Fact]
    public void WatchlistSummary_Deserializes_WithExtensionData()
    {
        var json = """{"id":"wl1","name":"Test","modified":0,"instruments":0,"isDefault":true}""";

        var response = JsonSerializer.Deserialize<WatchlistSummary>(json);

        response.ShouldNotBeNull();
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("isDefault");
    }

    [Fact]
    public void WatchlistDetail_Deserializes_WithRows()
    {
        var json = """
        {
            "id": "wl1",
            "name": "Tech Stocks",
            "rows": [
                { "C": 265598, "H": "AAPL", "sym": "AAPL" },
                { "C": 272093, "H": "MSFT", "sym": "MSFT" }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<WatchlistDetail>(json);

        response.ShouldNotBeNull();
        response.Id.ShouldBe("wl1");
        response.Name.ShouldBe("Tech Stocks");
        response.Rows.Count.ShouldBe(2);
        response.Rows[0].C.ShouldBe(265598);
        response.Rows[0].H.ShouldBe("AAPL");
        response.Rows[0].Sym.ShouldBe("AAPL");
        response.Rows[1].C.ShouldBe(272093);
    }

    [Fact]
    public void WatchlistDetailRow_Deserializes_WithNullSym()
    {
        var json = """{"C":265598,"H":"AAPL","sym":null}""";

        var response = JsonSerializer.Deserialize<WatchlistDetailRow>(json);

        response.ShouldNotBeNull();
        response.C.ShouldBe(265598);
        response.H.ShouldBe("AAPL");
        response.Sym.ShouldBeNull();
    }

    [Fact]
    public void DeleteWatchlistResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"deleted":true,"id":"wl1"}""";

        var response = JsonSerializer.Deserialize<DeleteWatchlistResponse>(json);

        response.ShouldNotBeNull();
        response.Deleted.ShouldBeTrue();
        response.Id.ShouldBe("wl1");
    }

    [Fact]
    public void WatchlistRow_Serializes_CorrectJsonPropertyNames()
    {
        var row = new WatchlistRow(C: 265598, H: "AAPL");

        var json = JsonSerializer.Serialize(row);

        json.ShouldContain("\"C\":265598");
        json.ShouldContain("\"H\":\"AAPL\"");
    }
}
