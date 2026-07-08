using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class AccountSummaryUpdateMapperTests
{
    // Real ssd frame captured live: data is a "result" array at the root; the account is
    // in "topic" ("ssd+DUO873728"), not in the payload.
    private const string _realSummaryFrame = """
        {"result":[{"key":"ExcessLiquidity-S","currency":"USD","monetaryValue":1005353.88,"severity":0,"timestamp":1783031080}],"topic":"ssd+DUO873728"}
        """;

    [Fact]
    public void Map_RealSummaryFrame_ReturnsAccountIdFromTopicAndOneRow()
    {
        var frame = JsonDocument.Parse(_realSummaryFrame).RootElement;

        var update = AccountSummaryUpdateMapper.Map(frame);

        update.AccountId.ShouldBe("DUO873728");
        update.Result.Count.ShouldBe(1);
        var row = update.Result[0];
        row.Key.ShouldBe("ExcessLiquidity-S");
        row.Currency.ShouldBe("USD");
        row.MonetaryValue.ShouldBe(1005353.88m);
        row.Severity.ShouldBe(0);
        row.Timestamp.ShouldBe(1783031080L);
    }

    [Fact]
    public void Map_MultipleRows_ReturnsAllOfThem()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DU1","result":[{"key":"K1"},{"key":"K2"}]}""").RootElement;

        var update = AccountSummaryUpdateMapper.Map(frame);

        update.Result.Count.ShouldBe(2);
        update.Result[0].Key.ShouldBe("K1");
        update.Result[1].Key.ShouldBe("K2");
    }

    [Fact]
    public void Map_MissingResult_ReturnsEmptyRows()
    {
        var frame = JsonDocument.Parse("""{"topic":"ssd+DU1"}""").RootElement;

        var update = AccountSummaryUpdateMapper.Map(frame);

        update.AccountId.ShouldBe("DU1");
        update.Result.ShouldBeEmpty();
    }

    [Fact]
    public void Map_MissingTopic_AccountIdIsEmpty()
    {
        var frame = JsonDocument.Parse("""{"result":[]}""").RootElement;

        var update = AccountSummaryUpdateMapper.Map(frame);

        update.AccountId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Map_NonMonetaryRow_MapsValueField()
    {
        // PRB-3.3: the captured ssd frame carries non-monetary rows shaped {key, value, severity,
        // timestamp} (e.g. {"key":"Cushion","value":"1"}) whose `value` had nowhere to map. The
        // AccountSummaryRow.value member (design §12.5) must now preserve it.
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DUO873728","result":[{"key":"Cushion","value":"1","severity":0,"timestamp":1783031080}]}""").RootElement;

        var row = AccountSummaryUpdateMapper.Map(frame).Result.Single();

        row.Key.ShouldBe("Cushion");
        row.Value.ShouldBe("1");
        row.MonetaryValue.ShouldBeNull();
        row.Currency.ShouldBeNull();
    }

    [Fact]
    public void Map_UnmappedRowKey_LandsInAdditionalData()
    {
        // The [JsonExtensionData] overflow bag (design §12.5) preserves any ssd row key the DTO
        // does not name first-class, so a wire-shape addition survives instead of being dropped.
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DU1","result":[{"key":"K","brand_new_field":"42"}]}""").RootElement;

        var row = AccountSummaryUpdateMapper.Map(frame).Result.Single();

        row.AdditionalData.ShouldNotBeNull();
        row.AdditionalData!.ShouldContainKey("brand_new_field");
    }

    [Fact]
    public void Map_OneMalformedRow_YieldsRemainingRows()
    {
        // WIR-1 / PRB-3.2: one malformed row in an ssd frame (135 rows on a real capture) must not
        // discard the whole frame — per-element isolation deserializes each row independently so
        // every good row still maps (mirrors VCR-03 for str).
        var frame = JsonDocument.Parse(
            """
            {"topic":"ssd+DU1","result":[
              {"key":"Good-1","currency":"USD","monetaryValue":100},
              {"key":"Bad","monetaryValue":"garbage-object"},
              {"key":"Good-2","currency":"USD","monetaryValue":200}
            ]}
            """).RootElement;

        var rows = AccountSummaryUpdateMapper.Map(frame).Result;

        rows.Select(r => r.Key).ShouldBe(["Good-1", "Good-2"]);
    }

    [Fact]
    public void Map_OneMalformedRow_ReportsExactlyOneDropToCallback()
    {
        var frame = JsonDocument.Parse(
            """
            {"topic":"ssd+DU1","result":[
              {"key":"Good-1","currency":"USD","monetaryValue":100},
              {"key":"Bad","monetaryValue":"garbage-object"},
              {"key":"Good-2","currency":"USD","monetaryValue":200}
            ]}
            """).RootElement;

        var dropped = new List<Exception>();
        var update = AccountSummaryUpdateMapper.Map(frame, dropped.Add);

        update.Result.Count.ShouldBe(2);
        dropped.Count.ShouldBe(1);
    }

    [Fact]
    public void Map_MonetaryRowMissingMonetaryValue_ReportsCensus()
    {
        // WIR-5: a monetary summary row (one naming a currency) that omits monetaryValue raises the
        // required-money-field census so wire drift on the account-money path is observable.
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DU1","result":[{"key":"ExcessLiquidity-S","currency":"USD","severity":0}]}""").RootElement;

        var absent = new List<string>();
        AccountSummaryUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldContain("monetaryValue");
    }

    [Fact]
    public void Map_NonMonetaryRow_ReportsNoCensus()
    {
        // A non-monetary row (no currency, carries `value`) legitimately has no monetaryValue, so it
        // must not raise a false census on every Cushion-style row.
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DU1","result":[{"key":"Cushion","value":"1","severity":0}]}""").RootElement;

        var absent = new List<string>();
        AccountSummaryUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldBeEmpty();
    }

    [Fact]
    public void Map_MonetaryRowWithMonetaryValue_ReportsNoCensus()
    {
        var frame = JsonDocument.Parse(_realSummaryFrame).RootElement;

        var absent = new List<string>();
        AccountSummaryUpdateMapper.Map(frame, onRequiredMoneyFieldAbsent: absent.Add);

        absent.ShouldBeEmpty();
    }

    [Fact]
    public void Map_MalformedRow_NotCensused()
    {
        // A dropped (malformed) row is counted as a mapper drop; it must not also raise a census.
        var frame = JsonDocument.Parse(
            """{"topic":"ssd+DU1","result":[{"key":"Bad","currency":"USD","monetaryValue":"garbage-object"}]}""").RootElement;

        var absent = new List<string>();
        var update = AccountSummaryUpdateMapper.Map(
            frame, onRowDropped: _ => { }, onRequiredMoneyFieldAbsent: absent.Add);

        update.Result.ShouldBeEmpty();
        absent.ShouldBeEmpty();
    }
}
