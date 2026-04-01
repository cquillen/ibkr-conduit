using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Flex;

/// <summary>
/// Exception thrown when a Flex Web Service query returns an error response.
/// </summary>
[ExcludeFromCodeCoverage]
public class FlexQueryException : Exception
{
    /// <summary>
    /// The IBKR Flex error code from the XML response.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Creates a new <see cref="FlexQueryException"/> with the specified error code and message.
    /// </summary>
    /// <param name="errorCode">The IBKR Flex error code.</param>
    /// <param name="message">The error message from the Flex response.</param>
    public FlexQueryException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
