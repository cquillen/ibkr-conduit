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
}
