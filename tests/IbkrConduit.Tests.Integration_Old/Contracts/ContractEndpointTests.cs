using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Contracts;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Contracts;

public class ContractEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public ContractEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetSecurityDefinitionInfoAsync_ReturnsSecDefList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/info")
                .WithParam("conid", "265598")
                .WithParam("sectype", "OPT")
                .WithParam("month", "DEC26")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "conid": 600000001,
                                "symbol": "SPY",
                                "secType": "OPT",
                                "exchange": "SMART",
                                "listingExchange": "CBOE",
                                "right": "C",
                                "strike": "450",
                                "maturityDate": "20261218"
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetSecurityDefinitionInfoAsync(
            "265598", "OPT", "DEC26", cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Conid.ShouldBe(600000001);
        result[0].Symbol.ShouldBe("SPY");
        result[0].Right.ShouldBe("C");
    }

    [Fact]
    public async Task GetOptionStrikesAsync_ReturnsCallAndPutStrikes()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/strikes")
                .WithParam("conid", "265598")
                .WithParam("sectype", "OPT")
                .WithParam("month", "DEC26")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "call": [400.0, 410.0, 420.0],
                            "put": [380.0, 390.0, 400.0]
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetOptionStrikesAsync(
            "265598", "OPT", "DEC26", cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Call.Count.ShouldBe(3);
        result.Put.Count.ShouldBe(3);
        result.Call[0].ShouldBe(400.0m);
    }

    [Fact]
    public async Task GetTradingRulesAsync_ReturnsTradingRules()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/rules")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "defaultSize": 100,
                            "sizeIncrement": 1,
                            "cashSize": 0,
                            "cashCurrency": "USD"
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();
        var request = new TradingRulesRequest(265598, "SMART", true, false, null);

        var result = await api.GetTradingRulesAsync(request, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.DefaultSize.ShouldBe(100);
        result.CashCurrency.ShouldBe("USD");
    }

    [Fact]
    public async Task GetFuturesBySymbolAsync_ReturnsFuturesMap()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/trsrv/futures")
                .WithParam("symbols", "ES")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "ES": [
                                {
                                    "symbol": "ESZ6",
                                    "conid": 495512552,
                                    "underlyingConid": 11004968,
                                    "expirationDate": "20261218",
                                    "ltd": "20261218"
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetFuturesBySymbolAsync("ES", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.ShouldContainKey("ES");
        result["ES"].Count.ShouldBe(1);
        result["ES"][0].Symbol.ShouldBe("ESZ6");
        result["ES"][0].Conid.ShouldBe(495512552);
    }

    [Fact]
    public async Task GetStocksBySymbolAsync_ReturnsStocksMap()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/trsrv/stocks")
                .WithParam("symbols", "AAPL")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "AAPL": [
                                {
                                    "name": "APPLE INC",
                                    "chineseName": null,
                                    "assetClass": "STK",
                                    "contracts": [
                                        {
                                            "conid": 265598,
                                            "exchange": "NASDAQ",
                                            "isUS": true
                                        }
                                    ]
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetStocksBySymbolAsync("AAPL", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.ShouldContainKey("AAPL");
        result["AAPL"].Count.ShouldBe(1);
        result["AAPL"][0].Name.ShouldBe("APPLE INC");
        result["AAPL"][0].Contracts[0].Conid.ShouldBe(265598);
    }

    [Fact]
    public async Task GetCurrencyPairsAsync_ReturnsCurrencyPairsMap()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/currency/pairs")
                .WithParam("currency", "USD")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "USD": [
                                {
                                    "symbol": "EUR.USD",
                                    "conid": 12087792,
                                    "secType": "CASH",
                                    "exchange": "IDEALPRO"
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetCurrencyPairsAsync("USD", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.ShouldContainKey("USD");
        result["USD"][0].Symbol.ShouldBe("EUR.USD");
        result["USD"][0].Conid.ShouldBe(12087792);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ReturnsRate()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/exchangerate")
                .WithParam("source", "USD")
                .WithParam("target", "EUR")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "rate": 0.9212
                        }
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetExchangeRateAsync("USD", "EUR", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Rate.ShouldBe(0.9212m);
    }

    [Fact]
    public async Task GetAllConidsByExchangeAsync_ReturnsConidList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/trsrv/all-conids")
                .WithParam("exchange", "NASDAQ")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            { "ticker": "AAPL", "conid": 265598, "exchange": "NASDAQ" },
                            { "ticker": "MSFT", "conid": 272093, "exchange": "NASDAQ" }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetAllConidsByExchangeAsync("NASDAQ", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Ticker.ShouldBe("AAPL");
        result[1].Ticker.ShouldBe("MSFT");
    }

    [Fact]
    public async Task GetTradingScheduleAsync_ReturnsScheduleList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/trsrv/secdef/schedule")
                .WithParam("assetClass", "STK")
                .WithParam("symbol", "AAPL")
                .WithParam("conid", "265598")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "id": "us-regular",
                                "tradeTimings": [
                                    {
                                        "openingTime": 1700000000000,
                                        "closingTime": 1700023400000,
                                        "cancelDayOrders": "Y"
                                    }
                                ]
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetTradingScheduleAsync(
            "STK", "AAPL", "265598", cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("us-regular");
        result[0].TradeTimings.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetSecurityDefinitionsByConidAsync_ReturnsSecDefs()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/trsrv/secdef")
                .WithParam("conids", "265598,272093")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
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
                        """));

        var api = CreateRefitClient<IIbkrContractApi>();

        var result = await api.GetSecurityDefinitionsByConidAsync(
            "265598,272093", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Secdef.Count.ShouldBe(1);
        result.Secdef[0].Conid.ShouldBe(265598);
        result.Secdef[0].Ticker.ShouldBe("SPY");
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private TApi CreateRefitClient<TApi>() where TApi : class
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<TApi>(httpClient);
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
