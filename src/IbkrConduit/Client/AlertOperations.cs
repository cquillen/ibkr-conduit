using IbkrConduit.Alerts;
using IbkrConduit.Diagnostics;

namespace IbkrConduit.Client;

/// <summary>
/// Alert operations that delegate to the underlying Refit API.
/// </summary>
public class AlertOperations : IAlertOperations
{
    private readonly IIbkrAlertApi _api;

    /// <summary>
    /// Creates a new <see cref="AlertOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit alert API client.</param>
    public AlertOperations(IIbkrAlertApi api) => _api = api;

    /// <inheritdoc />
    public async Task<CreateAlertResponse> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.CreateOrModifyAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        return await _api.CreateOrModifyAlertAsync(accountId, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<AlertSummary>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlerts");
        return await _api.GetAlertsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AlertDetail> GetAlertDetailAsync(string alertId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.GetAlertDetail");
        activity?.SetTag("alertId", alertId);
        return await _api.GetAlertDetailAsync(alertId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeleteAlertResponse> DeleteAlertAsync(string accountId, string alertId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Alerts.DeleteAlert");
        activity?.SetTag(LogFields.AccountId, accountId);
        activity?.SetTag("alertId", alertId);
        return await _api.DeleteAlertAsync(accountId, alertId, cancellationToken);
    }
}
