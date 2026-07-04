using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Serialization;

/// <summary>
/// Reads a nullable boolean from the several shapes IBKR uses for boolean-ish flags: a real
/// JSON boolean (<c>true</c>/<c>false</c>), a quoted <c>"0"</c>/<c>"1"</c> (its most common
/// form on trade/execution records), a bare number (<c>0</c>/<c>1</c>), or the string forms
/// <c>"true"</c>/<c>"false"</c> (case-insensitive). Empty/whitespace strings and JSON null map
/// to <see langword="null"/>. Genuinely unrecognized values throw a <see cref="JsonException"/>
/// so real wire surprises stay loud. Writes emit a JSON boolean.
/// </summary>
internal sealed class FlexibleBoolJsonConverter : JsonConverter<bool?>
{
    /// <inheritdoc />
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.TryGetInt64(out var n)
                ? n != 0
                : throw new JsonException("Cannot convert a non-integer number to bool."),
            JsonTokenType.String => ParseString(reader.GetString()),
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to bool."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteBooleanValue(value.Value);
        }
    }

    private static bool? ParseString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            var other => throw new JsonException($"Cannot convert \"{other}\" to bool."),
        };
    }
}
