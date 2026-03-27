using System.Threading;
using System.Threading.Tasks;

namespace IbkrConduit.Auth;

/// <summary>
/// Abstracts Live Session Token acquisition and caching from the signing handler.
/// </summary>
public interface ISessionTokenProvider
{
    /// <summary>
    /// Gets the current Live Session Token, acquiring it if necessary.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken);
}
