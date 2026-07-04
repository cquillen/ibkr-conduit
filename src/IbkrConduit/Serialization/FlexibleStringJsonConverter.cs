using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Serialization;

/// <summary>
/// A <see cref="JsonConverter{T}"/> that reads a JSON string, number, or null into a
/// <see cref="string"/>. IBKR's frames are inconsistent about whether numeric-looking
/// identifiers (e.g. <c>orderId</c>) are quoted, so a field typed as <see cref="string"/> in a
/// DTO may arrive as either a JSON string or a JSON number. Numbers that fit in a
/// <see cref="long"/> are converted via <see cref="CultureInfo.InvariantCulture"/>; other
/// numbers fall back to their raw JSON text. Writes are always emitted as a JSON string.
/// </summary>
internal sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(ref reader),
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to string."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    private static string ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        if (!reader.HasValueSequence)
        {
            return Encoding.UTF8.GetString(reader.ValueSpan);
        }

        var sequence = reader.ValueSequence;
        var bytes = new byte[sequence.Length];
        sequence.CopyTo(bytes);
        return Encoding.UTF8.GetString(bytes);
    }
}
