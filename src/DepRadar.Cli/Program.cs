using DepRadar.Cli;

// CI-friendly entry point: dispatch the verb and return a process exit code.
// `depradar scan <target>` is the only verb today; --help prints usage.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(CliOptions.Usage);
    Console.WriteLine();
    Console.WriteLine(DiffCommand.Usage);
    return args.Length == 0 ? ExitCodes.Usage : ExitCodes.Ok;
}

return args[0] switch
{
    "scan" => await ScanCommand.RunAsync(args[1..], cts.Token),
    "diff" => await DiffCommand.RunAsync(args[1..], cts.Token),
    var unknown => Fail(unknown),
};

static int Fail(string verb)
{
    Console.Error.WriteLine($"Unknown command '{verb}'.");
    Console.Error.WriteLine(CliOptions.Usage);
    Console.Error.WriteLine();
    Console.Error.WriteLine(DiffCommand.Usage);
    return ExitCodes.Usage;
}
