namespace IbkrConduit.Setup.Commands;

/// <summary>
/// Generates RSA key pairs and DH parameters for IBKR OAuth 1.0a.
/// </summary>
internal static class GenerateKeysCommand
{
    /// <summary>
    /// Runs the generate-keys subcommand.
    /// </summary>
    internal static Task<int> RunAsync(string[] args)
    {
        if (ArgParser.HasFlag(args, "--help", "-h"))
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        var outputDir = ArgParser.GetOption(args, "--output") ?? "./ibkr-credentials";
        var force = ArgParser.HasFlag(args, "--force");

        return Task.FromResult(Run(outputDir, force));
    }

    /// <summary>
    /// Core logic shared with the wizard. Returns exit code.
    /// </summary>
    internal static int Run(string outputDir, bool force)
    {
        var fullPath = Path.GetFullPath(outputDir);

        if (Directory.Exists(fullPath) && Directory.GetFiles(fullPath).Length > 0)
        {
            if (!force)
            {
                if (!ConsoleHelper.Confirm($"Files already exist in {fullPath}. Overwrite?"))
                {
                    ConsoleHelper.WriteWarning("Aborted. No files were changed.");
                    return 1;
                }
            }
        }

        Directory.CreateDirectory(fullPath);

        Console.WriteLine("  Generating RSA signature key pair (2048-bit)...");
        var signature = KeyGenerator.GenerateRsaKeyPair();
        ConsoleHelper.WriteSuccess("  Signature key pair generated.");

        Console.WriteLine("  Generating RSA encryption key pair (2048-bit)...");
        var encryption = KeyGenerator.GenerateRsaKeyPair();
        ConsoleHelper.WriteSuccess("  Encryption key pair generated.");

        Console.WriteLine("  Generating DH parameters (2048-bit safe prime)...");
        Console.WriteLine("  This may take 30-120 seconds — generating a cryptographically safe prime.");
        var dh = KeyGenerator.GenerateDhParameters();
        ConsoleHelper.WriteSuccess("  DH parameters generated.");

        File.WriteAllText(Path.Combine(fullPath, "public_signature.pem"), signature.PublicPem);
        File.WriteAllText(Path.Combine(fullPath, "public_encryption.pem"), encryption.PublicPem);
        File.WriteAllText(Path.Combine(fullPath, "dhparam.pem"), dh.Pem);
        File.WriteAllText(Path.Combine(fullPath, "private_signature.pem"), signature.PrivatePem);
        File.WriteAllText(Path.Combine(fullPath, "private_encryption.pem"), encryption.PrivatePem);
        // Store the DH prime hex for the configure step
        File.WriteAllText(Path.Combine(fullPath, ".dhprime.hex"), dh.PrimeHex);

        ConsoleHelper.WriteSuccess("Keys generated successfully.");
        Console.WriteLine();
        Console.WriteLine($"  Output directory: {fullPath}");
        Console.WriteLine();
        Console.WriteLine("  Files for portal upload:");
        Console.WriteLine($"    {Path.Combine(fullPath, "public_signature.pem")}");
        Console.WriteLine($"    {Path.Combine(fullPath, "public_encryption.pem")}");
        Console.WriteLine($"    {Path.Combine(fullPath, "dhparam.pem")}");
        Console.WriteLine();
        Console.WriteLine("  Private key files (keep secure, never share):");
        Console.WriteLine($"    {Path.Combine(fullPath, "private_signature.pem")}");
        Console.WriteLine($"    {Path.Combine(fullPath, "private_encryption.pem")}");
        Console.WriteLine();
        Console.WriteLine("Next step: Upload the 3 public files to the IBKR Self-Service Portal,");
        Console.WriteLine("then run 'ibkr-conduit-setup configure' to create the credential file.");

        return 0;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: ibkr-conduit-setup generate-keys [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --output <dir>    Output directory (default: ./ibkr-credentials)");
        Console.WriteLine("  --force           Overwrite existing files without prompting");
        Console.WriteLine("  --help, -h        Show this help message");
    }
}
