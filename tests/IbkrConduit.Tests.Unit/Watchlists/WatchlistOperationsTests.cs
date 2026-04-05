using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using IbkrConduit.Watchlists;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Watchlists;

public class WatchlistOperationsTests
{
    private readonly IIbkrWatchlistApi _api = Substitute.For<IIbkrWatchlistApi>();
    private readonly WatchlistOperations _sut;

    public WatchlistOperationsTests() => _sut = new WatchlistOperations(_api, new IbkrClientOptions(), NullLogger<WatchlistOperations>.Instance);

    [Fact]
    public async Task CreateWatchlistAsync_DelegatesToApi()
    {
        var request = new CreateWatchlistRequest("MyList", "My List", new List<WatchlistRow>());
        var expected = new CreateWatchlistResponse("MyList", "hash1", "My List", false, new List<WatchlistInstrument>());
        _api.CreateWatchlistAsync(Arg.Any<CreateWatchlistRequest>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.CreateWatchlistAsync(request, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).CreateWatchlistAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetWatchlistsAsync_DelegatesToApi()
    {
        var expected = new GetWatchlistsResponse(
            new GetWatchlistsData(false, false, false, new List<WatchlistSummary>
            {
                new("wl1", "My Watchlist", 1712345678L, false, false, "watchlist"),
            }),
            "content",
            "1");
        _api.GetWatchlistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetWatchlistsAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetWatchlistsAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetWatchlistAsync_DelegatesToApi()
    {
        var expected = new WatchlistDetail("wl1", "hash1", "My Watchlist", false, new List<WatchlistInstrument>());
        _api.GetWatchlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetWatchlistAsync("wl1", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteWatchlistAsync_DelegatesToApi()
    {
        var expected = new DeleteWatchlistResponse(
            new DeleteWatchlistData("wl1"),
            "context",
            "2");
        _api.DeleteWatchlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).DeleteWatchlistAsync("wl1", TestContext.Current.CancellationToken);
    }
}
