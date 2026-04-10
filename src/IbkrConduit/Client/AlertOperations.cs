using IbkrConduit.Alerts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Client;

/// <summary>
/// Alert operations that delegate to the underlying Refit API.
/// </summary>
internal partial class AlertOperations : IAlertOperations
{
    private readonly IIbkrAlertApi _api;
    private readonly IbkrClientOptions _options;
    private readonly ILogger<AlertOperations> _logger;
    private readonly ResultFactory _resultFactory;

    /// <summary>
    /// Creates a new <see cref="AlertOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit alert API client.</param>
    /// <param name="options">Client options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="resultFactory">Factory for converting API responses to results.</param>
    public AlertOperations(IIbkrAlertApi api, IbkrClientOptions options, ILogger<AlertOperations> logger, ResultFactory resultFactory)
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
    public async Task<Result<CreateAlertResponse>> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.CreateOrModifyAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.CreateOrModifyAlertAsync(accountId, request, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "CreateOrModifyAlert");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<AlertSummary>>> GetAlertsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlerts");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAlertsAsync(accountId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAlerts");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<AlertSummary>>> GetMtaAlertAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetMtaAlert");
        var response = await _api.GetMtaAlertAsync(cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetMtaAlert");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AlertDetail>> GetAlertDetailAsync(string alertId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlertDetail");
        activity?.SetTag("alertId", alertId);
        var response = await _api.GetAlertDetailAsync(alertId, cancellationToken: cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "GetAlertDetail");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AlertActivationResponse>> ActivateAlertAsync(string accountId, AlertActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.ActivateAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.ActivateAlertAsync(accountId, request, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "ActivateAlert");
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<DeleteAlertResponse>> DeleteAlertAsync(string accountId, string alertId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.DeleteAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("alertId", alertId);
        var response = await _api.DeleteAlertAsync(accountId, alertId, cancellationToken);
        var result = _resultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        LogResult(result, "DeleteAlert");
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
