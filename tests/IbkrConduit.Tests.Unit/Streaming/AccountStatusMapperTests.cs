using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// Pins <see cref="AccountStatusMapper"/>'s presence semantics for the interlock-carrying
/// <c>isPaper</c>/<c>isFT</c> flags (GAP2-3): a real IBKR verdict (JSON or string-encoded)
/// maps to true/false, while an absent flag maps to <c>null</c> — never a fabricated
/// <c>false</c> that a consumer would read as "IBKR said live account".
/// </summary>
public class AccountStatusMapperTests
{
    private static AccountStatusEvent Map(string json) =>
        AccountStatusMapper.Map(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Map_IsPaperBooleanTrue_MapsToTrue() =>
        Map("""{"topic":"act","args":{"isPaper":true}}""").IsPaper.ShouldBe(true);

    [Fact]
    public void Map_IsPaperBooleanFalse_MapsToFalse() =>
        Map("""{"topic":"act","args":{"isPaper":false}}""").IsPaper.ShouldBe(false);

    [Fact]
    public void Map_IsPaperStringEncodedTrue_MapsToTrue() =>
        Map("""{"topic":"act","args":{"isPaper":"true"}}""").IsPaper.ShouldBe(true);

    [Fact]
    public void Map_IsPaperNumericOne_MapsToTrue() =>
        Map("""{"topic":"act","args":{"isPaper":1}}""").IsPaper.ShouldBe(true);

    [Fact]
    public void Map_IsPaperUnrecognizedString_IsNull() =>
        Map("""{"topic":"act","args":{"isPaper":"maybe"}}""").IsPaper.ShouldBeNull();

    [Fact]
    public void Map_IsPaperAbsent_IsNull() =>
        Map("""{"topic":"act","args":{}}""").IsPaper.ShouldBeNull();

    [Fact]
    public void Map_MissingArgs_IsPaperAndIsFtAreNull()
    {
        var evt = Map("""{"topic":"act"}""");

        evt.IsPaper.ShouldBeNull();
        evt.IsFT.ShouldBeNull();
    }

    [Fact]
    public void Map_IsFtStringEncodedTrue_MapsToTrue() =>
        Map("""{"topic":"act","args":{"isFT":"1"}}""").IsFT.ShouldBe(true);

    [Fact]
    public void Map_IsFtAbsent_IsNull() =>
        Map("""{"topic":"act","args":{"isPaper":true}}""").IsFT.ShouldBeNull();
}
