using IbkrConduit.Watchlists;

namespace IbkrConduit.Client;

/// <summary>
/// Watchlist operations on the IBKR API.
/// </summary>
public interface IWatchlistOperations
{
    /// <summary>
    /// Creates a new watchlist.
    /// </summary>
    /// <param name="request">The watchlist definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CreateWatchlistResponse> CreateWatchlistAsync(CreateWatchlistRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all watchlists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<WatchlistSummary>> GetWatchlistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific watchlist by ID.
    /// </summary>
    /// <param name="id">The watchlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WatchlistDetail> GetWatchlistAsync(string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a watchlist by ID.
    /// </summary>
    /// <param name="id">The watchlist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeleteWatchlistResponse> DeleteWatchlistAsync(string id,
        CancellationToken cancellationToken = default);
}
