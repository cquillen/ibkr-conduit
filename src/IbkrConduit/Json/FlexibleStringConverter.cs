using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Json;

/// <summary>
/// Reads a JSON value as a string regardless of whether IBKR sends it as a
/// JSON string or a JSON number. This handles the common IBKR API inconsistency
/// where fields like order IDs alternate between string and number encoding.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number when reader.TryGetDouble(out var d) => d.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
