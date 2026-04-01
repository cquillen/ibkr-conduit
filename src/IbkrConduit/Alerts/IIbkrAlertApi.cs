using Refit;

namespace IbkrConduit.Alerts;

/// <summary>
/// Refit interface for IBKR alert (MTA) endpoints.
/// </summary>
public interface IIbkrAlertApi
{
    /// <summary>
    /// Creates or modifies an alert for the specified account.
    /// </summary>
    [Post("/v1/api/iserver/account/{accountId}/alert")]
    Task<CreateAlertResponse> CreateOrModifyAlertAsync(
        string accountId, [Body] CreateAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Market Trading Alerts (MTA) across accounts.
    /// </summary>
    [Get("/v1/api/iserver/account/mta")]
    Task<List<AlertSummary>> GetAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific alert.
    /// </summary>
    [Get("/v1/api/iserver/account/alert/{alertId}")]
    Task<AlertDetail> GetAlertDetailAsync(
        string alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an alert for the specified account.
    /// </summary>
    [Delete("/v1/api/iserver/account/{accountId}/alert/{alertId}")]
    Task<DeleteAlertResponse> DeleteAlertAsync(
        string accountId, string alertId, CancellationToken cancellationToken = default);
}
