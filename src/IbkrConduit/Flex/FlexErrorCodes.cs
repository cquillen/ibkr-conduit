namespace IbkrConduit.Flex;

/// <summary>
/// Information about a known Flex Web Service error code.
/// </summary>
/// <param name="Code">The numeric error code.</param>
/// <param name="Description">Human-readable description of the error, matching IBKR's documentation.</param>
/// <param name="IsRetryable">Whether the caller should retry after a delay (true) or fail immediately (false).</param>
internal sealed record FlexErrorInfo(int Code, string Description, bool IsRetryable);

/// <summary>
/// Known Flex Web Service error codes, classified as retryable or permanent
/// based on IBKR Flex Web Service documentation. Retryable codes indicate
/// transient conditions (data not ready, server under load, rate limiting);
/// permanent codes indicate configuration or input errors that will not
/// resolve by waiting.
/// </summary>
internal static class FlexErrorCodes
{
    private static readonly Dictionary<int, FlexErrorInfo> _codes = new()
    {
        [1001] = new(1001, "Statement could not be generated at this time. Please try again shortly.", IsRetryable: true),
        [1003] = new(1003, "Statement is not available.", IsRetryable: false),
        [1004] = new(1004, "Statement is incomplete at this time. Please try again shortly.", IsRetryable: true),
        [1005] = new(1005, "Settlement data is not ready at this time. Please try again shortly.", IsRetryable: true),
        [1006] = new(1006, "FIFO P/L data is not ready at this time. Please try again shortly.", IsRetryable: true),
        [1007] = new(1007, "MTM P/L data is not ready at this time. Please try again shortly.", IsRetryable: true),
        [1008] = new(1008, "MTM and FIFO P/L data is not ready at this time. Please try again shortly.", IsRetryable: true),
        [1009] = new(1009, "The server is under heavy load. Statement could not be generated at this time. Please try again shortly.", IsRetryable: true),
        [1010] = new(1010, "Legacy Flex Queries are no longer supported. Please convert over to Activity Flex.", IsRetryable: false),
        [1011] = new(1011, "Service account is inactive.", IsRetryable: false),
        [1012] = new(1012, "Token has expired.", IsRetryable: false),
        [1013] = new(1013, "IP restriction.", IsRetryable: false),
        [1014] = new(1014, "Query is invalid.", IsRetryable: false),
        [1015] = new(1015, "Token is invalid.", IsRetryable: false),
        [1016] = new(1016, "Account is invalid.", IsRetryable: false),
        [1017] = new(1017, "Reference code is invalid.", IsRetryable: false),
        [1018] = new(1018, "Too many requests. Limited to one request per second, 10 requests per minute per token.", IsRetryable: true),
        [1019] = new(1019, "Statement generation in progress. Please try again shortly.", IsRetryable: true),
        [1020] = new(1020, "Invalid request or unable to validate request.", IsRetryable: false),
        [1021] = new(1021, "Statement could not be retrieved at this time. Please try again shortly.", IsRetryable: true),
    };

    /// <summary>
    /// Looks up a known Flex error code. Returns null if the code is not recognized.
    /// </summary>
    /// <param name="code">The numeric error code from a Flex response.</param>
    /// <returns>Classification info for the code, or null if not in the known table.</returns>
    public static FlexErrorInfo? TryLookup(int code) => _codes.GetValueOrDefault(code);
}
