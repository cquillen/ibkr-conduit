using IbkrConduit.Examples.OrderMonitor;
using IbkrConduit.Orders;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderMonitor;

public class LiveOrderTableTests
{
    // Builds a REST LiveOrder like GetLiveOrdersAsync returns. Price is the working limit
    // price (null for market orders).
    private static LiveOrder MakeLiveOrder(
        int orderId, string ticker, string side, decimal totalSize, string orderType,
        decimal? price, string status, decimal filled = 0, string? orderRef = null) =>
        new(
            Account: "DUO",
            Conid: 1,
            ConidEx: "1",
            OrderId: orderId,
            Ticker: ticker,
            SecType: "STK",
            ListingExchange: "NASDAQ",
            Side: side,
            Status: status,
            OrderCcpStatus: null,
            OrderType: orderType,
            FilledQuantity: filled,
            RemainingQuantity: totalSize - filled,
            TotalSize: totalSize,
            CompanyName: null,
            AvgPrice: null,
            TimeInForce: "DAY",
            OrderDescription: $"{side} {totalSize} {ticker} {orderType}",
            Price: price)
        {
            OrderRef = orderRef,
        };

    private static OrderUpdate Update(
        string orderId, string status, decimal filled, string? orderRef = null) =>
        new()
        {
            OrderId = orderId,
            Conid = 1,
            Symbol = "AAPL",
            Side = "BUY",
            Size = 100,
            OrderType = "LMT",
            Price = 185m,
            Status = status,
            FilledQuantity = filled,
            OrderRef = orderRef,
        };

    // A sparse sor status delta: IBKR sends only the id plus whatever changed, so every
    // other field deserializes to its default (empty string / 0 / null).
    private static OrderUpdate SparseUpdate(string orderId, string status, decimal filled = 0) =>
        new() { OrderId = orderId, Status = status, FilledQuantity = filled };

    [Fact]
    public void Upsert_FirstUpdate_InsertsRow()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Submitted", 0));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].OrderId.ShouldBe("o1");
        snapshot[0].Status.ShouldBe("Submitted");
    }

    [Fact]
    public void Upsert_SameOrderId_UpdatesInPlace()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Submitted", 0));
        table.Upsert(Update("o1", "Filled", 100));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].Status.ShouldBe("Filled");
        snapshot[0].Filled.ShouldBe(100);
    }

    [Fact]
    public void Upsert_LaterUpdateWithNullOrderRef_RetainsEarlierOrderRef()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Submitted", 0, orderRef: "my-ref"));
        table.Upsert(Update("o1", "Filled", 100, orderRef: null));

        table.Snapshot()[0].OrderRef.ShouldBe("my-ref");
    }

    [Fact]
    public void Upsert_DifferentOrderIds_InsertsSeparateRows()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o2", "Submitted", 0));
        table.Upsert(Update("o1", "Submitted", 0));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(2);
        snapshot[0].OrderId.ShouldBe("o1"); // sorted by OrderId
        snapshot[1].OrderId.ShouldBe("o2");
    }

    [Fact]
    public void Upsert_SparseStatusDelta_RetainsPopulatedColumns()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Submitted", 0)); // full frame populates all columns
        table.Upsert(SparseUpdate("o1", "PreSubmitted")); // status-only delta must not blank them

        var row = table.Snapshot()[0];
        row.Status.ShouldBe("PreSubmitted"); // the one changed field applies
        row.Symbol.ShouldBe("AAPL");
        row.Side.ShouldBe("BUY");
        row.Qty.ShouldBe(100);
        row.Type.ShouldBe("LMT");
        row.Price.ShouldBe(185m);
    }

    [Fact]
    public void Upsert_SparseDelta_RetainsFilledWhenIncomingIsZero()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Filled", 100)); // 100 shares filled
        table.Upsert(SparseUpdate("o1", "Filled", filled: 0)); // delta omits fill count

        table.Snapshot()[0].Filled.ShouldBe(100); // fill count is not reset to 0
    }

    [Fact]
    public void Upsert_Delta_StillAppliesRealFieldChanges()
    {
        var table = new LiveOrderTable();

        table.Upsert(Update("o1", "Submitted", 0)); // Price 185, Filled 0
        table.Upsert(new OrderUpdate
        {
            OrderId = "o1",
            Status = "Filled",
            FilledQuantity = 100,
            Price = 186m, // e.g. re-priced on modify
        });

        var row = table.Snapshot()[0];
        row.Status.ShouldBe("Filled");
        row.Filled.ShouldBe(100);
        row.Price.ShouldBe(186m); // real non-default values still overwrite
    }

    [Fact]
    public void Seed_LimitOrder_PopulatesAllColumns()
    {
        var table = new LiveOrderTable();

        table.Seed(MakeLiveOrder(
            888626139, "QQQ", "BUY", 1, "Limit", price: 400.00m,
            status: "PreSubmitted", filled: 0, orderRef: "colpersist-1700"));

        var row = table.Snapshot()[0];
        row.OrderId.ShouldBe("888626139");
        row.Symbol.ShouldBe("QQQ");
        row.Side.ShouldBe("BUY");
        row.Qty.ShouldBe(1);
        row.Type.ShouldBe("Limit");
        row.Price.ShouldBe(400.00m); // parsed from the unmapped "price" key
        row.Status.ShouldBe("PreSubmitted");
        row.Filled.ShouldBe(0);
        row.OrderRef.ShouldBe("colpersist-1700");
    }

    [Fact]
    public void Seed_MarketOrder_LeavesPriceNull()
    {
        var table = new LiveOrderTable();

        // Market orders have no limit price (IBKR's price="" deserializes to null).
        table.Seed(MakeLiveOrder(
            201804948, "QQQ", "BUY", 1, "Market", price: null, status: "PreSubmitted"));

        var row = table.Snapshot()[0];
        row.Type.ShouldBe("Market");
        row.Price.ShouldBeNull();
    }

    [Fact]
    public void Seed_ThenSparseSorDelta_RetainsSeededColumns()
    {
        var table = new LiveOrderTable();

        // REST seed populates the row in full...
        table.Seed(MakeLiveOrder(
            888626139, "QQQ", "BUY", 1, "Limit", price: 400.00m,
            status: "PreSubmitted", orderRef: "colpersist-1700"));
        // ...then the sor snapshot's id-only frame arrives (no ticker/side/size/price/ref).
        table.Upsert(SparseUpdate("888626139", status: string.Empty));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(1); // same row, matched by IBKR orderId
        var row = snapshot[0];
        row.Symbol.ShouldBe("QQQ");
        row.Side.ShouldBe("BUY");
        row.Qty.ShouldBe(1);
        row.Type.ShouldBe("Limit");
        row.Price.ShouldBe(400.00m);
        row.Status.ShouldBe("PreSubmitted");
        row.OrderRef.ShouldBe("colpersist-1700"); // custom id survives the sparse frame
    }
}
