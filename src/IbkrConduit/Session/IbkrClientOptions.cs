using System.Diagnostics.CodeAnalysis;
using IbkrConduit.Auth;
using IbkrConduit.Flex;
using IbkrConduit.Health;

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
    /// Override the base URL for WebSocket connections.
    /// Default is <c>wss://api.ibkr.com/v1/api/ws</c>. Set this to a mock
    /// WebSocket server URL for integration testing.
    /// </summary>
    public string? WebSocketBaseUrl { get; set; }

    /// <summary>
    /// Interval in seconds between tickle requests to keep the session alive.
    /// Default is 60. Reduce for integration testing.
    /// </summary>
    public int TickleIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Interval between tickle attempts after a failure, in seconds. Default is 5.
    /// During a failure burst (network outage, IBKR backend transient errors), the tickle
    /// loop polls at this faster cadence so the WebSocket reconnect watchdog can detect
    /// recovery quickly. The first successful tickle returns the loop to
    /// <see cref="TickleIntervalSeconds"/>. Set lower for faster recovery; set higher to
    /// reduce load against a degraded backend.
    /// </summary>
    public int TickleFailureIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Interval in seconds between WebSocket "tic" ping messages used to keep
    /// the streaming session alive. IBKR requires a ping at least once per
    /// minute. Default is 30. Reduce for integration testing.
    /// </summary>
    public int WebSocketHeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Per-subscriber buffer size for WebSocket observables. When a consumer
    /// falls behind, the oldest queued message is dropped to make room for
    /// the newest. Default is 2048 (ADR-0002: 256 was too tight for burst
    /// replay). Increase for bursty sources, decrease for memory-constrained
    /// hosts. The drop policy itself (DropOldest) is fixed and not configurable,
    /// but every eviction is observable — a Warning log plus the
    /// <c>ibkr.conduit.streaming.frames.dropped</c> counter (tagged tenant/topic/cause).
    /// </summary>
    public int StreamingBufferSize { get; set; } = 2048;

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
    /// How long the per-account order lock is retained for a pending order confirmation round before
    /// it is released and the abandoned order's outcome is treated as ambiguous (ADR-0006, design doc
    /// §9.10). A placement that returns an <see cref="Orders.OrderConfirmationRequired"/> retains the
    /// account lock until the round resolves — a reply, a dismiss, or this timeout — so overlapping
    /// same-account confirmation windows cannot occur in-process and a consumer that never replies
    /// cannot wedge the account's order flow. Must be greater than zero. Default is 30 seconds.
    /// <para>
    /// <b>Consumer obligation:</b> reply to a confirmation promptly. A slow decision delays subsequent
    /// same-account placements up to this timeout; for automated throughput, suppress questions via
    /// <see cref="SuppressMessageIds"/> instead. If the timeout elapses, that order's outcome is
    /// ambiguous — reconcile via live-order/trade queries before resubmitting.
    /// </para>
    /// </summary>
    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional hook to configure the health-status staleness and warning thresholds
    /// (design doc §7.7, PVR-07). Invoked once at registration <em>after</em> the defaults
    /// have been derived from <see cref="TickleIntervalSeconds"/> — the staleness window
    /// defaults to twice the tickle interval so a longer <see cref="TickleIntervalSeconds"/>
    /// cannot silently outrun it. Set any threshold on the supplied
    /// <see cref="Health.HealthStatusOptions"/> to override the derived default; leave it
    /// null to accept the tickle-derived defaults.
    /// </summary>
    public Action<HealthStatusOptions>? ConfigureHealthStatus { get; set; }

    /// <summary>
    /// Maximum time to wait for a Flex Web Service report to finish generating.
    /// Wider date ranges and reports with many sections can take minutes to generate.
    /// Default is 60 seconds — sufficient for built-in period queries (e.g., LastBusinessDay)
    /// but often too short for custom date ranges. Increase for year-scale historical reports.
    /// </summary>
    public TimeSpan FlexPollTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Flex Web Service query IDs for strongly-typed Flex operations.
    /// Configure the IDs for the query templates you want to call via typed methods.
    /// The generic <see cref="Client.IFlexOperations.ExecuteQueryAsync(string, System.Threading.CancellationToken)"/>
    /// still takes a query ID parameter.
    /// </summary>
    public FlexQueryOptions FlexQueries { get; set; } = new();

    /// <summary>
    /// When <c>true</c>, <see cref="Session.SessionManager.DisposeAsync"/> skips its
    /// best-effort logout because the caller already issued one. The manager path
    /// (<see cref="Client.TenantBuilder"/>) sets this so a managed tenant logs out exactly
    /// once — <see cref="Client.ManagedTenant"/> owns the single bounded logout. Internal:
    /// the single-account <c>AddIbkrClient</c> path leaves it <c>false</c> and still logs
    /// out on dispose.
    /// </summary>
    internal bool SkipLogoutOnDispose { get; set; }

    /// <summary>
    /// Upper bound on a managed tenant's best-effort logout during teardown, so a hung
    /// logout can never block disposal for minutes — even when the caller passes
    /// <see cref="System.Threading.CancellationToken.None"/>. Default is 10 seconds.
    /// Internal (tests shrink it to keep the bounded-teardown assertion fast).
    /// </summary>
    internal TimeSpan LogoutTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates a per-tenant copy: scalars are copied, the mutable
    /// <see cref="SuppressMessageIds"/> list and <see cref="FlexQueries"/> are
    /// deep-copied so a per-tenant override cannot mutate the shared baseline.
    /// </summary>
    internal IbkrClientOptions Clone() => new()
    {
        Credentials = Credentials,
        Compete = Compete,
        SuppressMessageIds = new List<string>(SuppressMessageIds),
        PreflightCacheDuration = PreflightCacheDuration,
        FlexToken = FlexToken,
        BaseUrl = BaseUrl,
        WebSocketBaseUrl = WebSocketBaseUrl,
        TickleIntervalSeconds = TickleIntervalSeconds,
        TickleFailureIntervalSeconds = TickleFailureIntervalSeconds,
        WebSocketHeartbeatIntervalSeconds = WebSocketHeartbeatIntervalSeconds,
        StreamingBufferSize = StreamingBufferSize,
        ConfigureHealthStatus = ConfigureHealthStatus,
        ProactiveRefreshMargin = ProactiveRefreshMargin,
        StrictResponseValidation = StrictResponseValidation,
        ThrowOnApiError = ThrowOnApiError,
        ConfirmationTimeout = ConfirmationTimeout,
        FlexPollTimeout = FlexPollTimeout,
        FlexQueries = FlexQueries.Clone(),
        SkipLogoutOnDispose = SkipLogoutOnDispose,
        LogoutTimeout = LogoutTimeout,
    };
}
