using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace IbkrConduit.Serialization;

/// <summary>
/// Shared <see cref="RefitSettings"/> used by every Refit client the library registers. Builds
/// on Refit's own default <see cref="JsonSerializerOptions"/> — so Refit's web defaults
/// (camelCase naming, case-insensitive matching, <see cref="JsonNumberHandling.AllowReadingFromString"/>,
/// and its <c>object</c>/enum converters) are preserved exactly and nothing regresses — and adds
/// the empty-tolerant numeric converters (<see cref="EmptyTolerantDecimalConverter"/> and friends).
/// Those converters subsume <see cref="JsonNumberHandling.AllowReadingFromString"/> and further make
/// a numeric field IBKR sends as <c>""</c> or whitespace deserialize to <c>null</c> (nullable) or
/// <c>0</c> (non-nullable) instead of throwing a <see cref="JsonException"/> mid-response.
/// </summary>
internal static class IbkrRefitSettings
{
    /// <summary>
    /// The JSON options applied to every Refit client: Refit's defaults plus the empty-tolerant
    /// numeric converters. A type-level converter here takes precedence over any property-level
    /// <c>[JsonNumberHandling(AllowReadingFromString)]</c> on the REST models, so those annotations
    /// become harmlessly redundant.
    /// </summary>
    public static readonly JsonSerializerOptions Options = BuildOptions();

    /// <summary>
    /// Creates a fresh <see cref="RefitSettings"/> wrapping the shared <see cref="Options"/> and the
    /// <see cref="IbkrUrlParameterFormatter"/> (lowercase-bool query values, matching IBKR's documented
    /// wire format — e.g. the live-orders <c>force=true</c> cache-clear, §10.6).
    /// </summary>
    public static RefitSettings Create() =>
        new()
        {
            ContentSerializer = new SystemTextJsonContentSerializer(Options),
            UrlParameterFormatter = new IbkrUrlParameterFormatter(),
        };

    private static JsonSerializerOptions BuildOptions()
    {
        // Start from Refit's actual defaults (copy constructor keeps the instance mutable and
        // preserves ObjectToInferredTypesConverter + CamelCaseStringEnumConverter), then layer the
        // empty-tolerant numeric converters on top.
        var options = new JsonSerializerOptions(SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions());
        options.Converters.Add(new EmptyTolerantDecimalConverter());
        options.Converters.Add(new EmptyTolerantNullableDecimalConverter());
        options.Converters.Add(new EmptyTolerantIntConverter());
        options.Converters.Add(new EmptyTolerantNullableIntConverter());
        options.Converters.Add(new EmptyTolerantLongConverter());
        options.Converters.Add(new EmptyTolerantNullableLongConverter());
        options.Converters.Add(new EmptyTolerantDoubleConverter());
        options.Converters.Add(new EmptyTolerantNullableDoubleConverter());
        return options;
    }
}
