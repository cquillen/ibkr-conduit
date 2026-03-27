using System;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class HmacSha256SignerTests
{
    [Fact]
    public void SignatureMethod_ReturnsHmacSha256()
    {
        var signer = new HmacSha256Signer(new byte[] { 0x01 });
        signer.SignatureMethod.ShouldBe("HMAC-SHA256");
    }

    [Fact]
    public void Sign_ReturnsBase64EncodedHmac()
    {
        var key = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var signer = new HmacSha256Signer(key);
        var baseString = "test base string";

        var signature = signer.Sign(baseString);

        using var hmac = new HMACSHA256(key);
        var expected = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString)));
        signature.ShouldBe(expected);
    }
}
