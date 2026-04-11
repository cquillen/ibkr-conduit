using System;
using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// Abstracts Live Session Token acquisition, caching, and refresh from the signing handler.
/// </summary>
internal interface ISessionTokenProvider
{
    /// <summary>
    /// Gets the expiry time of the current cached token, or null if no token has been acquired.
    /// </summary>
    DateTimeOffset? CurrentTokenExpiry { get; }

    /// <summary>
    /// Gets the current Live Session Token, acquiring it if necessary.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Acquires a fresh Live Session Token, replacing any cached value.
    /// Thread-safe: concurrent callers are serialized and subsequent callers
    /// receive the already-refreshed token.
    /// </summary>
    Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken);
}
