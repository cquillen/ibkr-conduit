using System.Globalization;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Examples.OrderMonitor;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            PrintHelp();
            return 0;
        }

        if (!TryParseArgs(args, out var realtimeOnly, out var days, out var duration,
                out var durationDisplay, out var logFilePath, out var logLevel, out var error))
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
        var panelBuffer = new PanelLogBuffer();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(logLevel);
            b.AddProvider(panelBuffer);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                b.AddProvider(new FileLoggerProvider(logFilePath));
            }
        });
        services.AddIbkrClient(opts => opts.Credentials = credentials);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IIbkrClient>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("OrderMonitor");

        using var cts = new CancellationTokenSource();
        if (duration is { } d)
        {
            cts.CancelAfter(d);
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var mode = realtimeOnly ? "realtime-only" : $"including {days}d history";
        var bannerSuffix = duration is null
            ? "Press Ctrl+C to exit."
            : $"Press Ctrl+C to exit (auto-exits in {durationDisplay}).";
        Console.WriteLine($"Monitoring orders + executions ({mode}). {bannerSuffix}");

        try
        {
            await OrderMonitorHost.RunAsync(client, realtimeOnly, days, logger, panelBuffer, cts.Token);
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
        Console.WriteLine("Usage: ibkr-conduit-order-monitor [--realtime-only] [--days <n>] [--duration <timespan>] [--log-file <path>] [--log-level <level>] [--help]");
        Console.WriteLine();
        Console.WriteLine("Streams the account's order-status and trade-execution streams and renders");
        Console.WriteLine("them to a continuously-updating pair of Spectre.Console tables.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --realtime-only        Suppress historical replay; show only post-launch activity.");
        Console.WriteLine("  --days <n>             Days of history to replay on subscribe (default 1).");
        Console.WriteLine("  --duration <timespan>  Auto-exit after this duration (60s, 5m, 1h, 00:01:30).");
        Console.WriteLine("  --log-file <path>      Tee all log lines to a file in addition to the panel.");
        Console.WriteLine("  --log-level <level>    Min level for the file provider (default Debug).");
        Console.WriteLine("  -h, --help, /?         Show this help and exit.");
        Console.WriteLine();
        Console.WriteLine("Prerequisites:");
        Console.WriteLine("  A populated .ibkr-credentials/ibkr-credentials.json in the current working directory.");
    }

    /// <summary>
    /// Parses OrderMonitor's CLI. Returns false with an <paramref name="error"/> message on bad input.
    /// <paramref name="durationDisplay"/> echoes the raw user-supplied duration string for the banner.
    /// </summary>
    internal static bool TryParseArgs(
        string[] args,
        out bool realtimeOnly,
        out int days,
        out TimeSpan? duration,
        out string? durationDisplay,
        out string? logFilePath,
        out LogLevel logLevel,
        out string error)
    {
        realtimeOnly = false;
        days = 1;
        duration = null;
        durationDisplay = null;
        logFilePath = null;
        logLevel = LogLevel.Debug;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--realtime-only":
                    realtimeOnly = true;
                    continue;

                case "--days":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --days requires a value (e.g. 1, 7).";
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) || d < 0)
                    {
                        error = $"Error: --days must be a non-negative integer (got '{args[i + 1]}').";
                        return false;
                    }

                    days = d;
                    i++;
                    continue;

                case "--duration":
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

                case "--log-file":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --log-file requires a path (e.g. ./debug.log).";
                        return false;
                    }

                    logFilePath = args[i + 1];
                    i++;
                    continue;

                case "--log-level":
                    if (i + 1 >= args.Length)
                    {
                        error = "Error: --log-level requires a value (Trace, Debug, Information, Warning, Error, Critical, None).";
                        return false;
                    }

                    if (!Enum.TryParse<LogLevel>(args[i + 1], ignoreCase: true, out var parsedLevel))
                    {
                        error = $"Error: --log-level must be one of Trace, Debug, Information, Warning, Error, Critical, None (got '{args[i + 1]}').";
                        return false;
                    }

                    logLevel = parsedLevel;
                    i++;
                    continue;

                default:
                    error = $"Error: unknown argument '{args[i]}'. OrderMonitor takes no positional arguments.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseDuration(string value, out TimeSpan result)
    {
        result = TimeSpan.Zero;

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

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}
