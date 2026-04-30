using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Examples.MarketDataStream;

internal static class Program
{
    private static readonly string[] _defaultSymbols =
    {
        "EUR.USD", "GBP.USD", "USD.JPY", "AUD.USD",
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintHelp();
            return 0;
        }

        if (!TryParseArgs(args, out var symbols, out var duration, out var durationDisplay, out var logFilePath, out var argError))
        {
            Console.Error.WriteLine(argError);
            return 2;
        }

        const string credentialsPath = ".ibkr-credentials/ibkr-credentials.json";
        if (!File.Exists(credentialsPath))
        {
            Console.Error.WriteLine(
                $"Error: credentials file not found at {credentialsPath}. Run ibkr-conduit-setup first.");
            return 1;
        }

        // credentials must outlive provider: SessionTokenProvider holds a reference
        // to the RSA keys inside credentials and calls into them on every token refresh.
        // C# using disposes in reverse declaration order, so provider (declared below)
        // disposes first — keep this ordering on any future refactor.
        using var credentials = OAuthCredentialsFactory.FromFile(credentialsPath);

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            // Lower the framework's global minimum so the file provider (when
            // enabled) can capture Debug+ even while the console stays quiet.
            // The console-specific filter below clamps console output to Warning+.
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddConsole();
            b.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>(
                level => level >= LogLevel.Warning);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                b.AddProvider(new FileLoggerProvider(logFilePath));
            }
        });
        services.AddIbkrClient(opts => opts.Credentials = credentials);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MarketDataStream");

        using var cts = new CancellationTokenSource();
        if (duration is { } d)
        {
            cts.CancelAfter(d);
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // prevent immediate process termination on first Ctrl+C
            cts.Cancel();
        };

        var bannerSuffix = duration is null
            ? "Press Ctrl+C to exit."
            : $"Press Ctrl+C to exit (auto-exits in {durationDisplay}).";
        Console.WriteLine($"Streaming {symbols.Count} symbols. {bannerSuffix}");

        try
        {
            var totalTicks = await StreamHost.RunAsync(client, symbols, logger, cts.Token);
            Console.WriteLine($"Streamed {totalTicks} ticks across {symbols.Count} symbols.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled.");
            return 0;
        }
        catch (InvalidOperationException ex)
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

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-stream [SYMBOL...] [--duration <timespan>] [--log-file <path>] [--help]");
        Console.WriteLine();
        Console.WriteLine("Subscribes to live market data for one or more symbols and renders ticks");
        Console.WriteLine("to a continuously-updating Spectre.Console table.");
        Console.WriteLine();
        Console.WriteLine("Symbols:");
        Console.WriteLine("  AAPL                   US stock (no dot)");
        Console.WriteLine("  EUR.USD                Forex pair, format BASE.QUOTE");
        Console.WriteLine("  AAPL EUR.USD           Mix asset classes freely");
        Console.WriteLine("  (no symbols given)     Defaults to: EUR.USD, GBP.USD, USD.JPY, AUD.USD");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --duration <timespan>  Auto-exit after this duration. Accepted forms:");
        Console.WriteLine("                           60s, 5m, 1h        Shorthand suffixes");
        Console.WriteLine("                           00:01:30           TimeSpan literal");
        Console.WriteLine("                         If omitted, runs until Ctrl+C.");
        Console.WriteLine("  --log-file <path>      Tee all log lines (Debug+ from every category) to a file");
        Console.WriteLine("                         in addition to the console. Useful when the live table");
        Console.WriteLine("                         clobbers important warnings before you can read them.");
        Console.WriteLine("  -h, --help, /?         Show this help and exit.");
        Console.WriteLine();
        Console.WriteLine("Prerequisites:");
        Console.WriteLine("  A populated .ibkr-credentials/ibkr-credentials.json in the current");
        Console.WriteLine("  working directory. Run `ibkr-conduit-setup` to generate one.");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  Success or graceful cancellation.");
        Console.WriteLine("  1  Runtime error (resolver/stream/credentials).");
        Console.WriteLine("  2  Bad CLI arguments.");
    }

    /// <summary>
    /// Parses positional symbols and an optional <c>--duration &lt;timespan&gt;</c> flag.
    /// Returns false with an <paramref name="error"/> message on bad input.
    /// <paramref name="durationDisplay"/> is the raw user-supplied string (e.g. "60s") so the
    /// banner can echo exactly what the user typed rather than a re-formatted TimeSpan.
    /// </summary>
    internal static bool TryParseArgs(
        string[] args,
        out IReadOnlyList<string> symbols,
        out TimeSpan? duration,
        out string? durationDisplay,
        out string? logFilePath,
        out string error)
    {
        symbols = Array.Empty<string>();
        duration = null;
        durationDisplay = null;
        logFilePath = null;
        error = string.Empty;

        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--duration")
            {
                if (i + 1 >= args.Length)
                {
                    error = "Error: --duration requires a value (e.g. 60s, 5m, 00:01:30).";
                    return false;
                }

                if (!TryParseDuration(args[i + 1], out var parsed))
                {
                    error = "Error: --duration must be a TimeSpan (e.g. 60s, 5m, 00:01:30).";
                    return false;
                }

                duration = parsed;
                durationDisplay = args[i + 1];
                i++;
                continue;
            }

            if (args[i] == "--log-file")
            {
                if (i + 1 >= args.Length)
                {
                    error = "Error: --log-file requires a path (e.g. ./debug.log).";
                    return false;
                }

                logFilePath = args[i + 1];
                i++;
                continue;
            }

            positional.Add(args[i]);
        }

        symbols = positional.Count > 0 ? positional : _defaultSymbols;
        return true;
    }

    private static bool TryParseDuration(string value, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        // "60s", "5m", "1h" shortcuts (case-insensitive).
        if (value.Length >= 2)
        {
            var suffix = char.ToLowerInvariant(value[^1]);
            var numberPart = value[..^1];
            if (double.TryParse(numberPart, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                && double.IsFinite(n) && n >= 0)
            {
                if (suffix == 's') { result = TimeSpan.FromSeconds(n); return true; }
                if (suffix == 'm') { result = TimeSpan.FromMinutes(n); return true; }
                if (suffix == 'h') { result = TimeSpan.FromHours(n); return true; }
            }
        }

        // Standard TimeSpan format ("00:01:30").
        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}
