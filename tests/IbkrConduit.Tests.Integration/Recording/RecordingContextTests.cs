using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Recording;

public class RecordingContextTests
{
    [Fact]
    public void NextStep_ReturnsIncrementingValues()
    {
        var ctx = new RecordingContext();

        var step1 = ctx.NextStep();
        var step2 = ctx.NextStep();
        var step3 = ctx.NextStep();

        step1.ShouldBe(1);
        step2.ShouldBe(2);
        step3.ShouldBe(3);
    }

    [Fact]
    public async Task NextStep_WhenCalledConcurrently_ProducesUniqueValues()
    {
        var ctx = new RecordingContext();
        var results = new ConcurrentBag<int>();
        const int count = 100;

        var tasks = Enumerable.Range(0, count)
            .Select(_ => Task.Run(() => results.Add(ctx.NextStep())))
            .ToArray();

        await Task.WhenAll(tasks);

        results.Count.ShouldBe(count);
        results.Distinct().Count().ShouldBe(count);
    }

    [Fact]
    public void Reset_ResetsCounterAndSetsScenarioName()
    {
        var ctx = new RecordingContext();
        ctx.NextStep();
        ctx.NextStep();

        ctx.Reset("new-scenario");

        ctx.ScenarioName.ShouldBe("new-scenario");
        ctx.NextStep().ShouldBe(1);
    }

    [Fact]
    public void ScenarioName_DefaultsToNull()
    {
        var ctx = new RecordingContext();

        ctx.ScenarioName.ShouldBeNull();
    }

    [Fact]
    public void IsActive_WhenScenarioNameIsNull_ReturnsFalse()
    {
        var ctx = new RecordingContext();

        ctx.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_WhenScenarioNameIsSet_ReturnsTrue()
    {
        var ctx = new RecordingContext();
        ctx.Reset("test");

        ctx.IsActive.ShouldBeTrue();
    }
}
