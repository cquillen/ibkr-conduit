namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Runs the full setup wizard (generate keys, portal walkthrough, configure, validate).
/// </summary>
internal static class WizardCommand
{
    /// <summary>
    /// Runs the wizard (default command).
    /// </summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (ArgParser.HasFlag(args, "--help", "-h"))
        {
            ShowHelp();
            return 0;
        }

        Console.WriteLine("==========================================================");
        Console.WriteLine("  IBKR Conduit — OAuth Credential Setup");
        Console.WriteLine("==========================================================");
        Console.WriteLine();
        Console.WriteLine("This wizard will guide you through the complete setup:");
        Console.WriteLine("  1. Generate cryptographic keys");
        Console.WriteLine("  2. Upload keys to the IBKR portal");
        Console.WriteLine("  3. Collect portal credentials");
        Console.WriteLine("  4. Validate the connection");
        Console.WriteLine();

        // Step 1: Generate Keys
        ConsoleHelper.WriteStep(1, 4, "Generate Cryptographic Keys");
        Console.WriteLine();

        var outputDir = "./ibkr-credentials";
        Console.Write($"Output directory [{outputDir}]: ");
        var customDir = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customDir))
        {
            outputDir = customDir;
        }

        var force = false;
        var fullPath = Path.GetFullPath(outputDir);
        if (Directory.Exists(fullPath) && Directory.GetFiles(fullPath).Length > 0)
        {
            force = ConsoleHelper.Confirm($"Files already exist in {fullPath}. Overwrite?");
            if (!force)
            {
                ConsoleHelper.WriteWarning("Using existing key files.");
            }
        }

        if (force || !Directory.Exists(fullPath) || Directory.GetFiles(fullPath).Length == 0)
        {
            var result = GenerateKeysCommand.Run(outputDir, force: true);
            if (result != 0)
            {
                return result;
            }
        }

        Console.WriteLine();

        // Step 2: Portal Instructions
        ConsoleHelper.WriteStep(2, 4, "Upload Keys to the IBKR Portal");
        Console.WriteLine();
        Console.WriteLine("  Follow these steps in the IBKR Self-Service Portal:");
        Console.WriteLine();
        Console.WriteLine("  1. Log in at: https://www.interactivebrokers.com/sso/Login");
        Console.WriteLine("  2. Navigate to: Settings > User Settings > API > OAuth");
        Console.WriteLine("  3. Click 'Add New Application'");
        Console.WriteLine("  4. Upload the following files:");
        Console.WriteLine($"     - Signature Public Key:  {Path.Combine(fullPath, "public_signature.pem")}");
        Console.WriteLine($"     - Encryption Public Key: {Path.Combine(fullPath, "public_encryption.pem")}");
        Console.WriteLine($"     - DH Parameters:         {Path.Combine(fullPath, "dhparam.pem")}");
        Console.WriteLine("  5. Click 'Create' to generate credentials");
        Console.WriteLine("  6. Copy the Consumer Key, Access Token, and Access Token Secret");
        Console.WriteLine();

        ConsoleHelper.WaitForEnter("Press Enter when you have completed the portal setup...");
        Console.WriteLine();

        // Step 3: Collect Credentials
        ConsoleHelper.WriteStep(3, 4, "Collect Portal Credentials");
        Console.WriteLine();
        Console.WriteLine("Enter the values from the IBKR portal:");
        Console.WriteLine();

        var configResult = ConfigureCommand.Run(outputDir);
        if (configResult != 0)
        {
            return configResult;
        }

        Console.WriteLine();

        // Step 4: Validate
        ConsoleHelper.WriteStep(4, 4, "Validate Connection");
        Console.WriteLine();

        var jsonPath = Path.Combine(fullPath, "ibkr-credentials.json");
        var validateResult = await ValidateCommand.RunAsync(jsonPath);

        Console.WriteLine();
        Console.WriteLine("==========================================================");

        if (validateResult == 0)
        {
            ConsoleHelper.WriteSuccess("Setup complete!");
            Console.WriteLine();
            Console.WriteLine("Usage example:");
            Console.WriteLine();
            Console.WriteLine("  using IbkrConduit.Auth;");
            Console.WriteLine("  using IbkrConduit.Http;");
            Console.WriteLine("  using Microsoft.Extensions.DependencyInjection;");
            Console.WriteLine();
            Console.WriteLine("  using var creds = OAuthCredentialsFactory.FromFile(");
            Console.WriteLine($"      \"{jsonPath}\");");
            Console.WriteLine("  var services = new ServiceCollection();");
            Console.WriteLine("  services.AddLogging();");
            Console.WriteLine("  services.AddIbkrClient(opts => opts.Credentials = creds);");
            Console.WriteLine("  await using var provider = services.BuildServiceProvider();");
            Console.WriteLine("  var client = provider.GetRequiredService<IIbkrClient>();");
        }
        else
        {
            ConsoleHelper.WriteWarning("Setup completed with validation errors.");
            Console.WriteLine("You can re-run validation later with:");
            Console.WriteLine($"  ibkr-conduit-setup validate --credentials \"{jsonPath}\"");
        }

        Console.WriteLine();
        ConsoleHelper.WriteWarning("Security reminder: The credential file contains private keys.");
        ConsoleHelper.WriteWarning("Add 'ibkr-credentials.json' and '*.pem' to your .gitignore.");
        Console.WriteLine("==========================================================");

        return validateResult;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-setup [wizard]");
        Console.WriteLine();
        Console.WriteLine("Runs the full setup wizard: generate keys, upload to portal,");
        Console.WriteLine("collect credentials, and validate the connection.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h    Show this help message");
    }
}
