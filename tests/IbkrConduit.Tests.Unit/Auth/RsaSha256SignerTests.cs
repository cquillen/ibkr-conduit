using System;
using System.Security.Cryptography;
using System.Text;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class RsaSha256SignerTests
{
    [Fact]
    public void SignatureMethod_ReturnsRsaSha256()
    {
        using var rsa = RSA.Create(2048);
        var signer = new RsaSha256Signer(rsa);
        signer.SignatureMethod.ShouldBe("RSA-SHA256");
    }

    [Fact]
    public void Sign_ReturnsBase64EncodedSignature()
    {
        using var rsa = RSA.Create(2048);
        var signer = new RsaSha256Signer(rsa);
        var baseString = "test base string";

        var signature = signer.Sign(baseString);

        var bytes = Convert.FromBase64String(signature);
        bytes.Length.ShouldBeGreaterThan(0);

        var data = Encoding.UTF8.GetBytes(baseString);
        rsa.VerifyData(data, bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).ShouldBeTrue();
    }
}
