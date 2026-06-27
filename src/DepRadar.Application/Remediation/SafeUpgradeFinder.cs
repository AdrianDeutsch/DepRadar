using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Remediation;

/// <summary>
/// Finds the smallest stable version of a <em>direct</em> dependency whose entire
/// resolved graph is vulnerability-free. Because it scores the whole transitive closure
/// of each candidate, bumping the direct dependency this way also clears <em>transitive</em>
/// advisories (the parent-bump fix) — not just the direct package's own.
/// </summary>
public sealed class SafeUpgradeFinder(ProjectAnalyzer analyzer, IPackageMetadataSource metadata)
{
    /// <summary>How many candidate versions to score before giving up.</summary>
    public const int MaxCandidates = 12;

    /// <summary>
    /// The minimal stable version above <paramref name="current"/> whose graph carries no
    /// advisories, or <see langword="null"/> if none of the candidates resolve clean.
    /// </summary>
    public async Task<string?> FindMinimalCleanVersionAsync(PackageId package, SemVer current, CancellationToken cancellationToken)
    {
        var details = await metadata.GetAsync(package, cancellationToken);
        if (details is null)
        {
            return null;
        }

        foreach (var candidate in Candidates(details.Versions.Select(version => version.Version), current))
        {
            var graph = await analyzer.AnalyzeAsync(package, candidate, cancellationToken);
            if (graph is not null && graph.Nodes.All(node => node.Input.Vulnerabilities.Count == 0))
            {
                return candidate.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// The stable versions above <paramref name="current"/>, ascending and capped — pure,
    /// so the candidate selection is unit-testable without any network.
    /// </summary>
    public static IReadOnlyList<SemVer> Candidates(IEnumerable<string> versions, SemVer current) =>
        versions
            .Where(version => SemVer.TryParse(version, out _))
            .Select(SemVer.Parse)
            .Where(version => version.IsStable && version.CompareTo(current) > 0)
            .Distinct()
            .OrderBy(version => version)
            .Take(MaxCandidates)
            .ToList();
}
