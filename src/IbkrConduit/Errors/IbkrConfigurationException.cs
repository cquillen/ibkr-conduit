using System;

namespace IbkrConduit.Errors;

/// <summary>
/// Thrown when session initialization fails due to misconfigured credentials or options.
/// Not an API error — this indicates a configuration problem on the consumer side.
/// </summary>
public class IbkrConfigurationException : Exception
{
    /// <summary>Suggests which credential or option field to check.</summary>
    public string? CredentialHint { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrConfigurationException"/> wrapping an inner exception.
    /// </summary>
    /// <param name="message">A friendly, actionable error message.</param>
    /// <param name="credentialHint">The credential or option field name(s) to check.</param>
    /// <param name="innerException">The original exception from the failed operation.</param>
    public IbkrConfigurationException(string message, string? credentialHint, Exception innerException)
        : base(message, innerException)
    {
        CredentialHint = credentialHint;
    }

    /// <summary>
    /// Creates a new <see cref="IbkrConfigurationException"/> without an inner exception.
    /// </summary>
    /// <param name="message">A friendly, actionable error message.</param>
    /// <param name="credentialHint">The credential or option field name(s) to check.</param>
    public IbkrConfigurationException(string message, string? credentialHint)
        : base(message)
    {
        CredentialHint = credentialHint;
    }
}
