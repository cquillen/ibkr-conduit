using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials
using var creds = OAuthCredentialsFactory.FromEnvironment();
Console.WriteLine($"Consumer key: {creds.ConsumerKey}");

// Wire up via DI — the way a real consumer would
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(creds, new IbkrClientOptions { Compete = true });

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// --- Query accounts ---
Console.WriteLine("\n=== ACCOUNTS ===");
var accounts = await client.Portfolio.GetAccountsAsync();
foreach (var acct in accounts)
{
    Console.WriteLine($"  {acct.Id} — {acct.AccountTitle} ({acct.Type})");
}

// --- Query live orders ---
Console.WriteLine("\n=== LIVE ORDERS ===");
try
{
    var liveOrders = await client.Orders.GetLiveOrdersAsync();
    if (liveOrders.Count == 0)
    {
        Console.WriteLine("  No live orders in current session.");
    }
    else
    {
        foreach (var order in liveOrders)
        {
            Console.WriteLine($"  Order {order.OrderId}: {order.Side} {order.TotalSize} {order.Ticker} — Status: {order.Status} (Filled: {order.FilledQuantity}, Remaining: {order.RemainingQuantity})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error querying orders: {ex.Message}");
}

// --- Query trades ---
Console.WriteLine("\n=== TRADES ===");
try
{
    var trades = await client.Orders.GetTradesAsync();
    if (trades.Count == 0)
    {
        Console.WriteLine("  No trades in current session.");
    }
    else
    {
        foreach (var trade in trades)
        {
            Console.WriteLine($"  Trade {trade.ExecutionId}: {trade.Side} {trade.Size} {trade.Symbol} @ {trade.Price}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error querying trades: {ex.Message}");
}

// --- Query positions (raw HTTP — no Refit method yet) ---
Console.WriteLine("\n=== POSITIONS ===");
try
{
    var accountId = accounts[0].Id;
    var posResponse = await provider.GetRequiredService<HttpClient>()
        .GetAsync($"https://api.ibkr.com/v1/api/portfolio/{accountId}/positions/0");

    // The DI HttpClient won't have signing. Use the Refit-backing client via a raw call instead.
    // We'll use a simple signed HttpClient approach.
}
catch { /* ignore */ }

// Use a signed HttpClient directly
try
{
    var accountId = accounts[0].Id;
    using var posClient = new HttpClient(new IbkrConduit.Auth.OAuthSigningHandler(
        provider.GetRequiredService<IbkrConduit.Auth.ISessionTokenProvider>(),
        creds.ConsumerKey, creds.AccessToken,
        provider.GetRequiredService<IbkrConduit.Session.ISessionManager>())
    {
        InnerHandler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        },
    })
    {
        BaseAddress = new Uri("https://api.ibkr.com"),
    };

    var accountId2 = accounts[0].Id;
    var posJson = await posClient.GetStringAsync($"/v1/api/portfolio/{accountId2}/positions/0");
    Console.WriteLine(posJson);
}
catch (Exception ex)
{
    Console.WriteLine($"  Error querying positions: {ex.Message}");
}

Console.WriteLine("\nDone.");
