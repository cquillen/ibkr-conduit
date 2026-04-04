namespace IbkrConduit.Errors;

/// <summary>
/// Exception wrapping an <see cref="IbkrError"/>. Thrown by <see cref="Result{T}.EnsureSuccess"/>
/// and when <see cref="IbkrConduit.Session.IbkrClientOptions.ThrowOnApiError"/> is enabled.
/// Use pattern matching on <see cref="Error"/> to discriminate error subtypes.
/// </summary>
public class IbkrApiException : Exception
{
    /// <summary>The structured error details.</summary>
    public IbkrError Error { get; }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/> wrapping the given error.
    /// </summary>
    /// <param name="error">The structured error.</param>
    public IbkrApiException(IbkrError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new <see cref="IbkrApiException"/> wrapping the given error with an inner exception.
    /// </summary>
    /// <param name="error">The structured error.</param>
    /// <param name="innerException">The inner exception.</param>
    public IbkrApiException(IbkrError error, Exception innerException)
        : base(error.Message, innerException)
    {
        Error = error;
    }
}
