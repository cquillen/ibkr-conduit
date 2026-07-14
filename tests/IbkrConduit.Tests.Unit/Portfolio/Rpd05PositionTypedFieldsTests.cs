using System.Text.Json;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

/// <summary>
/// RPD-05: proves <see cref="Position"/> exposes the nine documented-but-previously-untyped fields
/// (<c>baseMktValue</c>/<c>baseMktPrice</c>/<c>baseAvgCost</c>/<c>baseRealizedPnl</c>/
/// <c>baseUnrealizedPnl</c>/<c>lastTradingDay</c>/<c>expiry</c>/<c>putOrCall</c>/<c>strike</c>) as
/// typed nullable properties (ADR-0001), and that <c>strike</c> tolerates both the JSON-number and
/// JSON-string wire shapes observed on the live probe (a live, reproducible instability tied to
/// read-freshness — not just cross-source doc disagreement, per the backlog entry).
/// </summary>
public class Rpd05PositionTypedFieldsTests
{
    private const string _positionJsonTemplate = """
        {
            "acctId": "DU123",
            "conid": 265598,
            "contractDesc": "AAPL AUG2026 200 C",
            "position": 1.0,
            "mktPrice": 12.5,
            "mktValue": 1250.0,
            "avgCost": 1150.0,
            "avgPrice": 11.5,
            "realizedPnl": 0.0,
            "unrealizedPnl": 100.0,
            "currency": "USD",
            "name": "AAPL AUG2026 200 C",
            "assetClass": "OPT",
            "sector": null,
            "ticker": "AAPL",
            "multiplier": 100,
            "isUS": true,
            "baseMktValue": 1250.0,
            "baseMktPrice": 12.5,
            "baseAvgCost": 1150.0,
            "baseRealizedPnl": 0.0,
            "baseUnrealizedPnl": 100.0,
            "lastTradingDay": "20260821",
            "expiry": "20260821",
            "putOrCall": "C",
            "strike": __STRIKE__
        }
        """;

    [Fact]
    public void Position_DeserializesFromJson_TypesBaseCurrencyAndOptionFields()
    {
        var json = _positionJsonTemplate.Replace("__STRIKE__", "200.0");

        var position = JsonSerializer.Deserialize<Position>(json);

        position.ShouldNotBeNull();
        position.BaseMarketValue.ShouldBe(1250.0m);
        position.BaseMarketPrice.ShouldBe(12.5m);
        position.BaseAverageCost.ShouldBe(1150.0m);
        position.BaseRealizedPnl.ShouldBe(0.0m);
        position.BaseUnrealizedPnl.ShouldBe(100.0m);
        position.LastTradingDay.ShouldBe("20260821");
        position.Expiry.ShouldBe("20260821");
        position.PutOrCall.ShouldBe("C");
        position.Strike.ShouldBe(200.0m);
    }

    [Fact]
    public void Position_DeserializesFromJson_AbsentTypedFieldsAreNull()
    {
        var json = """
            {
                "acctId": "DU123",
                "conid": 265598,
                "contractDesc": "SPY",
                "position": 100.0,
                "mktPrice": 450.0,
                "mktValue": 45000.0,
                "avgCost": 440.0,
                "avgPrice": 440.0,
                "realizedPnl": 0.0,
                "unrealizedPnl": 1000.0,
                "currency": "USD",
                "name": "SPDR S&P 500",
                "assetClass": "STK",
                "sector": null,
                "ticker": "SPY",
                "multiplier": null,
                "isUS": true
            }
            """;

        var position = JsonSerializer.Deserialize<Position>(json);

        position.ShouldNotBeNull();
        position.BaseMarketValue.ShouldBeNull();
        position.BaseMarketPrice.ShouldBeNull();
        position.BaseAverageCost.ShouldBeNull();
        position.BaseRealizedPnl.ShouldBeNull();
        position.BaseUnrealizedPnl.ShouldBeNull();
        position.LastTradingDay.ShouldBeNull();
        position.Expiry.ShouldBeNull();
        position.PutOrCall.ShouldBeNull();
        position.Strike.ShouldBeNull();
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("\"0\"")]
    public void Position_DeserializesStrike_NumberAndStringShapesEqualZero(string strikeToken)
    {
        var json = _positionJsonTemplate.Replace("__STRIKE__", strikeToken);

        var position = JsonSerializer.Deserialize<Position>(json);

        position.ShouldNotBeNull();
        position.Strike.ShouldBe(0m);
    }

    [Fact]
    public void Position_DeserializesStrike_QuotedNonZeroNumberParsesCorrectly()
    {
        var json = _positionJsonTemplate.Replace("__STRIKE__", "\"704\"");

        var position = JsonSerializer.Deserialize<Position>(json);

        position.ShouldNotBeNull();
        position.Strike.ShouldBe(704m);
    }
}
