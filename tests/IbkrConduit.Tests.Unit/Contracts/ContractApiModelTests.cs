using System.Text.Json;
using IbkrConduit.Contracts;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Contracts;

public class ContractApiModelTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void SecurityDefinitionInfo_DeserializesFromJson()
    {
        var json = """
            {
                "conid": 265598,
                "symbol": "SPY",
                "secType": "OPT",
                "exchange": "SMART",
                "listingExchange": "AMEX",
                "right": "C",
                "strike": "450",
                "maturityDate": "20261218",
                "isUS": true
            }
            """;

        var result = JsonSerializer.Deserialize<SecurityDefinitionInfo>(json, _options);

        result.ShouldNotBeNull();
        result.Conid.ShouldBe(265598);
        result.Symbol.ShouldBe("SPY");
        result.SecurityType.ShouldBe("OPT");
        result.Exchange.ShouldBe("SMART");
        result.ListingExchange.ShouldBe("AMEX");
        result.Right.ShouldBe("C");
        result.Strike.ShouldBe("450");
        result.MaturityDate.ShouldBe("20261218");
    }

    [Fact]
    public void SecurityDefinitionInfo_CapturesUnknownProperties()
    {
        var json = """
            {
                "conid": 1,
                "symbol": "X",
                "secType": "STK",
                "exchange": "E",
                "listingExchange": "L",
                "right": null,
                "strike": null,
                "maturityDate": null,
                "unknownField": "surprise"
            }
            """;

        var result = JsonSerializer.Deserialize<SecurityDefinitionInfo>(json, _options);

        result.ShouldNotBeNull();
        result.ExtensionData.ShouldNotBeNull();
        result.ExtensionData.ShouldContainKey("unknownField");
    }

    [Fact]
    public void OptionStrikes_DeserializesFromJson()
    {
        var json = """
            {
                "call": [400.0, 410.0, 420.0, 430.0],
                "put": [390.0, 400.0, 410.0]
            }
            """;

        var result = JsonSerializer.Deserialize<OptionStrikes>(json, _options);

        result.ShouldNotBeNull();
        result.Call.ShouldNotBeNull();
        result.Call.Count.ShouldBe(4);
        result.Call[0].ShouldBe(400.0m);
        result.Put.ShouldNotBeNull();
        result.Put.Count.ShouldBe(3);
    }

    [Fact]
    public void TradingRulesRequest_SerializesToJson()
    {
        var request = new TradingRulesRequest(265598, "SMART", true, false, null);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"conid\":265598");
        json.ShouldContain("\"isBuy\":true");
    }

    [Fact]
    public void TradingRules_DeserializesFromJson()
    {
        var json = """
            {
                "orderTypes": [
                    { "label": "Limit", "value": "LMT" },
                    { "label": "Market", "value": "MKT" }
                ],
                "tifTypes": [
                    { "label": "Day", "value": "DAY" }
                ],
                "defaultSize": 100,
                "sizeIncrement": 1,
                "cashSize": 0,
                "cashCurrency": "USD"
            }
            """;

        var result = JsonSerializer.Deserialize<TradingRules>(json, _options);

        result.ShouldNotBeNull();
        result.DefaultSize.ShouldBe(100);
        result.SizeIncrement.ShouldBe(1);
        result.CashCurrency.ShouldBe("USD");
    }

    [Fact]
    public void SecurityDefinitionResponse_DeserializesFromJson()
    {
        var json = """
            {
                "secdef": [
                    {
                        "conid": 265598,
                        "currency": "USD",
                        "name": "SPDR S&P 500",
                        "assetClass": "STK",
                        "expiry": null,
                        "lastTradingDay": null,
                        "group": "Stocks",
                        "putOrCall": null,
                        "sector": "ETF",
                        "sectorGroup": null,
                        "strike": 0,
                        "ticker": "SPY",
                        "undConid": 0
                    }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<SecurityDefinitionResponse>(json, _options);

        result.ShouldNotBeNull();
        result.Secdef.ShouldNotBeNull();
        result.Secdef.Count.ShouldBe(1);
        result.Secdef[0].Conid.ShouldBe(265598);
        result.Secdef[0].Ticker.ShouldBe("SPY");
        result.Secdef[0].AssetClass.ShouldBe("STK");
    }

    [Fact]
    public void ExchangeConid_DeserializesFromJson()
    {
        var json = """
            {
                "ticker": "AAPL",
                "conid": 265598,
                "exchange": "NASDAQ"
            }
            """;

        var result = JsonSerializer.Deserialize<ExchangeConid>(json, _options);

        result.ShouldNotBeNull();
        result.Ticker.ShouldBe("AAPL");
        result.Conid.ShouldBe(265598);
        result.Exchange.ShouldBe("NASDAQ");
    }

    [Fact]
    public void FutureContract_DeserializesFromJson()
    {
        var json = """
            {
                "symbol": "ES",
                "conid": 495512552,
                "underlyingConid": 11004968,
                "expirationDate": "20261218",
                "ltd": "20261218"
            }
            """;

        var result = JsonSerializer.Deserialize<FutureContract>(json, _options);

        result.ShouldNotBeNull();
        result.Symbol.ShouldBe("ES");
        result.Conid.ShouldBe(495512552);
        result.UnderlyingConid.ShouldBe(11004968);
        result.ExpirationDate.ShouldBe("20261218");
        result.LastTradingDay.ShouldBe("20261218");
    }

    [Fact]
    public void StockContract_DeserializesFromJson()
    {
        var json = """
            {
                "name": "APPLE INC",
                "chineseName": null,
                "assetClass": "STK",
                "contracts": [
                    { "conid": 265598, "exchange": "NASDAQ", "isUS": true }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<StockContract>(json, _options);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("APPLE INC");
        result.AssetClass.ShouldBe("STK");
        result.Contracts.ShouldNotBeNull();
        result.Contracts.Count.ShouldBe(1);
        result.Contracts[0].Conid.ShouldBe(265598);
    }

    [Fact]
    public void StockContractDetail_DeserializesFromJson()
    {
        var json = """
            {
                "conid": 265598,
                "exchange": "NASDAQ",
                "isUS": true
            }
            """;

        var result = JsonSerializer.Deserialize<StockContractDetail>(json, _options);

        result.ShouldNotBeNull();
        result.Conid.ShouldBe(265598);
        result.Exchange.ShouldBe("NASDAQ");
        result.IsUs.ShouldBe(true);
    }

    [Fact]
    public void TradingSchedule_DeserializesFromJson()
    {
        var json = """
            {
                "id": "schedule-1",
                "tradeTimings": [
                    {
                        "openingTime": 1700000000000,
                        "closingTime": 1700086400000,
                        "cancelDayOrders": "Y"
                    }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<TradingSchedule>(json, _options);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("schedule-1");
        result.TradeTimings.ShouldNotBeNull();
        result.TradeTimings.Count.ShouldBe(1);
        result.TradeTimings[0].OpeningTime.ShouldBe(1700000000000);
        result.TradeTimings[0].ClosingTime.ShouldBe(1700086400000);
    }

    [Fact]
    public void CurrencyPair_DeserializesFromJson()
    {
        var json = """
            {
                "symbol": "EUR.USD",
                "conid": 12087792,
                "secType": "CASH",
                "exchange": "IDEALPRO"
            }
            """;

        var result = JsonSerializer.Deserialize<CurrencyPair>(json, _options);

        result.ShouldNotBeNull();
        result.Symbol.ShouldBe("EUR.USD");
        result.Conid.ShouldBe(12087792);
        result.SecurityType.ShouldBe("CASH");
        result.Exchange.ShouldBe("IDEALPRO");
    }

    [Fact]
    public void ExchangeRate_DeserializesFromJson()
    {
        var json = """
            {
                "rate": 1.0856
            }
            """;

        var result = JsonSerializer.Deserialize<ExchangeRateResponse>(json, _options);

        result.ShouldNotBeNull();
        result.Rate.ShouldBe(1.0856m);
    }

    [Fact]
    public void SecurityDefinitionInfo_ConidFromString_Deserializes()
    {
        var json = """
            {
                "conid": "265598",
                "symbol": "SPY",
                "secType": "STK",
                "exchange": "SMART",
                "listingExchange": "AMEX",
                "right": null,
                "strike": null,
                "maturityDate": null
            }
            """;

        var result = JsonSerializer.Deserialize<SecurityDefinitionInfo>(json, _options);

        result.ShouldNotBeNull();
        result.Conid.ShouldBe(265598);
    }

    [Fact]
    public void SecurityDefinition_DeserializesFromJson()
    {
        var json = """
            {
                "conid": 265598,
                "currency": "USD",
                "name": "SPDR S&P 500",
                "assetClass": "STK",
                "expiry": null,
                "lastTradingDay": null,
                "group": "Stocks",
                "putOrCall": null,
                "sector": "ETF",
                "sectorGroup": null,
                "strike": 0,
                "ticker": "SPY",
                "undConid": 0
            }
            """;

        var result = JsonSerializer.Deserialize<SecurityDefinition>(json, _options);

        result.ShouldNotBeNull();
        result.Conid.ShouldBe(265598);
        result.Currency.ShouldBe("USD");
        result.Name.ShouldBe("SPDR S&P 500");
        result.Ticker.ShouldBe("SPY");
        result.Strike.ShouldBe(0);
        result.UndConid.ShouldBe(0);
    }
}
