using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IbkrConduit.Streaming;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class OrderUpdateMapperTests
{
    // Real sor frame captured live: order(s) are wrapped in an args array, and orderId
    // arrives as a JSON number rather than a quoted string.
    private const string _realOrderFrame = """
        {"topic":"sor","args":[{"acct":"DUO873728","conidex":"756733","conid":756733,"orderId":656804954,"isEventTrading":"0"}]}
        """;

    // Real sor frame captured live for a market order: "price" arrives as an empty string
    // (no limit price on a market order), and the symbol/size live under "ticker"/"totalSize"
    // rather than "symbol"/"size". Before the empty-tolerant converters, deserializing
    // "price":"" threw a JsonException that killed the whole sor subscription.
    private const string _realMarketOrderFrame = """
        {"topic":"sor","args":[{"acct":"DUO873728","conid":320227571,"orderId":196655192,"orderDesc":"Buy 1 QQQ Market, Day","ticker":"QQQ","secType":"STK","remainingQuantity":1.0,"filledQuantity":0.0,"totalSize":1.0,"companyName":"INVESCO QQQ TRUST SERIES 1","status":"Inactive","orderType":"Market","order_ref":"submit-143824-5264","price":"","side":"BUY"}]}
        """;

    [Fact]
    public void MapMany_RealMarketOrderFrameWithEmptyPrice_YieldsOrderWithNullPriceAndCorrectFields()
    {
        var frame = JsonDocument.Parse(_realMarketOrderFrame).RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(1);
        var order = orders[0];
        order.Price.ShouldBeNull();
        order.Symbol.ShouldBe("QQQ");
        order.Size.ShouldBe(1m);
        order.Side.ShouldBe("BUY");
        order.Status.ShouldBe("Inactive");
        order.OrderRef.ShouldBe("submit-143824-5264");
        order.FilledQuantity.ShouldBe(0m);
        order.RemainingQuantity.ShouldBe(1m);
        order.OrderId.ShouldBe("196655192");
        order.Conid.ShouldBe(320227571);
    }

    [Fact]
    public void MapMany_SparseFrame_OmittedFieldsAreNull()
    {
        // WIR-1 (critical): a sparse sor delta omits fields wholesale. Every wire-optional field
        // the frame does not carry must deserialize to null — never a fabricated 0m / "" — so a
        // consumer's sparse-delta merge can tell "absent from this frame" from a genuine zero /
        // empty and never regresses a partially-filled order to unfilled.
        var frame = JsonDocument.Parse(_realOrderFrame).RootElement;

        var order = OrderUpdateMapper.MapMany(frame).Single();

        order.OrderId.ShouldBe("656804954"); // the dedupe key is present on every frame
        order.Conid.ShouldBe(756733);        // present on this frame
        order.Size.ShouldBeNull();
        order.FilledQuantity.ShouldBeNull();
        order.RemainingQuantity.ShouldBeNull();
        order.Status.ShouldBeNull();
        order.Side.ShouldBeNull();
        order.Symbol.ShouldBeNull();
        order.OrderType.ShouldBeNull();
    }

    [Fact]
    public void MapMany_FrameOmittingConid_ConidIsNull()
    {
        // conid is wire-optional on a sor delta; its absence must be null, not a fabricated 0.
        var frame = JsonDocument.Parse("""{"topic":"sor","args":[{"orderId":42}]}""").RootElement;

        OrderUpdateMapper.MapMany(frame).Single().Conid.ShouldBeNull();
    }

    [Fact]
    public void MapMany_RealOrderFrame_YieldsOneOrderWithNumericOrderIdCoercedToString()
    {
        var frame = JsonDocument.Parse(_realOrderFrame).RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(1);
        orders[0].OrderId.ShouldBe("656804954");
        orders[0].Conid.ShouldBe(756733);
    }

    [Fact]
    public void MapMany_TwoOrdersInArgs_YieldsBoth()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sor","args":[{"orderId":1},{"orderId":2}]}""").RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(2);
        orders[0].OrderId.ShouldBe("1");
        orders[1].OrderId.ShouldBe("2");
    }

    [Fact]
    public void MapMany_MissingArgs_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"sor"}""").RootElement;

        OrderUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_ArgsNotAnArray_ReturnsEmpty()
    {
        var frame = JsonDocument.Parse("""{"topic":"sor","args":{"orderId":1}}""").RootElement;

        OrderUpdateMapper.MapMany(frame).ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_StatusBearingFrameMissingPrice_ReportsPriceToCensus()
    {
        // WIR-5: a status-bearing sor frame (a full order-state frame, not a bare sparse delta)
        // that omits a required money field raises the census signal so wire drift is observable,
        // even though the order is still delivered (with Price=null per ADR-0001).
        var frame = JsonDocument.Parse(
            """{"topic":"sor","args":[{"orderId":1,"status":"Submitted","totalSize":100}]}""").RootElement;

        var absent = new List<string>();
        OrderUpdateMapper.MapMany(frame, onRequiredMoneyFieldAbsent: absent.Add).ToList();

        absent.ShouldContain("price");
        absent.ShouldNotContain("totalSize");
    }

    [Fact]
    public void MapMany_SparseIdentityDelta_ReportsNoCensus()
    {
        // ADR-0001: a sor delta omits fields wholesale. A bare identity delta (no status) legitimately
        // carries no money fields, so it must NOT raise a false census signal on every normal delta.
        var frame = JsonDocument.Parse(_realOrderFrame).RootElement;

        var absent = new List<string>();
        OrderUpdateMapper.MapMany(frame, onRequiredMoneyFieldAbsent: absent.Add).ToList();

        absent.ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_StatusBearingFrameWithMoneyFields_ReportsNoCensus()
    {
        // The real market-order snapshot carries status + totalSize + price:"" (present-but-empty);
        // presence is presence, so a well-formed order-state frame raises no census.
        var frame = JsonDocument.Parse(_realMarketOrderFrame).RootElement;

        var absent = new List<string>();
        OrderUpdateMapper.MapMany(frame, onRequiredMoneyFieldAbsent: absent.Add).ToList();

        absent.ShouldBeEmpty();
    }

    [Fact]
    public void MapMany_OneMalformedElement_YieldsRemainingOrders()
    {
        // WIR-1 / PRB-3.2: one malformed order mid-array must not discard the frame's tail — a sor
        // snapshot frame carries a whole day of orders. Per-element isolation deserializes each
        // element independently so every good order is still delivered (mirrors VCR-03 for str).
        var frame = JsonDocument.Parse(
            """
            {"topic":"sor","args":[
              {"orderId":1,"conid":265598},
              {"orderId":"bad","conid":"garbage-object"},
              {"orderId":2,"conid":272093}
            ]}
            """).RootElement;

        var orders = OrderUpdateMapper.MapMany(frame).ToList();

        orders.Count.ShouldBe(2);
        orders[0].OrderId.ShouldBe("1");
        orders[1].OrderId.ShouldBe("2");
    }

    [Fact]
    public void MapMany_OneMalformedElement_ReportsExactlyOneDropToCallback()
    {
        // The malformed element must be reported to the drop callback so the caller can count and
        // log it per the VCR-02 drop taxonomy — never silently swallowed (WIR-1).
        var frame = JsonDocument.Parse(
            """
            {"topic":"sor","args":[
              {"orderId":1,"conid":265598},
              {"orderId":"bad","conid":"garbage-object"},
              {"orderId":2,"conid":272093}
            ]}
            """).RootElement;

        var dropped = new List<Exception>();
        var orders = OrderUpdateMapper.MapMany(frame, dropped.Add).ToList();

        orders.Count.ShouldBe(2);
        dropped.Count.ShouldBe(1);
    }

    [Fact]
    public void MapMany_MalformedElement_NotCensused()
    {
        // A dropped (malformed) element is already counted as a mapper drop; it must not also raise
        // a census signal — the two taxonomies are distinct and must not double-count one element.
        var frame = JsonDocument.Parse(
            """{"topic":"sor","args":[{"orderId":"bad","conid":"garbage-object","status":"Submitted"}]}""").RootElement;

        var absent = new List<string>();
        var orders = OrderUpdateMapper.MapMany(
            frame, onElementDropped: _ => { }, onRequiredMoneyFieldAbsent: absent.Add).ToList();

        orders.ShouldBeEmpty();
        absent.ShouldBeEmpty();
    }
}
