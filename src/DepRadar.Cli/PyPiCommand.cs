using System.Collections.Frozen;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Policy;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>pypi</c> command: scans a PyPI (Python) package's transitive graph for security
/// and maintenance risk (multi-ecosystem), reusing the same renderer and policy gate as the
/// NuGet <c>scan</c> command.
/// </summary>
internal static class PyPiCommand
{
    /// <summary>The usage banner for <c>pypi</c>.</summary>
    public const string Usage = "Usage: depradar pypi <package> [version] [--fail-on <none|low|medium|high|critical>] [--json]";

    /// <summary>Runs <c>pypi</c> with the arguments after the verb.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var json = false;
        var failOn = RiskLevel.High;
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;

                case "--fail-on":
                    if (i + 1 >= args.Length || !Enum.TryParse(args[++i], ignoreCase: true, out failOn))
                    {
                        await Console.Error.WriteLineAsync($"Invalid --fail-on value. Expected one of: {string.Join(", ", Enum.GetNames<RiskLevel>())}.");
                        return ExitCodes.Usage;
                    }

                    break;

                default:
                    if (args[i].StartsWith('-'))
                    {
                        await Console.Error.WriteLineAsync($"Unknown option '{args[i]}'.");
                        return ExitCodes.Usage;
                    }

                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count == 0)
        {
            await Console.Error.WriteLineAsync(Usage);
            return ExitCodes.Usage;
        }

        await using var provider = CliHost.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IPyPiScanner>();

        var graph = await scanner.ScanAsync(positional[0], positional.Count > 1 ? positional[1] : null, cancellationToken);
        if (graph is null)
        {
            await Console.Error.WriteLineAsync($"'{positional[0]}' was not found on PyPI.");
            return ExitCodes.Usage;
        }

        var outcome = PolicyEvaluator.Evaluate(graph, new RiskPolicy(failOn, AllowDeprecated: true, FrozenSet<LicenseCategory>.Empty));

        if (json)
        {
            ConsoleReport.WriteJson(graph, outcome, []);
        }
        else
        {
            ConsoleReport.WriteText(graph, outcome, []);
        }

        return outcome.Passed ? ExitCodes.Ok : ExitCodes.PolicyViolation;
    }
}
