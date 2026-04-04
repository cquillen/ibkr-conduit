using IbkrConduit.Errors;
using IbkrConduit.Fyi;

namespace IbkrConduit.Client;

/// <summary>
/// FYI notification operations on the IBKR API.
/// </summary>
public interface IFyiOperations
{
    /// <summary>
    /// Returns the total number of unread FYI bulletins.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<UnreadBulletinCountResponse>> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current notification subscription settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<FyiSettingItem>>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a specific FYI subscription type.
    /// </summary>
    /// <param name="typecode">The FYI typecode.</param>
    /// <param name="enabled">Whether to enable or disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiAcknowledgementResponse>> UpdateSettingAsync(string typecode, bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the disclaimer for a specific FYI typecode.
    /// </summary>
    /// <param name="typecode">The FYI typecode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiDisclaimerResponse>> GetDisclaimerAsync(string typecode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the disclaimer for a specific FYI typecode as read.
    /// </summary>
    /// <param name="typecode">The FYI typecode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiAcknowledgementResponse>> MarkDisclaimerReadAsync(string typecode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the FYI delivery options (email and device settings).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiDeliveryOptionsResponse>> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables email delivery for FYI notifications.
    /// </summary>
    /// <param name="enabled">Whether to enable or disable email delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiAcknowledgementResponse>> SetEmailDeliveryAsync(bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a device for FYI notifications.
    /// </summary>
    /// <param name="request">The device registration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiAcknowledgementResponse>> RegisterDeviceAsync(FyiDeviceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device from the notification delivery list.
    /// </summary>
    /// <param name="deviceId">The device identifier to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<bool>> DeleteDeviceAsync(string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of FYI notifications.
    /// </summary>
    /// <param name="max">Maximum number of notifications to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<FyiNotification>>> GetNotificationsAsync(string? max = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets more notifications for pagination.
    /// </summary>
    /// <param name="id">The notification ID to paginate from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<List<FyiNotification>>> GetMoreNotificationsAsync(string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a specific notification as read.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<FyiNotificationReadResponse>> MarkNotificationReadAsync(string notificationId,
        CancellationToken cancellationToken = default);
}
