using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;

namespace ApiCapture;

/// <summary>
/// Executes endpoint captures from the endpoint table, recording responses
/// and validating expected status codes.
/// </summary>
public static class CaptureRunner
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    /// <summary>
    /// Runs all matching endpoint entries, recording responses and deleting
    /// recordings for entries where the actual status doesn't match expected.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    /// <param name="entries">The endpoint entries to execute.</param>
    /// <param name="delayMs">Delay in milliseconds between requests to respect rate limits. Default 1000ms.</param>
    /// <param name="verbose">When true, prints pretty-printed request and response bodies to the console.</param>
    /// <returns>A summary of results.</returns>
    public static async Task<CaptureResult> RunAsync(
        CaptureContext ctx, IReadOnlyList<EndpointEntry> entries, int delayMs = 1000, bool verbose = false)
    {
        var passed = 0;
        var failed = 0;
        var errors = 0;
        var total = entries.Count;
        var current = 0;
        string? currentCategory = null;
        var variables = new Dictionary<string, string>();

        foreach (var entry in entries)
        {
            current++;

            // Switch recording scenario when category changes
            if (entry.Category != currentCategory)
            {
                currentCategory = entry.Category;
                ctx.Recording.Reset(currentCategory.ToLowerInvariant());
                Console.WriteLine($"\n=== {currentCategory} ===\n");
            }

            var url = ResolveTemplate(entry.UrlTemplate, ctx, variables);
            var body = entry.BodyTemplate is not null
                ? ResolveTemplate(entry.BodyTemplate, ctx, variables)
                : null;

            try
            {
                Console.Write($"  [{current}/{total}] {entry.Method.Method} {url} ");

                using var request = new HttpRequestMessage(entry.Method, url);
                if (body is not null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                var response = await ctx.CaptureClient.SendAsync(request);
                var actualStatus = (int)response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync();

                if (actualStatus == entry.ExpectedStatus)
                {
                    Console.WriteLine($"-> {actualStatus} OK");
                    passed++;

                    if (entry.CaptureAs is not null && entry.CaptureJsonPath is not null)
                    {
                        try
                        {
                            var node = JsonNode.Parse(responseBody);
                            var path = JsonPath.Parse(entry.CaptureJsonPath);
                            var result = path.Evaluate(node);
                            var match = result.Matches.FirstOrDefault();
                            if (match is not null)
                            {
                                var value = match.Value?.ToString() ?? string.Empty;
                                variables[entry.CaptureAs] = value;
                                Console.WriteLine($"    captured {entry.CaptureAs} = {value}");
                            }
                            else
                            {
                                Console.WriteLine($"    capture MISS: no match for {entry.CaptureJsonPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    capture ERROR: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"-> {actualStatus} EXPECTED {entry.ExpectedStatus} [{entry.Name}]");
                    failed++;
                }

                if (verbose)
                {
                    PrintVerboseDetail(entry, body, actualStatus, responseBody);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"-> ERROR: {ex.Message} [{entry.Name}]");
                errors++;
            }

            // Rate limit between requests
            if (current < total && delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
        }

        ctx.Recording.ScenarioName = null;

        return new CaptureResult(passed, failed, errors);
    }

    private static void PrintVerboseDetail(EndpointEntry entry, string? requestBody, int status, string responseBody)
    {
        Console.WriteLine();
        Console.WriteLine($"    {"Name:",-10} {entry.Name}");

        if (requestBody is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"    {"Request:",-10}");
            Console.WriteLine(PrettyPrintJson(requestBody, "      "));
        }

        Console.WriteLine();
        Console.WriteLine($"    {"Response:",-10} {status}");
        Console.WriteLine(PrettyPrintJson(responseBody, "      "));

        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
    }

    private static string PrettyPrintJson(string json, string indent)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var pretty = node?.ToJsonString(PrettyPrint) ?? json;
            return string.Join('\n', pretty.Split('\n').Select(line => indent + line));
        }
        catch
        {
            return indent + json;
        }
    }

    /// <summary>
    /// Filters the endpoint table by category and optional name pattern.
    /// </summary>
    public static IReadOnlyList<EndpointEntry> Filter(
        IReadOnlyList<EndpointEntry> entries, string? category, string? namePattern)
    {
        var filtered = entries.AsEnumerable();

        if (category is not null)
        {
            filtered = filtered.Where(e =>
                e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (namePattern is not null)
        {
            filtered = filtered.Where(e =>
                e.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    private static string ResolveTemplate(
        string template, CaptureContext ctx, Dictionary<string, string> variables)
    {
        var resolved = template.Replace("{accountId}", ctx.AccountId);
        foreach (var kvp in variables)
        {
            resolved = resolved.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return resolved;
    }
}

/// <summary>
/// Summary of a capture run.
/// </summary>
/// <param name="Passed">Endpoints that returned the expected status code.</param>
/// <param name="Failed">Endpoints that returned a different status code.</param>
/// <param name="Errors">Endpoints that threw an exception.</param>
public record CaptureResult(int Passed, int Failed, int Errors);
