using System.Text.Json;
using IbkrConduit.Serialization;

namespace IbkrConduit.Streaming;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used by every streaming DTO's
/// <c>JsonElement.Deserialize&lt;T&gt;()</c> call. Registers the empty-tolerant numeric
/// converters (<see cref="EmptyTolerantDecimalConverter"/> and friends) so a numeric field
/// that IBKR sends as <c>""</c> — e.g. <c>"price":""</c> on market orders, which have no
/// limit price — deserializes to <c>null</c>/<c>0</c> instead of throwing a
/// <see cref="JsonException"/> that would otherwise tear down the whole subscription.
/// </summary>
internal static class StreamingSerialization
{
    /// <summary>Options for deserializing WebSocket streaming frames.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new EmptyTolerantDecimalConverter(),
            new EmptyTolerantNullableDecimalConverter(),
            new EmptyTolerantIntConverter(),
            new EmptyTolerantNullableIntConverter(),
            new EmptyTolerantLongConverter(),
            new EmptyTolerantNullableLongConverter(),
            new EmptyTolerantDoubleConverter(),
            new EmptyTolerantNullableDoubleConverter(),
        },
    };
}
