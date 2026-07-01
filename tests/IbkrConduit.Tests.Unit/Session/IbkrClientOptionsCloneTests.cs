using IbkrConduit.Flex;
using IbkrConduit.Session;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Session;

public class IbkrClientOptionsCloneTests
{
    [Fact]
    public void Clone_MutatingCloneList_DoesNotAffectOriginal()
    {
        var original = new IbkrClientOptions();
        original.SuppressMessageIds.Add("o1");
        original.FlexQueries.CashTransactionsQueryId = "100";

        var clone = original.Clone();
        clone.SuppressMessageIds.Add("c1");
        clone.FlexQueries.CashTransactionsQueryId = "200";
        clone.TickleIntervalSeconds = 999;

        original.SuppressMessageIds.ShouldBe(new[] { "o1" });
        original.FlexQueries.CashTransactionsQueryId.ShouldBe("100");
        original.TickleIntervalSeconds.ShouldBe(60);
    }

    [Fact]
    public void Clone_CopiesScalarValues()
    {
        var original = new IbkrClientOptions
        {
            TickleIntervalSeconds = 30,
            StrictResponseValidation = true,
            FlexToken = "flex",
            BaseUrl = "https://example.test",
        };

        var clone = original.Clone();

        clone.TickleIntervalSeconds.ShouldBe(30);
        clone.StrictResponseValidation.ShouldBeTrue();
        clone.FlexToken.ShouldBe("flex");
        clone.BaseUrl.ShouldBe("https://example.test");
    }

    [Fact]
    public void Clone_CopiesWebSocketBaseUrl()
    {
        var original = new IbkrClientOptions
        {
            WebSocketBaseUrl = "wss://custom.test/v1/api/ws",
        };

        var clone = original.Clone();

        clone.WebSocketBaseUrl.ShouldBe("wss://custom.test/v1/api/ws");
    }
}
