using System.Collections.Frozen;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Policy;
using DepRadar.Application.Risk;
using DepRadar.Application.Sarif;
using DepRadar.Application.Sbom;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>How one ecosystem plugs into the shared <see cref="EcosystemCommand"/>.</summary>
/// <param name="RegistryLabel">Registry name for the not-found message, e.g. <c>"the npm registry"</c>.</param>
/// <param name="ParseManifest">Parses the ecosystem's manifest into direct dependencies.</param>
/// <param name="ResolveScanner">Resolves the ecosystem's scan delegate from the DI scope.</param>
internal sealed record EcosystemCli(
    string RegistryLabel,
    Func<string, IReadOnlyList<ManifestDependency>> ParseManifest,
    Func<IServiceProvider, Func<string, string?, CancellationToken, Task<GraphAssessment?>>> ResolveScanner);

/// <summary>
/// The shared engine behind <c>depradar npm</c> and <c>depradar pypi</c>: scans a single
/// package (optionally at a version/range) or a whole manifest file, then reuses the same
/// renderer, policy gate and SBOM/SARIF writers as the NuGet <c>scan</c> command.
/// </summary>
internal static class EcosystemCommand
{
    /// <summary>Runs the command with the arguments after the verb.</summary>
    public static async Task<int> RunAsync(EcosystemCli cli, string usage, string[] args, CancellationToken cancellationToken)
    {
        var json = false;
        var failOn = RiskLevel.High;
        string? sbomPath = null;
        string? sarifPath = null;
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

                case "--sbom" when i + 1 < args.Length:
                    sbomPath = args[++i];
                    break;

                case "--sarif" when i + 1 < args.Length:
                    sarifPath = args[++i];
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
            await Console.Error.WriteLineAsync(usage);
            return ExitCodes.Usage;
        }

        if (!TryResolveTargets(cli, positional, out var targets, out var targetError))
        {
            await Console.Error.WriteLineAsync(targetError);
            return ExitCodes.Usage;
        }

        await using var provider = CliHost.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var scan = cli.ResolveScanner(scope.ServiceProvider);

        var assessments = new List<GraphAssessment>();
        var unresolved = new List<string>();
        foreach (var (name, specifier) in targets)
        {
            var assessment = await scan(name, specifier, cancellationToken);
            if (assessment is null)
            {
                unresolved.Add(specifier is null ? name : $"{name} {specifier}");
            }
            else
            {
                assessments.Add(assessment);
            }
        }

        if (assessments.Count == 0)
        {
            await Console.Error.WriteLineAsync($"Nothing from '{positional[0]}' could be resolved on {cli.RegistryLabel}.");
            return ExitCodes.Usage;
        }

        var graph = GraphMerge.Union(assessments);
        var outcome = PolicyEvaluator.Evaluate(graph, new RiskPolicy(failOn, AllowDeprecated: true, FrozenSet<LicenseCategory>.Empty));

        if (json)
        {
            ConsoleReport.WriteJson(graph, outcome, unresolved);
        }
        else
        {
            ConsoleReport.WriteText(graph, outcome, unresolved);
        }

        if (sbomPath is not null)
        {
            await File.WriteAllTextAsync(sbomPath, CycloneDxBuilder.Build(graph, DateTimeOffset.UtcNow), cancellationToken);
            if (!json)
            {
                Console.WriteLine($"  SBOM written to {sbomPath}");
            }
        }

        if (sarifPath is not null)
        {
            await File.WriteAllTextAsync(sarifPath, SarifBuilder.Build(graph, positional[0]), cancellationToken);
            if (!json)
            {
                Console.WriteLine($"  SARIF written to {sarifPath}");
            }
        }

        return outcome.Passed ? ExitCodes.Ok : ExitCodes.PolicyViolation;
    }

    /// <summary>A package name scans one root; an existing manifest file scans its direct dependencies.</summary>
    private static bool TryResolveTargets(
        EcosystemCli cli,
        List<string> positional,
        out IReadOnlyList<(string Name, string? Specifier)> targets,
        out string? error)
    {
        error = null;
        targets = [];

        if (File.Exists(positional[0]))
        {
            if (positional.Count > 1)
            {
                error = "A manifest scan does not take a version argument.";
                return false;
            }

            try
            {
                var dependencies = cli.ParseManifest(File.ReadAllText(positional[0]));
                if (dependencies.Count == 0)
                {
                    error = $"No dependencies found in '{positional[0]}'.";
                    return false;
                }

                targets = dependencies
                    .Select(d => (d.Name, string.IsNullOrWhiteSpace(d.Specifier) ? null : d.Specifier))
                    .ToList();
                return true;
            }
            catch (FormatException exception)
            {
                error = $"Could not parse '{positional[0]}': {exception.Message}";
                return false;
            }
        }

        targets = [(positional[0], positional.Count > 1 ? positional[1] : null)];
        return true;
    }
}
