using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using IbkrConduit.Watchlists;

namespace IbkrConduit.Client;

/// <summary>
/// Watchlist operations that delegate to the underlying Refit API.
/// </summary>
public class WatchlistOperations : IWatchlistOperations
{
    private readonly IIbkrWatchlistApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="WatchlistOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit watchlist API client.</param>
    /// <param name="options">Client options.</param>
    public WatchlistOperations(IIbkrWatchlistApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<CreateWatchlistResponse>> CreateWatchlistAsync(CreateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.CreateWatchlist");
        activity?.SetTag("watchlistId", request.Id);
        var response = await _api.CreateWatchlistAsync(request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<GetWatchlistsResponse>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlists");
        var response = await _api.GetWatchlistsAsync(cancellationToken: cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<WatchlistDetail>> GetWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlist");
        activity?.SetTag("watchlistId", id);
        var response = await _api.GetWatchlistAsync(id, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<DeleteWatchlistResponse>> DeleteWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.DeleteWatchlist");
        activity?.SetTag("watchlistId", id);
        var response = await _api.DeleteWatchlistAsync(id, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }
}
