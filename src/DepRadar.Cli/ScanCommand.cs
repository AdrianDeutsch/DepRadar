using DepRadar.Application.Analysis;
using DepRadar.Application.Policy;
using DepRadar.Application.Projects;
using DepRadar.Application.Risk;
using DepRadar.Application.Sarif;
using DepRadar.Application.Sbom;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>scan</c> command: resolves and scores a package or whole project entirely
/// in-process (no server, no database), then gates the result against a policy and
/// returns a CI-friendly exit code.
/// </summary>
internal static class ScanCommand
{
    /// <summary>Runs <c>scan</c> with the arguments after the verb.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (!CliOptions.TryParse(args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync(error);
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync(CliOptions.Usage);
            return ExitCodes.Usage;
        }

        if (!TryResolveTargets(options!.Target, out var targets, out var targetError))
        {
            await Console.Error.WriteLineAsync(targetError);
            return ExitCodes.Usage;
        }

        await using var provider = CliHost.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var analyzer = scope.ServiceProvider.GetRequiredService<ProjectAnalyzer>();

        var assessments = new List<GraphAssessment>();
        var unresolved = new List<string>();
        foreach (var target in targets)
        {
            var assessment = await analyzer.AnalyzeAsync(PackageId.Create(target), pinnedVersion: null, cancellationToken);
            if (assessment is null)
            {
                unresolved.Add(target);
            }
            else
            {
                assessments.Add(assessment);
            }
        }

        if (assessments.Count == 0)
        {
            await Console.Error.WriteLineAsync($"Nothing could be resolved from '{options.Target}'.");
            return ExitCodes.Usage;
        }

        var graph = Merge(assessments);
        if (!TryResolvePolicy(options, out var policy, out var policyError))
        {
            await Console.Error.WriteLineAsync(policyError);
            return ExitCodes.Usage;
        }

        var outcome = PolicyEvaluator.Evaluate(graph, policy!);

        if (options.Json)
        {
            ConsoleReport.WriteJson(graph, outcome, unresolved);
        }
        else
        {
            ConsoleReport.WriteText(graph, outcome, unresolved);
        }

        if (options.SbomPath is { } sbomPath)
        {
            await File.WriteAllTextAsync(sbomPath, CycloneDxBuilder.Build(graph, DateTimeOffset.UtcNow), cancellationToken);
            if (!options.Json)
            {
                Console.WriteLine($"  SBOM written to {sbomPath}");
            }
        }

        if (options.SarifPath is { } sarifPath)
        {
            await File.WriteAllTextAsync(sarifPath, SarifBuilder.Build(graph, options.Target), cancellationToken);
            if (!options.Json)
            {
                Console.WriteLine($"  SARIF written to {sarifPath}");
            }
        }

        return outcome.Passed ? ExitCodes.Ok : ExitCodes.PolicyViolation;
    }

    /// <summary>
    /// The gate comes from a policy file when one is given (or <c>./depradar.json</c> exists),
    /// otherwise from the CLI flags. An explicit but invalid file is a usage error.
    /// </summary>
    private static bool TryResolvePolicy(CliOptions options, out RiskPolicy? policy, out string? error)
    {
        error = null;
        var path = options.PolicyPath ?? (File.Exists("depradar.json") ? "depradar.json" : null);
        if (path is null)
        {
            policy = options.ToPolicy();
            return true;
        }

        try
        {
            policy = PolicyFile.Parse(File.ReadAllText(path));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            policy = null;
            error = $"Could not read policy '{path}': {exception.Message}";
            return false;
        }
    }

    /// <summary>A package id scans one root; an existing project file scans its direct dependencies.</summary>
    private static bool TryResolveTargets(string target, out IReadOnlyList<string> packages, out string? error)
    {
        error = null;

        if (File.Exists(target))
        {
            try
            {
                packages = ProjectFileParser.ParseDirectPackages(File.ReadAllText(target));
                if (packages.Count == 0)
                {
                    error = $"No package references found in '{target}'.";
                    return false;
                }

                return true;
            }
            catch (FormatException ex)
            {
                packages = [];
                error = $"Could not parse '{target}': {ex.Message}";
                return false;
            }
        }

        try
        {
            // Validate the id up front so a typo fails as usage, not mid-run.
            _ = PackageId.Create(target);
            packages = [target];
            return true;
        }
        catch (ArgumentException ex)
        {
            packages = [];
            error = $"'{target}' is neither a file nor a valid package id: {ex.Message}";
            return false;
        }
    }

    /// <summary>Unions the per-root assessments into one graph (distinct nodes and edges).</summary>
    private static GraphAssessment Merge(List<GraphAssessment> assessments)
    {
        if (assessments.Count == 1)
        {
            return assessments[0];
        }

        var nodes = assessments
            .SelectMany(a => a.Nodes)
            .GroupBy(n => (n.Package.Value, n.Version.ToString()))
            .Select(g => g.First())
            .ToList();

        var edges = assessments
            .SelectMany(a => a.Edges)
            .GroupBy(e => (e.DependentId, e.DependentVersion, e.DependencyId, e.DependencyVersion))
            .Select(g => g.First())
            .ToList();

        return new GraphAssessment(assessments[0].Root, nodes, edges, assessments.Any(a => a.Truncated));
    }
}
