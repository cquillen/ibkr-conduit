#!/usr/bin/env dotnet
#:project ../src/IbkrConduit/IbkrConduit.csproj
#:package Microsoft.Extensions.Logging.Console
#:property PublishAot=false

using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials from environment variables.
// See tools/set-e2e-env.sh for the required variables.
using var credentials = OAuthCredentialsFactory.FromEnvironment();

var verbose = args.Any(a => a is "--verbose" or "-v");
var logLevel = verbose ? LogLevel.Debug : LogLevel.Warning;

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(logLevel));
services.AddIbkrClient(opts => opts.Credentials = credentials);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Step 1: Discover accounts
var accounts = (await client.Portfolio.GetAccountsAsync()).EnsureSuccess().Value;
if (accounts.Count == 0)
{
    Console.WriteLine("No accounts found.");
    return;
}

var accountId = accounts[0].Id;
Console.WriteLine($"Using account: {accountId}");

// Step 2: Look up conids for SPY and QQQ
var spyResults = (await client.Contracts.SearchBySymbolAsync("SPY")).EnsureSuccess().Value;
var qqResults = (await client.Contracts.SearchBySymbolAsync("QQQ")).EnsureSuccess().Value;

var spyConid = spyResults.First(c => c.Symbol == "SPY").Conid;
var qqqConid = qqResults.First(c => c.Symbol == "QQQ").Conid;

Console.WriteLine($"SPY conid: {spyConid}");
Console.WriteLine($"QQQ conid: {qqqConid}");

// Step 3: Place a market order for 2 shares of SPY
Console.WriteLine("\nPlacing market order: BUY 2 SPY...");
var spyOrder = new OrderRequest
{
    Conid = spyConid,
    Side = "BUY",
    Quantity = 2,
    OrderType = "MKT",
    Tif = "DAY",
};

var spyResult = (await client.Orders.PlaceOrderAsync(accountId, spyOrder)).EnsureSuccess().Value;
var spyOrderId = await AutoConfirmAsync(client, spyResult, "SPY MKT");

// Step 4: Place a limit order for 1 share of QQQ at $500 (below market — should not fill)
Console.WriteLine("Placing limit order: BUY 1 QQQ @ $500 LMT...");
var qqqOrder = new OrderRequest
{
    Conid = qqqConid,
    Side = "BUY",
    Quantity = 1,
    OrderType = "LMT",
    Price = 500.00m,
    Tif = "DAY",
};

var qqqResult = (await client.Orders.PlaceOrderAsync(accountId, qqqOrder)).EnsureSuccess().Value;
var qqqOrderId = await AutoConfirmAsync(client, qqqResult, "QQQ LMT");

// Step 5: Wait for execution
Console.WriteLine("\nWaiting 5 seconds for orders to process...");
await Task.Delay(TimeSpan.FromSeconds(5));

// Step 6: Query order status
Console.WriteLine("Querying order status:\n");

if (spyOrderId is not null)
{
    var spyStatus = (await client.Orders.GetOrderStatusAsync(spyOrderId)).EnsureSuccess().Value;
    PrintStatus("SPY MKT", spyStatus);
}

if (qqqOrderId is not null)
{
    var qqqStatus = (await client.Orders.GetOrderStatusAsync(qqqOrderId)).EnsureSuccess().Value;
    PrintStatus("QQQ LMT", qqqStatus);
}

// Step 7: Show all live orders
Console.WriteLine("\nAll live orders:");
var liveOrders = (await client.Orders.GetLiveOrdersAsync()).EnsureSuccess().Value;
if (liveOrders.Count == 0)
{
    Console.WriteLine("  (none)");
}
else
{
    foreach (var o in liveOrders)
    {
        Console.WriteLine($"  {o.OrderId} {o.Ticker} {o.Side} {o.TotalSize:N0} {o.OrderType} — {o.Status}");
    }
}

// Step 8: Cancel the QQQ limit order if it's still working
if (qqqOrderId is not null)
{
    Console.WriteLine($"\nCancelling QQQ limit order {qqqOrderId}...");
    try
    {
        var cancelResult = (await client.Orders.CancelOrderAsync(accountId, qqqOrderId)).EnsureSuccess().Value;
        Console.WriteLine($"  Cancel response: {cancelResult.Message}");
    }
    catch (IbkrConduit.Errors.IbkrApiException ex)
    {
        Console.WriteLine($"  Cancel failed (order may already be filled): {ex.Message}");
    }
}

Console.WriteLine("\nDone.");

// --- Helpers ---

static void PrintStatus(string label, OrderStatus status)
{
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
        "  {0}: status={1}, filled={2:N0}/{3:N0}, avgPrice={4:N2}",
        label, status.Status, status.FilledQuantity ?? 0, status.TotalSize ?? 0, status.AvgFillPrice ?? 0));
}

static async Task<string?> AutoConfirmAsync(
    IIbkrClient client, OneOf.OneOf<OrderSubmitted, OrderConfirmationRequired> result, string label)
{
    return await result.Match<Task<string?>>(
        async submitted =>
        {
            Console.WriteLine($"  {label} submitted: orderId={submitted.OrderId}");
            await Task.CompletedTask;
            return submitted.OrderId;
        },
        async confirmation =>
        {
            Console.WriteLine($"  {label} requires confirmation:");
            foreach (var msg in confirmation.Messages)
            {
                Console.WriteLine($"    - {msg}");
            }

            Console.WriteLine($"  Confirming...");
            var reply = (await client.Orders.ReplyAsync(confirmation.ReplyId, true)).EnsureSuccess().Value;
            return reply.Match(
                submitted =>
                {
                    Console.WriteLine($"  {label} confirmed: orderId={submitted.OrderId}");
                    return (string?)submitted.OrderId;
                },
                furtherConfirmation =>
                {
                    Console.WriteLine($"  {label} requires further confirmation (not handled in this example).");
                    return null;
                });
        });
}
