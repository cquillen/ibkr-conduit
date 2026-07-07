using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Portfolio;

namespace IbkrConduit.Serialization;

/// <summary>
/// Normalizes IBKR's two documented shapes for <c>GET /portfolio/subaccounts2</c> into a single
/// <see cref="SubAccountsPage"/>. IBKR's own live documentation is self-inconsistent for this path —
/// DOC-01's schema and DOC-03's prose claim an object wrapper <c>{metadata, subaccounts}</c>, while
/// DOC-03's own example and the sole wire sample (a paper/non-FA account) return a bare array of
/// sub-accounts (<c>docs/ibkr-doc-evidence/2026-07-07-subaccounts2-response-shape.md</c>). Rather than
/// pick one, this converter tolerates both:
/// <list type="bullet">
/// <item>a bare JSON array → the array becomes <see cref="SubAccountsPage.Subaccounts"/> and
/// <see cref="SubAccountsPage.Metadata"/> is <see langword="null"/> (no page metadata is present);</item>
/// <item>an object wrapper → its <c>metadata</c> and <c>subaccounts</c> members populate the DTO;</item>
/// <item>a JSON <c>null</c> body → an empty page with null metadata.</item>
/// </list>
/// </summary>
internal sealed class SubAccountsPageJsonConverter : JsonConverter<SubAccountsPage>
{
    /// <summary>
    /// Opt into handling a JSON <c>null</c> body so a null response deserializes to an empty page
    /// rather than a null <see cref="SubAccountsPage"/> (System.Text.Json short-circuits null for
    /// reference types unless a converter claims it).
    /// </summary>
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override SubAccountsPage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return new SubAccountsPage(null, []);

            case JsonTokenType.StartArray:
                // Bare-array form: the whole payload is the sub-account list; no page metadata exists.
                var bare = JsonSerializer.Deserialize<List<SubAccount>>(ref reader, options) ?? [];
                return new SubAccountsPage(null, bare);

            case JsonTokenType.StartObject:
                return ReadWrapper(ref reader, options);

            default:
                throw new JsonException(
                    $"Cannot convert token type {reader.TokenType} to {nameof(SubAccountsPage)}; expected an array or object.");
        }
    }

    private static SubAccountsPage ReadWrapper(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        SubAccountsPageMetadata? metadata = null;
        List<SubAccount> subaccounts = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, "metadata", StringComparison.OrdinalIgnoreCase))
            {
                metadata = JsonSerializer.Deserialize<SubAccountsPageMetadata>(ref reader, options);
            }
            else if (string.Equals(propertyName, "subaccounts", StringComparison.OrdinalIgnoreCase))
            {
                subaccounts = JsonSerializer.Deserialize<List<SubAccount>>(ref reader, options) ?? [];
            }
            else
            {
                reader.Skip();
            }
        }

        return new SubAccountsPage(metadata, subaccounts);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SubAccountsPage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("metadata");
        JsonSerializer.Serialize(writer, value.Metadata, options);
        writer.WritePropertyName("subaccounts");
        JsonSerializer.Serialize(writer, value.Subaccounts, options);
        writer.WriteEndObject();
    }
}
