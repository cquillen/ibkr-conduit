using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Streaming;

/// <summary>
/// Live end-to-end coverage for the WebSocket trade-execution stream
/// (<see cref="IStreamingOperations.TradeExecutionsAsync"/>, the <c>str</c> topic) against a
/// real IBKR paper trading account. Requires the <c>IBKR_CONSUMER_KEY</c> (and related
/// <c>IBKR_*</c> OAuth) environment variables — see
/// <see cref="OAuthCredentialsFactory.FromEnvironment"/> — and skips automatically when they
/// are not set.
/// </summary>
/// <remarks>
/// This is the residual E2E verification item for the trade-execution stream: the mappers and
/// fan-out are unit-tested, but this confirms the whole DI-composed pipeline
/// (session bridge → WebSocket connect → <c>str</c> subscribe → parse/fan-out → observable)
/// works against IBKR's real gateway. A quiet paper account may have no recent fills, so the
/// assertion is "connects and streams without error"; any execution that does replay is
/// additionally validated.
/// </remarks>
[Collection("IBKR E2E")]
public sealed class TradeExecutionStreamE2E
{
    /// <summary>
    /// Establishes the brokerage session, subscribes to the <c>str</c> execution stream with
    /// one day of history, connects, and asserts the stream delivers without pushing an error.
    /// Any replayed execution is validated to carry its <see cref="TradeExecution.ExecutionId"/>
    /// dedupe key.
    /// </summary>
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task TradeExecutionsAsync_Live_ConnectsAndStreamsWithoutError()
    {
        var ct = TestContext.Current.CancellationToken;

        using var credentials = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(opts => opts.Credentials = credentials);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        // Resolving accounts establishes the brokerage session (ssodh/init) before the
        // WebSocket connects — str is iserver-dependent, and this mirrors the other
        // streaming E2E tests and the consumer examples.
        var accounts = (await client.Portfolio.GetAccountsAsync(ct)).EnsureSuccess().Value;
        accounts.ShouldNotBeEmpty("the paper account should have at least one brokerage account");

        var firstExecution = new TaskCompletionSource<TradeExecution>(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await client.Streaming.TradeExecutionsAsync(days: 1, cancellationToken: ct);

        // Subscribe before connecting so replayed executions aren't missed.
        using var observerSubscription = subscription.Stream.Subscribe(new StreamObserver<TradeExecution>(
            onNext: execution => firstExecution.TrySetResult(execution),
            onError: ex => streamError.TrySetResult(ex),
            onCompleted: () => { }));

        await client.Streaming.ConnectAsync(ct);

        // Historical executions replay on subscribe when the account has recent fills; a quiet
        // paper account may have none. So wait for whichever comes first — an execution or an
        // error — within a bounded window, and treat "neither" as the healthy quiet-account path.
        using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(TimeSpan.FromSeconds(15));

        Exception? pushedError = null;
        TradeExecution? received = null;
        try
        {
            var completed = await Task.WhenAny(firstExecution.Task, streamError.Task).WaitAsync(window.Token);
            if (completed == streamError.Task)
            {
                pushedError = await streamError.Task;
            }
            else
            {
                received = await firstExecution.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // No execution and no error within the window — the healthy quiet-account path.
        }

        pushedError.ShouldBeNull("the str stream must connect and deliver without pushing an error");
        if (received is not null)
        {
            received.ExecutionId.ShouldNotBeNullOrEmpty("each execution carries its dedupe key");
        }
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
