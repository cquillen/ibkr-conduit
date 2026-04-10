using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Watchlist operations that delegate to the underlying Refit API.
/// </summary>
internal partial class WatchlistOperations : IWatchlistOperations
{
    private readonly IIbkrWatchlistApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<WatchlistOperations> _logger;
    private readonly ResultFactory _resultFactory;

    /// <summary>
    /// Creates a new <see cref="WatchlistOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit watchlist API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="resultFactory">Factory for converting API responses to results.</param>
    public WatchlistOperations(IIbkrWatchlistApi api, IbkrClientOptions options, ILogger<WatchlistOperations> logger, ResultFactory resultFactory)
    {
        _api = api;
        _options = options;
        _logger = logger;
        _resultFactory = resultFactory;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Operation} completed with status {StatusCode}")]
    private static partial void LogOperationCompleted(ILogger logger, string operation, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Operation} failed: {ErrorType} (status {StatusCode})")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string errorType, int? statusCode);

    /// <inheritdoc />
    public async Task<Result<CreateWatchlistResponse>> CreateWatchlistAsync(CreateWatchlistRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.CreateWatchlist");
        activity?.SetTag("watchlistId", request.Id);
        var response = await _api.CreateWatchlistAsync(request, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "CreateWatchlist");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<GetWatchlistsResponse>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlists");
        var response = await _api.GetWatchlistsAsync(cancellationToken: cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetWatchlists");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<WatchlistDetail>> GetWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.GetWatchlist");
        activity?.SetTag("watchlistId", id);
        var response = await _api.GetWatchlistAsync(id, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetWatchlist");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<DeleteWatchlistResponse>> DeleteWatchlistAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Watchlists.DeleteWatchlist");
        activity?.SetTag("watchlistId", id);
        var response = await _api.DeleteWatchlistAsync(id, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "DeleteWatchlist");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    private void LogResult<T>(Result<T> result, string operation)
    {
        if (result.IsSuccess)
        {
            LogOperationCompleted(_logger, operation, 200);
        }
        else
        {
            LogOperationFailed(_logger, operation, result.Error.GetType().Name, (int?)result.Error.StatusCode);
        }
    }
}
