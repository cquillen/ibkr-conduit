using System;
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Http;

/// <summary>
/// Thrown when a rate limiter queue is full and cannot accept more requests.
/// </summary>
[ExcludeFromCodeCoverage]
public class RateLimitRejectedException : Exception
{
    /// <summary>
    /// Creates a new instance with the specified message.
    /// </summary>
    /// <param name="message">A description of the rate limit rejection.</param>
    public RateLimitRejectedException(string message) : base(message) { }

    /// <summary>
    /// Creates a new instance with the specified message and inner exception.
    /// </summary>
    /// <param name="message">A description of the rate limit rejection.</param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public RateLimitRejectedException(string message, Exception innerException)
        : base(message, innerException) { }
}
