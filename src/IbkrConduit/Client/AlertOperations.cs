using IbkrConduit.Alerts;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// Alert operations that delegate to the underlying Refit API.
/// </summary>
public class AlertOperations : IAlertOperations
{
    private readonly IIbkrAlertApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="AlertOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit alert API client.</param>
    /// <param name="options">Client options.</param>
    public AlertOperations(IIbkrAlertApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<CreateAlertResponse>> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.CreateOrModifyAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.CreateOrModifyAlertAsync(accountId, request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<AlertSummary>>> GetAlertsAsync(string accountId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlerts");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.GetAlertsAsync(accountId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<AlertSummary>>> GetMtaAlertAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetMtaAlert");
        var response = await _api.GetMtaAlertAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AlertDetail>> GetAlertDetailAsync(string alertId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlertDetail");
        activity?.SetTag("alertId", alertId);
        var response = await _api.GetAlertDetailAsync(alertId, cancellationToken: cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<AlertActivationResponse>> ActivateAlertAsync(string accountId, AlertActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.ActivateAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        var response = await _api.ActivateAlertAsync(accountId, request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
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
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }
}
