using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Auth;

namespace IbkrConduit.Session;

/// <summary>
/// Configuration options for the IBKR client session behavior.
/// </summary>
[ExcludeFromCodeCoverage]
public class IbkrClientOptions
{
    /// <summary>
    /// OAuth credentials for authenticating with the IBKR API.
    /// Must be set before calling <c>AddIbkrClient</c>.
    /// </summary>
    public IbkrOAuthCredentials? Credentials { get; set; }

    /// <summary>
    /// Whether to compete with existing sessions when initializing.
    /// Default is <c>true</c>.
    /// </summary>
    public bool Compete { get; set; } = true;

    /// <summary>
    /// List of question message IDs to suppress after session initialization.
    /// </summary>
    public List<string> SuppressMessageIds { get; set; } = new();

    /// <summary>
    /// How long a conid stays in the pre-flight cache before a fresh pre-flight is required.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan PreflightCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Flex Web Service access token. Generated in Client Portal under
    /// Reporting / Flex Queries / Flex Web Configuration.
    /// Required for Flex operations. If null, Flex operations will throw.
    /// </summary>
    public string? FlexToken { get; set; }

    /// <summary>
    /// Override the base URL for all IBKR API requests.
    /// Default is <c>https://api.ibkr.com</c>. Set this to a WireMock server
    /// URL for integration testing.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Interval in seconds between tickle requests to keep the session alive.
    /// Default is 60. Reduce for integration testing.
    /// </summary>
    public int TickleIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How long before token expiry to trigger a proactive refresh.
    /// Default is 1 hour. Reduce for integration testing.
    /// </summary>
    public TimeSpan ProactiveRefreshMargin { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// When true, throws <see cref="Errors.IbkrSchemaViolationException"/> if a JSON response
    /// contains fields not mapped to the DTO, or if DTO fields are missing from the response.
    /// Default is false (log warnings only). Enable in dev/test environments for fail-fast behavior.
    /// </summary>
    public bool StrictResponseValidation { get; set; }

    /// <summary>
    /// When true, facade methods call <see cref="Errors.Result{T}.EnsureSuccess"/> internally,
    /// throwing <see cref="Errors.IbkrApiException"/> on API errors. Default false.
    /// </summary>
    public bool ThrowOnApiError { get; set; }

    /// <summary>
    /// Maximum time to wait for a Flex Web Service report to finish generating.
    /// Wider date ranges and reports with many sections can take minutes to generate.
    /// Default is 60 seconds — sufficient for built-in period queries (e.g., LastBusinessDay)
    /// but often too short for custom date ranges. Increase for year-scale historical reports.
    /// </summary>
    public TimeSpan FlexPollTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
