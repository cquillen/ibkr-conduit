using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Accounts;

public class AccountOperationsTests
{
    private readonly IIbkrAccountApi _api = Substitute.For<IIbkrAccountApi>();
    private readonly AccountOperations _sut;

    public AccountOperationsTests() => _sut = new AccountOperations(_api, new IbkrClientOptions(), NullLogger<AccountOperations>.Instance, new ResultFactory(NullLogger<ResultFactory>.Instance));

    [Fact]
    public async Task GetAccountsAsync_DelegatesToApi()
    {
        var expected = new IserverAccountsResponse(["U123"], "U123");
        _api.GetAccountsAsync(Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetAccountsAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetAccountsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SwitchAccountAsync_DelegatesToApi()
    {
        var expected = new SwitchAccountResponse("Account already set");
        _api.SwitchAccountAsync(Arg.Any<SwitchAccountRequest>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.SwitchAccountAsync("U456", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).SwitchAccountAsync(
            Arg.Is<SwitchAccountRequest>(r => r.AcctId == "U456"),
            TestContext.Current.CancellationToken);
    }

}
