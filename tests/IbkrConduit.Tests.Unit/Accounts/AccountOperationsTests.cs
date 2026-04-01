using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Client;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Accounts;

public class AccountOperationsTests
{
    private readonly IIbkrAccountApi _api = Substitute.For<IIbkrAccountApi>();
    private readonly AccountOperations _sut;

    public AccountOperationsTests() => _sut = new AccountOperations(_api);

    [Fact]
    public async Task GetAccountsAsync_DelegatesToApi()
    {
        var expected = new IserverAccountsResponse(["U123"], "U123");
        _api.GetAccountsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetAccountsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SwitchAccountAsync_DelegatesToApi()
    {
        var expected = new SwitchAccountResponse(true, "U456");
        _api.SwitchAccountAsync(Arg.Any<SwitchAccountRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.SwitchAccountAsync("U456", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).SwitchAccountAsync(
            Arg.Is<SwitchAccountRequest>(r => r.AcctId == "U456"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetDynAccountAsync_DelegatesToApi()
    {
        var expected = new DynAccountResponse(true, "U789");
        _api.SetDynAccountAsync(Arg.Any<DynAccountRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.SetDynAccountAsync("U789", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).SetDynAccountAsync(
            Arg.Is<DynAccountRequest>(r => r.AcctId == "U789"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchAccountsAsync_DelegatesToApi()
    {
        var expected = new List<AccountSearchResult>
        {
            new("U123", "Test Account", "INDIVIDUAL"),
        };
        _api.SearchAccountsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.SearchAccountsAsync("U123", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).SearchAccountsAsync("U123", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAccountInfoAsync_DelegatesToApi()
    {
        var expected = new IserverAccountInfo("U123", "Test Account", "INDIVIDUAL");
        _api.GetAccountInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAccountInfoAsync("U123", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetAccountInfoAsync("U123", TestContext.Current.CancellationToken);
    }
}
