using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load credentials from environment variables.
using var creds = OAuthCredentialsFactory.FromEnvironment();
// Redact the consumer key rather than echoing it in full — mirrors the
// IbkrOAuthCredentials.ToString redaction convention (source of truth), which
// renders the consumer key as "[redacted]" so no credential material reaches a log.
Console.WriteLine("Consumer key: [redacted]");

// Wire up via DI — exactly as a real consumer would (see examples/ and .claude/rules/testing.md).
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.Compete = true;
});

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IIbkrClient>();

// --- Query accounts ---
Console.WriteLine("\n=== ACCOUNTS ===");
var accountsResult = await client.Portfolio.GetAccountsAsync();
if (!accountsResult.IsSuccess)
{
    PrintError("accounts", accountsResult.Error);
    Console.WriteLine("\nCannot continue without accounts. Done.");
    return;
}

var accounts = accountsResult.Value;
if (accounts.Count == 0)
{
    Console.WriteLine("  No accounts found.");
}
else
{
    foreach (var acct in accounts)
    {
        Console.WriteLine($"  {acct.Id} — {acct.AccountTitle} ({acct.Type})");
    }
}

// --- Query live orders ---
Console.WriteLine("\n=== LIVE ORDERS ===");
// The endpoint primes on the first call, so read again and use the primed snapshot —
// IsSnapshot==false means "not yet primed", not "no orders" (design doc §10.6).
_ = await client.Orders.GetLiveOrdersAsync();
var liveOrdersResult = await client.Orders.GetLiveOrdersAsync();
if (!liveOrdersResult.IsSuccess)
{
    PrintError("live orders", liveOrdersResult.Error);
}
else
{
    var snapshot = liveOrdersResult.Value;
    if (snapshot.Orders.Count == 0)
    {
        Console.WriteLine(snapshot.IsSnapshot
            ? "  No live orders in current session."
            : "  Live-order snapshot not yet primed — no authoritative order list this call.");
    }
    else
    {
        foreach (var order in snapshot.Orders)
        {
            Console.WriteLine($"  Order {order.OrderId}: {order.Side} {order.TotalSize} {order.Ticker} — Status: {order.Status} (Filled: {order.FilledQuantity}, Remaining: {order.RemainingQuantity})");
        }
    }
}

// --- Query trades ---
Console.WriteLine("\n=== TRADES ===");
var tradesResult = await client.Orders.GetTradesAsync();
if (!tradesResult.IsSuccess)
{
    PrintError("trades", tradesResult.Error);
}
else
{
    var trades = tradesResult.Value;
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

// --- Query positions (public portfolio surface) ---
Console.WriteLine("\n=== POSITIONS ===");
if (accounts.Count == 0)
{
    Console.WriteLine("  No account to query positions for.");
}
else
{
    var accountId = accounts[0].Id;
    var positionsResult = await client.Portfolio.GetPositionsAsync(accountId);
    if (!positionsResult.IsSuccess)
    {
        PrintError("positions", positionsResult.Error);
    }
    else
    {
        var positions = positionsResult.Value;
        if (positions.Count == 0)
        {
            Console.WriteLine($"  No positions in {accountId}.");
        }
        else
        {
            foreach (var p in positions)
            {
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture,
                        "  {0,-10} qty={1,10:N0} mktPrice={2,12:N2} mktValue={3,14:N2} unrealizedPnl={4,14:N2}",
                        p.Ticker ?? p.ContractDescription, p.Quantity, p.MarketPrice, p.MarketValue, p.UnrealizedPnl));
            }
        }
    }
}

Console.WriteLine("\nDone.");

// Unwrap a failed Result by pattern-matching the IbkrError taxonomy — never echo a raw
// credential; the taxonomy fields carry only status/message/path, no secret material.
static void PrintError(string operation, IbkrError error)
{
    var detail = error switch
    {
        IbkrSessionError s =>
            $"session error (competing={s.IsCompeting}, status={s.StatusCode}): {s.Message}",
        IbkrRateLimitError r =>
            $"rate limited (retryAfter={r.RetryAfter}, status={r.StatusCode}): {r.Message}",
        IbkrOrderRejectedError o =>
            $"order rejected: {o.RejectionMessage}",
        IbkrAmbiguousOrderError a =>
            $"ambiguous order outcome (status={a.StatusCode}, reauthSucceeded={a.ReauthSucceeded}): {a.Message}",
        IbkrFlexError f =>
            $"flex error (code={f.ErrorCode}, retryable={f.IsRetryable}): {f.CodeDescription ?? f.Message}",
        IbkrHiddenError h =>
            $"hidden error (200 OK with error body): {h.Message}",
        IbkrApiError e =>
            $"API error (status={e.StatusCode}): {e.Message}",
        _ => $"{error.GetType().Name} (status={error.StatusCode}): {error.Message}",
    };

    Console.WriteLine($"  Error querying {operation}: {detail}");
}
