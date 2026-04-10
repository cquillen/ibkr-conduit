using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Fyi;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Fyi;

public class FyiOperationsTests
{
    private readonly IIbkrFyiApi _api = Substitute.For<IIbkrFyiApi>();
    private readonly FyiOperations _sut;

    public FyiOperationsTests() => _sut = new FyiOperations(_api, new IbkrClientOptions(), NullLogger<FyiOperations>.Instance);

    [Fact]
    public async Task GetUnreadCountAsync_DelegatesToApi()
    {
        var expected = new UnreadBulletinCountResponse(5);
        _api.GetUnreadCountAsync(Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetUnreadCountAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetUnreadCountAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetSettingsAsync_DelegatesToApi()
    {
        var expected = new List<FyiSettingItem>
        {
            new("OR", "Order Fill", "Notifies on fills", 1, 1),
        };
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetSettingsAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetSettingsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateSettingAsync_DelegatesToApi()
    {
        var expected = new FyiAcknowledgementResponse(1, 42);
        _api.UpdateSettingAsync(Arg.Any<string>(), Arg.Any<FyiSettingUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(FakeApiResponse.Success(expected));

        var result = await _sut.UpdateSettingAsync("OR", true, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).UpdateSettingAsync(
            "OR",
            Arg.Is<FyiSettingUpdateRequest>(r => r.Enabled == true),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetDisclaimerAsync_DelegatesToApi()
    {
        var expected = new FyiDisclaimerResponse("OR", "Disclaimer text");
        _api.GetDisclaimerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetDisclaimerAsync("OR", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetDisclaimerAsync("OR", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MarkDisclaimerReadAsync_DelegatesToApi()
    {
        var expected = new FyiAcknowledgementResponse(1, 10);
        _api.MarkDisclaimerReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.MarkDisclaimerReadAsync("OR", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).MarkDisclaimerReadAsync("OR", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetDeliveryOptionsAsync_DelegatesToApi()
    {
        var expected = new FyiDeliveryOptionsResponse(1, []);
        _api.GetDeliveryOptionsAsync(Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetDeliveryOptionsAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetDeliveryOptionsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetEmailDeliveryAsync_True_PassesTrueAsLowercaseString()
    {
        var expected = new FyiAcknowledgementResponse(1, 5);
        _api.SetEmailDeliveryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.SetEmailDeliveryAsync(true, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).SetEmailDeliveryAsync("true", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetEmailDeliveryAsync_False_PassesFalseAsLowercaseString()
    {
        var expected = new FyiAcknowledgementResponse(1, 5);
        _api.SetEmailDeliveryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.SetEmailDeliveryAsync(false, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).SetEmailDeliveryAsync("false", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RegisterDeviceAsync_DelegatesToApi()
    {
        var request = new FyiDeviceRequest("My Phone", "device-001", "ios", true);
        var expected = new FyiAcknowledgementResponse(1, 15);
        _api.RegisterDeviceAsync(Arg.Any<FyiDeviceRequest>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.RegisterDeviceAsync(request, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).RegisterDeviceAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteDeviceAsync_DelegatesToApi()
    {
        _api.DeleteDeviceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(""));

        await _sut.DeleteDeviceAsync("device-001", TestContext.Current.CancellationToken);

        await _api.Received(1).DeleteDeviceAsync("device-001", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetNotificationsAsync_DelegatesToApi()
    {
        var expected = new List<FyiNotification>
        {
            new(0, "1700000000", "Title", "Content", "notif-1", 0, "OR"),
        };
        _api.GetNotificationsAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetNotificationsAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetNotificationsAsync(10, null, null, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetMoreNotificationsAsync_DelegatesToApi()
    {
        var expected = new List<FyiNotification>
        {
            new(0, "1700000000", "Title", "Content", "notif-2", 0, "OR"),
        };
        _api.GetMoreNotificationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetMoreNotificationsAsync("notif-1", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetMoreNotificationsAsync("notif-1", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MarkNotificationReadAsync_DelegatesToApi()
    {
        var expected = new FyiNotificationReadResponse(1, 8, new FyiNotificationReadDetail(1, "notif-1"));
        _api.MarkNotificationReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.MarkNotificationReadAsync("notif-1", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).MarkNotificationReadAsync("notif-1", TestContext.Current.CancellationToken);
    }
}
