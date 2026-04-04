using System;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrConfigurationExceptionTests
{
    [Fact]
    public void Constructor_WithInnerException_CarriesAllProperties()
    {
        var inner = new InvalidOperationException("original");

        var ex = new IbkrConfigurationException(
            "Friendly message", "EncryptionPrivateKey", inner);

        ex.Message.ShouldBe("Friendly message");
        ex.CredentialHint.ShouldBe("EncryptionPrivateKey");
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithoutInnerException_CarriesMessageAndHint()
    {
        var ex = new IbkrConfigurationException(
            "Check config", "ConsumerKey, AccessToken");

        ex.Message.ShouldBe("Check config");
        ex.CredentialHint.ShouldBe("ConsumerKey, AccessToken");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NullHint_Allowed()
    {
        var ex = new IbkrConfigurationException("msg", null, new Exception("x"));

        ex.CredentialHint.ShouldBeNull();
    }

    [Fact]
    public void IsNotIbkrApiException()
    {
        var ex = new IbkrConfigurationException("msg", "hint");

        ex.ShouldNotBeAssignableTo<IbkrApiException>();
        ex.ShouldBeAssignableTo<Exception>();
    }
}
