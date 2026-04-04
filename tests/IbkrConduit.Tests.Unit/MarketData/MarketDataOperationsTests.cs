using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.MarketData;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.MarketData;

public class MarketDataOperationsTests : IDisposable
{
    private readonly FakeMarketDataApi _fakeApi = new();
    private readonly MarketDataOperations _sut;

    public MarketDataOperationsTests()
    {
        _sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance);
    }

    [Fact]
    public async Task GetRegulatorySnapshotAsync_DelegatesToApi()
    {
        _fakeApi.RegulatorySnapshotResponse = new MarketDataSnapshotRaw(
            265598, null, 1702334859712, null, "RpB");

        var result = await _sut.GetRegulatorySnapshotAsync(265598, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Conid.ShouldBe(265598);
    }

    [Fact]
    public async Task UnsubscribeAsync_DelegatesToApi()
    {
        _fakeApi.UnsubscribeResponseValue = new UnsubscribeResponse(true);

        var result = await _sut.UnsubscribeAsync(265598, TestContext.Current.CancellationToken);

        result.Value.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task UnsubscribeAllAsync_DelegatesToApi()
    {
        _fakeApi.UnsubscribeAllResponseValue = new UnsubscribeAllResponse(true);

        var result = await _sut.UnsubscribeAllAsync(TestContext.Current.CancellationToken);

        result.Value.Unsubscribed.ShouldBeTrue();
    }

    [Fact]
    public async Task RunScannerAsync_DelegatesToApi()
    {
        _fakeApi.ScannerResponseValue = new ScannerResponse(
            [new ScannerContract("0", "AMD", "4391", 4391, null, null, null, null, null)],
            "Trades");

        var request = new ScannerRequest("STK", "TOP_TRADE_COUNT", "STK.US.MAJOR", null);
        var result = await _sut.RunScannerAsync(request, TestContext.Current.CancellationToken);

        result.Value.Contracts.ShouldNotBeNull();
        result.Value.Contracts!.Count.ShouldBe(1);
        result.Value.Contracts[0].Symbol.ShouldBe("AMD");
    }

    [Fact]
    public async Task GetScannerParametersAsync_DelegatesToApi()
    {
        _fakeApi.ScannerParametersValue = new ScannerParameters(null, null, null, null);

        var result = await _sut.GetScannerParametersAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RunHmdsScannerAsync_DelegatesToApi()
    {
        _fakeApi.HmdsScannerResponseValue = new HmdsScannerResponse(
            "100", "25", "0", "20231214-18:55:25", "scanner1",
            new HmdsScannerContractWrapper(
                [new HmdsScannerContract("20231214-18:55:25", "431424315")]));

        var request = new HmdsScannerRequest("BOND", "BOND.US",
            "HIGH_BOND_ASK_YIELD_ALL", "BOND", 25, []);
        var result = await _sut.RunHmdsScannerAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Contracts.ShouldNotBeNull();
        result.Value.Contracts!.Contract.ShouldNotBeNull();
        result.Value.Contracts.Contract!.Count.ShouldBe(1);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    private class FakeMarketDataApi : IIbkrMarketDataApi
    {
        public MarketDataSnapshotRaw? RegulatorySnapshotResponse { get; set; }
        public UnsubscribeResponse? UnsubscribeResponseValue { get; set; }
        public UnsubscribeAllResponse? UnsubscribeAllResponseValue { get; set; }
        public ScannerResponse? ScannerResponseValue { get; set; }
        public ScannerParameters? ScannerParametersValue { get; set; }
        public HmdsScannerResponse? HmdsScannerResponseValue { get; set; }

        public Task<IApiResponse<List<MarketDataSnapshotRaw>>> GetSnapshotAsync(
            string conids, string fields, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(new List<MarketDataSnapshotRaw>()));

        public Task<IApiResponse<HistoricalDataResponse>> GetHistoryAsync(
            string conid, string period, string bar, bool? outsideRth = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(new HistoricalDataResponse("SPY", "SPY", null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null, null)));

        public Task<IApiResponse<MarketDataSnapshotRaw>> GetRegulatorySnapshotAsync(
            int conid, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(RegulatorySnapshotResponse!));

        public Task<IApiResponse<UnsubscribeResponse>> UnsubscribeAsync(
            UnsubscribeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(UnsubscribeResponseValue!));

        public Task<IApiResponse<UnsubscribeAllResponse>> UnsubscribeAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(UnsubscribeAllResponseValue!));

        public Task<IApiResponse<ScannerResponse>> RunScannerAsync(
            ScannerRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(ScannerResponseValue!));

        public Task<IApiResponse<ScannerParameters>> GetScannerParametersAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(ScannerParametersValue!));

        public Task<IApiResponse<HmdsScannerResponse>> RunHmdsScannerAsync(
            HmdsScannerRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeApiResponse.Success(HmdsScannerResponseValue!));
    }
}
