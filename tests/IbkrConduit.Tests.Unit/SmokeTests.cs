using Shouldly;

namespace IbkrConduit.Tests.Unit;

public class SmokeTests
{
    [Fact]
    public void LibraryVersion_ShouldBeSet()
    {
        IbkrConduitInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
