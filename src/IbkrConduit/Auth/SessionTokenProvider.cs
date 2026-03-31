using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Auth;

/// <summary>
/// Lazy-acquires and caches the Live Session Token. Thread-safe via semaphore.
/// Supports forced refresh for re-authentication scenarios.
/// </summary>
public class SessionTokenProvider : ISessionTokenProvider, IDisposable
{
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ILiveSessionTokenClient _lstClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private LiveSessionToken? _cached;
    private long _version;

    /// <summary>
    /// Creates a new provider that will acquire the LST on first use.
    /// </summary>
    public SessionTokenProvider(IbkrOAuthCredentials credentials, ILiveSessionTokenClient lstClient)
    {
        _credentials = credentials;
        _lstClient = lstClient;
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.GetToken");

        if (_cached != null)
        {
            activity?.SetTag(LogFields.Cached, true);
            return _cached;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
            {
                activity?.SetTag(LogFields.Cached, true);
                return _cached;
            }

            activity?.SetTag(LogFields.Cached, false);
            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Session.RefreshToken");

        var versionBeforeWait = Interlocked.Read(ref _version);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // If another caller already refreshed while we were waiting, return the new token
            if (Interlocked.Read(ref _version) != versionBeforeWait && _cached != null)
            {
                return _cached;
            }

            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            Interlocked.Increment(ref _version);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
