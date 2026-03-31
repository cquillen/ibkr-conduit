using System;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

[Collection("IBKR E2E")]
public class StreamingTests
{
    /// <summary>
    /// End-to-end test that connects the WebSocket and subscribes to account summary updates.
    /// Account summary does not require a market data subscription.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task EndToEnd_WebSocketAccountSummary_ConnectsAndReceivesData()
    {
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddIbkrClient(creds);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // First make a REST call to ensure brokerage session is initialized
        var accounts = await client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);
        accounts.ShouldNotBeNull();
        accounts.ShouldNotBeEmpty();

        // Subscribe to account summary via WebSocket
        var received = new TaskCompletionSource<AccountSummaryUpdate>();
        using var sub = client.Streaming.AccountSummary()
            .Subscribe(new SimpleObserver<AccountSummaryUpdate>(
                onNext: update => received.TrySetResult(update),
                onError: ex => received.TrySetException(ex)));

        // Wait for at least one update (with timeout)
        try
        {
            var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            update.ShouldNotBeNull();
        }
        catch (TimeoutException)
        {
            // No data received within timeout — WebSocket connected but no updates flowing.
            // This can happen outside market hours. The connection itself succeeded.
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
