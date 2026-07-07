using System.Text.Json;
using IbkrConduit.Streaming.Mappers;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

/// <summary>
/// Pins <see cref="SessionStatusMapper"/>'s presence semantics (GAP2-2): an <c>sts</c> frame
/// that carries no authentication verdict must map to <c>Authenticated == null</c>, never a
/// fabricated <c>false</c> that a consumer would read as a real "session dead" signal.
/// </summary>
public class SessionStatusMapperTests
{
    [Fact]
    public void Map_AuthenticatedTrue_MapsToTrue()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":true}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(true);
    }

    [Fact]
    public void Map_AuthenticatedFalse_MapsToFalse()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":false}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(false);
    }

    [Fact]
    public void Map_MissingAuthenticatedProperty_AuthenticatedIsNull()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBeNull();
    }

    [Fact]
    public void Map_MissingArgs_DoesNotFabricateUnauthenticatedEvent()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts"}""").RootElement;

        // Absence must not be reported as an explicit "not authenticated" verdict.
        SessionStatusMapper.Map(frame).Authenticated.ShouldBeNull();
    }
}
