using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class PnlUpdateMapperTests
{
    // Real spl frame captured live: args is an object keyed by "{account}.Core"; the
    // account lives in the key, not in the value.
    private const string _realPnlFrame = """
        {"topic":"spl","args":{"DUO873728.Core":{"rowType":1,"dpl":-58.28,"nl":1020000.0,"upl":4600.0,"el":1010000.0,"uel":1005353.88,"mv":37910.0}}}
        """;

    [Fact]
    public void MapMany_RealPnlFrame_YieldsOneUpdateWithAccountParsedFromKey()
    {
        var frame = JsonDocument.Parse(_realPnlFrame).RootElement;

        var updates = PnlUpdateMapper.MapMany(frame).ToList();

        updates.Count.ShouldBe(1);
        var pnl = updates[0];
        pnl.AccountId.ShouldBe("DUO873728");
        pnl.DailyPnl.ShouldBe(-58.28m);
        pnl.NetLiquidation.ShouldBe(1020000.0m);
        pnl.UnrealizedPnl.ShouldBe(4600.0m);
        pnl.RealizedPnl.ShouldBe(0m); // rpl absent from this frame -> default
    }

    [Fact]
    public void MapMany_RealPnlFrame_UnmappedFieldsLandInAdditionalData()
    {
        var frame = JsonDocument.Parse(_realPnlFrame).RootElement;

        var pnl = PnlUpdateMapper.MapMany(frame).Single();

        pnl.AdditionalData.ShouldNotBeNull();
        pnl.AdditionalData!.ShouldContainKey("el");
        pnl.AdditionalData.ShouldContainKey("uel");
        pnl.AdditionalData.ShouldContainKey("mv");
    }

    [Fact]
    public void MapMany_MissingArgs_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"spl"}""").RootElement;

        PnlUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_ArgsNotAnObject_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"spl","args":[1,2]}""").RootElement;

        PnlUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_KeyWithoutDot_UsesWholeKeyAsAccountId()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"spl","args":{"DU123":{"dpl":1.0}}}""").RootElement;

        var pnl = PnlUpdateMapper.MapMany(frame).Single();

        pnl.AccountId.ShouldBe("DU123");
    }
}
