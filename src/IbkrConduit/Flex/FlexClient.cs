using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Xml.Linq;
using IbkrConduit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Flex;

/// <summary>
/// Internal HTTP client for the IBKR Flex Web Service two-step query flow.
/// Step 1: SendRequest to get a reference code.
/// Step 2: GetStatement with polling until the report is ready.
/// </summary>
internal sealed partial class FlexClient
{
    private const string _defaultBaseUrl = "https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/";
    private const int _rateLimitErrorCode = 1018;
    private const int _rateLimitDelayMs = 10000; // stay within IBKR's 10 requests/minute/token limit

    private static readonly Histogram<double> _queryDuration =
        IbkrConduitDiagnostics.Meter.CreateHistogram<double>("ibkr.conduit.flex.query.duration", "ms");

    private static readonly Counter<long> _queryCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.query.count");

    private static readonly Counter<long> _pollCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.poll.count");

    private static readonly Counter<long> _errorCount =
        IbkrConduitDiagnostics.Meter.CreateCounter<long>("ibkr.conduit.flex.error.count");

    private static readonly int[] _pollDelaysMs = [1000, 2000, 3000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000];
    private const int _defaultMaxTotalWaitMs = 60000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientName;
    private readonly string _flexToken;
    private readonly string _baseUrl;
    private readonly int _maxTotalWaitMs;
    private readonly ILogger<FlexClient> _logger;
    private int _lastPollCount;

    /// <summary>
    /// Creates a new <see cref="FlexClient"/> instance.
    /// A fresh <see cref="HttpClient"/> is created per request via the named client.
    /// </summary>
    /// <param name="httpClientFactory">The factory for creating HTTP clients.</param>
    /// <param name="clientName">The named client to request from the factory.</param>
    /// <param name="flexToken">The Flex Web Service access token.</param>
    /// <param name="logger">Logger for poll progress.</param>
    /// <param name="baseUrl">Optional base URL override (used for testing).</param>
    /// <param name="pollTimeout">Maximum time to wait for a report to finish generating. Defaults to 60 seconds.</param>
    public FlexClient(IHttpClientFactory httpClientFactory, string clientName, string flexToken, ILogger<FlexClient> logger, string? baseUrl = null, TimeSpan? pollTimeout = null)
    {
        _httpClientFactory = httpClientFactory;
        _clientName = clientName;
        _flexToken = flexToken;
        _baseUrl = baseUrl ?? _defaultBaseUrl;
        _maxTotalWaitMs = pollTimeout is null ? _defaultMaxTotalWaitMs : (int)pollTimeout.Value.TotalMilliseconds;
        _logger = logger;
    }

    /// <summary>
    /// Executes a Flex Query using the two-step SendRequest/GetStatement flow.
    /// </summary>
    /// <param name="queryId">The Flex Query template ID.</param>
    /// <param name="fromDate">Optional start date in yyyyMMdd format.</param>
    /// <param name="toDate">Optional end date in yyyyMMdd format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed XML document containing the Flex Query results.</returns>
    /// <exception cref="FlexQueryException">Thrown when the Flex service returns an error.</exception>
    /// <exception cref="TimeoutException">Thrown when polling exceeds the maximum wait time.</exception>
    public async Task<XDocument> ExecuteQueryAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.ExecuteQuery");
        activity?.SetTag(LogFields.QueryId, queryId);

        _queryCount.Add(1);
        var sw = Stopwatch.StartNew();

        var referenceCode = await SendRequestAsync(queryId, fromDate, toDate, cancellationToken);
        var result = await PollForStatementAsync(referenceCode, cancellationToken);

        _queryDuration.Record(sw.Elapsed.TotalMilliseconds);
        activity?.SetTag(LogFields.PollCount, _lastPollCount);
        LogFlexQueryExecuted(queryId);
        return result;
    }

    internal async Task<string> SendRequestAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.SendRequest");
        activity?.SetTag(LogFields.QueryId, queryId);

        var url = $"{_baseUrl}SendRequest?t={_flexToken}&q={queryId}&v=3";
        if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
        {
            url += $"&fd={fromDate}&td={toDate}";
        }

        var httpClient = _httpClientFactory.CreateClient(_clientName);
        var response = await httpClient.GetStringAsync(url, cancellationToken);
        var doc = XDocument.Parse(response);
        var root = doc.Root!;

        var status = root.Element("Status")?.Value;
        if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            var errorCodeStr = root.Element("ErrorCode")?.Value ?? "0";
            _ = int.TryParse(errorCodeStr, out var errorCode);
            var errorMessage = root.Element("ErrorMessage")?.Value ?? "Unknown Flex query error";
            throw new FlexQueryException(errorCode, errorMessage);
        }

        var referenceCode = root.Element("ReferenceCode")?.Value;
        if (string.IsNullOrEmpty(referenceCode))
        {
            throw new FlexQueryException(0, "SendRequest returned Success but no ReferenceCode.");
        }

        LogSendRequestSucceeded(referenceCode);
        return referenceCode;
    }

    internal async Task<XDocument> PollForStatementAsync(string referenceCode, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.GetStatement");
        activity?.SetTag("reference_code", referenceCode);

        var httpClient = _httpClientFactory.CreateClient(_clientName);
        var url = $"{_baseUrl}GetStatement?t={_flexToken}&q={referenceCode}&v=3";
        var totalWaited = 0;
        var attempt = 0;

        while (totalWaited < _maxTotalWaitMs)
        {
            attempt++;
            _pollCount.Add(1);
            var responseStr = await httpClient.GetStringAsync(url, cancellationToken);
            var doc = XDocument.Parse(responseStr);

            var classification = ClassifyPollResponse(doc, out var errorCode, out var errorMessage);

            if (classification == FlexResponseClass.Success)
            {
                LogGetStatementCompleted(totalWaited);
                _lastPollCount = attempt;
                activity?.SetTag(LogFields.Attempt, attempt);
                return doc;
            }

            if (classification == FlexResponseClass.Permanent)
            {
                _errorCount.Add(1, new KeyValuePair<string, object?>(LogFields.ErrorCode, errorCode));
                throw new FlexQueryException(errorCode, errorMessage ?? "Unknown Flex query error");
            }

            // Transient — choose the delay.
            // Rate limit responses get a longer backoff to respect IBKR's 10 req/min/token limit.
            // Other transient responses use the ramp-up schedule, capped at 5s after the initial attempts.
            var delayMs = errorCode == _rateLimitErrorCode
                ? _rateLimitDelayMs
                : attempt <= _pollDelaysMs.Length
                    ? _pollDelaysMs[attempt - 1]
                    : 5000;

            // Don't sleep past the timeout
            var remaining = _maxTotalWaitMs - totalWaited;
            if (remaining <= 0)
            {
                break;
            }

            var actualDelay = Math.Min(delayMs, remaining);
            LogStatementInProgress(actualDelay, totalWaited);
            await Task.Delay(actualDelay, cancellationToken);
            totalWaited += actualDelay;
        }

        throw new TimeoutException(
            $"Flex statement generation did not complete within {_maxTotalWaitMs / 1000}s for reference code '{referenceCode}'.");
    }

    private enum FlexResponseClass
    {
        /// <summary>Root is <c>FlexQueryResponse</c> or <c>Status=Success</c> — the report is ready.</summary>
        Success,

        /// <summary>Known retryable error code or <c>Status=Warn</c> fallback — keep polling.</summary>
        Transient,

        /// <summary>Known non-retryable code, <c>Status=Fail</c>, or malformed response — fail immediately.</summary>
        Permanent,
    }

    /// <summary>
    /// Classifies a GetStatement response as success, transient (retry), or permanent (fail).
    /// Uses the known error-code table first; falls back to the Status element for unknown codes.
    /// </summary>
    /// <param name="doc">The parsed XML response.</param>
    /// <param name="errorCode">The parsed error code, or 0 if none.</param>
    /// <param name="errorMessage">The error message from the response, or the documented description if none.</param>
    private static FlexResponseClass ClassifyPollResponse(
        XDocument doc, out int errorCode, out string? errorMessage)
    {
        errorCode = 0;
        errorMessage = null;

        var root = doc.Root;
        if (root is null)
        {
            errorMessage = "Flex response has no root element.";
            return FlexResponseClass.Permanent;
        }

        // Successful report delivery: the root element is FlexQueryResponse (the actual report),
        // not FlexStatementResponse (the control envelope).
        if (string.Equals(root.Name.LocalName, "FlexQueryResponse", StringComparison.Ordinal))
        {
            return FlexResponseClass.Success;
        }

        var status = root.Element("Status")?.Value;
        var errorCodeStr = root.Element("ErrorCode")?.Value;
        errorMessage = root.Element("ErrorMessage")?.Value;

        if (int.TryParse(errorCodeStr, out var parsedCode))
        {
            errorCode = parsedCode;
        }

        // Prefer the error code classification when the code is known.
        var info = FlexErrorCodes.TryLookup(errorCode);
        if (info is not null)
        {
            errorMessage ??= info.Description;
            return info.IsRetryable ? FlexResponseClass.Transient : FlexResponseClass.Permanent;
        }

        // Unknown code (or no code): fall back to the Status element.
        return status switch
        {
            "Success" => FlexResponseClass.Success,
            "Warn" => FlexResponseClass.Transient, // Warn implies "try again"
            _ => FlexResponseClass.Permanent,       // Fail, missing, or other → fail fast
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Flex query {QueryId} executed successfully")]
    private partial void LogFlexQueryExecuted(string queryId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex SendRequest succeeded, reference code: {ReferenceCode}")]
    private partial void LogSendRequestSucceeded(string referenceCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex GetStatement completed after {TotalWaited}ms")]
    private partial void LogGetStatementCompleted(int totalWaited);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex statement generation in progress, waiting {DelayMs}ms (total waited: {TotalWaited}ms)")]
    private partial void LogStatementInProgress(int delayMs, int totalWaited);
}
