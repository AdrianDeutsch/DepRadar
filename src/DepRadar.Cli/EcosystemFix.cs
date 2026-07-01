using DepRadar.Application.Ecosystems;
using DepRadar.Application.Remediation;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Cli;

/// <summary>
/// The ecosystem-agnostic core of <c>depradar fix</c> for npm/PyPI manifests: for every
/// direct dependency whose resolved graph carries advisories, walk the published versions
/// upward (via <see cref="SafeUpgradeFinder.Candidates"/>) until one resolves a clean
/// graph — the same parent-bump semantics as the NuGet fix.
/// </summary>
internal static class EcosystemFix
{
    /// <summary>Scans a package at a version/range and scores its graph.</summary>
    public delegate Task<GraphAssessment?> ScanDelegate(string package, string? version, CancellationToken cancellationToken);

    /// <summary>Lists a package's published versions.</summary>
    public delegate Task<IReadOnlyList<SemVer>> ListVersionsDelegate(string package, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the minimal clean bump for each fixable vulnerable dependency
    /// (package name → safe version). Non-fixable findings are written to stderr.
    /// </summary>
    public static async Task<Dictionary<string, string>> ResolveBumpsAsync(
        IReadOnlyList<ManifestDependency> dependencies,
        Func<ManifestDependency, bool> isPatchable,
        ScanDelegate scan,
        ListVersionsDelegate listVersions,
        CancellationToken cancellationToken)
    {
        var bumps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dependency in dependencies)
        {
            var graph = await scan(dependency.Name, NullIfBlank(dependency.Specifier), cancellationToken);
            if (graph is null || graph.Nodes.All(node => node.Input.Vulnerabilities.Count == 0))
            {
                continue;
            }

            if (!isPatchable(dependency))
            {
                await Console.Error.WriteLineAsync(
                    $"  {dependency.Name}: vulnerable, but '{dependency.Specifier}' is not an exact pin DepRadar can rewrite — bump it manually.");
                continue;
            }

            // The root node's resolved version is what the manifest effectively installs.
            var current = graph.Nodes.First(node => node.Package.Value == graph.Root.Value).Version;

            var versions = await listVersions(dependency.Name, cancellationToken);
            var safe = await FindMinimalCleanAsync(dependency.Name, current, versions, scan, cancellationToken);
            if (safe is null)
            {
                await Console.Error.WriteLineAsync(
                    $"  {dependency.Name}: vulnerable, but no newer version resolves a clean graph (consider replacing it).");
            }
            else
            {
                bumps[dependency.Name] = safe;
            }
        }

        return bumps;
    }

    /// <summary>The smallest candidate above <paramref name="current"/> whose whole graph is advisory-free.</summary>
    private static async Task<string?> FindMinimalCleanAsync(
        string package,
        SemVer current,
        IReadOnlyList<SemVer> versions,
        ScanDelegate scan,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in SafeUpgradeFinder.Candidates(versions.Select(version => version.ToString()), current))
        {
            var graph = await scan(package, candidate.ToString(), cancellationToken);
            if (graph is not null && graph.Nodes.All(node => node.Input.Vulnerabilities.Count == 0))
            {
                return candidate.ToString();
            }
        }

        return null;
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
