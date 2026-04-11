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

        var outputDir = "./.ibkr-credentials";
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

        // Generate consumer key
        var consumerKey = KeyGenerator.GenerateConsumerKey();
        Console.WriteLine();
        ConsoleHelper.WriteSuccess($"Generated Consumer Key: {consumerKey}");
        Console.WriteLine();

        // Step 2: Portal Instructions
        ConsoleHelper.WriteStep(2, 4, "Configure OAuth in the IBKR Portal");
        Console.WriteLine();
        Console.WriteLine("  Follow these steps in the IBKR Self-Service Portal:");
        Console.WriteLine();
        Console.WriteLine("  1. Log in at: https://ndcdyn.interactivebrokers.com/sso/Login?action=OAUTH");
        Console.WriteLine("     (This goes directly to the OAuth configuration page)");
        Console.WriteLine();
        Console.WriteLine("  2. If this is your first time, accept the OAuth agreement");
        Console.WriteLine("     (check the box, type your name, and submit)");
        Console.WriteLine();
        Console.WriteLine("  3. Check the 'Enabled' checkbox if not already enabled");
        Console.WriteLine();
        Console.WriteLine($"  4. Enter the following Consumer Key in the text field and click 'Save Key':");
        Console.WriteLine($"     {consumerKey}");
        Console.WriteLine();
        Console.WriteLine("  5. Upload the 3 key files using the upload controls on the page:");
        Console.WriteLine("     (Ignore the OpenSSL commands shown on the page — our tool already generated the keys)");
        Console.WriteLine($"     - Public Signing Key:     {Path.Combine(fullPath, "public_signature.pem")}");
        Console.WriteLine($"     - Public Encryption Key:  {Path.Combine(fullPath, "public_encryption.pem")}");
        Console.WriteLine($"     - Diffie-Hellman Params:  {Path.Combine(fullPath, "dhparam.pem")}");
        Console.WriteLine();
        Console.WriteLine("  6. Click 'Generate Token' at the bottom of the page");
        Console.WriteLine("     This will populate the Access Token and Access Token Secret fields");
        Console.WriteLine();
        Console.WriteLine("  7. Copy the Access Token and Access Token Secret BEFORE leaving the page");
        Console.WriteLine("     (They disappear on refresh — you can regenerate them later if needed)");
        Console.WriteLine();

        ConsoleHelper.WaitForEnter("Press Enter when you have completed the portal setup...");
        Console.WriteLine();

        // Step 3: Collect Credentials
        ConsoleHelper.WriteStep(3, 4, "Collect Portal Credentials");
        Console.WriteLine();
        Console.WriteLine("  The Consumer Key has already been generated for you.");
        Console.WriteLine("  Enter the remaining values from the IBKR portal:");
        Console.WriteLine();

        var configResult = ConfigureCommand.Run(outputDir, presetConsumerKey: consumerKey);
        if (configResult != 0)
        {
            return configResult;
        }

        Console.WriteLine();

        // Step 4: Validate
        ConsoleHelper.WriteStep(4, 4, "Validate Connection");
        Console.WriteLine();

        var jsonPath = Path.Combine(fullPath, "ibkr-credentials.json");

        Console.WriteLine("  NOTE: IBKR OAuth access can take up to a few days to activate after");
        Console.WriteLine("  initial configuration. If validation fails, this is likely the reason.");
        Console.WriteLine();

        var validateResult = await ValidateCommand.RunAsync(jsonPath);

        Console.WriteLine();
        Console.WriteLine("==========================================================");

        if (validateResult == 0)
        {
            ConsoleHelper.WriteSuccess("Setup complete! Credentials are valid.");
        }
        else
        {
            ConsoleHelper.WriteWarning("Credential file saved, but validation failed.");
            Console.WriteLine();
            Console.WriteLine("  This is expected if your OAuth access was just configured —");
            Console.WriteLine("  IBKR can take up to a few days to activate new OAuth credentials.");
            Console.WriteLine();
            Console.WriteLine("  Try again later with:");
            Console.WriteLine($"    ibkr-conduit-setup validate --credentials \"{jsonPath}\"");
        }

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

        Console.WriteLine();
        ConsoleHelper.WriteWarning("Security reminder: The credential file contains private keys.");
        ConsoleHelper.WriteWarning("Add 'ibkr-credentials.json' and '*.pem' to your .gitignore.");
        Console.WriteLine("==========================================================");

        // Return 0 even if validation failed — the credential file was saved successfully.
        // Validation failure is expected for newly configured OAuth and not a setup error.
        return 0;
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
