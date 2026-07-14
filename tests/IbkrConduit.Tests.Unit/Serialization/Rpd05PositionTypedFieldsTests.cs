using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IbkrConduit.Portfolio;
using IbkrConduit.Serialization;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Pins RPD-05: nine previously-unmodeled <see cref="Position"/> fields — <c>baseMktValue</c>,
/// <c>baseMktPrice</c>, <c>baseAvgCost</c>, <c>baseRealizedPnl</c>, <c>baseUnrealizedPnl</c>,
/// <c>lastTradingDay</c>, <c>expiry</c>, <c>putOrCall</c>, <c>strike</c> — promoted from
/// <see cref="Position.AdditionalData"/> to typed nullable properties per the ADR-0001
/// nullable-as-presence rule. <c>strike</c> additionally guards a live, reproducible wire-type
/// instability found by the RPD-05 probe (<c>recordings/rpd05-strike-type/</c>): the same field
/// serializes as a JSON number on one read and a JSON string on another, so it must deserialize
/// identically either way. These deserialize through the library's actual Refit content
/// serializer so they exercise the same empty-tolerant converters every registered client uses.
/// </summary>
public class Rpd05PositionTypedFieldsTests
{
    private static Task<T?> DeserializeAsync<T>(string json)
    {
        var serializer = IbkrRefitSettings.Create().ContentSerializer;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return serializer.FromHttpContentAsync<T>(content, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Position_TypedFieldsPresent_DeserializeToTypedValues()
    {
        var json = """
            {"acctId":"U1","conid":1,"contractDesc":"AAPL 20261218 150 C","position":1.0,
             "mktPrice":10.0,"mktValue":10.0,"avgCost":10.0,"avgPrice":10.0,"realizedPnl":0.0,
             "unrealizedPnl":0.0,"currency":"USD","name":"APPLE INC","assetClass":"OPT",
             "ticker":"AAPL","isUS":true,
             "baseMktValue":1000.5,"baseMktPrice":100.05,"baseAvgCost":99.5,
             "baseRealizedPnl":1.25,"baseUnrealizedPnl":-2.5,
             "lastTradingDay":"261218","expiry":"20261218","putOrCall":"C","strike":150.0}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        pos!.BaseMarketValue.ShouldBe(1000.5m);
        pos.BaseMarketPrice.ShouldBe(100.05m);
        pos.BaseAverageCost.ShouldBe(99.5m);
        pos.BaseRealizedPnl.ShouldBe(1.25m);
        pos.BaseUnrealizedPnl.ShouldBe(-2.5m);
        pos.LastTradingDay.ShouldBe("261218");
        pos.Expiry.ShouldBe("20261218");
        pos.PutOrCall.ShouldBe("C");
        pos.Strike.ShouldBe(150.0m);
    }

    [Fact]
    public async Task Position_TypedFieldsOmitted_DeserializeToNull()
    {
        // A plain STK row — none of the nine option/base-currency fields present on the wire.
        var json = """
            {"acctId":"U1","conid":2,"contractDesc":"QQQ","position":3.0,"mktPrice":10.0,
             "avgCost":10.0,"avgPrice":10.0,"realizedPnl":0.0,"currency":"USD","name":"INVESCO QQQ",
             "assetClass":"STK","ticker":"QQQ","isUS":true}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        pos!.BaseMarketValue.ShouldBeNull();
        pos.BaseMarketPrice.ShouldBeNull();
        pos.BaseAverageCost.ShouldBeNull();
        pos.BaseRealizedPnl.ShouldBeNull();
        pos.BaseUnrealizedPnl.ShouldBeNull();
        pos.LastTradingDay.ShouldBeNull();
        pos.Expiry.ShouldBeNull();
        pos.PutOrCall.ShouldBeNull();
        pos.Strike.ShouldBeNull();
    }

    [Fact]
    public async Task Position_Strike_NumberAndStringShapes_DeserializeToSameValue()
    {
        // Per the RPD-05 live probe: the same STK position row's strike serialized as a JSON
        // number (0.0) on a sparse first read and a JSON string ("0") on an enriched second read
        // of the same session. Both shapes must deserialize to the identical decimal value.
        var numberShapeJson = """
            {"acctId":"U1","conid":3,"contractDesc":"SPY","position":1.0,"currency":"USD",
             "name":"SPY","assetClass":"STK","ticker":"SPY","strike":0.0}
            """;
        var stringShapeJson = """
            {"acctId":"U1","conid":3,"contractDesc":"SPY","position":1.0,"currency":"USD",
             "name":"SPY","assetClass":"STK","ticker":"SPY","strike":"0"}
            """;

        var numberShaped = await DeserializeAsync<Position>(numberShapeJson);
        var stringShaped = await DeserializeAsync<Position>(stringShapeJson);

        numberShaped.ShouldNotBeNull();
        stringShaped.ShouldNotBeNull();
        numberShaped!.Strike.ShouldBe(0m);
        stringShaped!.Strike.ShouldBe(0m);
        numberShaped.Strike.ShouldBe(stringShaped.Strike);
    }

    [Fact]
    public async Task Position_TypedFields_AreNotCapturedInAdditionalData()
    {
        // Promoting these fields to typed properties must remove them from the extension-data
        // catch-all — otherwise they'd be double-modeled (typed property + raw JsonElement).
        var json = """
            {"acctId":"U1","conid":1,"contractDesc":"AAPL 20261218 150 C","position":1.0,
             "currency":"USD","name":"APPLE INC","assetClass":"OPT","ticker":"AAPL",
             "baseMktValue":1000.5,"lastTradingDay":"261218","expiry":"20261218",
             "putOrCall":"C","strike":150.0}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        // No field in the JSON above is unmodeled, so the extension-data catch-all stays empty —
        // if any of the nine fields were still falling through to it, this would be non-null.
        pos!.AdditionalData.ShouldBeNull();
    }
}
