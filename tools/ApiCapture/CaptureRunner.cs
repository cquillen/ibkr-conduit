using System.Text;

namespace ApiCapture;

/// <summary>
/// Executes endpoint captures from the endpoint table, recording responses
/// and validating expected status codes.
/// </summary>
public static class CaptureRunner
{
    /// <summary>
    /// Runs all matching endpoint entries, recording responses and deleting
    /// recordings for entries where the actual status doesn't match expected.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    /// <param name="entries">The endpoint entries to execute.</param>
    /// <returns>A summary of results.</returns>
    public static async Task<CaptureResult> RunAsync(CaptureContext ctx, IReadOnlyList<EndpointEntry> entries)
    {
        var passed = 0;
        var failed = 0;
        var errors = 0;
        string? currentCategory = null;

        foreach (var entry in entries)
        {
            // Switch recording scenario when category changes
            if (entry.Category != currentCategory)
            {
                currentCategory = entry.Category;
                ctx.Recording.Reset(currentCategory.ToLowerInvariant());
                Console.WriteLine($"\n=== {currentCategory} ===\n");
            }

            var url = ResolveTemplate(entry.UrlTemplate, ctx);
            var body = entry.BodyTemplate is not null
                ? ResolveTemplate(entry.BodyTemplate, ctx)
                : null;

            try
            {
                Console.Write($"  {entry.Method.Method} {url} ");

                using var request = new HttpRequestMessage(entry.Method, url);
                if (body is not null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                var response = await ctx.CaptureClient.SendAsync(request);
                var actualStatus = (int)response.StatusCode;

                if (actualStatus == entry.ExpectedStatus)
                {
                    Console.WriteLine($"-> {actualStatus} OK");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"-> {actualStatus} EXPECTED {entry.ExpectedStatus} [{entry.Name}]");
                    failed++;

                    // Delete the recording file for mismatched status
                    DeleteLastRecording(ctx);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"-> ERROR: {ex.Message} [{entry.Name}]");
                errors++;

                // Delete the recording file on exception
                DeleteLastRecording(ctx);
            }
        }

        ctx.Recording.ScenarioName = null;

        return new CaptureResult(passed, failed, errors);
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

    private static string ResolveTemplate(string template, CaptureContext ctx) =>
        template.Replace("{accountId}", ctx.AccountId);

    private static void DeleteLastRecording(CaptureContext ctx)
    {
        var path = ctx.RecordingHandler.LastWrittenPath;
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>
/// Summary of a capture run.
/// </summary>
/// <param name="Passed">Endpoints that returned the expected status code.</param>
/// <param name="Failed">Endpoints that returned a different status code.</param>
/// <param name="Errors">Endpoints that threw an exception.</param>
public record CaptureResult(int Passed, int Failed, int Errors);
