namespace ApiCapture.Modules;

/// <summary>
/// Captures portfolio-related IBKR API endpoints including positions, summary,
/// ledger, allocation, performance analytics, and sub-account data.
/// </summary>
public static class PortfolioCapture
{
    /// <summary>
    /// Runs the portfolio capture module against all portfolio and PA endpoints.
    /// </summary>
    /// <param name="ctx">The initialized capture context.</param>
    public static async Task RunAsync(CaptureContext ctx)
    {
        ctx.Recording.Reset("portfolio");
        Console.WriteLine("=== Portfolio Capture ===\n");

        try
        {
            Console.WriteLine("  GET /v1/api/portfolio/accounts");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/portfolio/accounts");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/positions/0");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/positions/0");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/summary");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/summary");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/ledger");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/ledger");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/meta");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/meta");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/allocation");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/allocation");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/position/756733");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/position/756733");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  GET /v1/api/portfolio/positions/756733");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/portfolio/positions/756733");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio/{ctx.AccountId}/combo/positions");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio/{ctx.AccountId}/combo/positions");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  GET /v1/api/portfolio2/{ctx.AccountId}/positions");
            var response = await ctx.CaptureClient.GetAsync($"/v1/api/portfolio2/{ctx.AccountId}/positions");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  GET /v1/api/portfolio/subaccounts");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/portfolio/subaccounts");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  GET /v1/api/portfolio/subaccounts2");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/portfolio/subaccounts2");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  GET /v1/api/iserver/account/pnl/partitioned");
            var response = await ctx.CaptureClient.GetAsync("/v1/api/iserver/account/pnl/partitioned");
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine($"  POST /v1/api/portfolio/{ctx.AccountId}/positions/invalidate");
            var invalidateContent = new StringContent(string.Empty);
            var response = await ctx.CaptureClient.PostAsync(
                $"/v1/api/portfolio/{ctx.AccountId}/positions/invalidate", invalidateContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/pa/performance");
            var perfContent = new StringContent(
                $$$"""{"acctIds":["{{{ctx.AccountId}}}"],"period":"1M"}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/pa/performance", perfContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/pa/transactions");
            var txnContent = new StringContent(
                $$$"""{"acctIds":["{{{ctx.AccountId}}}"],"conids":[],"currency":"USD","days":30}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/pa/transactions", txnContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/portfolio/allocation");
            var allocContent = new StringContent(
                $$$"""{"acctIds":["{{{ctx.AccountId}}}"]}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/portfolio/allocation", allocContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        try
        {
            Console.WriteLine("  POST /v1/api/pa/allperiods");
            var allPeriodsContent = new StringContent(
                $$$"""{"acctIds":["{{{ctx.AccountId}}}"]}""",
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await ctx.CaptureClient.PostAsync("/v1/api/pa/allperiods", allPeriodsContent);
            Console.WriteLine($"    -> {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    -> ERROR: {ex.Message}");
        }

        ctx.Recording.ScenarioName = null;
        Console.WriteLine("\nPortfolio capture complete. Recordings saved to: recordings/portfolio/");
    }
}
