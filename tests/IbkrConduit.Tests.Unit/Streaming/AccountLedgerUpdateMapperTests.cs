using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class AccountLedgerUpdateMapperTests
{
    // Real sld frame captured live: data is a "result" array at the root; the account is
    // in "topic" ("sld+DUO873728"), not in the payload.
    private const string _realLedgerFrame = """
        {"result":[{"acctCode":"DUO873728","cashbalance":976920.88,"cashBalanceFXSegment":0.0,"key":"LedgerListBASE","dividends":0.0,"exchangeRate":1.0,"netLiquidationValue":1017353.16,"realizedPnl":0.0,"unrealizedPnl":4598.91,"secondKey":"BASE","settledCash":976920.88,"severity":0,"stockMarketValue":37910.67,"interest":2521.61,"timestamp":1783031080}],"topic":"sld+DUO873728"}
        """;

    [Fact]
    public void Map_RealLedgerFrame_ReturnsAccountIdFromTopicAndOneRow()
    {
        var frame = JsonDocument.Parse(_realLedgerFrame).RootElement;

        var update = AccountLedgerUpdateMapper.Map(frame);

        update.AccountId.ShouldBe("DUO873728");
        update.Result.Count.ShouldBe(1);
        var row = update.Result[0];
        row.Key.ShouldBe("LedgerListBASE");
        row.AccountCode.ShouldBe("DUO873728");
        row.SecondKey.ShouldBe("BASE");
        row.CashBalance.ShouldBe(976920.88m);
        row.SettledCash.ShouldBe(976920.88m);
        row.NetLiquidationValue.ShouldBe(1017353.16m);
        row.StockMarketValue.ShouldBe(37910.67m);
        row.RealizedPnl.ShouldBe(0.0m);
        row.UnrealizedPnl.ShouldBe(4598.91m);
        row.ExchangeRate.ShouldBe(1.0m);
        row.Dividends.ShouldBe(0.0m);
        row.Interest.ShouldBe(2521.61m);
        row.Timestamp.ShouldBe(1783031080L);
    }

    [Fact]
    public void Map_RealLedgerFrame_UnmappedFieldLandsInAdditionalData()
    {
        var frame = JsonDocument.Parse(_realLedgerFrame).RootElement;

        var update = AccountLedgerUpdateMapper.Map(frame);

        var row = update.Result[0];
        row.AdditionalData.ShouldNotBeNull();
        row.AdditionalData!.ShouldContainKey("cashBalanceFXSegment");
    }

    [Fact]
    public void Map_MissingResult_ReturnsEmptyRows()
    {
        var frame = JsonDocument.Parse("""{"topic":"sld+DU1"}""").RootElement;

        var update = AccountLedgerUpdateMapper.Map(frame);

        update.AccountId.ShouldBe("DU1");
        update.Result.ShouldBeEmpty();
    }

    [Fact]
    public void Map_BlankCurrencyEntry_OnlyKeyAndTimestampPresent()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sld+DU1","result":[{"key":"LedgerListUSD","timestamp":1700248325}]}""").RootElement;

        var update = AccountLedgerUpdateMapper.Map(frame);

        var row = update.Result[0];
        row.Key.ShouldBe("LedgerListUSD");
        row.Timestamp.ShouldBe(1700248325L);
        row.CashBalance.ShouldBeNull();
    }
}
