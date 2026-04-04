using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.MarketData;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.MarketData;

public class MarketDataApiTests : IDisposable
{
    private readonly WireMockServer _server;

    public MarketDataApiTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetRegulatorySnapshotAsync_WithMockedServer_ReturnsSnapshot()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/md/regsnapshot")
                .WithParam("conid", "265598")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "conid": 265598,
                            "conidEx": "265598",
                            "31": "193.18",
                            "84": "193.06",
                            "86": "193.14",
                            "6509": "RpB"
                        }
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.GetRegulatorySnapshotAsync(265598, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Conid.ShouldBe(265598);
        result.MarketDataAvailability.ShouldBe("RpB");
    }

    [Fact]
    public async Task UnsubscribeAsync_WithMockedServer_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/unsubscribe")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{ "success": true }"""));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.UnsubscribeAsync(
            new UnsubscribeRequest(265598),
            TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task UnsubscribeAllAsync_WithMockedServer_ReturnsConfirmation()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/marketdata/unsubscribeall")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{ "unsubscribed": true }"""));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.UnsubscribeAllAsync(TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Unsubscribed.ShouldBeTrue();
    }

    [Fact]
    public async Task RunScannerAsync_WithMockedServer_ReturnsContracts()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/run")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "contracts": [
                                {
                                    "server_id": "0",
                                    "symbol": "AMD",
                                    "conidex": "4391",
                                    "con_id": 4391,
                                    "company_name": "ADVANCED MICRO DEVICES",
                                    "contract_description_1": "AMD",
                                    "listing_exchange": "NASDAQ.NMS",
                                    "sec_type": "STK"
                                }
                            ],
                            "scan_data_column_name": "Trades"
                        }
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var request = new ScannerRequest("STK", "TOP_TRADE_COUNT", "STK.US.MAJOR", null);
        var result = await api.RunScannerAsync(request, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Count.ShouldBe(1);
        result.Contracts[0].Symbol.ShouldBe("AMD");
        result.Contracts[0].ConId.ShouldBe(4391);
    }

    [Fact]
    public async Task GetScannerParametersAsync_WithMockedServer_ReturnsParameters()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/params")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "scan_type_list": [
                                {
                                    "display_name": "Top % Gainers",
                                    "code": "TOP_PERC_GAIN",
                                    "instruments": ["STK"]
                                }
                            ],
                            "instrument_list": [
                                {
                                    "display_name": "US Stocks",
                                    "type": "STK",
                                    "filters": ["priceAbove"]
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
                                    "locations": []
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var result = await api.GetScannerParametersAsync(TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.ScanTypeList.ShouldNotBeNull();
        result.ScanTypeList!.Count.ShouldBe(1);
        result.ScanTypeList[0].Code.ShouldBe("TOP_PERC_GAIN");
        result.InstrumentList.ShouldNotBeNull();
        result.FilterList.ShouldNotBeNull();
        result.LocationTree.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunHmdsScannerAsync_WithMockedServer_ReturnsContracts()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/hmds/scanner")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
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
                        """));

        var api = CreateRefitClient<IIbkrMarketDataApi>();

        var request = new HmdsScannerRequest("BOND", "BOND.US",
            "HIGH_BOND_ASK_YIELD_ALL", "BOND", 25, []);
        var result = await api.RunHmdsScannerAsync(request, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Total.ShouldBe("17262");
        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Contract.ShouldNotBeNull();
        result.Contracts.Contract!.Count.ShouldBe(1);
        result.Contracts.Contract[0].ContractId.ShouldBe("431424315");
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
