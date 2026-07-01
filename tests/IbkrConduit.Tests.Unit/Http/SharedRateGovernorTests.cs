using IbkrConduit.Http;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Http;

public class SharedRateGovernorTests
{
    [Fact]
    public async Task NoOpSharedRateGovernor_AcquireAsync_CompletesImmediately()
    {
        ISharedRateGovernor governor = new NoOpSharedRateGovernor();
        await Should.NotThrowAsync(() => governor.AcquireAsync(CancellationToken.None).AsTask());
    }
}
