using IbkrConduit.Flex;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexErrorCodesTests
{
    [Theory]
    [InlineData(1001, true)]  // could not be generated — try again
    [InlineData(1003, false)] // not available — permanent
    [InlineData(1004, true)]  // incomplete — try again
    [InlineData(1005, true)]  // settlement not ready — try again
    [InlineData(1006, true)]  // FIFO P/L not ready — try again
    [InlineData(1007, true)]  // MTM P/L not ready — try again
    [InlineData(1008, true)]  // MTM+FIFO P/L not ready — try again
    [InlineData(1009, true)]  // server under heavy load — try again
    [InlineData(1010, false)] // legacy queries not supported — permanent
    [InlineData(1011, false)] // service account inactive — permanent
    [InlineData(1012, false)] // token expired — permanent
    [InlineData(1013, false)] // IP restriction — permanent
    [InlineData(1014, false)] // query invalid — permanent
    [InlineData(1015, false)] // token invalid — permanent
    [InlineData(1016, false)] // account invalid — permanent
    [InlineData(1017, false)] // reference code invalid — permanent
    [InlineData(1018, true)]  // too many requests — rate limit, try again
    [InlineData(1019, true)]  // generation in progress — try again
    [InlineData(1020, false)] // invalid request — permanent
    [InlineData(1021, true)]  // could not be retrieved — try again
    public void TryLookup_ReturnsExpectedClassification(int code, bool expectedRetryable)
    {
        var info = FlexErrorCodes.TryLookup(code);

        info.ShouldNotBeNull();
        info.Code.ShouldBe(code);
        info.IsRetryable.ShouldBe(expectedRetryable);
        info.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryLookup_UnknownCode_ReturnsNull()
    {
        FlexErrorCodes.TryLookup(9999).ShouldBeNull();
        FlexErrorCodes.TryLookup(0).ShouldBeNull();
        FlexErrorCodes.TryLookup(-1).ShouldBeNull();
    }

    [Fact]
    public void TryLookup_KnownCode_DescriptionMatchesDocumentation()
    {
        FlexErrorCodes.TryLookup(1019)!.Description.ShouldContain("in progress");
        FlexErrorCodes.TryLookup(1015)!.Description.ShouldContain("Token is invalid");
        FlexErrorCodes.TryLookup(1018)!.Description.ShouldContain("Too many requests");
    }
}
