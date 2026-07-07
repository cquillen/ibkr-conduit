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

    [Fact]
    public void Map_FrameWithCompetingTrue_SurfacesCompeting()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sts","args":{"authenticated":false,"competing":true,"fail":""}}""").RootElement;

        var evt = SessionStatusMapper.Map(frame);

        evt.Competing.ShouldBe(true);
        evt.Authenticated.ShouldBe(false);
    }

    [Fact]
    public void Map_FrameWithCompetingFalse_MapsCompetingFalse()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sts","args":{"authenticated":true,"competing":false}}""").RootElement;

        SessionStatusMapper.Map(frame).Competing.ShouldBe(false);
    }

    [Fact]
    public void Map_MissingCompetingProperty_CompetingIsNull()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":true}}""").RootElement;

        SessionStatusMapper.Map(frame).Competing.ShouldBeNull();
    }

    [Fact]
    public void Map_FrameWithFailReason_SurfacesFailReason()
    {
        var frame = JsonDocument.Parse(
            """{"topic":"sts","args":{"authenticated":false,"fail":"Competing session"}}""").RootElement;

        SessionStatusMapper.Map(frame).FailReason.ShouldBe("Competing session");
    }

    [Fact]
    public void Map_MissingFailProperty_FailReasonIsNull()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":true}}""").RootElement;

        SessionStatusMapper.Map(frame).FailReason.ShouldBeNull();
    }

    [Fact]
    public void Map_StringEncodedAuthenticatedFalse_MapsToFalse()
    {
        // GAP2-1: IBKR type-drifts boolean-ish flags to strings (FlexibleBoolJsonConverter exists
        // for exactly this). A string-encoded session-death frame must still surface Authenticated
        // == false, not be dropped or fabricated into null.
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":"false"}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(false);
    }

    [Fact]
    public void Map_StringEncodedAuthenticatedTrue_MapsToTrue()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":"true"}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(true);
    }

    [Fact]
    public void Map_ZeroStringAuthenticated_MapsToFalse()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":"0"}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(false);
    }

    [Fact]
    public void Map_OneStringAuthenticated_MapsToTrue()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":"1"}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(true);
    }

    [Fact]
    public void Map_NumericZeroAuthenticated_MapsToFalse()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":0}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(false);
    }

    [Fact]
    public void Map_NumericOneAuthenticated_MapsToTrue()
    {
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":1}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBe(true);
    }

    [Fact]
    public void Map_UnrecognizedAuthenticatedValue_MapsToNullNotThrow()
    {
        // A genuinely unparseable value must not throw (which would drop the whole sts frame,
        // re-opening GAP2-1); absence-of-verdict maps to null per ADR-0001.
        var frame = JsonDocument.Parse("""{"topic":"sts","args":{"authenticated":"maybe"}}""").RootElement;

        SessionStatusMapper.Map(frame).Authenticated.ShouldBeNull();
    }
}
