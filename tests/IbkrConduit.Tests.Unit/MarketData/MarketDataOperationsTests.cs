using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.MarketData;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.MarketData;

public class MarketDataOperationsTests : IDisposable
{
    private readonly FakeMarketDataApi _fakeApi = new();
    private readonly MarketDataOperations _sut;

    public MarketDataOperationsTests()
    {
        var options = new IbkrClientOptions
        {
            PreflightCacheDuration = TimeSpan.FromMinutes(5),
        };
        _sut = new MarketDataOperations(_fakeApi, options, NullLogger<MarketDataOperations>.Instance);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithFieldData_ReturnsMappedSnapshot()
    {
        var raw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
            ["84"] = JsonDocument.Parse("\"193.06\"").RootElement,
            ["86"] = JsonDocument.Parse("\"193.14\"").RootElement,
            ["87"] = JsonDocument.Parse("\"1234567\"").RootElement,
            ["70"] = JsonDocument.Parse("\"195.00\"").RootElement,
            ["71"] = JsonDocument.Parse("\"191.50\"").RootElement,
            ["82"] = JsonDocument.Parse("\"1.25\"").RootElement,
            ["83"] = JsonDocument.Parse("\"0.65\"").RootElement,
        });

        _fakeApi.SnapshotResponses.Enqueue([raw]);

        var result = await _sut.GetSnapshotAsync([265598], ["31", "84", "86"], TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Conid.ShouldBe(265598);
        result[0].LastPrice.ShouldBe("193.18");
        result[0].BidPrice.ShouldBe("193.06");
        result[0].AskPrice.ShouldBe("193.14");
        result[0].Volume.ShouldBe("1234567");
        result[0].High.ShouldBe("195.00");
        result[0].Low.ShouldBe("191.50");
        result[0].Change.ShouldBe("1.25");
        result[0].ChangePercent.ShouldBe("0.65");
    }

    [Fact]
    public async Task GetSnapshotAsync_AllFieldsContainsAllResponseFields()
    {
        var raw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
            ["7633"] = JsonDocument.Parse("\"0.25\"").RootElement,
        });

        _fakeApi.SnapshotResponses.Enqueue([raw]);

        var result = await _sut.GetSnapshotAsync([265598], ["31", "7633"], TestContext.Current.CancellationToken);

        result[0].AllFields.ShouldNotBeNull();
        result[0].AllFields!["31"].ShouldBe("193.18");
        result[0].AllFields!["7633"].ShouldBe("0.25");
        result[0].ImpliedVolatility.ShouldBe("0.25");
    }

    [Fact]
    public async Task GetSnapshotAsync_EmptyResponse_TriggersPreflightRetry()
    {
        // First call: empty response (no field data)
        var emptyRaw = new MarketDataSnapshotRaw(265598, null, null, "server1", null);
        _fakeApi.SnapshotResponses.Enqueue([emptyRaw]);

        // Second call: full data
        var fullRaw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
        });
        _fakeApi.SnapshotResponses.Enqueue([fullRaw]);

        var result = await _sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);

        _fakeApi.SnapshotCallCount.ShouldBe(2);
        result[0].LastPrice.ShouldBe("193.18");
    }

    [Fact]
    public async Task GetSnapshotAsync_CachedConid_SkipsPreflight()
    {
        // First call: empty response triggers preflight
        var emptyRaw = new MarketDataSnapshotRaw(265598, null, null, "server1", null);
        _fakeApi.SnapshotResponses.Enqueue([emptyRaw]);

        var fullRaw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
        });
        _fakeApi.SnapshotResponses.Enqueue([fullRaw]);

        await _sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);
        _fakeApi.SnapshotCallCount.ShouldBe(2);

        // Second call: same conid, should be cached — no retry
        var emptyRaw2 = new MarketDataSnapshotRaw(265598, null, null, "server1", null);
        _fakeApi.SnapshotResponses.Enqueue([emptyRaw2]);

        await _sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);
        _fakeApi.SnapshotCallCount.ShouldBe(3); // Only 1 more call, not 2
    }

    [Fact]
    public async Task GetSnapshotAsync_MapsMarketDataAvailability()
    {
        var raw = new MarketDataSnapshotRaw(265598, null, 1702334859712, "server1", "R")
        {
            Fields = new Dictionary<string, JsonElement>
            {
                ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
            },
        };
        _fakeApi.SnapshotResponses.Enqueue([raw]);

        var result = await _sut.GetSnapshotAsync([265598], ["31", "6509"], TestContext.Current.CancellationToken);

        result[0].MarketDataAvailability.ShouldBe("R");
        result[0].Updated.ShouldBe(1702334859712);
    }

    [Fact]
    public async Task GetSnapshotAsync_MultipleConids_MapsAll()
    {
        var raw1 = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"193.18\"").RootElement,
        });
        var raw2 = CreateRawSnapshot(8314, new Dictionary<string, JsonElement>
        {
            ["31"] = JsonDocument.Parse("\"175.50\"").RootElement,
        });
        _fakeApi.SnapshotResponses.Enqueue([raw1, raw2]);

        var result = await _sut.GetSnapshotAsync([265598, 8314], ["31"], TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Conid.ShouldBe(265598);
        result[0].LastPrice.ShouldBe("193.18");
        result[1].Conid.ShouldBe(8314);
        result[1].LastPrice.ShouldBe("175.50");
    }

    [Fact]
    public async Task GetHistoryAsync_DelegatesToApi()
    {
        _fakeApi.HistoryResponse = new HistoricalDataResponse(
            "AAPL", "Apple Inc", 100, "20231201-09:30:00",
            "195.00", "191.50", "1d", 60, "R", 0,
            false, 1, 1, "2", false, 2,
            [new HistoricalBar(192.00m, 193.50m, 195.00m, 191.50m, 50000000, 1701432600000)],
            1, 10);

        var result = await _sut.GetHistoryAsync(265598, "1d", "1min", cancellationToken: TestContext.Current.CancellationToken);

        result.Symbol.ShouldBe("AAPL");
        result.Data.ShouldNotBeNull();
        result.Data!.Count.ShouldBe(1);
        result.Data[0].Open.ShouldBe(192.00m);
        result.Data[0].Close.ShouldBe(193.50m);
        result.Data[0].High.ShouldBe(195.00m);
        result.Data[0].Low.ShouldBe(191.50m);
        result.Data[0].Volume.ShouldBe(50000000m);
    }

    [Fact]
    public async Task GetSnapshotAsync_MapsPnlFields()
    {
        var raw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["73"] = JsonDocument.Parse("\"50000.00\"").RootElement,
            ["74"] = JsonDocument.Parse("\"150.25\"").RootElement,
            ["75"] = JsonDocument.Parse("\"1234.56\"").RootElement,
            ["78"] = JsonDocument.Parse("\"567.89\"").RootElement,
            ["79"] = JsonDocument.Parse("\"890.12\"").RootElement,
        });
        _fakeApi.SnapshotResponses.Enqueue([raw]);

        var result = await _sut.GetSnapshotAsync([265598], ["73", "74", "75", "78", "79"], TestContext.Current.CancellationToken);

        result[0].MarketValue.ShouldBe("50000.00");
        result[0].AvgPrice.ShouldBe("150.25");
        result[0].UnrealizedPnl.ShouldBe("1234.56");
        result[0].DailyPnl.ShouldBe("567.89");
        result[0].RealizedPnl.ShouldBe("890.12");
    }

    [Fact]
    public async Task GetSnapshotAsync_MapsOhlcFields()
    {
        var raw = CreateRawSnapshot(265598, new Dictionary<string, JsonElement>
        {
            ["7295"] = JsonDocument.Parse("\"192.00\"").RootElement,
            ["7296"] = JsonDocument.Parse("\"193.50\"").RootElement,
            ["7741"] = JsonDocument.Parse("\"191.75\"").RootElement,
            ["7762"] = JsonDocument.Parse("\"98765432\"").RootElement,
            ["85"] = JsonDocument.Parse("\"200\"").RootElement,
            ["88"] = JsonDocument.Parse("\"300\"").RootElement,
            ["7059"] = JsonDocument.Parse("\"50\"").RootElement,
        });
        _fakeApi.SnapshotResponses.Enqueue([raw]);

        var result = await _sut.GetSnapshotAsync([265598], ["7295", "7296", "7741", "7762", "85", "88", "7059"], TestContext.Current.CancellationToken);

        result[0].Open.ShouldBe("192.00");
        result[0].Close.ShouldBe("193.50");
        result[0].PriorClose.ShouldBe("191.75");
        result[0].VolumeLong.ShouldBe("98765432");
        result[0].AskSize.ShouldBe("200");
        result[0].BidSize.ShouldBe("300");
        result[0].LastSize.ShouldBe("50");
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    private static MarketDataSnapshotRaw CreateRawSnapshot(int conid, Dictionary<string, JsonElement> fields) =>
        new(conid, null, 1702334859712, "server1", null)
        {
            Fields = fields,
        };

    private class FakeMarketDataApi : IIbkrMarketDataApi
    {
        public Queue<List<MarketDataSnapshotRaw>> SnapshotResponses { get; } = new();
        public HistoricalDataResponse? HistoryResponse { get; set; }
        public int SnapshotCallCount { get; private set; }

        public Task<List<MarketDataSnapshotRaw>> GetSnapshotAsync(
            string conids, string fields, CancellationToken cancellationToken = default)
        {
            SnapshotCallCount++;
            return Task.FromResult(SnapshotResponses.Dequeue());
        }

        public Task<HistoricalDataResponse> GetHistoryAsync(
            string conid, string period, string bar, bool? outsideRth = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HistoryResponse!);
    }
}
