#!/usr/bin/env dotnet
#:project ../src/IbkrConduit/IbkrConduit.csproj
#:package Microsoft.Extensions.Logging.Console
#:property PublishAot=false

using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials from environment variables.
// See tools/set-e2e-env.sh for the required variables.
using var credentials = OAuthCredentialsFactory.FromEnvironment();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(credentials);

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// Retrieve live orders (cancelled, filled, submitted)
var orders = await client.Orders.GetLiveOrdersAsync();

if (orders.Count == 0)
{
    Console.WriteLine("No live orders.");
    return;
}

Console.WriteLine($"{orders.Count} live order(s):");
Console.WriteLine(
    string.Format(CultureInfo.InvariantCulture,
        "{0,-12} {1,-6} {2,-6} {3,8} {4,10} {5,-12} {6,10} {7,10}",
        "Order ID", "Symbol", "Side", "Qty", "Type", "Status", "Filled", "Remaining"));
Console.WriteLine(new string('-', 82));

foreach (var o in orders)
{
    Console.WriteLine(
        string.Format(CultureInfo.InvariantCulture,
            "{0,-12} {1,-6} {2,-6} {3,8:N0} {4,10} {5,-12} {6,10:N0} {7,10:N0}",
            o.OrderId, o.Ticker, o.Side, o.TotalSize, o.OrderType, o.Status,
            o.FilledQuantity, o.RemainingQuantity));
}
