using System.Text.Json;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionNewEndpointModelsTests
{
    [Fact]
    public void SuppressResetResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"status":"submitted"}""";

        var response = JsonSerializer.Deserialize<SuppressResetResponse>(json);

        response.ShouldNotBeNull();
        response.Status.ShouldBe("submitted");
    }

    [Fact]
    public void SuppressResetResponse_Deserializes_WithExtensionData()
    {
        var json = """{"status":"submitted","extra":"value"}""";

        var response = JsonSerializer.Deserialize<SuppressResetResponse>(json);

        response.ShouldNotBeNull();
        response.Status.ShouldBe("submitted");
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("extra");
    }

    [Fact]
    public void AuthStatusResponse_Deserializes_FullResponse()
    {
        var json = """
        {
            "authenticated": true,
            "competing": false,
            "connected": true,
            "fail": null,
            "message": "all good",
            "prompts": ["prompt1"]
        }
        """;

        var response = JsonSerializer.Deserialize<AuthStatusResponse>(json);

        response.ShouldNotBeNull();
        response.Authenticated.ShouldBeTrue();
        response.Competing.ShouldBeFalse();
        response.Connected.ShouldBeTrue();
        response.Fail.ShouldBeNull();
        response.Message.ShouldBe("all good");
        response.Prompts.ShouldNotBeNull();
        response.Prompts!.Count.ShouldBe(1);
        response.Prompts[0].ShouldBe("prompt1");
    }

    [Fact]
    public void AuthStatusResponse_Deserializes_MinimalResponse()
    {
        var json = """{"authenticated":false,"competing":false,"connected":false,"fail":"not authenticated","message":null,"prompts":null}""";

        var response = JsonSerializer.Deserialize<AuthStatusResponse>(json);

        response.ShouldNotBeNull();
        response.Authenticated.ShouldBeFalse();
        response.Fail.ShouldBe("not authenticated");
    }

    [Fact]
    public void ReauthenticateResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"message":"triggered"}""";

        var response = JsonSerializer.Deserialize<ReauthenticateResponse>(json);

        response.ShouldNotBeNull();
        response.Message.ShouldBe("triggered");
    }

    [Fact]
    public void ReauthenticateResponse_Deserializes_WithExtensionData()
    {
        var json = """{"message":"triggered","extra_field":123}""";

        var response = JsonSerializer.Deserialize<ReauthenticateResponse>(json);

        response.ShouldNotBeNull();
        response.Message.ShouldBe("triggered");
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("extra_field");
    }

    [Fact]
    public void SsoValidateResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"USER_ID":12345,"expire":1700000000,"RESULT":true,"AUTH_TIME":1699990000}""";

        var response = JsonSerializer.Deserialize<SsoValidateResponse>(json);

        response.ShouldNotBeNull();
        response.UserId.ShouldBe(12345);
        response.Expire.ShouldBe(1700000000);
        response.Result.ShouldBeTrue();
        response.AuthTime.ShouldBe(1699990000);
    }

    [Fact]
    public void SsoValidateResponse_Deserializes_WithExtensionData()
    {
        var json = """{"USER_ID":12345,"expire":0,"RESULT":true,"AUTH_TIME":0,"IP":"1.2.3.4"}""";

        var response = JsonSerializer.Deserialize<SsoValidateResponse>(json);

        response.ShouldNotBeNull();
        response.UserId.ShouldBe(12345);
        response.AdditionalData.ShouldNotBeNull();
        response.AdditionalData.ShouldContainKey("IP");
    }
}
