using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Fyi;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Fyi;

public class FyiEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public FyiEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/unreadnumber")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"BN":4}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetUnreadCountAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.BN.ShouldBe(4);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [
                            {"FC":"M8","H":0,"A":1,"FD":"871(m) trades notification.","FN":"871(m) Trades"},
                            {"FC":"SM","H":0,"A":1,"FD":"System messages.","FN":"System Messages"}
                        ]
                        """));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetSettingsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].FC.ShouldBe("M8");
        result[1].FC.ShouldBe("SM");
    }

    [Fact]
    public async Task UpdateSettingAsync_ReturnsAcknowledgement()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/settings/SM")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"V":1,"T":10}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.UpdateSettingAsync("SM", new FyiSettingUpdateRequest(true),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.V.ShouldBe(1);
    }

    [Fact]
    public async Task GetDisclaimerAsync_ReturnsDisclaimer()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/SM")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"FC":"SM","DT":"This is a disclaimer."}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetDisclaimerAsync("SM", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.FC.ShouldBe("SM");
        result.DT.ShouldBe("This is a disclaimer.");
    }

    [Fact]
    public async Task MarkDisclaimerReadAsync_ReturnsAcknowledgement()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/disclaimer/CT")
                .UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"V":1,"T":10}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.MarkDisclaimerReadAsync("CT", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.V.ShouldBe(1);
    }

    [Fact]
    public async Task GetDeliveryOptionsAsync_ReturnsOptions()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "E": [{"NM":"iPhone","I":"apn://device1","UI":"apn://device1","A":1}],
                            "M": 1
                        }
                        """));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetDeliveryOptionsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.M.ShouldBe(1);
        result.E.Count.ShouldBe(1);
        result.E[0].NM.ShouldBe("iPhone");
    }

    [Fact]
    public async Task SetEmailDeliveryAsync_ReturnsAcknowledgement()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/email")
                .UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"V":1,"T":10}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.SetEmailDeliveryAsync("true", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.V.ShouldBe(1);
    }

    [Fact]
    public async Task RegisterDeviceAsync_ReturnsAcknowledgement()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/device")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"V":1,"T":10}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.RegisterDeviceAsync(
            new FyiDeviceRequest("iPhone", "apn://device1", "apn://device1", true),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.V.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteDeviceAsync_SucceedsWithoutException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/deliveryoptions/1")
                .UsingDelete())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200));

        var api = CreateRefitClient<IIbkrFyiApi>();

        await api.DeleteDeviceAsync("1", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNotifications()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        [{
                            "R": 0,
                            "D": "1702469440.0",
                            "MS": "Option Expiration",
                            "MD": "Options expiring soon.",
                            "ID": "2023121370119463",
                            "HT": 0,
                            "FC": "OE"
                        }]
                        """));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetNotificationsAsync("10", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ID.ShouldBe("2023121370119463");
        result[0].FC.ShouldBe("OE");
    }

    [Fact]
    public async Task GetMoreNotificationsAsync_ReturnsNotifications()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/more")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[]"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.GetMoreNotificationsAsync("12345678901234567",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MarkNotificationReadAsync_ReturnsReadResponse()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/fyi/notifications/12345678901234567")
                .UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"V":1,"T":5,"P":{"R":1,"ID":"12345678901234567"}}"""));

        var api = CreateRefitClient<IIbkrFyiApi>();

        var result = await api.MarkNotificationReadAsync("12345678901234567",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.V.ShouldBe(1);
        result.P.ShouldNotBeNull();
        result.P!.R.ShouldBe(1);
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
