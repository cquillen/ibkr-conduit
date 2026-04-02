using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ApiCapture.Recording;

/// <summary>
/// Delegating handler that records HTTP request/response pairs to WireMock-compatible
/// JSON files when a recording scenario is active.
/// </summary>
public sealed partial class RecordingDelegatingHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly RecordingContext _context;
    private readonly string _outputDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingDelegatingHandler"/> class.
    /// </summary>
    public RecordingDelegatingHandler(RecordingContext context, string outputDirectory)
    {
        _context = context;
        _outputDirectory = outputDirectory;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!_context.IsActive)
        {
            return response;
        }

        var requestBody = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        var responseBody = response.Content is not null
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : null;

        // Reconstruct response content so downstream consumers can still read it
        if (responseBody is not null && response.Content is not null)
        {
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            response.Content = new StringContent(responseBody, Encoding.UTF8, mediaType);
        }

        var step = _context.NextStep();
        var scenarioName = _context.ScenarioName!;

        var sanitizedPath = SanitizeUrl(request.RequestUri?.PathAndQuery ?? "/");
        var requestHeaders = BuildSanitizedHeaders(request.Headers);

        var responseHeaders = new Dictionary<string, string>();
        if (response.Content?.Headers.ContentType is not null)
        {
            responseHeaders["Content-Type"] = response.Content.Headers.ContentType.ToString();
        }

        var record = new Dictionary<string, object?>
        {
            ["Request"] = new Dictionary<string, object?>
            {
                ["Path"] = sanitizedPath,
                ["Methods"] = new[] { request.Method.Method },
                ["Headers"] = requestHeaders.Count > 0 ? requestHeaders : null,
                ["Body"] = TryParseJson(requestBody),
            },
            ["Response"] = new Dictionary<string, object?>
            {
                ["StatusCode"] = (int)response.StatusCode,
                ["Headers"] = responseHeaders.Count > 0 ? responseHeaders : null,
                ["Body"] = TryParseJson(responseBody),
            },
            ["Metadata"] = new Dictionary<string, object?>
            {
                ["Scenario"] = scenarioName,
                ["Step"] = step,
                ["RecordedAt"] = DateTime.UtcNow.ToString("o"),
            },
        };

        var slug = DeriveSlug(request.RequestUri?.AbsolutePath ?? "/");
        var fileName = $"{step:D3}-{request.Method.Method}-{slug}.json";
        var scenarioDir = Path.Combine(_outputDirectory, scenarioName);
        Directory.CreateDirectory(scenarioDir);

        var filePath = Path.Combine(scenarioDir, fileName);
        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        return response;
    }

    /// <summary>
    /// Attempts to parse a string as JSON so it serializes as a structured value
    /// rather than an escaped string. Falls back to the raw string for non-JSON content.
    /// </summary>
    private static object? TryParseJson(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(value);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string SanitizeUrl(string pathAndQuery)
    {
        var sanitized = OAuthTokenPattern().Replace(pathAndQuery, "oauth_token=REDACTED");
        sanitized = OAuthSignaturePattern().Replace(sanitized, "oauth_signature=REDACTED");
        return sanitized;
    }

    private static Dictionary<string, string> BuildSanitizedHeaders(
        System.Net.Http.Headers.HttpRequestHeaders headers)
    {
        var result = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                result[header.Key] = "REDACTED";
            }
            else if (string.Equals(header.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
            {
                result[header.Key] = "api=REDACTED";
            }
            else
            {
                result[header.Key] = string.Join(", ", header.Value);
            }
        }

        return result;
    }

    private static string DeriveSlug(string path)
    {
        // Strip /v1/api/ prefix if present
        var slug = path;
        if (slug.StartsWith("/v1/api/", StringComparison.Ordinal))
        {
            slug = slug["/v1/api/".Length..];
        }
        else if (slug.StartsWith('/'))
        {
            slug = slug[1..];
        }

        // Replace slashes with hyphens and trim trailing hyphens
        slug = slug.Replace('/', '-').TrimEnd('-');

        return slug;
    }

    [GeneratedRegex(@"oauth_token=[^&\s]+")]
    private static partial Regex OAuthTokenPattern();

    [GeneratedRegex(@"oauth_signature=[^&\s]+")]
    private static partial Regex OAuthSignaturePattern();
}
