using IbkrConduit.Setup;
using IbkrConduit.Setup.Commands;

var command = args.Length > 0 ? args[0] : "wizard";

return command switch
{
    "generate-keys" => await GenerateKeysCommand.RunAsync(args),
    "configure" => await ConfigureCommand.RunAsync(args),
    "validate" => await ValidateCommand.RunAsync(args),
    "wizard" => await WizardCommand.RunAsync(args),
    "--help" or "-h" => ShowHelp(),
    _ => ShowUnknownCommand(command),
};

static int ShowHelp()
{
    Console.WriteLine("ibkr-conduit-setup — IBKR OAuth 1.0a Credential Setup Tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  ibkr-conduit-setup                     Run the full setup wizard (default)");
    Console.WriteLine("  ibkr-conduit-setup generate-keys       Generate RSA key pairs and DH parameters");
    Console.WriteLine("  ibkr-conduit-setup configure            Collect portal credentials and write JSON file");
    Console.WriteLine("  ibkr-conduit-setup validate             Validate credentials against the IBKR API");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --help, -h                              Show this help message");
    Console.WriteLine();
    Console.WriteLine("Run 'ibkr-conduit-setup <command> --help' for command-specific options.");
    return 0;
}

static int ShowUnknownCommand(string command)
{
    ConsoleHelper.WriteError($"Unknown command: '{command}'. Run 'ibkr-conduit-setup --help' for usage.");
    return 1;
}
