using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.MarketData;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 10: WebSocket Streaming.
/// Exercises WebSocket market data, order updates, and account summary subscriptions
/// against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario10_WebSocketStreamingTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task WebSocketStreaming_FullWorkflow()
    {
        var (_, client) = CreateClient();

        try
        {

            // Step 1: Initialize session by getting accounts
            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeNull();
            accounts.ShouldNotBeEmpty();

            // Step 2: Search SPY conid
            var searchResults = await client.Contracts.SearchBySymbolAsync("SPY", CT);
            searchResults.ShouldNotBeEmpty("SPY search should return results");
            var spyConid = searchResults.First(r =>
                string.Equals(r.Symbol, "SPY", StringComparison.OrdinalIgnoreCase)).Conid;
            spyConid.ShouldBeGreaterThan(0, "SPY conid should be a positive integer");

            // Step 3: Subscribe to market data for SPY
            var marketDataReceived = new TaskCompletionSource<MarketDataTick>();
            using var mdSub = client.Streaming.MarketData(
                    spyConid,
                    [MarketDataFields.LastPrice, MarketDataFields.AskPrice])
                .Subscribe(new SimpleObserver<MarketDataTick>(
                    onNext: tick => marketDataReceived.TrySetResult(tick),
                    onError: ex => marketDataReceived.TrySetException(ex)));

            // Step 4: Wait for at least one market data message (with timeout)
            try
            {
                using var mdCts = CancellationTokenSource.CreateLinkedTokenSource(CT);
                mdCts.CancelAfter(TimeSpan.FromSeconds(10));
                var tick = await marketDataReceived.Task.WaitAsync(mdCts.Token);
                tick.ShouldNotBeNull();
                tick.Conid.ShouldBe(spyConid);
            }
            catch (OperationCanceledException)
            {
                // No market data received within timeout — can happen outside market hours.
                // The subscription itself succeeded without error.
            }
            catch (TimeoutException)
            {
                // No data received within timeout — WebSocket connected but no updates flowing.
            }

            // Step 5: Subscribe to order updates
            var orderReceived = new TaskCompletionSource<OrderUpdate>();
            using var orderSub = client.Streaming.OrderUpdates()
                .Subscribe(new SimpleObserver<OrderUpdate>(
                    onNext: update => orderReceived.TrySetResult(update),
                    onError: ex => orderReceived.TrySetException(ex)));

            // Step 6: Subscribe to account summary
            var summaryReceived = new TaskCompletionSource<AccountSummaryUpdate>();
            using var summarySub = client.Streaming.AccountSummary()
                .Subscribe(new SimpleObserver<AccountSummaryUpdate>(
                    onNext: update => summaryReceived.TrySetResult(update),
                    onError: ex => summaryReceived.TrySetException(ex)));

            // Step 7: Wait briefly for account summary data
            try
            {
                using var sumCts = CancellationTokenSource.CreateLinkedTokenSource(CT);
                sumCts.CancelAfter(TimeSpan.FromSeconds(10));
                var summary = await summaryReceived.Task.WaitAsync(sumCts.Token);
                summary.ShouldNotBeNull();
            }
            catch (OperationCanceledException)
            {
                // No account summary received within timeout — acceptable outside market hours.
            }
            catch (TimeoutException)
            {
                // No data received within timeout.
            }


            // Step 8: Dispose client (handles disconnect)
        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task SubscribeInvalidTopic_DoesNotCrash()
    {
        var (_, client) = CreateClient();

        try
        {

            // Initialize session
            var accounts = await client.Portfolio.GetAccountsAsync(CT);
            accounts.ShouldNotBeNull();

            // Subscribe to market data with an invalid conid — should not crash
            var received = new TaskCompletionSource<MarketDataTick>();
            using var sub = client.Streaming.MarketData(
                    -999999,
                    [MarketDataFields.LastPrice])
                .Subscribe(new SimpleObserver<MarketDataTick>(
                    onNext: tick => received.TrySetResult(tick),
                    onError: ex => received.TrySetException(ex)));

            // Wait briefly — we expect no messages or a graceful error, not a crash
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(CT);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await received.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected: no data for invalid conid, subscription timed out gracefully.
            }
            catch (TimeoutException)
            {
                // Expected: no data for invalid conid.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Minimal IObserver implementation for tests — avoids System.Reactive dependency.
    /// </summary>
    private sealed class SimpleObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly Action<Exception>? _onError;
        private readonly Action? _onCompleted;

        public SimpleObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(T value) => _onNext(value);
        public void OnError(Exception error) => _onError?.Invoke(error);
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
