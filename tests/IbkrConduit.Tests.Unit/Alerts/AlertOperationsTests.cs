using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Alerts;
using IbkrConduit.Client;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Alerts;

public class AlertOperationsTests
{
    private readonly IIbkrAlertApi _api = Substitute.For<IIbkrAlertApi>();
    private readonly AlertOperations _sut;

    public AlertOperationsTests() => _sut = new AlertOperations(_api, new IbkrClientOptions());

    [Fact]
    public async Task CreateOrModifyAlertAsync_DelegatesToApi()
    {
        var request = new CreateAlertRequest(0, "TestAlert", "Alert message", 0, 0, "GTC", []);
        var expected = new CreateAlertResponse(1, 42, true, "Created");
        _api.CreateOrModifyAlertAsync(Arg.Any<string>(), Arg.Any<CreateAlertRequest>(), Arg.Any<CancellationToken>())
            .Returns(FakeApiResponse.Success(expected));

        var result = await _sut.CreateOrModifyAlertAsync("U123", request, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).CreateOrModifyAlertAsync("U123", request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAlertsAsync_DelegatesToApi()
    {
        var expected = new List<AlertSummary>
        {
            new("U123", 42, "TestAlert", 1, 0),
        };
        _api.GetAlertsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetAlertsAsync("U123", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetAlertsAsync("U123", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetMtaAlertAsync_DelegatesToApi()
    {
        var expected = new List<AlertSummary>
        {
            new("U123", 42, "TestAlert", 1, 0),
        };
        _api.GetMtaAlertAsync(Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetMtaAlertAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetMtaAlertAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAlertDetailAsync_DelegatesToApi()
    {
        var expected = new AlertDetail("U123", 42, "TestAlert", "Alert message", 1, 0, []);
        _api.GetAlertDetailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetAlertDetailAsync("42", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetAlertDetailAsync("42", Arg.Any<string>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ActivateAlertAsync_DelegatesToApi()
    {
        var request = new AlertActivationRequest(42, 1);
        var expected = new AlertActivationResponse(1, 42, true, "Activated");
        _api.ActivateAlertAsync(Arg.Any<string>(), Arg.Any<AlertActivationRequest>(), Arg.Any<CancellationToken>())
            .Returns(FakeApiResponse.Success(expected));

        var result = await _sut.ActivateAlertAsync("U123", request, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).ActivateAlertAsync("U123", request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAlertAsync_DelegatesToApi()
    {
        var expected = new DeleteAlertResponse(1, 42, true, "Deleted");
        _api.DeleteAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.DeleteAlertAsync("U123", "42", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).DeleteAlertAsync("U123", "42", TestContext.Current.CancellationToken);
    }
}
