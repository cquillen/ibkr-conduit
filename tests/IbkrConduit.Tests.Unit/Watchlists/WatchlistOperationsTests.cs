using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Watchlists;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Watchlists;

public class WatchlistOperationsTests
{
    private readonly IIbkrWatchlistApi _api = Substitute.For<IIbkrWatchlistApi>();
    private readonly WatchlistOperations _sut;

    public WatchlistOperationsTests() => _sut = new WatchlistOperations(_api);

    [Fact]
    public async Task CreateWatchlistAsync_DelegatesToApi()
    {
        var request = new CreateWatchlistRequest("MyList", new List<WatchlistRow>());
        var expected = new CreateWatchlistResponse("MyList");
        _api.CreateWatchlistAsync(Arg.Any<CreateWatchlistRequest>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).CreateWatchlistAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetWatchlistsAsync_DelegatesToApi()
    {
        var expected = new List<WatchlistSummary> { new("wl1", "My Watchlist", 1712345678L, 3) };
        _api.GetWatchlistsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetWatchlistsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetWatchlistsAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetWatchlistAsync_DelegatesToApi()
    {
        var expected = new WatchlistDetail("wl1", "My Watchlist", new List<WatchlistDetailRow>());
        _api.GetWatchlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteWatchlistAsync_DelegatesToApi()
    {
        var expected = new DeleteWatchlistResponse(true, "wl1");
        _api.DeleteWatchlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
        await _api.Received(1).DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);
    }
}
