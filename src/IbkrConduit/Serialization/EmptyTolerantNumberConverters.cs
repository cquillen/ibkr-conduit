using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IbkrConduit.Serialization;

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="decimal"/> that tolerates IBKR's habit of
/// sending empty-string numeric fields — on both the streaming WebSocket frames (e.g.
/// <c>"price":""</c> for market orders on the <c>sor</c> topic, which has no limit price) and
/// the REST API. A JSON number deserializes normally; an empty or whitespace-only JSON string
/// deserializes to <c>0</c>; any other JSON string is parsed as a culture-invariant decimal
/// (also covers IBKR's occasional quoted numbers, e.g. <c>"150.25"</c>, subsuming
/// <see cref="JsonNumberHandling.AllowReadingFromString"/>).
/// </summary>
internal sealed class EmptyTolerantDecimalConverter : JsonConverter<decimal>
{
    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDecimal(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseDecimalOrNull(reader.GetString()) ?? default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to decimal."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Nullable-<see cref="decimal"/> counterpart of <see cref="EmptyTolerantDecimalConverter"/>.
/// A JSON number deserializes normally; an empty/whitespace JSON string or JSON <c>null</c>
/// both deserialize to <c>null</c>; any other JSON string is parsed as a culture-invariant decimal.
/// </summary>
internal sealed class EmptyTolerantNullableDecimalConverter : JsonConverter<decimal?>
{
    /// <inheritdoc />
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDecimal(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseDecimalOrNull(reader.GetString()),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to decimal?."),
        };

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

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="int"/> that tolerates empty-string numeric
/// fields the same way <see cref="EmptyTolerantDecimalConverter"/> does for <see cref="decimal"/>.
/// </summary>
internal sealed class EmptyTolerantIntConverter : JsonConverter<int>
{
    /// <inheritdoc />
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseIntOrNull(reader.GetString()) ?? default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to int."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Nullable-<see cref="int"/> counterpart of <see cref="EmptyTolerantIntConverter"/>.
/// </summary>
internal sealed class EmptyTolerantNullableIntConverter : JsonConverter<int?>
{
    /// <inheritdoc />
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseIntOrNull(reader.GetString()),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to int?."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
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

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="long"/> that tolerates empty-string numeric
/// fields the same way <see cref="EmptyTolerantDecimalConverter"/> does for <see cref="decimal"/>.
/// </summary>
internal sealed class EmptyTolerantLongConverter : JsonConverter<long>
{
    /// <inheritdoc />
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseLongOrNull(reader.GetString()) ?? default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to long."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Nullable-<see cref="long"/> counterpart of <see cref="EmptyTolerantLongConverter"/>.
/// </summary>
internal sealed class EmptyTolerantNullableLongConverter : JsonConverter<long?>
{
    /// <inheritdoc />
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseLongOrNull(reader.GetString()),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to long?."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
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

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="double"/> that tolerates empty-string numeric
/// fields the same way <see cref="EmptyTolerantDecimalConverter"/> does for <see cref="decimal"/>.
/// </summary>
internal sealed class EmptyTolerantDoubleConverter : JsonConverter<double>
{
    /// <inheritdoc />
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseDoubleOrNull(reader.GetString()) ?? default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to double."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Nullable-<see cref="double"/> counterpart of <see cref="EmptyTolerantDoubleConverter"/>.
/// </summary>
internal sealed class EmptyTolerantNullableDoubleConverter : JsonConverter<double?>
{
    /// <inheritdoc />
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String => EmptyTolerantNumberParsing.ParseDoubleOrNull(reader.GetString()),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to double?."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
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

/// <summary>
/// Shared string-parsing helpers for the empty-tolerant numeric converters. An empty or
/// whitespace-only string parses to <c>null</c> ("no value was sent"); any other string is
/// parsed as a culture-invariant number.
/// </summary>
internal static class EmptyTolerantNumberParsing
{
    // Consistent styles across all four helpers. IBKR never emits thousands separators in JSON,
    // so plain float/integer styles suffice; keeping them uniform avoids one helper silently
    // accepting a shape the others reject.
    private const NumberStyles _floatStyles = NumberStyles.Float;
    private const NumberStyles _integerStyles = NumberStyles.Integer;

    /// <summary>Parses a string to <see cref="decimal"/>, or <c>null</c> if empty/whitespace.</summary>
    public static decimal? ParseDecimalOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : Parse(text, static (t, c) => decimal.Parse(t, _floatStyles, c), nameof(Decimal));

    /// <summary>Parses a string to <see cref="int"/>, or <c>null</c> if empty/whitespace.</summary>
    public static int? ParseIntOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : Parse(text, static (t, c) => int.Parse(t, _integerStyles, c), nameof(Int32));

    /// <summary>Parses a string to <see cref="long"/>, or <c>null</c> if empty/whitespace.</summary>
    public static long? ParseLongOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : Parse(text, static (t, c) => long.Parse(t, _integerStyles, c), nameof(Int64));

    /// <summary>Parses a string to <see cref="double"/>, or <c>null</c> if empty/whitespace.</summary>
    public static double? ParseDoubleOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : Parse(text, static (t, c) => double.Parse(t, _floatStyles, c), nameof(Double));

    /// <summary>
    /// Runs <paramref name="parse"/> and rethrows any <see cref="FormatException"/> or
    /// <see cref="OverflowException"/> as a <see cref="JsonException"/> that names the offending
    /// value and target type — preserving the diagnostic context the
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/> path used to give for genuine
    /// garbage (empty/whitespace is already handled by the callers and never reaches here).
    /// </summary>
    private static T Parse<T>(string text, Func<string, CultureInfo, T> parse, string targetTypeName)
    {
        try
        {
            return parse(text, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new JsonException($"Could not parse '{text}' as {targetTypeName}.", ex);
        }
    }
}
