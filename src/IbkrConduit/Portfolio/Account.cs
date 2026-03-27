using System.Text.Json.Serialization;

namespace IbkrConduit.Portfolio;

/// <summary>
/// Represents an IBKR account from the /portfolio/accounts endpoint.
/// </summary>
public class Account
{
    /// <summary>
    /// The account identifier (e.g., "U1234567").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The account title/description.
    /// </summary>
    [JsonPropertyName("accountTitle")]
    public string AccountTitle { get; init; } = string.Empty;

    /// <summary>
    /// The account type (e.g., "INDIVIDUAL").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}
