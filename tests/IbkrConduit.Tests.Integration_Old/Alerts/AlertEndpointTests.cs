using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Alerts;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Alerts;

public class AlertEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public AlertEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task CreateOrModifyAlertAsync_ReturnsCreatedAlert()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567/alert")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"request_id":1,"order_id":12345,"success":true,"text":"Alert created","order_status":"Submitted"}"""));

        var api = CreateRefitClient<IIbkrAlertApi>();
        var request = new CreateAlertRequest(
            OrderId: 0,
            AlertName: "Price Alert",
            AlertMessage: "SPY above 500",
            AlertRepeatable: 1,
            OutsideRth: 0,
            Tif: "GTC",
            Conditions: new List<AlertCondition>
            {
                new(Type: 1, Conidex: "265598", Operator: ">=", TriggerMethod: "0", Value: "500"),
            });

        var result = await api.CreateOrModifyAlertAsync("DU1234567", request, TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(1);
        result.OrderId.ShouldBe(12345);
        result.Success.ShouldBeTrue();
        result.Text.ShouldBe("Alert created");
    }

    [Fact]
    public async Task GetMtaAlertAsync_ReturnsAlertList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/mta")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {
                                "account": "DU1234567",
                                "order_id": 12345,
                                "alert_name": "Price Alert",
                                "alert_active": 1,
                                "alert_repeatable": 0
                            }
                        ]
                        """));

        var api = CreateRefitClient<IIbkrAlertApi>();

        var result = await api.GetMtaAlertAsync(TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Account.ShouldBe("DU1234567");
        result[0].OrderId.ShouldBe(12345);
        result[0].AlertName.ShouldBe("Price Alert");
        result[0].AlertActive.ShouldBe(1);
    }

    [Fact]
    public async Task GetAlertDetailAsync_ReturnsAlertDetail()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/alert/12345")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "account": "DU1234567",
                            "order_id": 12345,
                            "alert_name": "Price Alert",
                            "alert_message": "SPY above 500",
                            "alert_active": 1,
                            "alert_repeatable": 1,
                            "condition_size": 1,
                            "condition_outside_rth": 0,
                            "conditions": [
                                {
                                    "condition_type": 1,
                                    "conidex": "265598",
                                    "contract_description_1": "SPY",
                                    "condition_operator": ">=",
                                    "condition_trigger_method": "0",
                                    "condition_value": "500",
                                    "condition_logic_bind": "a",
                                    "condition_time_zone": "US/Eastern"
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrAlertApi>();

        var result = await api.GetAlertDetailAsync("12345", cancellationToken: TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.Account.ShouldBe("DU1234567");
        result.AlertName.ShouldBe("Price Alert");
        result.Conditions.Count.ShouldBe(1);
        result.Conditions[0].ConditionValue.ShouldBe("500");
    }

    [Fact]
    public async Task DeleteAlertAsync_ReturnsDeleteResult()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/DU1234567/alert/12345")
                .UsingDelete())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"request_id":1,"order_id":12345,"success":true,"text":"Alert deleted","failure_list":null}"""));

        var api = CreateRefitClient<IIbkrAlertApi>();

        var result = await api.DeleteAlertAsync("DU1234567", "12345", TestContext.Current.CancellationToken).Content!;

        result.ShouldNotBeNull();
        result.RequestId.ShouldBe(1);
        result.OrderId.ShouldBe(12345);
        result.Success.ShouldBeTrue();
        result.Text.ShouldBe("Alert deleted");
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private TApi CreateRefitClient<TApi>() where TApi : class
    {
        var lstBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                                     0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
                                     0x11, 0x12, 0x13, 0x14 };
        var tokenProvider = new FakeTokenProvider(
            new LiveSessionToken(lstBytes, DateTimeOffset.UtcNow.AddHours(24)));

        var signingHandler = new OAuthSigningHandler(tokenProvider, "TESTKEY01", "mytoken")
        {
            InnerHandler = new HttpClientHandler(),
        };

        var httpClient = new HttpClient(signingHandler)
        {
            BaseAddress = new Uri(_server.Url!),
        };

        return Refit.RestService.For<TApi>(httpClient);
    }

    private class FakeTokenProvider : ISessionTokenProvider
    {
        private readonly LiveSessionToken _token;

        public FakeTokenProvider(LiveSessionToken token) => _token = token;

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);

        public Task<LiveSessionToken> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
    }
}
