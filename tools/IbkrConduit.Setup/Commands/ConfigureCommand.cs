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

        var credentialsDir = ArgParser.GetOption(args, "--credentials") ?? "./ibkr-credentials";
        var consumerKey = ArgParser.GetOption(args, "--consumer-key");
        var accessToken = ArgParser.GetOption(args, "--access-token");
        var accessTokenSecret = ArgParser.GetOption(args, "--access-token-secret");

        return Task.FromResult(Run(credentialsDir, consumerKey, accessToken, accessTokenSecret));
    }

    /// <summary>
    /// Core logic shared with the wizard. Returns exit code.
    /// </summary>
    internal static int Run(
        string credentialsDir,
        string? consumerKey = null,
        string? accessToken = null,
        string? accessTokenSecret = null)
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

        var signaturePrivateKeyPem = File.ReadAllText(signaturePemPath);
        var encryptionPrivateKeyPem = File.ReadAllText(encryptionPemPath);

        if (consumerKey is null || accessToken is null || accessTokenSecret is null)
        {
            consumerKey ??= ConsoleHelper.PromptWithValidation(
                "Consumer Key: ",
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
            KeyGenerator.Rfc3526Group14PrimeHex);

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
        Console.WriteLine("  --credentials <dir>            Directory containing private key PEM files (default: ./ibkr-credentials)");
        Console.WriteLine("  --consumer-key <key>           Consumer key from the IBKR portal (9 alphanumeric chars)");
        Console.WriteLine("  --access-token <token>         Access token from the IBKR portal");
        Console.WriteLine("  --access-token-secret <secret> Encrypted access token secret (base64)");
        Console.WriteLine("  --help, -h                     Show this help message");
    }
}
