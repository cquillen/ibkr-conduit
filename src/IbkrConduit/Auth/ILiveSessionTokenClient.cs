namespace IbkrConduit.Auth;

/// <summary>
/// Acquires a Live Session Token from the IBKR OAuth endpoint.
/// </summary>
internal interface ILiveSessionTokenClient
{
    /// <summary>
    /// Performs the full LST acquisition flow: decrypt, DH exchange, sign, HTTP request,
    /// derive token, and validate.
    /// </summary>
    Task<LiveSessionToken> GetLiveSessionTokenAsync(
        IbkrOAuthCredentials credentials, CancellationToken cancellationToken);
}
