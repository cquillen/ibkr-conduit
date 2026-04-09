using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using IbkrConduit.Diagnostics;
using IbkrConduit.Errors;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Flex;

/// <summary>
/// Internal HTTP transport for the IBKR Flex Web Service. Exposes the raw
/// SendRequest and GetStatement endpoints as single HTTP calls returning
/// parsed XML or a transport-level failure. Does not poll, classify, or retry —
/// that is the responsibility of <see cref="IbkrConduit.Client.FlexOperations"/>.
/// </summary>
internal sealed partial class FlexClient
{
    private const string _defaultBaseUrl = "https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientName;
    private readonly string _flexToken;
    private readonly string _baseUrl;
    private readonly ILogger<FlexClient> _logger;

    /// <summary>
    /// Creates a new <see cref="FlexClient"/> instance.
    /// </summary>
    /// <param name="httpClientFactory">The factory for creating HTTP clients.</param>
    /// <param name="clientName">The named client to request from the factory.</param>
    /// <param name="flexToken">The Flex Web Service access token.</param>
    /// <param name="logger">Logger for raw response tracing.</param>
    /// <param name="baseUrl">Optional base URL override (used for testing).</param>
    public FlexClient(
        IHttpClientFactory httpClientFactory,
        string clientName,
        string flexToken,
        ILogger<FlexClient> logger,
        string? baseUrl = null)
    {
        _httpClientFactory = httpClientFactory;
        _clientName = clientName;
        _flexToken = flexToken;
        _baseUrl = baseUrl ?? _defaultBaseUrl;
        _logger = logger;
    }

    /// <summary>
    /// Calls the SendRequest endpoint. Returns the raw XML response, or a transport error
    /// if the HTTP call failed or the response body could not be parsed as XML.
    /// Does not interpret the response content — that is the caller's responsibility.
    /// </summary>
    public async Task<Result<XDocument>> SendRequestAsync(
        string queryId, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.SendRequest");
        activity?.SetTag(LogFields.QueryId, queryId);

        var url = $"{_baseUrl}SendRequest?t={_flexToken}&q={queryId}&v=3";
        if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
        {
            url += $"&fd={fromDate}&td={toDate}";
        }

        return await GetXmlAsync(url, $"flex/SendRequest?q={queryId}", cancellationToken);
    }

    /// <summary>
    /// Calls the GetStatement endpoint exactly once. Returns the raw XML response, or a
    /// transport error if the HTTP call failed or the response body could not be parsed
    /// as XML. Does not poll, classify, or retry — that is the caller's responsibility.
    /// </summary>
    public async Task<Result<XDocument>> GetStatementAsync(
        string referenceCode, CancellationToken cancellationToken)
    {
        using var activity = IbkrConduitDiagnostics.ActivitySource.StartActivity("IbkrConduit.Flex.GetStatement");
        activity?.SetTag("reference_code", referenceCode);

        var url = $"{_baseUrl}GetStatement?t={_flexToken}&q={referenceCode}&v=3";
        return await GetXmlAsync(url, $"flex/GetStatement?q={referenceCode}", cancellationToken);
    }

    private async Task<Result<XDocument>> GetXmlAsync(
        string url, string requestPath, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(_clientName);

        string responseStr;
        try
        {
            responseStr = await httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result<XDocument>.Failure(new IbkrApiError(
                StatusCode: ex.StatusCode,
                Message: ex.Message,
                RawBody: null,
                RequestPath: requestPath));
        }

        LogRawResponse(requestPath, responseStr);

        try
        {
            return Result<XDocument>.Success(XDocument.Parse(responseStr));
        }
        catch (XmlException ex)
        {
            return Result<XDocument>.Failure(new IbkrApiError(
                StatusCode: null,
                Message: $"Flex response was not valid XML: {ex.Message}",
                RawBody: responseStr,
                RequestPath: requestPath));
        }
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Flex {RequestPath} response body: {ResponseBody}")]
    private partial void LogRawResponse(string requestPath, string responseBody);
}
