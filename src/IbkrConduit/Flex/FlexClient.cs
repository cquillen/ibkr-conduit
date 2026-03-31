using System.Diagnostics;
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
    private const int _inProgressErrorCode = 1019;

    private static readonly int[] _pollDelaysMs = [1000, 2000, 3000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000];
    private const int _maxTotalWaitMs = 60000;

    private readonly HttpClient _httpClient;
    private readonly string _flexToken;
    private readonly string _baseUrl;
    private readonly ILogger<FlexClient> _logger;
    private int _lastPollCount;

    /// <summary>
    /// Creates a new <see cref="FlexClient"/> instance.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests. If BaseAddress is set, it is used as the base URL.</param>
    /// <param name="flexToken">The Flex Web Service access token.</param>
    /// <param name="logger">Logger for poll progress.</param>
    public FlexClient(HttpClient httpClient, string flexToken, ILogger<FlexClient> logger)
    {
        _httpClient = httpClient;
        _flexToken = flexToken;
        _baseUrl = httpClient.BaseAddress != null
            ? httpClient.BaseAddress.ToString().TrimEnd('/') + "/"
            : _defaultBaseUrl;
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

        var referenceCode = await SendRequestAsync(queryId, fromDate, toDate, cancellationToken);
        var result = await PollForStatementAsync(referenceCode, cancellationToken);

        activity?.SetTag(LogFields.PollCount, _lastPollCount);
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

        var response = await _httpClient.GetStringAsync(url, cancellationToken);
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

        var url = $"{_baseUrl}GetStatement?t={_flexToken}&q={referenceCode}&v=3";
        var totalWaited = 0;
        var attempt = 0;

        foreach (var delayMs in _pollDelaysMs)
        {
            if (totalWaited >= _maxTotalWaitMs)
            {
                break;
            }

            attempt++;
            var responseStr = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = XDocument.Parse(responseStr);

            if (!IsInProgress(doc))
            {
                CheckForError(doc);
                LogGetStatementCompleted(totalWaited);
                _lastPollCount = attempt;
                activity?.SetTag(LogFields.Attempt, attempt);
                return doc;
            }

            LogStatementInProgress(delayMs, totalWaited);
            await Task.Delay(delayMs, cancellationToken);
            totalWaited += delayMs;
        }

        // One final attempt after all delays
        attempt++;
        var finalResponseStr = await _httpClient.GetStringAsync(url, cancellationToken);
        var finalDoc = XDocument.Parse(finalResponseStr);

        if (IsInProgress(finalDoc))
        {
            throw new TimeoutException(
                $"Flex statement generation did not complete within {_maxTotalWaitMs / 1000}s for reference code '{referenceCode}'.");
        }

        CheckForError(finalDoc);
        _lastPollCount = attempt;
        activity?.SetTag(LogFields.Attempt, attempt);
        return finalDoc;
    }

    private static bool IsInProgress(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
        {
            return false;
        }

        var errorCodeStr = root.Element("ErrorCode")?.Value;
        if (int.TryParse(errorCodeStr, out var errorCode))
        {
            return errorCode == _inProgressErrorCode;
        }

        return false;
    }

    private static void CheckForError(XDocument doc)
    {
        var root = doc.Root;
        if (root == null)
        {
            return;
        }

        var errorCodeStr = root.Element("ErrorCode")?.Value;
        if (int.TryParse(errorCodeStr, out var errorCode) && errorCode != 0)
        {
            var errorMessage = root.Element("ErrorMessage")?.Value ?? "Unknown Flex query error";
            throw new FlexQueryException(errorCode, errorMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex SendRequest succeeded, reference code: {ReferenceCode}")]
    private partial void LogSendRequestSucceeded(string referenceCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex GetStatement completed after {TotalWaited}ms")]
    private partial void LogGetStatementCompleted(int totalWaited);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flex statement generation in progress, waiting {DelayMs}ms (total waited: {TotalWaited}ms)")]
    private partial void LogStatementInProgress(int delayMs, int totalWaited);
}
