using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class TradeExecutionMapperTests
{
    private const string TwoExecutionFrame = """
        {
          "topic":"str",
          "args":[
            {
              "execution_id":"0000e0d5.1","symbol":"AAPL","supports_tax_opt":"1",
              "side":"B","order_description":"BUY 100 @ 150.25 on NASDAQ",
              "trade_time":"20260702-14:30:05","trade_time_r":1751466605000,
              "size":100,"order_ref":"my-coid-1","price":"150.25","exchange":"NASDAQ",
              "net_amount":15025.0,"account":"DU111","accountCode":"DU111",
              "company_name":"APPLE INC","contract_description_1":"AAPL",
              "contract_description_2":"","sec_type":"STK","conid":265598,
              "conidEx":"265598","open_close":"???","liquidation_trade":"0",
              "is_event_trading":"0"
            },
            {
              "execution_id":"0000e0d5.2","symbol":"MSFT","side":"S",
              "size":50,"price":"420.10","conid":272093,"account":"DU111",
              "net_amount":21005.0,"trade_time_r":1751466606000
            }
          ]
        }
        """;

    [Fact]
    public void MapMany_FrameWithTwoExecutions_ReturnsBothWithFieldsMapped()
    {
        var frame = JsonDocument.Parse(TwoExecutionFrame).RootElement;

        var executions = TradeExecutionMapper.MapMany(frame).ToList();

        executions.Count.ShouldBe(2);
        var first = executions[0];
        first.ExecutionId.ShouldBe("0000e0d5.1");
        first.Symbol.ShouldBe("AAPL");
        first.Side.ShouldBe("B");
        first.Size.ShouldBe(100m);
        first.Price.ShouldBe(150.25m);          // "150.25" (string) parsed to decimal
        first.NetAmount.ShouldBe(15025.0m);
        first.Conid.ShouldBe(265598);
        first.ConidEx.ShouldBe("265598");
        first.OrderRef.ShouldBe("my-coid-1");
        first.Exchange.ShouldBe("NASDAQ");
        first.TradeTime.ShouldBe("20260702-14:30:05");
        first.TradeTimeR.ShouldBe(1751466605000);
        first.OpenClose.ShouldBe("???");
        first.SecType.ShouldBe("STK");
        first.CompanyName.ShouldBe("APPLE INC");
        executions[1].Symbol.ShouldBe("MSFT");
        executions[1].Price.ShouldBe(420.10m);
    }

    [Fact]
    public void MapMany_UnknownField_LandsInAdditionalData()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x","brand_new_field":"42"}]}""").RootElement;

        var execution = TradeExecutionMapper.MapMany(frame).Single();

        execution.AdditionalData.ShouldNotBeNull();
        execution.AdditionalData!.ShouldContainKey("brand_new_field");
    }

    [Fact]
    public void MapMany_MissingArgs_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"str"}""").RootElement;

        TradeExecutionMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_ArgsNotAnArray_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"str","args":{"execution_id":"x"}}""").RootElement;

        TradeExecutionMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_EmptyArgsArray_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"str","args":[]}""").RootElement;

        TradeExecutionMapper.MapMany(frame).ShouldBeEmpty();
    }
}
