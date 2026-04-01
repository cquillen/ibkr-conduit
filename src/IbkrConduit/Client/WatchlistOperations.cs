using IbkrConduit.Diagnostics;
using IbkrConduit.Watchlists;

namespace IbkrConduit.Client;

/// <summary>
/// Watchlist operations that delegate to the underlying Refit API.
/// </summary>
public class WatchlistOperations : IWatchlistOperations
{
    private readonly IIbkrWatchlistApi _api;

    /// <summary>
    /// Creates a new <see cref="WatchlistOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit watchlist API client.</param>
    public WatchlistOperations(IIbkrWatchlistApi api) => _api = api;

    /// <inheritdoc />
    public async Task<CreateWatchlistResponse> CreateWatchlistAsync(CreateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.CreateWatchlist");
        activity?.SetTag("watchlistId", request.Id);
        return await _api.CreateWatchlistAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<WatchlistSummary>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlists");
        return await _api.GetWatchlistsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WatchlistDetail> GetWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlist");
        activity?.SetTag("watchlistId", id);
        return await _api.GetWatchlistAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeleteWatchlistResponse> DeleteWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.DeleteWatchlist");
        activity?.SetTag("watchlistId", id);
        return await _api.DeleteWatchlistAsync(id, cancellationToken);
    }
}
