using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Allocation;
using IbkrConduit.Auth;
using IbkrConduit.Http;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Allocation;

public class AllocationEndpointTests : IDisposable
{
    private readonly WireMockServer _server;

    public AllocationEndpointTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsSubAccounts()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/accounts")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "accounts": [
                                {
                                    "data": [
                                        {"value": "2677.89", "key": "NetLiquidation"},
                                        {"value": "2134.76", "key": "AvailableEquity"}
                                    ],
                                    "name": "U123456"
                                }
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Accounts.Count.ShouldBe(1);
        result.Accounts[0].Name.ShouldBe("U123456");
        result.Accounts[0].Data[0].Key.ShouldBe("NetLiquidation");
    }

    [Fact]
    public async Task GetGroupsAsync_ReturnsGroupList()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/group")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "data": [
                                {"allocation_method": "N", "size": 10, "name": "Group_1_NetLiq"}
                            ]
                        }
                        """));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.GetGroupsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe("Group_1_NetLiq");
        result.Data[0].AllocationMethod.ShouldBe("N");
        result.Data[0].Size.ShouldBe(10);
    }

    [Fact]
    public async Task AddGroupAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/group")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":true}"""));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.AddGroupAsync(
            new AllocationGroupRequest(
                "Group_1_NetLiq",
                [new AllocationGroupAccount("U123", 10)],
                "N"),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetGroupAsync_ReturnsSingleGroup()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/group/single")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "name": "Group_1_NetLiq",
                            "accounts": [
                                {"amount": 1, "name": "DU1234567"},
                                {"amount": 5, "name": "DU9876543"}
                            ],
                            "default_method": "R"
                        }
                        """));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.GetGroupAsync(
            new AllocationGroupNameRequest("Group_1_NetLiq"),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Group_1_NetLiq");
        result.Accounts.Count.ShouldBe(2);
        result.DefaultMethod.ShouldBe("R");
    }

    [Fact]
    public async Task DeleteGroupAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/group/delete")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":true}"""));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.DeleteGroupAsync(
            new AllocationGroupNameRequest("Group_1_NetLiq"),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ModifyGroupAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/group")
                .UsingPut())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":true}"""));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.ModifyGroupAsync(
            new AllocationGroupRequest(
                "Group_1_NetLiq",
                [new AllocationGroupAccount("U123", 15)],
                "N"),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPresetsAsync_ReturnsPresets()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/presets")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""
                        {
                            "group_auto_close_positions": false,
                            "default_method_for_all": "N",
                            "profiles_auto_close_positions": false,
                            "strict_credit_check": false,
                            "group_proportional_allocation": false
                        }
                        """));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.GetPresetsAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.DefaultMethodForAll.ShouldBe("N");
        result.GroupAutoClosePositions.ShouldBeFalse();
        result.GroupProportionalAllocation.ShouldBeFalse();
    }

    [Fact]
    public async Task SetPresetsAsync_ReturnsSuccess()
    {
        _server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/account/allocation/presets")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"success":true}"""));

        var api = CreateRefitClient<IIbkrAllocationApi>();

        var result = await api.SetPresetsAsync(
            new AllocationPresetsRequest("E", true, true, false, false),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
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
