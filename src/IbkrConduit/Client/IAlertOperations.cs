using IbkrConduit.Alerts;

namespace IbkrConduit.Client;

/// <summary>
/// Alert (MTA) operations on the IBKR API.
/// </summary>
public interface IAlertOperations
{
    /// <summary>
    /// Creates or modifies an alert for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="request">The alert definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CreateAlertResponse> CreateOrModifyAlertAsync(string accountId, CreateAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Market Trading Alerts (MTA) across accounts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<AlertSummary>> GetAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific alert.
    /// </summary>
    /// <param name="alertId">The alert identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AlertDetail> GetAlertDetailAsync(string alertId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an alert for the specified account.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="alertId">The alert identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DeleteAlertResponse> DeleteAlertAsync(string accountId, string alertId,
        CancellationToken cancellationToken = default);
}
