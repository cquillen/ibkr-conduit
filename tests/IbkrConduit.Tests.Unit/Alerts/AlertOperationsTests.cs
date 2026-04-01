using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Alerts;

public class AlertOperationsTests
{
    private readonly IIbkrAlertApi _api = Substitute.For<IIbkrAlertApi>();
    private readonly AlertOperations _sut;

    public AlertOperationsTests() => _sut = new AlertOperations(_api);

    [Fact]
    public async Task CreateOrModifyAlertAsync_DelegatesToApi()
    {
        var request = new CreateAlertRequest(0, "TestAlert", "Alert message", 0, 0, []);
        var expected = new CreateAlertResponse(1, 42, "PreSubmitted", null);
        _api.CreateOrModifyAlertAsync(Arg.Any<string>(), Arg.Any<CreateAlertRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.CreateOrModifyAlertAsync("U123", request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).CreateOrModifyAlertAsync("U123", request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAlertsAsync_DelegatesToApi()
    {
        var expected = new List<AlertSummary>
        {
            new("U123", 42, "TestAlert", 1, "PreSubmitted"),
        };
        _api.GetAlertsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAlertsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetAlertsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAlertDetailAsync_DelegatesToApi()
    {
        var expected = new AlertDetail("U123", 42, "TestAlert", "Alert message", 1, 0, []);
        _api.GetAlertDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAlertDetailAsync("42", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetAlertDetailAsync("42", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAlertAsync_DelegatesToApi()
    {
        var expected = new DeleteAlertResponse(1, 42, "Cancelled", null);
        _api.DeleteAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.DeleteAlertAsync("U123", "42", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).DeleteAlertAsync("U123", "42", TestContext.Current.CancellationToken);
    }
}
