namespace ApiCapture.Modules;

/// <summary>
/// Captures account-related IBKR API endpoints including account listing,
/// details, search, and account switching.
/// </summary>
public static class AccountsCapture
{
    /// <summary>
    /// Runs the accounts capture module against all account endpoints.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    public static async Task RunAsync(CaptureContext ctx)
    {
        ctx.Recording.Reset("accounts");
        Console.WriteLine("=== Accounts Capture ===\n");

        try
        {
            Console.WriteLine("  GET /v1/api/iserver/accounts");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/iserver/accounts");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/iserver/account/{ctx.AccountId}");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/iserver/account/{ctx.AccountId}");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/iserver/account/search/{ctx.AccountId}");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/iserver/account/search/{ctx.AccountId}");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/iserver/account");
            var switchContent = new StringContent(
                $$$"""{"acctId":"{{{ctx.AccountId}}}"}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/iserver/account", switchContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/iserver/dynaccount");
            var dynaContent = new StringContent(
                $$$"""{"acctId":"{{{ctx.AccountId}}}"}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/iserver/dynaccount", dynaContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        ctx.Recording.ScenarioName = null;
        Console.WriteLine("\nAccounts capture complete. Recordings saved to: recordings/accounts/");
    }
}
