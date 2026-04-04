using Refit;

namespace IbkrConduit.Watchlists;

/// <summary>
/// Refit interface for IBKR watchlist endpoints.
/// </summary>
public interface IIbkrWatchlistApi
{
    /// <summary>
    /// Creates a new watchlist.
    /// </summary>
    [Post("/v1/api/iserver/watchlist")]
    Task<IApiResponse<CreateWatchlistResponse>> CreateWatchlistAsync(
        [Body] CreateWatchlistRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all watchlists.
    /// </summary>
    [Get("/v1/api/iserver/watchlists")]
    Task<IApiResponse<GetWatchlistsResponse>> GetWatchlistsAsync(
        [Query] string sc = "USER_WATCHLIST", CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific watchlist by ID.
    /// </summary>
    [Get("/v1/api/iserver/watchlist")]
    Task<IApiResponse<WatchlistDetail>> GetWatchlistAsync(
        [Query] string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a watchlist by ID.
    /// </summary>
    [Delete("/v1/api/iserver/watchlist")]
    Task<IApiResponse<DeleteWatchlistResponse>> DeleteWatchlistAsync(
        [Query] string id, CancellationToken cancellationToken = default);
}
