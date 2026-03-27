using System;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthEncodingTests
{
    [Theory]
    [InlineData("hello world", "hello+world")]
    [InlineData("a=b&c=d", "a%3Db%26c%3Dd")]
    [InlineData("simple", "simple")]
    [InlineData("https://api.ibkr.com/v1/api/test", "https%3A%2F%2Fapi.ibkr.com%2Fv1%2Fapi%2Ftest")]
    public void QuotePlus_EncodesCorrectly(string input, string expected)
    {
        var result = OAuthEncoding.QuotePlus(input);
        result.ShouldBe(expected);
    }

    [Fact]
    public void GenerateNonce_Returns16AlphanumericChars()
    {
        var nonce = OAuthEncoding.GenerateNonce();
        nonce.Length.ShouldBe(16);
        nonce.ShouldMatch(@"^[A-Za-z0-9]{16}$");
    }

    [Fact]
    public void GenerateNonce_ProducesDifferentValues()
    {
        var nonce1 = OAuthEncoding.GenerateNonce();
        var nonce2 = OAuthEncoding.GenerateNonce();
        nonce1.ShouldNotBe(nonce2);
    }

    [Fact]
    public void GenerateTimestamp_ReturnsUnixSecondsString()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamp = OAuthEncoding.GenerateTimestamp();
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var parsed = long.Parse(timestamp);
        parsed.ShouldBeGreaterThanOrEqualTo(before);
        parsed.ShouldBeLessThanOrEqualTo(after);
    }
}
