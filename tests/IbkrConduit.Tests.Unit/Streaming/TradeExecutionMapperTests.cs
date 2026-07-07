using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class TradeExecutionMapperTests
{
    private const string _twoExecutionFrame = """
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
        var frame = JsonDocument.Parse(_twoExecutionFrame).RootElement;

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
    public void MapMany_EmptyStringPriceAndNetAmount_YieldNullNotZero()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x","price":"","net_amount":""}]}""").RootElement;

        var execution = TradeExecutionMapper.MapMany(frame).Single();

        execution.Price.ShouldBeNull();
        execution.NetAmount.ShouldBeNull();
    }

    [Fact]
    public void MapMany_MapsNewlyAddedFields_FullyCoversRealFrame()
    {
        // A real str frame (the commission-added follow-up), captured live 2026-07-02.
        var frame = JsonDocument.Parse(
            """
            {"topic":"str","args":[{
              "execution_id":"00025b49.6a4ffd74.01.01","symbol":"SPY","side":"B",
              "supports_tax_opt":"1","size":1.0,"price":"740.90","order_ref":"submit-174550-7a5e",
              "exchange":"NASDAQ","commission":"1.0","net_amount":740.9,
              "account":"DUO873728","accountCode":"DUO873728",
              "account_allocation_name":"DUO873728","listing_exchange":"ARCA",
              "conid":756733,"position":"48","clearing_id":"IB","clearing_name":"IB",
              "liquidation_trade":"0","is_event_trading":"0","order_id":345375760
            }]}
            """).RootElement;

        var e = TradeExecutionMapper.MapMany(frame).Single();

        e.OrderId.ShouldBe("345375760");   // numeric on the wire -> string (correlates with OrderUpdate.OrderId)
        e.Commission.ShouldBe(1.0m);       // quoted string -> decimal
        e.Position.ShouldBe(48m);          // quoted string -> decimal
        e.ListingExchange.ShouldBe("ARCA");
        e.AccountAllocationName.ShouldBe("DUO873728");
        e.ClearingId.ShouldBe("IB");
        e.ClearingName.ShouldBe("IB");
        e.SupportsTaxOpt.ShouldBe(true);   // "1" -> true
        e.LiquidationTrade.ShouldBe(false); // "0" -> false
        e.IsEventTrading.ShouldBe(false);  // "0" -> false
        // Every field on this real frame is now first-class — nothing spills into AdditionalData.
        e.AdditionalData.ShouldBeNull();
    }

    [Fact]
    public void MapMany_EmptyCommissionAndPosition_YieldNullNotZero()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x","commission":"","position":""}]}""").RootElement;

        var execution = TradeExecutionMapper.MapMany(frame).Single();

        execution.Commission.ShouldBeNull();
        execution.Position.ShouldBeNull();
    }

    [Fact]
    public void MapMany_EmptyStringSize_PreservesAbsenceAsNull()
    {
        // FIL-6 / WIR-3: a fill whose size IBKR sends as "" must surface as null, never a
        // fabricated 0m that reads as a real (impossible) zero-quantity fill.
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x","size":""}]}""").RootElement;

        TradeExecutionMapper.MapMany(frame).Single().Size.ShouldBeNull();
    }

    [Fact]
    public void MapMany_OmittedSize_PreservesAbsenceAsNull()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"str","args":[{"execution_id":"x"}]}""").RootElement;

        TradeExecutionMapper.MapMany(frame).Single().Size.ShouldBeNull();
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

    [Fact]
    public void MapMany_OneMalformedElement_YieldsRemainingExecutions()
    {
        // FIL-2: one malformed execution mid-array must not discard the frame's tail — a str
        // snapshot frame carries up to a whole day's fills. Per-element isolation deserializes
        // each element independently so every good execution is still delivered.
        var frame = JsonDocument.Parse(
            """
            {"topic":"str","args":[
              {"execution_id":"good-1","symbol":"AAPL","conid":265598},
              {"execution_id":"bad","conid":"garbage-object"},
              {"execution_id":"good-2","symbol":"MSFT","conid":272093}
            ]}
            """).RootElement;

        var executions = TradeExecutionMapper.MapMany(frame).ToList();

        executions.Count.ShouldBe(2);
        executions[0].ExecutionId.ShouldBe("good-1");
        executions[1].ExecutionId.ShouldBe("good-2");
    }

    [Fact]
    public void MapMany_OneMalformedElement_ReportsExactlyOneDropToCallback()
    {
        // The malformed element must be reported to the drop callback so the caller can count and
        // log it per the VCR-02 drop taxonomy — never silently swallowed (FIL-2).
        var frame = JsonDocument.Parse(
            """
            {"topic":"str","args":[
              {"execution_id":"good-1","conid":265598},
              {"execution_id":"bad","conid":"garbage-object"},
              {"execution_id":"good-2","conid":272093}
            ]}
            """).RootElement;

        var dropped = new List<Exception>();
        var executions = TradeExecutionMapper.MapMany(frame, dropped.Add).ToList();

        executions.Count.ShouldBe(2);
        dropped.Count.ShouldBe(1);
    }

    [Fact]
    public void MapMany_AllElementsMalformed_YieldsNothingAndReportsEachDrop()
    {
        var frame = JsonDocument.Parse(
            """
            {"topic":"str","args":[
              {"execution_id":"bad-1","conid":"garbage-object"},
              {"execution_id":"bad-2","conid":"also-garbage"}
            ]}
            """).RootElement;

        var dropped = new List<Exception>();
        var executions = TradeExecutionMapper.MapMany(frame, dropped.Add).ToList();

        executions.ShouldBeEmpty();
        dropped.Count.ShouldBe(2);
    }
}
