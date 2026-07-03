using IbkrConduit.Examples.OrderMonitor;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderMonitor;

public class LiveOrderTableTests
{
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
}
