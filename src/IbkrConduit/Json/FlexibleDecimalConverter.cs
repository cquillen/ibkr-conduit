using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Json;

/// <summary>
/// Reads a JSON value as a nullable decimal, handling IBKR's inconsistent encoding:
/// JSON numbers, string-encoded numbers, empty strings, and null.
/// </summary>
public class FlexibleDecimalConverter : JsonConverter<decimal?>
{
    /// <inheritdoc />
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return null;
                }

                if (decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                return null;
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Cannot convert {reader.TokenType} to decimal.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
