using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// Lazy-acquires and caches the Live Session Token. Thread-safe via semaphore.
/// No refresh logic in M1 — the 24h validity is sufficient for validation.
/// </summary>
public class SessionTokenProvider : ISessionTokenProvider, IDisposable
{
    private readonly IbkrOAuthCredentials _credentials;
    private readonly ILiveSessionTokenClient _lstClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private LiveSessionToken? _cached;

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
        if (_cached != null)
        {
            return _cached;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = await _lstClient.GetLiveSessionTokenAsync(_credentials, cancellationToken);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
