using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Validates credentials against the IBKR API.
/// </summary>
internal static class ValidateCommand
{
    /// <summary>
    /// Runs the validate subcommand.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (ArgParser.HasFlag(args, "--help", "-h"))
        {
            ShowHelp();
            return 0;
        }

        var credentialsPath = ArgParser.GetOption(args, "--credentials");
        if (credentialsPath is null)
        {
            ConsoleHelper.WriteError("Missing required option: --credentials <path-to-json>");
            Console.WriteLine();
            ShowHelp();
            return 1;
        }

        return await RunAsync(credentialsPath);
    }

    /// <summary>
    /// Core logic shared with the wizard. Returns exit code.
    /// </summary>
    internal static async Task<int> RunAsync(string credentialsPath)
    {
        var fullPath = Path.GetFullPath(credentialsPath);

        if (!File.Exists(fullPath))
        {
            ConsoleHelper.WriteError($"Credential file not found: {fullPath}");
            return 1;
        }

        Console.WriteLine($"Validating credentials from: {fullPath}");
        Console.WriteLine();

        IbkrOAuthCredentials? creds = null;
        try
        {
            creds = OAuthCredentialsFactory.FromFile(fullPath);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddIbkrClient(opts => opts.Credentials = creds);

            await using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IIbkrClient>();

            await client.ValidateConnectionAsync(validateFlex: false);

            ConsoleHelper.WriteSuccess("Validation passed. Your credentials are correctly configured.");
            return 0;
        }
        catch (IbkrConfigurationException ex)
        {
            ConsoleHelper.WriteError($"Configuration error: {ex.Message}");
            if (ex.CredentialHint is not null)
            {
                ConsoleHelper.WriteWarning($"Hint: Check the '{ex.CredentialHint}' field in your credential file.");
            }

            return 1;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Validation failed: {ex.Message}");
            return 1;
        }
        finally
        {
            creds?.Dispose();
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-setup validate --credentials <path-to-json>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --credentials <path>   Path to the ibkr-credentials.json file (required)");
        Console.WriteLine("  --help, -h             Show this help message");
    }
}
