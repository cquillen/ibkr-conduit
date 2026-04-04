using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Alerts;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Alerts;

public class AlertTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetAlerts_Success_ReturnsAlertSummaries()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/DU9999999/alerts",
            FixtureLoader.LoadBody("Alerts", "GET-alerts-list"));

        var result = (await _harness.Client.Alerts.GetAlertsAsync("DU9999999", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].Account.ShouldBe("DU9999999");
        result[0].OrderId.ShouldBe(100001);
        result[0].AlertName.ShouldBe("SPY Price Alert");
        result[0].AlertActive.ShouldBe(1);
        result[0].AlertRepeatable.ShouldBe(0);
        result[0].OrderTime.ShouldBe("20260403-14:30:00");
        result[0].AlertTriggered.ShouldBeFalse();

        result[1].Account.ShouldBe("DU9999999");
        result[1].OrderId.ShouldBe(100002);
        result[1].AlertName.ShouldBe("AAPL Volume Alert");
        result[1].AlertActive.ShouldBe(0);
        result[1].AlertRepeatable.ShouldBe(1);
        result[1].AlertTriggered.ShouldBeTrue();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetMtaAlert_Success_ReturnsAlertSummaries()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/mta",
            FixtureLoader.LoadBody("Alerts", "GET-mta-alert"));

        var result = (await _harness.Client.Alerts.GetMtaAlertAsync(TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Account.ShouldBe("DU9999999");
        result[0].OrderId.ShouldBe(100001);
        result[0].AlertName.ShouldBe("SPY Price Alert");
        result[0].AlertActive.ShouldBe(1);
        result[0].AlertRepeatable.ShouldBe(0);
        result[0].AlertTriggered.ShouldBeFalse();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAlertDetail_Success_ReturnsFullAlertDetail()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/account/alert/100001",
            FixtureLoader.LoadBody("Alerts", "GET-alert-detail"));

        var result = (await _harness.Client.Alerts.GetAlertDetailAsync("100001", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Account.ShouldBe("DU9999999");
        result.OrderId.ShouldBe(100001);
        result.AlertName.ShouldBe("SPY Price Alert");
        result.AlertMessage.ShouldBe("SPY crossed above 500");
        result.AlertActive.ShouldBe(1);
        result.AlertRepeatable.ShouldBe(0);
        result.Tif.ShouldBe("GTC");
        result.AlertEmail.ShouldBe("test@example.com");
        result.AlertSendMessage.ShouldBe(1);
        result.AlertShowPopup.ShouldBe(1);
        result.AlertPlayAudio.ShouldBeNull();
        result.OrderStatus.ShouldBe("PreSubmitted");
        result.AlertTriggered.ShouldBeFalse();
        result.FgColor.ShouldBe("#000000");
        result.BgColor.ShouldBe("#FFFFFF");
        result.OrderNotEditable.ShouldBeFalse();
        result.ItwsOrdersOnly.ShouldBe(0);
        result.AlertMtaCurrency.ShouldBe("USD");
        result.ToolId.ShouldBe(12345678);
        result.TimeZone.ShouldBe("US/Eastern");
        result.ConditionSize.ShouldBe(1);
        result.ConditionOutsideRth.ShouldBe(0);

        result.Conditions.ShouldNotBeEmpty();
        result.Conditions.Count.ShouldBe(1);

        var condition = result.Conditions[0];
        condition.ConditionType.ShouldBe(1);
        condition.Conidex.ShouldBe("265598");
        condition.ContractDescription1.ShouldBe("SPDR S&P 500 ETF Trust");
        condition.ConditionOperator.ShouldBe(">=");
        condition.ConditionTriggerMethod.ShouldBe("0");
        condition.ConditionValue.ShouldBe("500.00");
        condition.ConditionLogicBind.ShouldBe("a");
        condition.ConditionTimeZone.ShouldBe("US/Eastern");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task CreateAlert_Success_ReturnsCreateAlertResponse()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/DU9999999/alert",
            FixtureLoader.LoadBody("Alerts", "POST-create-alert"));

        var request = new CreateAlertRequest(
            OrderId: 0,
            AlertName: "SPY Price Alert",
            AlertMessage: "SPY crossed above 500",
            AlertRepeatable: 0,
            OutsideRth: 0,
            Tif: "GTC",
            Conditions: new List<AlertCondition>
            {
                new(Type: 1, Conidex: "265598", Operator: ">=", TriggerMethod: "0", Value: "500"),
            });

        var result = (await _harness.Client.Alerts.CreateOrModifyAlertAsync("DU9999999", request, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(1);
        result.OrderId.ShouldBe(100003);
        result.Success.ShouldBeTrue();
        result.Text.ShouldBe("Alert has been created.");
        result.OrderStatus.ShouldBe("PreSubmitted");
        result.WarningMessage.ShouldBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task ActivateAlert_Success_ReturnsActivationResponse()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/account/DU9999999/alert/activate",
            FixtureLoader.LoadBody("Alerts", "POST-activate-alert"));

        var request = new AlertActivationRequest(AlertId: 100001, AlertActive: 1);
        var result = (await _harness.Client.Alerts.ActivateAlertAsync("DU9999999", request, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(2);
        result.OrderId.ShouldBe(100001);
        result.Success.ShouldBeTrue();
        result.Text.ShouldBe("Alert has been activated.");
        result.FailureList.ShouldBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task DeleteAlert_Success_ReturnsDeleteResponse()
    {
        _harness.StubAuthenticated(
            HttpMethod.Delete,
            "/v1/api/iserver/account/DU9999999/alert/100001",
            FixtureLoader.LoadBody("Alerts", "DELETE-alert"));

        var result = (await _harness.Client.Alerts.DeleteAlertAsync("DU9999999", "100001", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(3);
        result.OrderId.ShouldBe(100001);
        result.Success.ShouldBeTrue();
        result.Text.ShouldBe("Alert has been deleted.");
        result.FailureList.ShouldBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAlerts_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alerts")
                .UsingGet())
            .InScenario("get-alerts-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alerts")
                .UsingGet())
            .InScenario("get-alerts-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "GET-alerts-list")));

        var result = (await _harness.Client.Alerts.GetAlertsAsync("DU9999999", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetMtaAlert_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/mta")
                .UsingGet())
            .InScenario("get-mta-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/mta")
                .UsingGet())
            .InScenario("get-mta-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "GET-mta-alert")));

        var result = (await _harness.Client.Alerts.GetMtaAlertAsync(TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAlertDetail_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/alert/100001")
                .UsingGet())
            .InScenario("get-detail-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/alert/100001")
                .UsingGet())
            .InScenario("get-detail-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "GET-alert-detail")));

        var result = (await _harness.Client.Alerts.GetAlertDetailAsync("100001", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.AlertName.ShouldBe("SPY Price Alert");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task CreateAlert_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert")
                .UsingPost())
            .InScenario("create-alert-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert")
                .UsingPost())
            .InScenario("create-alert-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "POST-create-alert")));

        var request = new CreateAlertRequest(
            OrderId: 0,
            AlertName: "Test",
            AlertMessage: "Test message",
            AlertRepeatable: 0,
            OutsideRth: 0,
            Tif: "GTC",
            Conditions: new List<AlertCondition>());

        var result = (await _harness.Client.Alerts.CreateOrModifyAlertAsync("DU9999999", request, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task ActivateAlert_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert/activate")
                .UsingPost())
            .InScenario("activate-alert-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert/activate")
                .UsingPost())
            .InScenario("activate-alert-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "POST-activate-alert")));

        var request = new AlertActivationRequest(AlertId: 100001, AlertActive: 1);
        var result = (await _harness.Client.Alerts.ActivateAlertAsync("DU9999999", request, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task DeleteAlert_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert/100001")
                .UsingDelete())
            .InScenario("delete-alert-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU9999999/alert/100001")
                .UsingDelete())
            .InScenario("delete-alert-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Alerts", "DELETE-alert")));

        var result = (await _harness.Client.Alerts.DeleteAlertAsync("DU9999999", "100001", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();

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
