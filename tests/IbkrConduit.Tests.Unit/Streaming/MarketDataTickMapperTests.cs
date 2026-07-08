using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class MarketDataTickMapperTests
{
    [Fact]
    public void Map_ConidFromTopic_ExtractsConid()
    {
        var frame = JsonDocument.Parse("""{"topic":"smd+265598","31":"150.25"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Conid.ShouldBe(265598);
    }

    [Fact]
    public void Map_NumericConidAndUpdated_StillParse()
    {
        // The common shape: conid + _updated arrive as JSON numbers. They must still parse after the
        // WIR-5 ValueKind-tolerance guards are added.
        var frame = JsonDocument.Parse("""{"topic":"smd","conid":265598,"_updated":1751466605000}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Conid.ShouldBe(265598);
        tick.Updated.ShouldBe(1751466605000L);
    }

    [Fact]
    public void Map_StringConid_ParsesInsteadOfThrowing()
    {
        // WIR-5: IBKR type-drifts numeric-ish fields to quoted strings. A string conid must parse,
        // not throw a JsonException (GetInt32 on a string) that would drop the whole smd frame.
        var frame = JsonDocument.Parse("""{"topic":"smd","conid":"265598"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Conid.ShouldBe(265598);
    }

    [Fact]
    public void Map_StringUpdated_ParsesInsteadOfThrowing()
    {
        // WIR-5: _updated as a quoted string must parse, not throw (GetInt64 on a string).
        var frame = JsonDocument.Parse("""{"topic":"smd+265598","_updated":"1751466605000"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Updated.ShouldBe(1751466605000L);
    }

    [Fact]
    public void Map_NonNumericUpdated_DoesNotThrowAndLeavesUpdatedNull()
    {
        // A non-numeric _updated (unexpected ValueKind) must be tolerated rather than throwing.
        var frame = JsonDocument.Parse("""{"topic":"smd+265598","_updated":"not-a-timestamp"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Updated.ShouldBeNull();
        tick.Conid.ShouldBe(265598);
    }

    [Fact]
    public void Map_NumericFieldKeys_LandInFields()
    {
        var frame = JsonDocument.Parse("""{"topic":"smd+265598","6119":"a","6509":"b"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.Fields.ShouldNotBeNull();
        tick.Fields!.ShouldContainKey("6119");
        tick.Fields.ShouldContainKey("6509");
    }

    [Fact]
    public void Map_NonNumericUnmappedKeys_LandInAdditionalData()
    {
        // WIR-5: the captured smd frame carries non-numeric unmapped keys (conidEx, server_id).
        // They must survive in AdditionalData rather than being silently discarded.
        var frame = JsonDocument.Parse(
            """{"topic":"smd+265598","conidEx":"265598@SMART","server_id":"q0","6119":"a"}""").RootElement;

        var tick = MarketDataTickMapper.Map(frame);

        tick.AdditionalData.ShouldNotBeNull();
        tick.AdditionalData!.ShouldContainKey("conidEx");
        tick.AdditionalData.ShouldContainKey("server_id");
        tick.AdditionalData.ShouldNotContainKey("6119"); // numeric keys go to Fields, not AdditionalData
    }
}
