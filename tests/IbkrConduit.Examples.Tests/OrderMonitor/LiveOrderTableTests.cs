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
}
