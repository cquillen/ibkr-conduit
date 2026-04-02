namespace ApiCapture.Modules;

/// <summary>
/// Captures session-related IBKR API endpoints including auth init, tickle,
/// status, validation, and question suppression.
/// </summary>
public static class SessionCapture
{
    /// <summary>
    /// Runs the session capture module against all session/auth endpoints.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    public static async Task RunAsync(CaptureContext ctx)
    {
        ctx.Recording.Reset("session");
        Console.WriteLine("=== Session Capture ===\n");

        try
        {
            Console.WriteLine("  POST /v1/api/iserver/auth/ssodh/init");
            var initContent = new StringContent(
                """{"publish":true,"compete":true}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/iserver/auth/ssodh/init", initContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/tickle");
            var tickleContent = new StringContent(string.Empty);
            var response = await ctx.CaptureClient.PostAsync("/v1/api/tickle", tickleContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  GET /v1/api/iserver/auth/status");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/iserver/auth/status");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        // GET /sso/validate is skipped — only works for portal and OAuth2 clients, not OAuth 1.0a.
        // POST /iserver/reauthenticate is skipped — deprecated endpoint, returns 404.

        try
        {
            Console.WriteLine("  POST /v1/api/iserver/questions/suppress");
            var suppressContent = new StringContent(
                """{"messageIds":["o163"]}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/iserver/questions/suppress", suppressContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/iserver/questions/suppress/reset");
            var resetContent = new StringContent(string.Empty);
            var response = await ctx.CaptureClient.PostAsync("/v1/api/iserver/questions/suppress/reset", resetContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        // POST /logout is intentionally skipped — calling it would terminate the
        // brokerage session, preventing subsequent capture modules from running.

        ctx.Recording.ScenarioName = null;
        Console.WriteLine("\nSession capture complete. Recordings saved to: recordings/session/");
    }
}
