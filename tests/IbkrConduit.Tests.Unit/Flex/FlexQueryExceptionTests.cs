using IbkrConduit.Flex;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexQueryExceptionTests
{
    [Fact]
    public void Constructor_PreservesErrorCodeAndMessage()
    {
        var ex = new FlexQueryException(1019, "Statement generation in progress.");

        ex.ErrorCode.ShouldBe(1019);
        ex.Message.ShouldBe("Statement generation in progress.");
    }

    [Fact]
    public void Constructor_KnownRetryableCode_SetsIsRetryableTrue()
    {
        // 1019 is documented as retryable.
        var ex = new FlexQueryException(1019, "server message");

        ex.IsRetryable.ShouldBeTrue();
        ex.CodeDescription.ShouldNotBeNull();
        ex.CodeDescription.ShouldContain("in progress");
    }

    [Fact]
    public void Constructor_KnownPermanentCode_SetsIsRetryableFalse()
    {
        // 1015 is documented as permanent ("Token is invalid").
        var ex = new FlexQueryException(1015, "server message");

        ex.IsRetryable.ShouldBeFalse();
        ex.CodeDescription.ShouldNotBeNull();
        ex.CodeDescription.ShouldContain("Token is invalid");
    }

    [Fact]
    public void Constructor_UnknownCode_DefaultsToNonRetryable()
    {
        // Unknown codes default to IsRetryable = false for safety.
        var ex = new FlexQueryException(9999, "server message");

        ex.ErrorCode.ShouldBe(9999);
        ex.IsRetryable.ShouldBeFalse();
        ex.CodeDescription.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ZeroCode_DefaultsToNonRetryable()
    {
        // Code 0 is not in the table (it indicates "no error") so it defaults to non-retryable.
        var ex = new FlexQueryException(0, "some message");

        ex.IsRetryable.ShouldBeFalse();
        ex.CodeDescription.ShouldBeNull();
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(1004)]
    [InlineData(1005)]
    [InlineData(1006)]
    [InlineData(1007)]
    [InlineData(1008)]
    [InlineData(1009)]
    [InlineData(1018)]
    [InlineData(1019)]
    [InlineData(1021)]
    public void Constructor_AllRetryableCodes_SetIsRetryableTrue(int code)
    {
        var ex = new FlexQueryException(code, "test");
        ex.IsRetryable.ShouldBeTrue($"code {code} should be classified as retryable");
        ex.CodeDescription.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(1003)]
    [InlineData(1010)]
    [InlineData(1011)]
    [InlineData(1012)]
    [InlineData(1013)]
    [InlineData(1014)]
    [InlineData(1015)]
    [InlineData(1016)]
    [InlineData(1017)]
    [InlineData(1020)]
    public void Constructor_AllPermanentCodes_SetIsRetryableFalse(int code)
    {
        var ex = new FlexQueryException(code, "test");
        ex.IsRetryable.ShouldBeFalse($"code {code} should be classified as permanent");
        ex.CodeDescription.ShouldNotBeNull();
    }
}
