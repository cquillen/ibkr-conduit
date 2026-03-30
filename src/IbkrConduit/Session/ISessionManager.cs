using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Session;

/// <summary>
/// Manages the lifecycle of an IBKR brokerage session: initialization,
/// keepalive, re-authentication, and shutdown.
/// </summary>
internal interface ISessionManager : IAsyncDisposable
{
    /// <summary>
    /// Ensures the brokerage session is initialized. On first call, acquires an LST,
    /// initializes the session, suppresses questions, and starts the tickle timer.
    /// Subsequent calls return immediately if the session is already ready.
    /// </summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Forces re-authentication: refreshes the LST, re-initializes the brokerage session,
    /// re-suppresses questions, and restarts the tickle timer. Thread-safe.
    /// </summary>
    Task ReauthenticateAsync(CancellationToken cancellationToken);
}
