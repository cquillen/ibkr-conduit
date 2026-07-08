using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public void Map_OneMalformedRow_YieldsRemainingRows()
    {
        // WIR-1 / PRB-3.2: one malformed ledger row must not discard the frame's other currencies.
        // Per-element isolation deserializes each row independently (mirrors VCR-03 for str).
        var frame = JsonDocument.Parse(
            """
            {"topic":"sld+DU1","result":[
              {"key":"LedgerListUSD","cashbalance":100.0},
              {"key":"LedgerListBad","cashbalance":"garbage-object"},
              {"key":"LedgerListEUR","cashbalance":200.0}
            ]}
            """).RootElement;

        var rows = AccountLedgerUpdateMapper.Map(frame).Result;

        rows.Select(r => r.Key).ShouldBe(["LedgerListUSD", "LedgerListEUR"]);
    }

    [Fact]
    public void Map_OneMalformedRow_ReportsExactlyOneDropToCallback()
    {
        var frame = JsonDocument.Parse(
            """
            {"topic":"sld+DU1","result":[
              {"key":"LedgerListUSD","cashbalance":100.0},
              {"key":"LedgerListBad","cashbalance":"garbage-object"},
              {"key":"LedgerListEUR","cashbalance":200.0}
            ]}
            """).RootElement;

        var dropped = new List<Exception>();
        var update = AccountLedgerUpdateMapper.Map(frame, dropped.Add);

        update.Result.Count.ShouldBe(2);
        dropped.Count.ShouldBe(1);
    }

    [Fact]
    public void Map_SubstantiveRowMissingNetLiquidationValue_ReportsCensus()
    {
        // WIR-5: a substantive ledger row (one reporting a cash balance) that omits
        // netLiquidationValue raises the census so wire drift on the account-money path is observable.
        var frame = JsonDocument.Parse(
            """{"topic":"sld+DU1","result":[{"key":"LedgerListUSD","cashbalance":100.0,"secondKey":"USD"}]}""").RootElement;

        var absent = new List<string>();
        AccountLedgerUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldContain("netLiquidationValue");
    }

    [Fact]
    public void Map_BlankEntry_ReportsNoCensus()
    {
        // A blank 10-second no-change entry (only key + timestamp) carries no cashbalance, so it is
        // exempt from the census — it must not raise a false signal every interval.
        var frame = JsonDocument.Parse(
            """{"topic":"sld+DU1","result":[{"key":"LedgerListUSD","timestamp":1700248325}]}""").RootElement;

        var absent = new List<string>();
        AccountLedgerUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldBeEmpty();
    }

    [Fact]
    public void Map_SubstantiveRowWithMoneyFields_ReportsNoCensus()
    {
        var frame = JsonDocument.Parse(_realLedgerFrame).RootElement;

        var absent = new List<string>();
        AccountLedgerUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldBeEmpty();
    }
}
