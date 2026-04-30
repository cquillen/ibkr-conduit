using System.Collections.Generic;
using System.Text.Json;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionModelsTests
{
    [Fact]
    public void SsodhInitRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new SsodhInitRequest(Publish: true, Compete: true);

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"publish\":true");
        json.ShouldContain("\"compete\":true");
    }

    [Fact]
    public void SsodhInitResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"authenticated":true,"connected":true,"competing":false}""";

        var response = JsonSerializer.Deserialize<SsodhInitResponse>(json);

        response.ShouldNotBeNull();
        response.Authenticated.ShouldBeTrue();
        response.Connected.ShouldBeTrue();
        response.Competing.ShouldBeFalse();
    }

    [Fact]
    public void TickleResponse_Deserializes_NestedAuthStatus()
    {
        var json = """
        {
            "session": "abc123",
            "iserver": {
                "authStatus": {
                    "authenticated": true,
                    "competing": false,
                    "connected": true
                }
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TickleResponse>(json);

        response.ShouldNotBeNull();
        response.Session.ShouldBe("abc123");
        response.Iserver.ShouldNotBeNull();
        response.Iserver!.AuthStatus.ShouldNotBeNull();
        response.Iserver.AuthStatus!.Authenticated.ShouldBeTrue();
        response.Iserver.AuthStatus.Competing.ShouldBeFalse();
        response.Iserver.AuthStatus.Connected.ShouldBeTrue();
    }

    [Fact]
    public void TickleResponse_Deserializes_WithNullIserver()
    {
        var json = """{"session":"abc123"}""";

        var response = JsonSerializer.Deserialize<TickleResponse>(json);

        response.ShouldNotBeNull();
        response.Session.ShouldBe("abc123");
        response.Iserver.ShouldBeNull();
    }

    [Fact]
    public void SuppressRequest_Serializes_CorrectJsonPropertyNames()
    {
        var request = new SuppressRequest(MessageIds: new List<string> { "o163", "o451" });

        var json = JsonSerializer.Serialize(request);

        json.ShouldContain("\"messageIds\"");
        json.ShouldContain("\"o163\"");
        json.ShouldContain("\"o451\"");
    }

    [Fact]
    public void SuppressResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"status":"submitted"}""";

        var response = JsonSerializer.Deserialize<SuppressResponse>(json);

        response.ShouldNotBeNull();
        response.Status.ShouldBe("submitted");
    }

    [Fact]
    public void LogoutResponse_Deserializes_FromJsonCorrectly()
    {
        var json = """{"confirmed":true}""";

        var response = JsonSerializer.Deserialize<LogoutResponse>(json);

        response.ShouldNotBeNull();
        response.Confirmed.ShouldBeTrue();
    }

    [Fact]
    public void IbkrClientOptions_DefaultValues_AreCorrect()
    {
        var options = new IbkrClientOptions();

        options.Compete.ShouldBeTrue();
        options.SuppressMessageIds.ShouldBeEmpty();
        options.WebSocketHeartbeatIntervalSeconds.ShouldBe(30);
        options.StreamingBufferSize.ShouldBe(256);
        options.TickleFailureIntervalSeconds.ShouldBe(5);
    }

    [Fact]
    public void IbkrClientOptions_CustomValues_ArePreserved()
    {
        var options = new IbkrClientOptions
        {
            Compete = false,
            SuppressMessageIds = new List<string> { "o163" },
        };

        options.Compete.ShouldBeFalse();
        options.SuppressMessageIds.Count.ShouldBe(1);
        options.SuppressMessageIds[0].ShouldBe("o163");
    }
}
