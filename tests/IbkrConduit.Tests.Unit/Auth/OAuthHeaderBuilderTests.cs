using System.Collections.Generic;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class OAuthHeaderBuilderTests
{
    [Fact]
    public void Build_IncludesRealmAndSortedParams()
    {
        var signer = new FakeSigner("HMAC-SHA256", "dGVzdHNpZw==");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var header = builder.Build(
            "GET",
            "https://api.ibkr.com/v1/api/portfolio/accounts",
            "MYKEY",
            "mytoken");

        header.ShouldStartWith("OAuth realm=\"limited_poa\", ");
        header.ShouldContain("oauth_consumer_key=\"MYKEY\"");
        header.ShouldContain("oauth_token=\"mytoken\"");
        header.ShouldContain("oauth_signature_method=\"HMAC-SHA256\"");
        header.ShouldContain("oauth_signature=");
        header.ShouldContain("oauth_nonce=");
        header.ShouldContain("oauth_timestamp=");
    }

    [Fact]
    public void Build_WithExtraParams_IncludesThemInHeader()
    {
        var signer = new FakeSigner("RSA-SHA256", "c2ln");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var extraParams = new Dictionary<string, string>
        {
            ["diffie_hellman_challenge"] = "abc123",
        };

        var header = builder.Build(
            "POST",
            "https://api.ibkr.com/v1/api/oauth/live_session_token",
            "MYKEY",
            "mytoken",
            extraParams);

        header.ShouldContain("diffie_hellman_challenge=\"abc123\"");

        var dhIndex = header.IndexOf("diffie_hellman_challenge");
        var oauthIndex = header.IndexOf("oauth_consumer_key");
        dhIndex.ShouldBeLessThan(oauthIndex);
    }

    [Fact]
    public void Build_SignatureIsQuotePlusEncoded()
    {
        var signer = new FakeSigner("HMAC-SHA256", "a+b/c=");
        var baseStringBuilder = new StandardBaseStringBuilder();
        var builder = new OAuthHeaderBuilder(signer, baseStringBuilder);

        var header = builder.Build("GET", "https://example.com", "KEY", "TOK");

        var encodedSig = OAuthEncoding.QuotePlus("a+b/c=");
        header.ShouldContain($"oauth_signature=\"{encodedSig}\"");
    }

    /// <summary>
    /// Test double that returns a fixed signature, bypassing real crypto.
    /// </summary>
    private class FakeSigner : IOAuthSigner
    {
        private readonly string _signature;

        public FakeSigner(string method, string signature)
        {
            SignatureMethod = method;
            _signature = signature;
        }

        public string SignatureMethod { get; }

        public string Sign(string baseString) => _signature;
    }
}
