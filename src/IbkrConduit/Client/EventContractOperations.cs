using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.EventContracts;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Event contract (ForecastEx) operations that delegate to the underlying Refit API.
/// </summary>
internal partial class EventContractOperations : IEventContractOperations
{
    private readonly IIbkrEventContractApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<EventContractOperations> _logger;
    private readonly ResultFactory _resultFactory;

    /// <summary>
    /// Creates a new <see cref="EventContractOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit event contract API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="resultFactory">Factory for converting API responses to results.</param>
    public EventContractOperations(IIbkrEventContractApi api, IbkrClientOptions options, ILogger<EventContractOperations> logger, ResultFactory resultFactory)
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
    public async Task<Result<EventContractCategoryTreeResponse>> GetCategoryTreeAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetCategoryTree");
        var response = await _api.GetCategoryTreeAsync(cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetCategoryTree");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractMarketResponse>> GetMarketAsync(int underlyingConid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetMarket");
        activity?.SetTag("underlyingConid", underlyingConid);
        var response = await _api.GetMarketAsync(underlyingConid, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetMarket");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractRulesResponse>> GetContractRulesAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractRules");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractRulesAsync(conid, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetContractRules");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractDetailsResponse>> GetContractDetailsAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractDetails");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractDetailsAsync(conid, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetContractDetails");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<EventContractSchedulesResponse>> GetContractSchedulesAsync(int conid,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.EventContracts.GetContractSchedules");
        activity?.SetTag("conid", conid);
        var response = await _api.GetContractSchedulesAsync(conid, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetContractSchedules");
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
