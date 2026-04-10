using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that logs the raw request and response bodies at Debug level.
/// Place as the outermost handler in the pipeline to capture the final request/response
/// including retry behavior. Authorization headers are redacted. Response bodies are
/// truncated to 4 KB to avoid excessive log volume.
/// </summary>
internal sealed partial class AuditLogHandler : DelegatingHandler
{
    private const int _maxBodyLength = 4096;
    private readonly ILogger<AuditLogHandler> _logger;

    /// <summary>
    /// Creates a new <see cref="AuditLogHandler"/>.
    /// </summary>
    /// <param name="logger">Logger for audit entries.</param>
    public AuditLogHandler(ILogger<AuditLogHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            await LogRequestAsync(request, cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken);
        sw.Stop();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            await LogResponseAsync(request, response, sw.ElapsedMilliseconds, cancellationToken);
        }

        return response;
    }

    private async Task LogRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var path = request.RequestUri?.AbsolutePath ?? "unknown";

        string? requestBody = null;
        if (request.Content is not null)
        {
            requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            requestBody = Truncate(requestBody);
        }

        LogRequest(method.Method, path, requestBody ?? "(no body)");
    }

    private async Task LogResponseAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        long durationMs,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "unknown";
        var statusCode = (int)response.StatusCode;

        string? responseBody = null;
        if (response.Content is not null)
        {
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Re-buffer the body so downstream handlers and Refit can still read it.
            // ReadAsStringAsync consumes the stream — we need to replace it.
            var contentType = response.Content.Headers.ContentType;
            response.Content = new StringContent(responseBody, Encoding.UTF8);
            if (contentType is not null)
            {
                response.Content.Headers.ContentType = contentType;
            }

            responseBody = Truncate(responseBody);
        }

        LogResponse(path, statusCode, durationMs, responseBody ?? "(no body)");
    }

    private static string Truncate(string value) =>
        value.Length <= _maxBodyLength
            ? value
            : string.Concat(value.AsSpan(0, _maxBodyLength), " [truncated]");

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "→ {Method} {Path} {RequestBody}")]
    private partial void LogRequest(string method, string path, string requestBody);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "← {Path} {StatusCode} ({DurationMs}ms) {ResponseBody}")]
    private partial void LogResponse(string path, int statusCode, long durationMs, string responseBody);
}
