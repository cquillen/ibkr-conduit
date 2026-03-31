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

    /// <summary>
    /// How long a conid stays in the pre-flight cache before a fresh pre-flight is required.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan PreflightCacheDuration { get; init; } = TimeSpan.FromMinutes(5);
}
