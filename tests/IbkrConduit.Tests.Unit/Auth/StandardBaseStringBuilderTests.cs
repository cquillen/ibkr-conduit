using System.Collections.Generic;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class StandardBaseStringBuilderTests
{
    [Fact]
    public void Build_SortsParametersLexicographically()
    {
        var builder = new StandardBaseStringBuilder();
        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_token"] = "mytoken",
            ["oauth_consumer_key"] = "MYKEY",
            ["oauth_nonce"] = "abc123",
        };

        var result = builder.Build("GET", "https://api.ibkr.com/v1/api/test", parameters);

        var expectedParams = OAuthEncoding.QuotePlus(
            "oauth_consumer_key=MYKEY&oauth_nonce=abc123&oauth_token=mytoken");
        var expectedUrl = OAuthEncoding.QuotePlus("https://api.ibkr.com/v1/api/test");

        result.ShouldBe($"GET&{expectedUrl}&{expectedParams}");
    }

    [Fact]
    public void Build_UsesQuotePlusEncoding()
    {
        var builder = new StandardBaseStringBuilder();
        var parameters = new SortedDictionary<string, string>
        {
            ["key"] = "value with spaces",
        };

        var result = builder.Build("POST", "https://example.com/path", parameters);

        result.ShouldContain("POST&");
        result.ShouldContain(OAuthEncoding.QuotePlus("https://example.com/path"));
    }
}
