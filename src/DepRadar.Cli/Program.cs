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
    PrintUsage(Console.Out);
    return args.Length == 0 ? ExitCodes.Usage : ExitCodes.Ok;
}

return args[0] switch
{
    "scan" => await ScanCommand.RunAsync(args[1..], cts.Token),
    "diff" => await DiffCommand.RunAsync(args[1..], cts.Token),
    "fix" => await FixCommand.RunAsync(args[1..], cts.Token),
    var unknown => Fail(unknown),
};

static int Fail(string verb)
{
    Console.Error.WriteLine($"Unknown command '{verb}'.");
    PrintUsage(Console.Error);
    return ExitCodes.Usage;
}

static void PrintUsage(System.IO.TextWriter writer)
{
    writer.WriteLine(CliOptions.Usage);
    writer.WriteLine();
    writer.WriteLine(DiffCommand.Usage);
    writer.WriteLine(FixCommand.Usage);
}
