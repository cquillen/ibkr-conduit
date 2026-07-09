using System;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Session;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// Test double for <see cref="ISessionManager"/> that records calls to
/// <see cref="EnsureInitializedAsync"/>, so tests can assert the WebSocket client
/// establishes the brokerage session before connecting.
/// </summary>
internal sealed class FakeSessionManager : ISessionManager
{
    /// <summary>Number of times <see cref="EnsureInitializedAsync"/> has been called.</summary>
    public int EnsureInitializedCallCount { get; private set; }

    /// <summary>
    /// Invoked synchronously from inside <see cref="EnsureInitializedAsync"/> before it returns.
    /// Lets a test capture state (e.g. whether the WebSocket has connected yet) at ensure time.
    /// </summary>
    public Action? OnEnsureInitialized { get; set; }

    /// <inheritdoc />
    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        EnsureInitializedCallCount++;
        OnEnsureInitialized?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReauthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public bool SessionEstablished => true;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
