using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using IbkrConduit.Fyi;
using IbkrConduit.Session;

namespace IbkrConduit.Client;

/// <summary>
/// FYI notification operations that delegate to the underlying Refit API.
/// </summary>
internal class FyiOperations : IFyiOperations
{
    private readonly IIbkrFyiApi _api;
    private readonly IbkrClientOptions _options;

    /// <summary>
    /// Creates a new <see cref="FyiOperations"/> instance.
    /// </summary>
    /// <param name="api">The Refit FYI API client.</param>
    /// <param name="options">Client options.</param>
    public FyiOperations(IIbkrFyiApi api, IbkrClientOptions options)
    {
        _api = api;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<Result<UnreadBulletinCountResponse>> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetUnreadCount");
        var response = await _api.GetUnreadCountAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<FyiSettingItem>>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetSettings");
        var response = await _api.GetSettingsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiAcknowledgementResponse>> UpdateSettingAsync(string typecode, bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.UpdateSetting");
        activity?.SetTag("typecode", typecode);
        activity?.SetTag("enabled", enabled);
        var response = await _api.UpdateSettingAsync(typecode, new FyiSettingUpdateRequest(enabled), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiDisclaimerResponse>> GetDisclaimerAsync(string typecode,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetDisclaimer");
        activity?.SetTag("typecode", typecode);
        var response = await _api.GetDisclaimerAsync(typecode, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiAcknowledgementResponse>> MarkDisclaimerReadAsync(string typecode,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.MarkDisclaimerRead");
        activity?.SetTag("typecode", typecode);
        var response = await _api.MarkDisclaimerReadAsync(typecode, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiDeliveryOptionsResponse>> GetDeliveryOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetDeliveryOptions");
        var response = await _api.GetDeliveryOptionsAsync(cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiAcknowledgementResponse>> SetEmailDeliveryAsync(bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.SetEmailDelivery");
        activity?.SetTag("enabled", enabled);
        var response = await _api.SetEmailDeliveryAsync(enabled.ToString().ToLowerInvariant(), cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiAcknowledgementResponse>> RegisterDeviceAsync(FyiDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.RegisterDevice");
        var response = await _api.RegisterDeviceAsync(request, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteDeviceAsync(string deviceId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.DeleteDevice");
        var response = await _api.DeleteDeviceAsync(deviceId, cancellationToken);
        // Void-returning endpoint: check success status and return Result<bool>
        if (response.IsSuccessStatusCode)
        {
            var result = Result<bool>.Success(true);
            return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
        }

        var rawBody = response.Error?.Content ?? "";
        var error = new IbkrApiError(response.StatusCode, rawBody, rawBody, response.RequestMessage?.RequestUri?.AbsolutePath);
        var failResult = Result<bool>.Failure(error);
        return _options.ThrowOnApiError ? failResult.EnsureSuccess() : failResult;
    }

    /// <inheritdoc />
    public async Task<Result<List<FyiNotification>>> GetNotificationsAsync(string? max = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetNotifications");
        var response = await _api.GetNotificationsAsync(max, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<List<FyiNotification>>> GetMoreNotificationsAsync(string id,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.GetMoreNotifications");
        var response = await _api.GetMoreNotificationsAsync(id, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }

    /// <inheritdoc />
    public async Task<Result<FyiNotificationReadResponse>> MarkNotificationReadAsync(string notificationId,
        CancellationToken cancellationToken = default)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Fyi.MarkNotificationRead");
        activity?.SetTag("notificationId", notificationId);
        var response = await _api.MarkNotificationReadAsync(notificationId, cancellationToken);
        var result = ResultFactory.FromResponse(response, response.RequestMessage?.RequestUri?.AbsolutePath);
        return _options.ThrowOnApiError ? result.EnsureSuccess() : result;
    }
}
