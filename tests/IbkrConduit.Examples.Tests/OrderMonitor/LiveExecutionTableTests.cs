using IbkrConduit.Examples.OrderMonitor;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderMonitor;

public class LiveExecutionTableTests
{
    private static TradeExecution Exec(string id, string symbol = "AAPL") =>
        new() { ExecutionId = id, Symbol = symbol, Side = "BUY", Size = 1, Price = 100m };

    [Fact]
    public void Add_NewExecutions_AppearNewestFirst()
    {
        var table = new LiveExecutionTable();

        table.Add(Exec("e1"));
        table.Add(Exec("e2"));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(2);
        snapshot[0].ExecutionId.ShouldBe("e2");
        snapshot[1].ExecutionId.ShouldBe("e1");
    }

    [Fact]
    public void Add_DuplicateExecutionId_IsIgnored()
    {
        var table = new LiveExecutionTable();

        table.Add(Exec("e1"));
        table.Add(Exec("e1"));

        table.Snapshot().Count.ShouldBe(1);
        table.TotalSeen.ShouldBe(1);
    }

    [Fact]
    public void Add_BeyondCapacity_KeepsOnlyMostRecentButCountsAll()
    {
        var table = new LiveExecutionTable(capacity: 2);

        table.Add(Exec("e1"));
        table.Add(Exec("e2"));
        table.Add(Exec("e3"));

        var snapshot = table.Snapshot();
        snapshot.Count.ShouldBe(2);
        snapshot[0].ExecutionId.ShouldBe("e3");
        snapshot[1].ExecutionId.ShouldBe("e2");
        table.TotalSeen.ShouldBe(3);
    }
}
