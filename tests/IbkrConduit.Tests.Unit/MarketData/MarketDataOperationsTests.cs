using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Diagnostics;
using IbkrConduit.MarketData;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Unit.MarketData;

public class MarketDataOperationsTests : IDisposable
{
    private readonly FakeMarketDataApi _fakeApi = new();
    private readonly FakeLifecycleNotifier _notifier = new();
    private readonly MarketDataOperations _sut;

    public MarketDataOperationsTests()
    {
        _sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance,
            new TenantContext("test"),
            _notifier,
            TimeProvider.System);
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

    [Fact]
    public async Task GetSnapshotAsync_WhenPreflightNeeded_WaitsDelayThenRetries()
    {
        var fakeTime = new FakeTimeProvider();
        using var sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance,
            new TenantContext("test"),
            _notifier,
            fakeTime);

        // First call: no Fields — triggers preflight (HasFieldData returns false).
        _fakeApi.SnapshotFirstResponse =
        [
            new MarketDataSnapshotRaw(265598, null, null, null, null),
        ];

        // Retry call: has non-metadata field "31" — no further preflight.
        _fakeApi.SnapshotRetryResponse =
        [
            new MarketDataSnapshotRaw(265598, null, 1702334859712L, null, null)
            {
                Fields = new Dictionary<string, JsonElement>
                {
                    ["31"] = JsonDocument.Parse("\"150.25\"").RootElement,
                },
            },
        ];

        // Start snapshot — blocks on the fake 500ms preflight delay.
        var snapshotTask = sut.GetSnapshotAsync(
            [265598], ["31"], TestContext.Current.CancellationToken);

        // Advance 500ms to fire the preflight delay.
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));

        var result = await snapshotTask;

        result.IsSuccess.ShouldBeTrue();
        _fakeApi.SnapshotCallCount.ShouldBe(2, "should have called API twice: first + preflight retry");
    }

    [Fact]
    public async Task GetSnapshotAsync_AfterSessionLifecycleNotification_RePreflightsPreviouslyCachedConid()
    {
        // PVR-23 (RST-5): a session re-auth inside the preflight-cache window can leave the
        // server-side preflight state reset, so the next snapshot's first call comes back
        // field-less again. Without cache invalidation on re-auth, the cached conid marker
        // would suppress the retry and the field-less row would be treated as fresh.
        var fakeTime = new FakeTimeProvider();
        using var sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance,
            new TenantContext("test"),
            _notifier,
            fakeTime);

        _fakeApi.SnapshotResponseQueue = new Queue<List<MarketDataSnapshotRaw>>(
        [
            NoFieldsSnapshot(265598), // call 1: first round, no data -> preflight needed
            FieldsSnapshot(265598), // call 2: retry succeeds, conid cached
            NoFieldsSnapshot(265598), // call 3: post-reauth first round, no data again
            FieldsSnapshot(265598), // call 4: cache was cleared, so retry fires again
        ]);

        var firstTask = sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));
        var firstResult = await firstTask;

        firstResult.IsSuccess.ShouldBeTrue();
        _fakeApi.SnapshotCallCount.ShouldBe(2, "initial preflight round: first call + retry");

        await _notifier.NotifyAsync(TestContext.Current.CancellationToken);

        var secondTask = sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));
        var secondResult = await secondTask;

        secondResult.IsSuccess.ShouldBeTrue();
        _fakeApi.SnapshotCallCount.ShouldBe(4,
            "re-auth notification must clear the preflight cache so the conid re-preflights instead of being skipped as cached");
    }

    [Fact]
    public async Task GetSnapshotAsync_WithinCacheDurationWithoutNotification_SkipsPreflightForCachedConid()
    {
        // No regression: absent a lifecycle notification, a previously-preflighted conid stays
        // cached for the full PreflightCacheDuration and a second snapshot does not re-trigger
        // the preflight-delay retry round, even if that call's first response is field-less.
        var fakeTime = new FakeTimeProvider();
        using var sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance,
            new TenantContext("test"),
            _notifier,
            fakeTime);

        _fakeApi.SnapshotResponseQueue = new Queue<List<MarketDataSnapshotRaw>>(
        [
            NoFieldsSnapshot(265598), // call 1: first round, no data -> preflight needed
            FieldsSnapshot(265598), // call 2: retry succeeds, conid cached
            NoFieldsSnapshot(265598), // call 3: second snapshot's first round, still cached
        ]);

        var firstTask = sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));
        await firstTask;

        var secondResult = await sut.GetSnapshotAsync([265598], ["31"], TestContext.Current.CancellationToken);

        secondResult.IsSuccess.ShouldBeTrue();
        _fakeApi.SnapshotCallCount.ShouldBe(3, "cached conid should skip the preflight retry round entirely");
    }

    [Fact]
    public void Dispose_DisposesSessionLifecycleSubscription()
    {
        var notifier = new FakeLifecycleNotifier();
        var sut = new MarketDataOperations(
            _fakeApi,
            new IbkrClientOptions(),
            NullLogger<MarketDataOperations>.Instance,
            new TenantContext("test"),
            notifier,
            TimeProvider.System);

        notifier.SubscriptionDisposed.ShouldBeFalse();

        sut.Dispose();

        notifier.SubscriptionDisposed.ShouldBeTrue();
    }

    private static List<MarketDataSnapshotRaw> NoFieldsSnapshot(int conid) =>
    [
        new MarketDataSnapshotRaw(conid, null, null, null, null),
    ];

    private static List<MarketDataSnapshotRaw> FieldsSnapshot(int conid) =>
    [
        new MarketDataSnapshotRaw(conid, null, 1702334859712L, null, null)
        {
            Fields = new Dictionary<string, JsonElement>
            {
                ["31"] = JsonDocument.Parse("\"150.25\"").RootElement,
            },
        },
    ];

    public void Dispose()
    {
        _sut.Dispose();
    }

    private class FakeMarketDataApi : IIbkrMarketDataApi
    {
        private int _snapshotCallCount;

        public MarketDataSnapshotRaw? RegulatorySnapshotResponse { get; set; }
        public UnsubscribeResponse? UnsubscribeResponseValue { get; set; }
        public UnsubscribeAllResponse? UnsubscribeAllResponseValue { get; set; }
        public ScannerResponse? ScannerResponseValue { get; set; }
        public ScannerParameters? ScannerParametersValue { get; set; }
        public HmdsScannerResponse? HmdsScannerResponseValue { get; set; }
        public List<MarketDataSnapshotRaw>? SnapshotFirstResponse { get; set; }
        public List<MarketDataSnapshotRaw>? SnapshotRetryResponse { get; set; }

        /// <summary>
        /// When set, drives the response sequence directly (one entry dequeued per call),
        /// taking priority over <see cref="SnapshotFirstResponse"/>/<see cref="SnapshotRetryResponse"/>.
        /// Lets tests script call sequences longer than a single preflight round.
        /// </summary>
        public Queue<List<MarketDataSnapshotRaw>>? SnapshotResponseQueue { get; set; }

        public int SnapshotCallCount => _snapshotCallCount;

        public Task<IApiResponse<List<MarketDataSnapshotRaw>>> GetSnapshotAsync(
            string conids, string fields, CancellationToken cancellationToken = default)
        {
            var callNumber = Interlocked.Increment(ref _snapshotCallCount);

            if (SnapshotResponseQueue is { Count: > 0 })
            {
                return Task.FromResult(FakeApiResponse.Success(SnapshotResponseQueue.Dequeue()));
            }

            var response = callNumber == 1
                ? SnapshotFirstResponse ?? []
                : callNumber == 2
                    ? SnapshotRetryResponse ?? []
                    : [];
            return Task.FromResult(FakeApiResponse.Success(response));
        }

        public Task<IApiResponse<HistoricalDataResponse>> GetHistoryAsync(
            string conid, string period, string bar, bool? outsideRth = null,
            string? exchange = null, string? startTime = null, HistoryDirection? direction = null, HistoryBarSource? source = null,
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

    /// <summary>
    /// Minimal fake mirroring the subscribe/notify/dispose contract of
    /// <see cref="ISessionLifecycleNotifier"/> without the real implementation's locking —
    /// tests here are single-threaded and only need to drive one "session refreshed" callback.
    /// </summary>
    private sealed class FakeLifecycleNotifier : ISessionLifecycleNotifier
    {
        private readonly List<Func<CancellationToken, Task>> _subscribers = [];

        public bool SubscriptionDisposed { get; private set; }

        public IDisposable Subscribe(Func<CancellationToken, Task> onSessionRefreshed)
        {
            _subscribers.Add(onSessionRefreshed);
            return new CallbackDisposable(() =>
            {
                _subscribers.Remove(onSessionRefreshed);
                SubscriptionDisposed = true;
            });
        }

        public async Task NotifyAsync(CancellationToken cancellationToken)
        {
            foreach (var subscriber in _subscribers.ToArray())
            {
                await subscriber(cancellationToken);
            }
        }

        public IDisposable SubscribeTickleSucceeded(Func<CancellationToken, Task> onTickleSucceeded) =>
            new CallbackDisposable(() => { });

        public Task NotifyTickleSucceededAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        private sealed class CallbackDisposable(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
