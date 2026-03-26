using Shouldly;

namespace IbkrConduit.Tests.Integration;

public class SmokeTests
{
    [Fact]
    public void LibraryVersion_ShouldBe_0_1_0()
    {
        IbkrConduitInfo.Version.ShouldBe("0.1.0");
    }
}
