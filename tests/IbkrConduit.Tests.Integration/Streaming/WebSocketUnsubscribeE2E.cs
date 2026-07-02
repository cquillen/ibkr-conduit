using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.MarketData;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// Live end-to-end coverage for WebSocket unsubscribe
/// (<see cref="IIbkrSubscription{T}.UnsubscribeAsync"/>) against a real IBKR paper trading
/// account. Requires the <c>IBKR_CONSUMER_KEY</c> (and related <c>IBKR_*</c> OAuth)
/// environment variables — see <see cref="OAuthCredentialsFactory.FromEnvironment"/> — and
/// skips automatically when they are not set.
/// </summary>
/// <remarks>
/// This is the live confirmation of the one residual verification item from the
/// websocket-unsubscribe design spec (<c>docs/superpowers/specs/2026-07-02-websocket-unsubscribe-design.md</c>
/// §8): that IBKR's gateway actually accepts the accountful <c>usd+{accountId}+{}</c> cancel
/// form, not just the local WireMock/mock-WebSocket harness used by
/// <see cref="WebSocketUnsubscribeTest"/>.
/// </remarks>
[Collection("IBKR E2E")]
public sealed class WebSocketUnsubscribeE2E
{
    // SPY — the same fixture conid used throughout this project's integration test suite
    // (see e.g. Portfolio/PortfolioTests.cs, Contracts/ContractTests.cs). Liquid enough to
    // guarantee at least one market-data tick during regular trading hours.
    private const int _spyConid = 756733;

    /// <summary>
    /// Subscribes to account summary for the paper account, waits for at least one live
    /// update, unsubscribes, and asserts the observable completes — confirming the
    /// <c>usd+{accountId}+{}</c> cancel is accepted by IBKR.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task AccountSummaryAsync_UnsubscribeAsync_CompletesStreamLive()
    {
        var ct = TestContext.Current.CancellationToken;

        using var credentials = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = credentials);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // Resolving accounts also establishes the brokerage session (ssodh/init via the
        // reactive 401 flow) before the WebSocket connects — the same pattern every
        // consumer example and the other streaming integration tests use.
        var accounts = (await client.Portfolio.GetAccountsAsync(ct)).EnsureSuccess().Value;
        accounts.ShouldNotBeEmpty("the paper account should have at least one brokerage account");
        var accountId = accounts[0].Id;

        var firstUpdate = new TaskCompletionSource<AccountSummaryUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await client.Streaming.AccountSummaryAsync(accountId, cancellationToken: ct);

        // Subscribe to the observable before connecting so no replayed initial data is missed.
        using var observerSubscription = subscription.Stream.Subscribe(new StreamObserver<AccountSummaryUpdate>(
            onNext: update => firstUpdate.TrySetResult(update),
            onError: ex =>
            {
                firstUpdate.TrySetException(ex);
                completed.TrySetException(ex);
            },
            onCompleted: () => completed.TrySetResult()));

        await client.Streaming.ConnectAsync(ct);

        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        receiveCts.CancelAfter(TimeSpan.FromSeconds(30));
        var receivedUpdate = await firstUpdate.Task.WaitAsync(receiveCts.Token);
        receivedUpdate.ShouldNotBeNull();
        receivedUpdate.AccountId.ShouldBe(accountId);

        await subscription.UnsubscribeAsync(ct);

        using var completeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        completeCts.CancelAfter(TimeSpan.FromSeconds(10));
        await completed.Task.WaitAsync(completeCts.Token);
    }

    /// <summary>
    /// Subscribes to market data for a liquid conid, waits for at least one live tick,
    /// unsubscribes, and asserts the observable completes — confirming the
    /// <c>umd+{conid}+{}</c> cancel is accepted by IBKR.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task MarketDataAsync_UnsubscribeAsync_CompletesStreamLive()
    {
        var ct = TestContext.Current.CancellationToken;

        using var credentials = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = credentials);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // Establishes the brokerage session before the WebSocket connects, mirroring the
        // account-summary test above.
        var accounts = (await client.Portfolio.GetAccountsAsync(ct)).EnsureSuccess().Value;
        accounts.ShouldNotBeEmpty("the paper account should have at least one brokerage account");

        var firstTick = new TaskCompletionSource<MarketDataTick>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await client.Streaming.MarketDataAsync(
            _spyConid, [MarketDataFields.LastPrice, MarketDataFields.AskPrice], ct);

        using var observerSubscription = subscription.Stream.Subscribe(new StreamObserver<MarketDataTick>(
            onNext: tick => firstTick.TrySetResult(tick),
            onError: ex =>
            {
                firstTick.TrySetException(ex);
                completed.TrySetException(ex);
            },
            onCompleted: () => completed.TrySetResult()));

        await client.Streaming.ConnectAsync(ct);

        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        receiveCts.CancelAfter(TimeSpan.FromSeconds(30));
        var receivedTick = await firstTick.Task.WaitAsync(receiveCts.Token);
        receivedTick.ShouldNotBeNull();
        receivedTick.Conid.ShouldBe(_spyConid);

        await subscription.UnsubscribeAsync(ct);

        using var completeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        completeCts.CancelAfter(TimeSpan.FromSeconds(10));
        await completed.Task.WaitAsync(completeCts.Token);
    }

    /// <summary>
    /// Minimal <see cref="IObserver{T}"/> implementation for tests — avoids a
    /// System.Reactive dependency (this project doesn't reference it).
    /// </summary>
    private sealed class StreamObserver<T>(Action<T> onNext, Action<Exception> onError, Action onCompleted) : IObserver<T>
    {
        public void OnNext(T value) => onNext(value);

        public void OnError(Exception error) => onError(error);

        public void OnCompleted() => onCompleted();
    }
}
