using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Http;
using IbkrConduit.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneOf;

namespace IbkrConduit.Examples.OrderSubmit;

/// <summary>Parsed, validated order details from the command line.</summary>
internal sealed record ParsedOrder(
    string Side, decimal Quantity, string Symbol,
    string OrderType, decimal? Price, string Tif,
    string? OrderRef, string? Account, bool AutoConfirm, bool WhatIf);

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintHelp();
            return 0;
        }

        if (!TryParseArgs(args, out var parsed, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        const string credentialsPath = ".ibkr-credentials/ibkr-credentials.json";
        if (!File.Exists(credentialsPath))
        {
            Console.Error.WriteLine(
                $"Error: credentials file not found at {credentialsPath}. Run ibkr-conduit-setup first.");
            return 1;
        }

        using var credentials = OAuthCredentialsFactory.FromFile(credentialsPath);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddIbkrClient(opts => opts.Credentials = credentials);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        try
        {
            return await SubmitAsync(client, parsed!);
        }
        catch (IbkrConduit.Errors.IbkrApiException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> SubmitAsync(IIbkrClient client, ParsedOrder parsed)
    {
        // Resolve account (explicit --account or first discovered).
        var accountId = parsed.Account;
        if (string.IsNullOrEmpty(accountId))
        {
            var accounts = (await client.Portfolio.GetAccountsAsync()).EnsureSuccess().Value;
            if (accounts.Count == 0)
            {
                Console.Error.WriteLine("Error: no accounts found.");
                return 1;
            }

            accountId = accounts[0].Id;
        }

        // Resolve symbol -> conid (US stocks). IBKR can return several STK contracts sharing the
        // same symbol on different venues (a US listing plus foreign cross-listings), so prefer
        // the US primary listing rather than trusting the response ordering.
        var matches = (await client.Contracts.SearchBySymbolAsync(
            parsed.Symbol, secType: SecurityType.Stock)).EnsureSuccess().Value;
        var match = SelectStockContract(matches, parsed.Symbol);
        if (match is null)
        {
            Console.Error.WriteLine($"Error: could not resolve symbol '{parsed.Symbol}' to a stock contract.");
            return 1;
        }

        if (!IsUsPrimaryExchange(match.Description))
        {
            Console.Error.WriteLine(
                $"Warning: no US primary listing found for '{parsed.Symbol}'; using exchange '{match.Description}' (conid {match.Conid}).");
        }

        var orderRef = string.IsNullOrEmpty(parsed.OrderRef) ? GenerateOrderRef() : parsed.OrderRef;

        var order = new OrderRequest
        {
            Conid = match.Conid,
            Side = parsed.Side,
            Quantity = parsed.Quantity,
            OrderType = parsed.OrderType,
            Price = parsed.Price,
            Tif = parsed.Tif,
            CustomerOrderId = orderRef,
        };

        Console.WriteLine($"Account {accountId}: {parsed.Side} {parsed.Quantity} {parsed.Symbol} " +
            $"({parsed.OrderType}{(parsed.Price is { } p ? $" @ {p.ToString(CultureInfo.InvariantCulture)}" : string.Empty)}, " +
            $"{parsed.Tif}), order_ref={orderRef}");

        var contractName = string.IsNullOrEmpty(match.CompanyName) ? match.Description : match.CompanyName;
        Console.WriteLine($"Resolved: conid={match.Conid} {contractName} [{match.Description}]");

        if (parsed.WhatIf)
        {
            var whatIf = (await client.Orders.WhatIfOrderAsync(accountId, order)).EnsureSuccess().Value;
            Console.WriteLine("What-if preview:");
            Console.WriteLine($"  amount={whatIf.Amount?.Amount ?? "-"}, commission={whatIf.Amount?.Commission ?? "-"}, total={whatIf.Amount?.Total ?? "-"}");
            Console.WriteLine($"  initial margin={whatIf.Initial?.After ?? "-"}, maintenance={whatIf.Maintenance?.After ?? "-"}");
            if (!string.IsNullOrEmpty(whatIf.Warning)) { Console.WriteLine($"  warning: {whatIf.Warning}"); }
            if (!string.IsNullOrEmpty(whatIf.Error)) { Console.WriteLine($"  error: {whatIf.Error}"); }
            Console.WriteLine("Not submitted (--what-if).");

            // Exit non-zero when IBKR reports a preview error so scripts gating on the
            // exit code can distinguish a clean preview from a would-be rejection.
            return string.IsNullOrEmpty(whatIf.Error) ? 0 : 1;
        }

        var result = (await client.Orders.PlaceOrderAsync(accountId, order)).EnsureSuccess().Value;
        var orderId = await ResolveConfirmationAsync(client, result, parsed.AutoConfirm);
        if (orderId is null)
        {
            Console.WriteLine("Order was not submitted.");
            return 1;
        }

        Console.WriteLine($"Submitted: orderId={orderId}, order_ref={orderRef}. Watch it in OrderMonitor.");
        return 0;
    }

    /// <summary>
    /// Walks the IBKR confirmation chain. Auto-confirms when <paramref name="autoConfirm"/> is
    /// true; otherwise prompts y/n per confirmation. Returns the order id on success, or null if
    /// the user declined.
    /// </summary>
    private static async Task<string?> ResolveConfirmationAsync(
        IIbkrClient client, OneOf<OrderSubmitted, OrderConfirmationRequired> result, bool autoConfirm)
    {
        while (true)
        {
            if (result.IsT0)
            {
                return result.AsT0.OrderId;
            }

            var confirmation = result.AsT1;
            Console.WriteLine("IBKR requires confirmation:");
            foreach (var message in confirmation.Messages)
            {
                Console.WriteLine($"  - {message}");
            }

            if (!autoConfirm && !PromptYesNo("Confirm order?"))
            {
                return null;
            }

            result = (await client.Orders.ReplyAsync(confirmation.ReplyId, true)).EnsureSuccess().Value;
        }
    }

    private static bool PromptYesNo(string question)
    {
        Console.Write($"{question} [y/N] ");
        var answer = Console.ReadLine();
        return answer is not null && (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
            || answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Generates a unique-enough cOID for correlation in the monitor.</summary>
    private static string GenerateOrderRef()
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
        return $"submit-{now:HHmmss}-{suffix}";
    }

    /// <summary>
    /// The US primary listing venues an unqualified symbol should resolve to. Compared
    /// case-insensitively; anything else (foreign cross-listings like <c>MEXI</c>/<c>LSE</c>,
    /// or an absent exchange) is not a US primary listing.
    /// </summary>
    private static readonly string[] _usPrimaryExchanges =
    {
        "NASDAQ", "NYSE", "ARCA", "AMEX", "BATS",
    };

    /// <summary>
    /// Returns true when <paramref name="exchange"/> is a US primary listing venue
    /// (NASDAQ, NYSE, ARCA, AMEX, BATS). Case-insensitive; false for null, empty, or any
    /// non-US venue. For a symbol-search result the exchange is carried in
    /// <see cref="ContractSearchResult.Description"/> (e.g. "ARCA", "NASDAQ", "MEXI").
    /// </summary>
    internal static bool IsUsPrimaryExchange(string? exchange) =>
        !string.IsNullOrEmpty(exchange)
        && _usPrimaryExchanges.Contains(exchange, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Chooses the stock contract to trade from an IBKR symbol-search result. Considers only
    /// exact symbol matches (case-insensitive) and prefers the first US primary listing (by the
    /// result's <see cref="ContractSearchResult.Description"/> exchange); if none of the matches
    /// are US-listed, falls back to the first exact-symbol match. Returns null when no result
    /// matches the symbol at all.
    /// </summary>
    internal static ContractSearchResult? SelectStockContract(
        IReadOnlyList<ContractSearchResult> matches, string symbol)
    {
        var exact = matches
            .Where(c => string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return exact.FirstOrDefault(c => IsUsPrimaryExchange(c.Description))
            ?? exact.FirstOrDefault();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-submit <BUY|SELL> <QTY> <SYMBOL> [--market | --limit <price>] [options]");
        Console.WriteLine();
        Console.WriteLine("Submits a single US-stock order to the paper account.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --market               Market order (default).");
        Console.WriteLine("  --limit <price>        Limit order at <price>. Exclusive with --market.");
        Console.WriteLine("  --tif <DAY|GTC|IOC>    Time in force (default DAY).");
        Console.WriteLine("  --order-ref <str>      Customer order id (cOID). Auto-generated if omitted.");
        Console.WriteLine("  --account <id>         Account to submit under (default: first discovered).");
        Console.WriteLine("  --yes                  Auto-confirm IBKR order warnings (no prompt).");
        Console.WriteLine("  --what-if              Preview commission/margin; do not submit.");
        Console.WriteLine("  -h, --help, /?         Show this help and exit.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ibkr-conduit-submit BUY 100 AAPL");
        Console.WriteLine("  ibkr-conduit-submit BUY 1 QQQ --limit 500");
        Console.WriteLine("  ibkr-conduit-submit SELL 2 SPY --tif GTC --yes");
    }

    /// <summary>
    /// Parses the submit CLI into a validated <see cref="ParsedOrder"/>. Returns false with a
    /// non-empty <paramref name="error"/> on bad input. Does no I/O and generates no cOID —
    /// callers substitute a generated cOID when <see cref="ParsedOrder.OrderRef"/> is null.
    /// </summary>
    internal static bool TryParseArgs(string[] args, out ParsedOrder? order, out string error)
    {
        order = null;
        error = string.Empty;

        var positionals = new List<string>();
        string? tif = null;
        string? orderRef = null;
        string? account = null;
        var market = false;
        decimal? limitPrice = null;
        var autoConfirm = false;
        var whatIf = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--market":
                    market = true;
                    continue;

                case "--limit":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --limit requires a price (e.g. --limit 185.00).";
                        return false;
                    }

                    if (!decimal.TryParse(args[i + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
                    {
                        error = $"Error: --limit price must be a positive number (got '{args[i + 1]}').";
                        return false;
                    }

                    limitPrice = price;
                    i++;
                    continue;

                case "--tif":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --tif requires a value (DAY, GTC, IOC).";
                        return false;
                    }

                    var tifValue = args[i + 1].ToUpperInvariant();
                    if (tifValue is not ("DAY" or "GTC" or "IOC"))
                    {
                        error = $"Error: --tif must be one of DAY, GTC, IOC (got '{args[i + 1]}').";
                        return false;
                    }

                    tif = tifValue;
                    i++;
                    continue;

                case "--order-ref":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --order-ref requires a value.";
                        return false;
                    }

                    orderRef = args[i + 1];
                    i++;
                    continue;

                case "--account":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --account requires a value.";
                        return false;
                    }

                    account = args[i + 1];
                    i++;
                    continue;

                case "--yes":
                    autoConfirm = true;
                    continue;

                case "--what-if":
                    whatIf = true;
                    continue;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Error: unknown flag '{arg}'.";
                        return false;
                    }

                    positionals.Add(arg);
                    continue;
            }
        }

        if (market && limitPrice is not null)
        {
            error = "Error: --market and --limit are mutually exclusive.";
            return false;
        }

        if (positionals.Count != 3)
        {
            error = "Error: expected exactly 3 positional arguments: <BUY|SELL> <QTY> <SYMBOL>.";
            return false;
        }

        var side = positionals[0].ToUpperInvariant();
        if (side is not ("BUY" or "SELL"))
        {
            error = $"Error: side must be BUY or SELL (got '{positionals[0]}').";
            return false;
        }

        if (!decimal.TryParse(positionals[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            error = $"Error: quantity must be a positive number (got '{positionals[1]}').";
            return false;
        }

        var symbol = positionals[2].ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            error = "Error: symbol must not be empty.";
            return false;
        }

        var orderType = limitPrice is not null ? "LMT" : "MKT";
        order = new ParsedOrder(
            side, quantity, symbol, orderType, limitPrice, tif ?? "DAY",
            orderRef, account, autoConfirm, whatIf);
        return true;
    }
}
