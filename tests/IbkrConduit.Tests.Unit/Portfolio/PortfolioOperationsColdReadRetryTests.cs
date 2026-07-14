using System.Collections.Generic;
using System.Text.Json;
using IbkrConduit.Client;
using IbkrConduit.Portfolio;
using IbkrConduit.Serialization;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

/// <summary>
/// RPD-06 (ADR-0009, spec: docs/superpowers/specs/2026-07-14-rpd-06-cold-read-retry.md): unit
/// coverage for <see cref="PortfolioOperations.LooksSparse"/>, the heuristic sparse-first-read
/// predicate driving <c>GetPositionsAsync</c>'s internal retry-once. The last two cases inline the
/// verbatim <c>.Response.Body</c> arrays from the 2026-07-14 live-probe captures
/// (<c>recordings/coldread-rpd06/s1-positions-{1,2}.json</c>) — <c>recordings/</c> is gitignored and
/// Tests.Unit forbids file I/O (.claude/rules/testing.md), so the recorded shapes are pasted here and
/// deserialized via <see cref="IbkrRefitSettings"/> rather than read from disk.
/// </summary>
public class PortfolioOperationsColdReadRetryTests
{
    [Fact]
    public void LooksSparse_EmptyList_ReturnsFalse()
    {
        // An empty positions list is NOT itself evidence of a cold read (ADR-0009) — a genuinely
        // zero-position account must never trigger a retry from this predicate.
        PortfolioOperations.LooksSparse([]).ShouldBeFalse();
    }

    [Fact]
    public void LooksSparse_NonEmptyListRowMissingName_ReturnsTrue()
    {
        var positions = new List<Position>
        {
            new("DU123", 265598, "SPY", 100m, 450.00m, 45000.00m,
                440.00m, 440.00m, 0m, 1000m, "USD", null!, "STK",
                null, "SPY", null, true),
        };

        PortfolioOperations.LooksSparse(positions).ShouldBeTrue();
    }

    [Fact]
    public void LooksSparse_NonEmptyListRowMissingTicker_ReturnsTrue()
    {
        var positions = new List<Position>
        {
            new("DU123", 265598, "SPY", 100m, 450.00m, 45000.00m,
                440.00m, 440.00m, 0m, 1000m, "USD", "SPDR S&P 500", "STK",
                null, null!, null, true),
        };

        PortfolioOperations.LooksSparse(positions).ShouldBeTrue();
    }

    [Fact]
    public void LooksSparse_FullyPopulatedRow_ReturnsFalse()
    {
        var positions = new List<Position>
        {
            new("DU123", 265598, "SPY", 100m, 450.00m, 45000.00m,
                440.00m, 440.00m, 0m, 1000m, "USD", "SPDR S&P 500", "STK",
                null, "SPY", null, true),
        };

        PortfolioOperations.LooksSparse(positions).ShouldBeFalse();
    }

    [Fact]
    public void LooksSparse_RecordedColdReadFirstPositionsRead_ReturnsTrue()
    {
        // recordings/coldread-rpd06/s1-positions-1.json .Response.Body, verbatim — the FIRST
        // positions read of a fresh session (2026-07-14 live probe). name/ticker absent on both rows.
        var positions = JsonSerializer.Deserialize<List<Position>>(_coldReadSparsePositionsBody, IbkrRefitSettings.Options);

        PortfolioOperations.LooksSparse(positions!).ShouldBeTrue();
    }

    [Fact]
    public void LooksSparse_RecordedEnrichedSecondPositionsRead_ReturnsFalse()
    {
        // recordings/coldread-rpd06/s1-positions-2.json .Response.Body, verbatim — the immediate
        // no-delay follow-up of the SAME session (2026-07-14 live probe). name/ticker present.
        var positions = JsonSerializer.Deserialize<List<Position>>(_coldReadEnrichedPositionsBody, IbkrRefitSettings.Options);

        PortfolioOperations.LooksSparse(positions!).ShouldBeFalse();
    }

    private const string _coldReadSparsePositionsBody = """
        [
          {
            "acctId": "DUO873728",
            "conid": 320227571,
            "contractDesc": "QQQ",
            "position": 5.0,
            "mktPrice": 710.64001465,
            "mktValue": 3553.2,
            "currency": "USD",
            "avgCost": 638.976,
            "avgPrice": 638.976,
            "realizedPnl": 0.0,
            "unrealizedPnl": 358.32,
            "exchs": null,
            "expiry": null,
            "putOrCall": null,
            "multiplier": null,
            "strike": 0.0,
            "exerciseStyle": null,
            "conExchMap": [],
            "assetClass": "STK",
            "undConid": 0,
            "model": ""
          },
          {
            "acctId": "DUO873728",
            "conid": 756733,
            "contractDesc": "SPY",
            "position": 48.0,
            "mktPrice": 748.2199707,
            "mktValue": 35914.56,
            "currency": "USD",
            "avgCost": 659.81625,
            "avgPrice": 659.81625,
            "realizedPnl": 0.0,
            "unrealizedPnl": 4243.38,
            "exchs": null,
            "expiry": null,
            "putOrCall": null,
            "multiplier": null,
            "strike": 0.0,
            "exerciseStyle": null,
            "conExchMap": [],
            "assetClass": "STK",
            "undConid": 0,
            "model": ""
          }
        ]
        """;

    private const string _coldReadEnrichedPositionsBody = """
        [
          {
            "acctId": "DUO873728",
            "conid": 320227571,
            "contractDesc": "QQQ",
            "position": 5.0,
            "mktPrice": 710.64001465,
            "mktValue": 3553.2,
            "currency": "USD",
            "avgCost": 638.976,
            "avgPrice": 638.976,
            "realizedPnl": 0.0,
            "unrealizedPnl": 358.32,
            "exchs": null,
            "expiry": null,
            "putOrCall": null,
            "multiplier": 0.0,
            "strike": "0",
            "exerciseStyle": null,
            "conExchMap": [],
            "assetClass": "STK",
            "undConid": 0,
            "model": "",
            "baseMktValue": 3553.2,
            "baseMktPrice": 710.64001465,
            "baseAvgCost": 638.976,
            "baseAvgPrice": 638.976,
            "baseRealizedPnl": 0.0,
            "baseUnrealizedPnl": 358.32,
            "incrementRules": [ { "lowerEdge": 0.0, "increment": 0.01 } ],
            "displayRule": { "magnification": 0, "displayRuleStep": [ { "decimalDigits": 2, "lowerEdge": 0.0, "wholeDigits": 4 } ] },
            "time": 26,
            "chineseName": "&#x666F;&#x987A; QQQ&#x4FE1;&#x6258;&#x7CFB;&#x5217;1",
            "allExchanges": "AMEX,NYSE,CBOE,PHLX,CHX,ARCA,ISLAND,ISE,IDEAL,NASDAQQ,NASDAQ,DRCTEDGE,BEX,BATS",
            "listingExchange": "NASDAQ",
            "countryCode": "US",
            "name": "INVESCO QQQ TRUST SERIES 1",
            "lastTradingDay": null,
            "group": null,
            "sector": null,
            "sectorGroup": null,
            "ticker": "QQQ",
            "type": "ETF",
            "hasOptions": true,
            "fullName": "QQQ",
            "isUS": true,
            "isEventContract": false,
            "pageSize": 100
          },
          {
            "acctId": "DUO873728",
            "conid": 756733,
            "contractDesc": "SPY",
            "position": 48.0,
            "mktPrice": 748.2199707,
            "mktValue": 35914.56,
            "currency": "USD",
            "avgCost": 659.81625,
            "avgPrice": 659.81625,
            "realizedPnl": 0.0,
            "unrealizedPnl": 4243.38,
            "exchs": null,
            "expiry": null,
            "putOrCall": null,
            "multiplier": 0.0,
            "strike": "0",
            "exerciseStyle": null,
            "conExchMap": [],
            "assetClass": "STK",
            "undConid": 0,
            "model": "",
            "baseMktValue": 35914.56,
            "baseMktPrice": 748.2199707,
            "baseAvgCost": 659.81625,
            "baseAvgPrice": 659.81625,
            "baseRealizedPnl": 0.0,
            "baseUnrealizedPnl": 4243.38,
            "incrementRules": [ { "lowerEdge": 0.0, "increment": 0.01 } ],
            "displayRule": { "magnification": 0, "displayRuleStep": [ { "decimalDigits": 2, "lowerEdge": 0.0, "wholeDigits": 4 } ] },
            "time": 19,
            "chineseName": "&#x9053;&#x5BCC;SPDR&#x6807;&#x666E;500 ETF&#x4FE1;&#x6258;",
            "allExchanges": "AMEX,NYSE,CBOE,PHLX,CHX,ARCA,ISLAND,ISE,IDEAL,NASDAQQ,DRCTEDGE,BEX,BATS",
            "listingExchange": "ARCA",
            "countryCode": "US",
            "name": "SS SPDR S&P 500 ETF TRUST-US",
            "lastTradingDay": null,
            "group": null,
            "sector": null,
            "sectorGroup": null,
            "ticker": "SPY",
            "type": "ETF",
            "hasOptions": true,
            "fullName": "SPY",
            "isUS": true,
            "isEventContract": false,
            "pageSize": 100
          }
        ]
        """;
}
