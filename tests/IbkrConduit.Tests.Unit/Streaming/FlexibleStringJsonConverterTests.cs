using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class FlexibleStringJsonConverterTests
{
    [Fact]
    public void Read_IntegerNumberToken_ReturnsInvariantString()
    {
        var converter = new FlexibleStringJsonConverter();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("656804954"));
        reader.Read();

        var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());

        result.ShouldBe("656804954");
    }

    [Fact]
    public void Read_NonIntegerNumberToken_FallsBackToRawText()
    {
        var converter = new FlexibleStringJsonConverter();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("123.45"));
        reader.Read();

        var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());

        result.ShouldBe("123.45");
    }

    [Fact]
    public void Read_StringToken_ReturnsString()
    {
        var converter = new FlexibleStringJsonConverter();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("\"656804954\""));
        reader.Read();

        var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());

        result.ShouldBe("656804954");
    }

    [Fact]
    public void Read_NullToken_ReturnsNull()
    {
        var converter = new FlexibleStringJsonConverter();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("null"));
        reader.Read();

        var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());

        result.ShouldBeNull();
    }

    [Fact]
    public void Write_WritesJsonString()
    {
        var converter = new FlexibleStringJsonConverter();
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            converter.Write(writer, "656804954", new JsonSerializerOptions());
        }

        Encoding.UTF8.GetString(stream.ToArray()).ShouldBe("\"656804954\"");
    }

    private sealed record Wrapper
    {
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Value { get; init; } = string.Empty;
    }

    [Fact]
    public void Deserialize_NumericOrderIdField_RoundTripsThroughWrapper()
    {
        var wrapper = JsonSerializer.Deserialize<Wrapper>("""{"Value":656804954}""");

        wrapper.ShouldNotBeNull();
        wrapper!.Value.ShouldBe("656804954");
    }

    [Fact]
    public void Deserialize_StringOrderIdField_RoundTripsThroughWrapper()
    {
        var wrapper = JsonSerializer.Deserialize<Wrapper>("""{"Value":"656804954"}""");

        wrapper.ShouldNotBeNull();
        wrapper!.Value.ShouldBe("656804954");
    }
}
