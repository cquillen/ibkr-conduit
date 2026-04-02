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

}
