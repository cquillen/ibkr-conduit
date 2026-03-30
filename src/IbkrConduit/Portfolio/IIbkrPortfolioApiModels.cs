using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Represents an IBKR account from the /portfolio/accounts endpoint.
/// </summary>
/// <param name="Id">The account identifier (e.g., "U1234567").</param>
/// <param name="AccountTitle">The account title/description.</param>
/// <param name="Type">The account type (e.g., "INDIVIDUAL").</param>
public record Account(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("accountTitle")] string AccountTitle,
    [property: JsonPropertyName("type")] string Type);
