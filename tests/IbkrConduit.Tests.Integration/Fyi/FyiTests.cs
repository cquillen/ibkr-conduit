using System;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Fyi;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Fyi;

public class FyiTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    // --- GetUnreadCountAsync ---

    [Fact]
    public async Task GetUnreadCount_ReturnsCount()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/unreadnumber",
            FixtureLoader.LoadBody("Fyi", "GET-unread-count"));

        var result = (await _harness.Client.Notifications.GetUnreadCountAsync(
            TestContext.Current.CancellationToken)).Value;

        result.BN.ShouldBe(0);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetUnreadCount_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/unreadnumber")
                .UsingGet())
            .InScenario("unread-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/unreadnumber")
                .UsingGet())
            .InScenario("unread-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-unread-count")));

        var result = (await _harness.Client.Notifications.GetUnreadCountAsync(
            TestContext.Current.CancellationToken)).Value;

        result.BN.ShouldBe(0);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- GetSettingsAsync ---

    [Fact]
    public async Task GetSettings_ReturnsSettings()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/settings",
            FixtureLoader.LoadBody("Fyi", "GET-settings"));

        var result = (await _harness.Client.Notifications.GetSettingsAsync(
            TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3);
        result[0].FC.ShouldBe("BA");
        result[0].FN.ShouldBe("Borrow Availability");
        result[0].H.ShouldBe(1);
        result[0].A.ShouldBe(1);
        result[1].FC.ShouldBe("OE");
        result[2].FC.ShouldBe("EA");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetSettings_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings")
                .UsingGet())
            .InScenario("settings-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings")
                .UsingGet())
            .InScenario("settings-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-settings")));

        var result = (await _harness.Client.Notifications.GetSettingsAsync(
            TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    // --- UpdateSettingAsync ---

    [Fact]
    public async Task UpdateSetting_ReturnsAcknowledgement()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/fyi/settings/BA",
            FixtureLoader.LoadBody("Fyi", "POST-update-setting"));

        var result = (await _harness.Client.Notifications.UpdateSettingAsync(
            "BA", true, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);
        result.T.ShouldBe(10);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task UpdateSetting_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings/BA")
                .UsingPost())
            .InScenario("update-setting-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings/BA")
                .UsingPost())
            .InScenario("update-setting-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "POST-update-setting")));

        var result = (await _harness.Client.Notifications.UpdateSettingAsync(
            "BA", true, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- GetDisclaimerAsync ---

    [Fact]
    public async Task GetDisclaimer_ReturnsDisclaimer()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/disclaimer/BA",
            FixtureLoader.LoadBody("Fyi", "GET-disclaimer"));

        var result = (await _harness.Client.Notifications.GetDisclaimerAsync(
            "BA", TestContext.Current.CancellationToken)).Value;

        result.FC.ShouldBe("BA");
        result.DT.ShouldContain("information purposes only");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetDisclaimer_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/BA")
                .UsingGet())
            .InScenario("disclaimer-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/BA")
                .UsingGet())
            .InScenario("disclaimer-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-disclaimer")));

        var result = (await _harness.Client.Notifications.GetDisclaimerAsync(
            "BA", TestContext.Current.CancellationToken)).Value;

        result.FC.ShouldBe("BA");

        _harness.VerifyReauthenticationOccurred();
    }

    // --- MarkDisclaimerReadAsync ---

    [Fact]
    public async Task MarkDisclaimerRead_ReturnsAcknowledgement()
    {
        _harness.StubAuthenticated(
            HttpMethod.Put,
            "/v1/api/fyi/disclaimer/BA",
            FixtureLoader.LoadBody("Fyi", "PUT-mark-disclaimer-read"));

        var result = (await _harness.Client.Notifications.MarkDisclaimerReadAsync(
            "BA", TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);
        result.T.ShouldBe(10);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task MarkDisclaimerRead_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/BA")
                .UsingPut())
            .InScenario("mark-disclaimer-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/BA")
                .UsingPut())
            .InScenario("mark-disclaimer-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "PUT-mark-disclaimer-read")));

        var result = (await _harness.Client.Notifications.MarkDisclaimerReadAsync(
            "BA", TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- GetDeliveryOptionsAsync ---

    [Fact]
    public async Task GetDeliveryOptions_ReturnsOptions()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/deliveryoptions",
            FixtureLoader.LoadBody("Fyi", "GET-delivery-options"));

        var result = (await _harness.Client.Notifications.GetDeliveryOptionsAsync(
            TestContext.Current.CancellationToken)).Value;

        result.M.ShouldBe(1);
        result.E.ShouldNotBeEmpty();
        result.E[0].NM.ShouldBe("Test-Device");
        result.E[0].I.ShouldBe("test-device-id-001");
        result.E[0].UI.ShouldBe("Test Device UI");
        result.E[0].A.ShouldBe(1);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetDeliveryOptions_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions")
                .UsingGet())
            .InScenario("delivery-opts-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions")
                .UsingGet())
            .InScenario("delivery-opts-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-delivery-options")));

        var result = (await _harness.Client.Notifications.GetDeliveryOptionsAsync(
            TestContext.Current.CancellationToken)).Value;

        result.E.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    // --- GetNotificationsAsync ---

    [Fact]
    public async Task GetNotifications_ReturnsNotifications()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/notifications",
            FixtureLoader.LoadBody("Fyi", "GET-notifications"));

        var result = (await _harness.Client.Notifications.GetNotificationsAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();
        result[0].R.ShouldBe(1);
        result[0].D.ShouldBe("1775041875.0");
        result[0].MS.ShouldBe("IBKR FYI: Mutual Fund/ETF Advisory");
        result[0].ID.ShouldBe("2026040190532092");
        result[0].FC.ShouldBe("MF");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetNotifications_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications")
                .UsingGet())
            .InScenario("notifications-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications")
                .UsingGet())
            .InScenario("notifications-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-notifications")));

        var result = (await _harness.Client.Notifications.GetNotificationsAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    // --- GetMoreNotificationsAsync ---

    [Fact]
    public async Task GetMoreNotifications_ReturnsNotifications()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/fyi/notifications/more",
            FixtureLoader.LoadBody("Fyi", "GET-more-notifications"));

        var result = (await _harness.Client.Notifications.GetMoreNotificationsAsync(
            "2026040190532092", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();
        result[0].R.ShouldBe(0);
        result[0].MS.ShouldBe("IBKR FYI: Option Expiration");
        result[0].ID.ShouldBe("2026040100000001");
        result[0].FC.ShouldBe("OE");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetMoreNotifications_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/more")
                .UsingGet())
            .InScenario("more-notif-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/more")
                .UsingGet())
            .InScenario("more-notif-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "GET-more-notifications")));

        var result = (await _harness.Client.Notifications.GetMoreNotificationsAsync(
            "2026040190532092", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    // --- MarkNotificationReadAsync ---

    [Fact]
    public async Task MarkNotificationRead_ReturnsReadStatus()
    {
        _harness.StubAuthenticated(
            HttpMethod.Put,
            "/v1/api/fyi/notifications/2026040190532092",
            FixtureLoader.LoadBody("Fyi", "PUT-mark-notification-read"));

        var result = (await _harness.Client.Notifications.MarkNotificationReadAsync(
            "2026040190532092", TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);
        result.T.ShouldBe(5);
        result.P.ShouldNotBeNull();
        result.P!.R.ShouldBe(1);
        result.P!.ID.ShouldBe("2026040190532092");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task MarkNotificationRead_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/2026040190532092")
                .UsingPut())
            .InScenario("mark-notif-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/2026040190532092")
                .UsingPut())
            .InScenario("mark-notif-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "PUT-mark-notification-read")));

        var result = (await _harness.Client.Notifications.MarkNotificationReadAsync(
            "2026040190532092", TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- SetEmailDeliveryAsync ---

    [Fact]
    public async Task SetEmailDelivery_ReturnsAcknowledgement()
    {
        _harness.StubAuthenticated(
            HttpMethod.Put,
            "/v1/api/fyi/deliveryoptions/email",
            FixtureLoader.LoadBody("Fyi", "PUT-set-email-delivery"));

        var result = (await _harness.Client.Notifications.SetEmailDeliveryAsync(
            true, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);
        result.T.ShouldBe(10);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SetEmailDelivery_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/email")
                .UsingPut())
            .InScenario("email-delivery-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/email")
                .UsingPut())
            .InScenario("email-delivery-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "PUT-set-email-delivery")));

        var result = (await _harness.Client.Notifications.SetEmailDeliveryAsync(
            true, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- RegisterDeviceAsync ---

    [Fact]
    public async Task RegisterDevice_ReturnsAcknowledgement()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/fyi/deliveryoptions/device",
            FixtureLoader.LoadBody("Fyi", "POST-register-device"));

        var request = new FyiDeviceRequest("Test Device", "dev-001", "Test UI", true);
        var result = (await _harness.Client.Notifications.RegisterDeviceAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);
        result.T.ShouldBe(10);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task RegisterDevice_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/device")
                .UsingPost())
            .InScenario("register-device-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/device")
                .UsingPost())
            .InScenario("register-device-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Fyi", "POST-register-device")));

        var request = new FyiDeviceRequest("Test Device", "dev-001", "Test UI", true);
        var result = (await _harness.Client.Notifications.RegisterDeviceAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.V.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- DeleteDeviceAsync ---

    [Fact]
    public async Task DeleteDevice_ReturnsTrue()
    {
        _harness.StubAuthenticated(
            HttpMethod.Delete,
            "/v1/api/fyi/deliveryoptions/test-device-001",
            "");

        var result = (await _harness.Client.Notifications.DeleteDeviceAsync(
            "test-device-001", TestContext.Current.CancellationToken)).Value;

        result.ShouldBeTrue();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task DeleteDevice_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/test-device-001")
                .UsingDelete())
            .InScenario("delete-device-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/test-device-001")
                .UsingDelete())
            .InScenario("delete-device-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(""));

        var result = (await _harness.Client.Notifications.DeleteDeviceAsync(
            "test-device-001", TestContext.Current.CancellationToken)).Value;

        result.ShouldBeTrue();

        _harness.VerifyReauthenticationOccurred();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
