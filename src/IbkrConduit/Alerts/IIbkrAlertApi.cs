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
    Task<IApiResponse<CreateAlertResponse>> CreateOrModifyAlertAsync(
        string accountId, [Body] CreateAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves alerts for a specific account.
    /// </summary>
    [Get("/v1/api/iserver/account/{accountId}/alerts")]
    Task<IApiResponse<List<AlertSummary>>> GetAlertsAsync(
        string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Market Trading Alerts (MTA) across accounts.
    /// </summary>
    [Get("/v1/api/iserver/account/mta")]
    Task<IApiResponse<List<AlertSummary>>> GetMtaAlertAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific alert.
    /// </summary>
    [Get("/v1/api/iserver/account/alert/{alertId}")]
    Task<IApiResponse<AlertDetail>> GetAlertDetailAsync(
        string alertId, [Query] string type = "Q",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates an alert for the specified account.
    /// </summary>
    [Post("/v1/api/iserver/account/{accountId}/alert/activate")]
    Task<IApiResponse<AlertActivationResponse>> ActivateAlertAsync(
        string accountId, [Body] AlertActivationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an alert for the specified account.
    /// </summary>
    [Delete("/v1/api/iserver/account/{accountId}/alert/{alertId}")]
    Task<IApiResponse<DeleteAlertResponse>> DeleteAlertAsync(
        string accountId, string alertId, CancellationToken cancellationToken = default);
}
