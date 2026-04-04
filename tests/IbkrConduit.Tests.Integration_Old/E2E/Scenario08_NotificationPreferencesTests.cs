using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Fyi;
using Refit;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 8: Notification Preferences.
/// Exercises FYI settings, disclaimers, delivery options, device registration,
/// and notification management against a real IBKR paper trading account.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario08_NotificationPreferencesTests : E2eScenarioBase
{
    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task NotificationPreferences_FullWorkflow()
    {
        var (_, client) = CreateClient();
        var toggledTypecode = (string?)null;
        var originalEnabled = false;
        var originalEmailEnabled = (bool?)null;
        var registeredDeviceId = (string?)null;

        try
        {

            // Step 1: Get unread notification count
            var unreadCount = (await client.Notifications.GetUnreadCountAsync(CT)).Value;
            unreadCount.ShouldNotBeNull();
            unreadCount.BN.ShouldBeGreaterThanOrEqualTo(0, "Unread count should be non-negative");

            // Step 2: Get current FYI settings — capture original state
            var settings = (await client.Notifications.GetSettingsAsync(CT)).Value;
            settings.ShouldNotBeNull();
            settings.ShouldNotBeEmpty("Should have at least one notification setting");

            // Pick a typecode to toggle — prefer one that allows modification (A=1)
            var settingToToggle = settings.FirstOrDefault(s => s.A == 1) ?? settings[0];
            toggledTypecode = settingToToggle.FC;
            // H=0 means unread disclaimer (disabled), H=1 means read (enabled)
            originalEnabled = settingToToggle.H == 1;

            // Step 3: Toggle the setting (flip enabled state)
            var toggleResult = (await client.Notifications.UpdateSettingAsync(
                toggledTypecode, !originalEnabled, CT)).Value;
            toggleResult.ShouldNotBeNull();

            // Step 4: Restore handled in finally block

            // Step 5: Get disclaimer for the typecode
            try
            {
                var disclaimer = (await client.Notifications.GetDisclaimerAsync(toggledTypecode, CT)).Value;
                disclaimer.ShouldNotBeNull();
                disclaimer.FC.ShouldNotBeNullOrEmpty("Disclaimer should have a typecode");
            }
            catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                              or System.Net.HttpStatusCode.BadRequest)
            {
                // IBKR QUIRK: Some typecodes may not have disclaimers.
            }

            // Step 6: Mark disclaimer as read
            try
            {
                var markResult = (await client.Notifications.MarkDisclaimerReadAsync(toggledTypecode, CT)).Value;
                markResult.ShouldNotBeNull();
            }
            catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound
                                              or System.Net.HttpStatusCode.BadRequest
                                              or System.Net.HttpStatusCode.InternalServerError)
            {
                // IBKR QUIRK: Marking disclaimer read may fail if no disclaimer exists for the typecode.
            }

            // Step 7: Get delivery options
            var deliveryOptions = (await client.Notifications.GetDeliveryOptionsAsync(CT)).Value;
            deliveryOptions.ShouldNotBeNull();
            originalEmailEnabled = deliveryOptions.M == 1;

            // Step 8: Toggle email delivery (flip current state) — restore in finally
            var emailToggleResult = (await client.Notifications.SetEmailDeliveryAsync(
                !originalEmailEnabled.Value, CT)).Value;
            emailToggleResult.ShouldNotBeNull();

            // Step 9: Attempt device registration with synthetic token — may fail, that's OK
            try
            {
                var deviceRequest = new FyiDeviceRequest(
                    DeviceName: "E2E-TestDevice",
                    DeviceId: $"E2E-{Guid.NewGuid():N}",
                    UiName: "E2E Test UI",
                    Enabled: true);

                var registerResult = (await client.Notifications.RegisterDeviceAsync(deviceRequest, CT)).Value;
                registerResult.ShouldNotBeNull();

                // If registration succeeded, capture the device ID for cleanup
                registeredDeviceId = deviceRequest.DeviceId;
            }
            catch (ApiException)
            {
                // IBKR QUIRK: Device registration requires a real mobile device token.
                // Synthetic tokens are expected to be rejected.
            }

            // Step 10: If device registered, delete it
            if (registeredDeviceId is not null)
            {
                try
                {
                    await client.Notifications.DeleteDeviceAsync(registeredDeviceId, CT);
                    registeredDeviceId = null;
                }
                catch (ApiException)
                {
                    // IBKR QUIRK: Device delete may fail even after successful registration.
                }
            }

            // Step 11: Get notifications
            var notifications = (await client.Notifications.GetNotificationsAsync(cancellationToken: CT)).Value;
            notifications.ShouldNotBeNull();

            // Step 12: If notifications exist, get more for pagination
            if (notifications.Count > 0)
            {
                var lastNotification = notifications[^1];
                try
                {
                    var moreNotifications = (await client.Notifications.GetMoreNotificationsAsync(
                        lastNotification.ID, CT)).Value;
                    moreNotifications.ShouldNotBeNull();
                }
                catch (ApiException)
                {
                    // IBKR QUIRK: Pagination may return error if no more notifications exist.
                }

                // Step 13: Mark one notification as read
                var firstNotification = notifications[0];
                try
                {
                    var readResult = (await client.Notifications.MarkNotificationReadAsync(
                        firstNotification.ID, CT)).Value;
                    readResult.ShouldNotBeNull();
                }
                catch (ApiException)
                {
                    // IBKR QUIRK: Marking notification as read may fail for already-read notifications.
                }
            }

        }
        finally
        {
            // Restore toggled setting to original value
            if (toggledTypecode is not null)
            {
                try
                {
                    await client.Notifications.UpdateSettingAsync(toggledTypecode, originalEnabled, CT);
                }
                catch
                {
                    // Cleanup best-effort — IBKR may reject the restore if session expired
                }
            }

            // Restore email delivery to original value
            if (originalEmailEnabled is not null)
            {
                try
                {
                    await client.Notifications.SetEmailDeliveryAsync(originalEmailEnabled.Value, CT);
                }
                catch
                {
                    // Cleanup best-effort
                }
            }

            // Delete device if still registered
            if (registeredDeviceId is not null)
            {
                try
                {
                    await client.Notifications.DeleteDeviceAsync(registeredDeviceId, CT);
                }
                catch
                {
                    // Cleanup best-effort
                }
            }

            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task UpdateSetting_InvalidTypecode_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = (await client.Notifications.UpdateSettingAsync("NONEXISTENT", true, CT)).Value;

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid typecode.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid typecodes.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task GetDisclaimer_InvalidTypecode_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = (await client.Notifications.GetDisclaimerAsync("NONEXISTENT", CT)).Value;

                // IBKR QUIRK: If we get here, the API returned 200 for an invalid typecode.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for invalid typecodes.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task MarkNotificationRead_NonExistentId_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                var result = (await client.Notifications.MarkNotificationReadAsync("0", CT)).Value;

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent notification ID.
                result.ShouldNotBeNull();
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent notification IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }

    [EnvironmentFact("IBKR_CONSUMER_KEY")]
    public async Task DeleteDevice_NonExistentId_HandlesError()
    {
        var (_, client) = CreateClient();

        try
        {

            try
            {
                await client.Notifications.DeleteDeviceAsync("FAKE-DEVICE-999", CT);

                // IBKR QUIRK: If we get here, the API returned 200 for a non-existent device ID.
            }
            catch (ApiException)
            {
                // Expected: IBKR returns an HTTP error for non-existent device IDs.
            }

        }
        finally
        {
            await DisposeAsync();
        }
    }
}
