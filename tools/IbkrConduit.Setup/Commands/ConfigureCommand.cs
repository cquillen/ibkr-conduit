namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Collects portal credentials and writes the JSON credential file.
/// </summary>
internal static class ConfigureCommand
{
    /// <summary>
    /// Runs the configure subcommand.
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        if (ArgParser.HasFlag(args, "--help", "-h"))
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        var credentialsDir = ArgParser.GetOption(args, "--credentials") ?? "./.ibkr-credentials";
        var consumerKey = ArgParser.GetOption(args, "--consumer-key");
        var accessToken = ArgParser.GetOption(args, "--access-token");
        var accessTokenSecret = ArgParser.GetOption(args, "--access-token-secret");

        return Task.FromResult(Run(credentialsDir, consumerKey, accessToken, accessTokenSecret));
    }

    /// <summary>
    /// Core logic shared with the wizard. Returns exit code.
    /// </summary>
    /// <param name="credentialsDir">Directory containing private key PEM files.</param>
    /// <param name="consumerKey">Consumer key (from CLI flag or null for interactive prompt).</param>
    /// <param name="accessToken">Access token (from CLI flag or null for interactive prompt).</param>
    /// <param name="accessTokenSecret">Access token secret (from CLI flag or null for interactive prompt).</param>
    /// <param name="presetConsumerKey">Pre-generated consumer key from the wizard. Skips prompting for it.</param>
    internal static int Run(
        string credentialsDir,
        string? consumerKey = null,
        string? accessToken = null,
        string? accessTokenSecret = null,
        string? presetConsumerKey = null)
    {
        var fullPath = Path.GetFullPath(credentialsDir);

        var signaturePemPath = Path.Combine(fullPath, "private_signature.pem");
        var encryptionPemPath = Path.Combine(fullPath, "private_encryption.pem");

        if (!File.Exists(signaturePemPath))
        {
            ConsoleHelper.WriteError($"Private signature key not found: {signaturePemPath}");
            ConsoleHelper.WriteError("Run 'ibkr-conduit-setup generate-keys' first.");
            return 1;
        }

        if (!File.Exists(encryptionPemPath))
        {
            ConsoleHelper.WriteError($"Private encryption key not found: {encryptionPemPath}");
            ConsoleHelper.WriteError("Run 'ibkr-conduit-setup generate-keys' first.");
            return 1;
        }

        var dhPrimeHexPath = Path.Combine(fullPath, ".dhprime.hex");
        if (!File.Exists(dhPrimeHexPath))
        {
            ConsoleHelper.WriteError($"DH prime hex file not found: {dhPrimeHexPath}");
            ConsoleHelper.WriteError("Run 'ibkr-conduit-setup generate-keys' first.");
            return 1;
        }

        var signaturePrivateKeyPem = File.ReadAllText(signaturePemPath);
        var encryptionPrivateKeyPem = File.ReadAllText(encryptionPemPath);
        var dhPrimeHex = File.ReadAllText(dhPrimeHexPath).Trim();

        // Use preset consumer key if provided (wizard flow generates it)
        consumerKey ??= presetConsumerKey;

        if (consumerKey is null || accessToken is null || accessTokenSecret is null)
        {
            consumerKey ??= ConsoleHelper.PromptWithValidation(
                "Consumer Key (9 uppercase letters): ",
                CredentialFile.ValidateConsumerKey);

            accessToken ??= ConsoleHelper.PromptWithValidation(
                "Access Token: ",
                CredentialFile.ValidateAccessToken);

            accessTokenSecret ??= ConsoleHelper.PromptWithValidation(
                "Access Token Secret (encrypted, base64): ",
                CredentialFile.ValidateAccessTokenSecret);
        }
        else
        {
            var consumerKeyError = CredentialFile.ValidateConsumerKey(consumerKey);
            if (consumerKeyError is not null)
            {
                ConsoleHelper.WriteError(consumerKeyError);
                return 1;
            }

            var accessTokenError = CredentialFile.ValidateAccessToken(accessToken);
            if (accessTokenError is not null)
            {
                ConsoleHelper.WriteError(accessTokenError);
                return 1;
            }

            var accessTokenSecretError = CredentialFile.ValidateAccessTokenSecret(accessTokenSecret);
            if (accessTokenSecretError is not null)
            {
                ConsoleHelper.WriteError(accessTokenSecretError);
                return 1;
            }
        }

        var jsonPath = Path.Combine(fullPath, "ibkr-credentials.json");

        CredentialFile.Write(
            jsonPath,
            consumerKey,
            accessToken,
            accessTokenSecret,
            signaturePrivateKeyPem,
            encryptionPrivateKeyPem,
            dhPrimeHex);

        ConsoleHelper.WriteSuccess("Credential file written successfully.");
        Console.WriteLine();
        Console.WriteLine($"  {jsonPath}");
        Console.WriteLine();
        ConsoleHelper.WriteWarning("This file contains private keys. Do not commit it to source control.");
        Console.WriteLine();
        Console.WriteLine("Next step: Run 'ibkr-conduit-setup validate --credentials " + jsonPath + "' to test the connection.");

        return 0;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-setup configure [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --credentials <dir>            Directory containing private key PEM files (default: ./.ibkr-credentials)");
        Console.WriteLine("  --consumer-key <key>           Consumer key (9 uppercase letters)");
        Console.WriteLine("  --access-token <token>         Access token from the IBKR portal");
        Console.WriteLine("  --access-token-secret <secret> Encrypted access token secret (base64)");
        Console.WriteLine("  --help, -h                     Show this help message");
    }
}
