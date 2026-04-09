namespace IbkrConduit.Flex;

/// <summary>
/// Exception thrown when a Flex Web Service query returns an error response.
/// </summary>
public class FlexQueryException : Exception
{
    /// <summary>
    /// The IBKR Flex error code from the XML response.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Whether this error is documented as retryable (transient) or permanent.
    /// Populated from the known IBKR Flex error code table. Unknown codes
    /// default to <c>false</c> as a conservative safety measure — callers
    /// should not blindly retry errors the library doesn't recognize.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// The canonical description for this error code from IBKR's documentation,
    /// or <c>null</c> if the code is not in the known table. This is separate
    /// from <see cref="Exception.Message"/>, which carries whatever the server
    /// returned at runtime (typically the same text, but may differ).
    /// </summary>
    public string? CodeDescription { get; }

    /// <summary>
    /// Creates a new <see cref="FlexQueryException"/> with the specified error code and message.
    /// <see cref="IsRetryable"/> and <see cref="CodeDescription"/> are populated automatically
    /// from the known error code table.
    /// </summary>
    /// <param name="errorCode">The IBKR Flex error code.</param>
    /// <param name="message">The error message from the Flex response.</param>
    public FlexQueryException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
        var info = FlexErrorCodes.TryLookup(errorCode);
        IsRetryable = info?.IsRetryable ?? false;
        CodeDescription = info?.Description;
    }
}
