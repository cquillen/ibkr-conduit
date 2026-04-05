using Refit;

namespace IbkrConduit.Fyi;

/// <summary>
/// Refit interface for IBKR FYI/Notifications endpoints.
/// </summary>
internal interface IIbkrFyiApi
{
    /// <summary>
    /// Returns the total number of unread FYI bulletins.
    /// </summary>
    [Get("/v1/api/fyi/unreadnumber")]
    Task<IApiResponse<UnreadBulletinCountResponse>> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current notification subscription settings.
    /// </summary>
    [Get("/v1/api/fyi/settings")]
    Task<IApiResponse<List<FyiSettingItem>>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a specific FYI subscription type.
    /// </summary>
    [Post("/v1/api/fyi/settings/{typecode}")]
    Task<IApiResponse<FyiAcknowledgementResponse>> UpdateSettingAsync(
        string typecode, [Body] FyiSettingUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the disclaimer for a specific FYI typecode.
    /// </summary>
    [Get("/v1/api/fyi/disclaimer/{typecode}")]
    Task<IApiResponse<FyiDisclaimerResponse>> GetDisclaimerAsync(
        string typecode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the disclaimer for a specific FYI typecode as read.
    /// </summary>
    [Put("/v1/api/fyi/disclaimer/{typecode}")]
    Task<IApiResponse<FyiAcknowledgementResponse>> MarkDisclaimerReadAsync(
        string typecode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the FYI delivery options (email and device settings).
    /// </summary>
    [Get("/v1/api/fyi/deliveryoptions")]
    Task<IApiResponse<FyiDeliveryOptionsResponse>> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables email delivery for FYI notifications.
    /// </summary>
    [Put("/v1/api/fyi/deliveryoptions/email")]
    Task<IApiResponse<FyiAcknowledgementResponse>> SetEmailDeliveryAsync(
        [AliasAs("enabled")] string enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or updates a device for FYI notifications.
    /// </summary>
    [Post("/v1/api/fyi/deliveryoptions/device")]
    Task<IApiResponse<FyiAcknowledgementResponse>> RegisterDeviceAsync(
        [Body] FyiDeviceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device from the notification delivery list.
    /// </summary>
    [Delete("/v1/api/fyi/deliveryoptions/{deviceId}")]
    Task<IApiResponse<string>> DeleteDeviceAsync(
        string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of FYI notifications.
    /// </summary>
    [Get("/v1/api/fyi/notifications")]
    Task<IApiResponse<List<FyiNotification>>> GetNotificationsAsync(
        [AliasAs("max")] string? max = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets more notifications for pagination.
    /// </summary>
    [Get("/v1/api/fyi/notifications/more")]
    Task<IApiResponse<List<FyiNotification>>> GetMoreNotificationsAsync(
        [AliasAs("id")] string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a specific notification as read.
    /// </summary>
    [Put("/v1/api/fyi/notifications/{notificationId}")]
    Task<IApiResponse<FyiNotificationReadResponse>> MarkNotificationReadAsync(
        string notificationId, CancellationToken cancellationToken = default);
}
