using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Allocation;
using IbkrConduit.Client;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Allocation;

public class AllocationOperationsTests
{
    private readonly IIbkrAllocationApi _api = Substitute.For<IIbkrAllocationApi>();
    private readonly AllocationOperations _sut;

    public AllocationOperationsTests() => _sut = new AllocationOperations(_api);

    [Fact]
    public async Task GetAccountsAsync_DelegatesToApi()
    {
        var expected = new AllocationAccountsResponse(new List<AllocationAccountData>());
        _api.GetAccountsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetAccountsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetGroupsAsync_DelegatesToApi()
    {
        var expected = new AllocationGroupListResponse(new List<AllocationGroupSummary>());
        _api.GetGroupsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetGroupsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetGroupsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddGroupAsync_DelegatesToApi()
    {
        var request = new AllocationGroupRequest("TestGroup", new List<AllocationGroupAccount>(), "NetLiq");
        var expected = new AllocationSuccessResponse(true);
        _api.AddGroupAsync(Arg.Any<AllocationGroupRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.AddGroupAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).AddGroupAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetGroupAsync_WrapsNameInRequest()
    {
        var expected = new AllocationGroupDetail("TestGroup", new List<AllocationGroupAccount>(), "NetLiq");
        _api.GetGroupAsync(Arg.Any<AllocationGroupNameRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetGroupAsync("TestGroup", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetGroupAsync(
            Arg.Is<AllocationGroupNameRequest>(r => r.Name == "TestGroup"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteGroupAsync_WrapsNameInRequest()
    {
        var expected = new AllocationSuccessResponse(true);
        _api.DeleteGroupAsync(Arg.Any<AllocationGroupNameRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.DeleteGroupAsync("TestGroup", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).DeleteGroupAsync(
            Arg.Is<AllocationGroupNameRequest>(r => r.Name == "TestGroup"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ModifyGroupAsync_DelegatesToApi()
    {
        var request = new AllocationGroupRequest("TestGroup", new List<AllocationGroupAccount>(), "NetLiq");
        var expected = new AllocationSuccessResponse(true);
        _api.ModifyGroupAsync(Arg.Any<AllocationGroupRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.ModifyGroupAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).ModifyGroupAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPresetsAsync_DelegatesToApi()
    {
        var expected = new AllocationPresetsResponse(false, "NetLiq", false, false, false);
        _api.GetPresetsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetPresetsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetPresetsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetPresetsAsync_DelegatesToApi()
    {
        var request = new AllocationPresetsRequest("NetLiq", false, false, false, false);
        var expected = new AllocationSuccessResponse(true);
        _api.SetPresetsAsync(Arg.Any<AllocationPresetsRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.SetPresetsAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).SetPresetsAsync(request, TestContext.Current.CancellationToken);
    }
}
