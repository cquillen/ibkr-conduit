using IbkrConduit.Diagnostics;
using IbkrConduit.Fyi;

namespace IbkrConduit.Client;

/// <summary>
/// FYI notification operations that delegate to the underlying Refit API.
/// </summary>
public class FyiOperations : IFyiOperations
{
    private readonly IIbkrFyiApi _api;

    /// <summary>
    /// Creates a new <see cref="FyiOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit FYI API client.</param>
    public FyiOperations(IIbkrFyiApi api) => _api = api;

    /// <inheritdoc />
    public async Task<UnreadBulletinCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetUnreadCount");
        return await _api.GetUnreadCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<FyiSettingItem>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetSettings");
        return await _api.GetSettingsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiAcknowledgementResponse> UpdateSettingAsync(string typecode, bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.UpdateSetting");
        activity?.SetTag("typecode", typecode);
        activity?.SetTag("enabled", enabled);
        return await _api.UpdateSettingAsync(typecode, new FyiSettingUpdateRequest(enabled), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiDisclaimerResponse> GetDisclaimerAsync(string typecode,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetDisclaimer");
        activity?.SetTag("typecode", typecode);
        return await _api.GetDisclaimerAsync(typecode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiAcknowledgementResponse> MarkDisclaimerReadAsync(string typecode,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.MarkDisclaimerRead");
        activity?.SetTag("typecode", typecode);
        return await _api.MarkDisclaimerReadAsync(typecode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiDeliveryOptionsResponse> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetDeliveryOptions");
        return await _api.GetDeliveryOptionsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiAcknowledgementResponse> SetEmailDeliveryAsync(bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.SetEmailDelivery");
        activity?.SetTag("enabled", enabled);
        return await _api.SetEmailDeliveryAsync(enabled.ToString().ToLowerInvariant(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiAcknowledgementResponse> RegisterDeviceAsync(FyiDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.RegisterDevice");
        return await _api.RegisterDeviceAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDeviceAsync(string deviceId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.DeleteDevice");
        await _api.DeleteDeviceAsync(deviceId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<FyiNotification>> GetNotificationsAsync(string? max = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetNotifications");
        return await _api.GetNotificationsAsync(max, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<FyiNotification>> GetMoreNotificationsAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetMoreNotifications");
        return await _api.GetMoreNotificationsAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FyiNotificationReadResponse> MarkNotificationReadAsync(string notificationId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.MarkNotificationRead");
        activity?.SetTag("notificationId", notificationId);
        return await _api.MarkNotificationReadAsync(notificationId, cancellationToken);
    }
}
