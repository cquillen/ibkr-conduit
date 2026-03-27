using System.Collections.Generic;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class PrependingBaseStringBuilderTests
{
    [Fact]
    public void Build_PrependHexBeforeStandardBaseString()
    {
        var decryptedSecret = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var builder = new PrependingBaseStringBuilder(decryptedSecret);
        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = "MYKEY",
        };

        var result = builder.Build(
            "POST", "https://api.ibkr.com/v1/api/oauth/live_session_token", parameters);

        result.ShouldStartWith("deadbeef");

        var standard = new StandardBaseStringBuilder();
        var expectedStandard = standard.Build(
            "POST", "https://api.ibkr.com/v1/api/oauth/live_session_token", parameters);

        result.ShouldBe("deadbeef" + expectedStandard);
    }
}
