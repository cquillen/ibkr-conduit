using System;

namespace IbkrConduit.Errors;

/// <summary>
/// Indicates a transient backend or network failure during session operations.
/// The right response is to wait and retry; no configuration change is required.
/// Distinct from <see cref="IbkrConfigurationException"/>, which signals that
/// the consumer needs to fix credentials or options before any retry can succeed.
/// </summary>
public class IbkrTransientException : Exception
{
    /// <summary>
    /// Creates a new <see cref="IbkrTransientException"/> wrapping an inner exception.
    /// </summary>
    /// <param name="message">A description of the transient failure.</param>
    /// <param name="innerException">The original exception from the failed operation.</param>
    public IbkrTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new <see cref="IbkrTransientException"/> without an inner exception.
    /// </summary>
    /// <param name="message">A description of the transient failure.</param>
    public IbkrTransientException(string message)
        : base(message)
    {
    }
}
