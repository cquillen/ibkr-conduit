namespace ApiCapture.Modules;

/// <summary>
/// Spike capture module that exercises GET /v1/api/portfolio/accounts
/// to validate the end-to-end capture pipeline.
/// </summary>
public static class SpikeCapture
{
    /// <summary>
    /// Runs the spike capture: fetches portfolio accounts through the recording pipeline.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    public static async Task RunAsync(CaptureContext ctx)
    {
        ctx.Recording.Reset("spike");

        Console.WriteLine("Spike: GET /v1/api/portfolio/accounts");
        var response = await ctx.CaptureClient.GetAsync("/v1/api/portfolio/accounts");
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"  Status: {(int)response.StatusCode}");
        Console.WriteLine($"  Body: {body[..Math.Min(body.Length, 200)]}");

        ctx.Recording.ScenarioName = null;
        Console.WriteLine("\nRecording saved to: recordings/spike/");
    }
}
