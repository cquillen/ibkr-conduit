using IbkrConduit.Setup;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Setup;

public class InputValidationTests
{
    [Theory]
    [InlineData("TESTKEY01", true)]
    [InlineData("ABCDE1234", true)]
    [InlineData("abcde1234", true)]
    [InlineData("SHORT", false)]
    [InlineData("TOOLONGKEY", false)]
    [InlineData("", false)]
    [InlineData("KEY WITH!", false)]
    [InlineData("KEY-DASH1", false)]
    public void ValidateConsumerKey_ReturnsExpected(string input, bool shouldBeValid)
    {
        var error = CredentialFile.ValidateConsumerKey(input);

        if (shouldBeValid)
        {
            error.ShouldBeNull();
        }
        else
        {
            error.ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData("abc123", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    public void ValidateAccessToken_ReturnsExpected(string input, bool shouldBeValid)
    {
        var error = CredentialFile.ValidateAccessToken(input);

        if (shouldBeValid)
        {
            error.ShouldBeNull();
        }
        else
        {
            error.ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData("c2VjcmV0", true)]
    [InlineData("dGVzdA==", true)]
    [InlineData("not-valid-base64!!!", false)]
    [InlineData("", false)]
    public void ValidateAccessTokenSecret_ReturnsExpected(string input, bool shouldBeValid)
    {
        var error = CredentialFile.ValidateAccessTokenSecret(input);

        if (shouldBeValid)
        {
            error.ShouldBeNull();
        }
        else
        {
            error.ShouldNotBeNull();
        }
    }
}
