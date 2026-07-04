using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Serialization;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Covers <see cref="FlexibleBoolJsonConverter"/> — the converter that reads IBKR's several
/// boolean-ish shapes ("0"/"1", real JSON booleans, 0/1, "true"/"false") into a nullable bool.
/// </summary>
public class FlexibleBoolJsonConverterTests
{
    private sealed record Model
    {
        [JsonConverter(typeof(FlexibleBoolJsonConverter))]
        public bool? Flag { get; init; }
    }

    private static bool? Read(string flagJson) =>
        JsonSerializer.Deserialize<Model>($$"""{"Flag":{{flagJson}}}""")!.Flag;

    [Theory]
    [InlineData("\"1\"", true)]
    [InlineData("\"0\"", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"False\"", false)]  // case-insensitive
    public void Read_RecognizedValues_ParseToBool(string flagJson, bool expected) =>
        Read(flagJson).ShouldBe(expected);

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void Read_EmptyWhitespaceOrNull_MapsToNull(string flagJson) =>
        Read(flagJson).ShouldBeNull();

    [Fact]
    public void Read_UnrecognizedString_ThrowsJsonException() =>
        Should.Throw<JsonException>(() => Read("\"maybe\""));

    [Fact]
    public void Write_EmitsJsonBoolean()
    {
        var json = JsonSerializer.Serialize(new Model { Flag = true });

        json.ShouldBe("""{"Flag":true}""");
    }

    [Fact]
    public void Write_Null_EmitsJsonNull()
    {
        var json = JsonSerializer.Serialize(new Model { Flag = null });

        json.ShouldBe("""{"Flag":null}""");
    }
}
