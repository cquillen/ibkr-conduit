namespace IbkrConduit.Session;

/// <summary>
/// Configuration options for the IBKR client session behavior.
/// </summary>
public record IbkrClientOptions
{
    /// <summary>
    /// Whether to compete with existing sessions when initializing.
    /// Default is <c>true</c>.
    /// </summary>
    public bool Compete { get; init; } = true;

    /// <summary>
    /// List of question message IDs to suppress after session initialization.
    /// </summary>
    public List<string> SuppressMessageIds { get; init; } = new();
}
