using System.Globalization;
using System.Text.RegularExpressions;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using NuGet.Versioning;

namespace DepRadar.Infrastructure.External.NuGet;

/// <summary>
/// Reads package version + dependency metadata from the NuGet V3 registration API
/// (gz-semver2). For each version it picks a single representative target-framework
/// group so the resolved graph reflects what a modern consumer would restore.
/// Responses are cached so re-scans don't re-hit NuGet.
/// </summary>
internal sealed partial class NuGetClient(HttpClient httpClient, HybridCache cache)
{
    /// <summary>
    /// Fetches all listed versions and their declared dependencies, or
    /// <see langword="null"/> if the package is unknown to NuGet.
    /// </summary>
    public async Task<NuGetPackageData?> GetPackageDataAsync(PackageId id, CancellationToken cancellationToken)
    {
        var index = await GetJsonAsync<NuGetRegistrationIndex>(
            $"v3/registration5-gz-semver2/{id.Value}/index.json",
            cancellationToken);

        if (index?.Items is null)
        {
            return null;
        }

        var leaves = await CollectLeavesAsync(index, cancellationToken);

        var versions = new List<NuGetVersionData>(leaves.Count);
        foreach (var leaf in leaves)
        {
            var entry = leaf.CatalogEntry;
            if (entry?.Version is null || entry.Listed == false)
            {
                continue;
            }

            if (!NuGetVersion.TryParse(entry.Version, out var version))
            {
                continue;
            }

            var license = string.IsNullOrWhiteSpace(entry.LicenseExpression) ? null : entry.LicenseExpression;
            versions.Add(new NuGetVersionData(
                version,
                license,
                entry.Deprecation is not null,
                SelectRepresentativeDependencies(entry.DependencyGroups)));
        }

        return new NuGetPackageData(versions);
    }

    /// <summary>Flattens all registration pages, fetching any that are not inlined.</summary>
    private async Task<List<NuGetRegistrationLeaf>> CollectLeavesAsync(NuGetRegistrationIndex index, CancellationToken cancellationToken)
    {
        var leaves = new List<NuGetRegistrationLeaf>();
        foreach (var page in index.Items!)
        {
            var items = page.Items;
            if (items is null && page.PageUrl is not null)
            {
                var fetched = await GetJsonAsync<NuGetRegistrationPage>(page.PageUrl, cancellationToken);
                items = fetched?.Items;
            }

            if (items is not null)
            {
                leaves.AddRange(items);
            }
        }

        return leaves;
    }

    /// <summary>
    /// Picks the highest-priority target-framework group (modern <c>net</c> &gt;
    /// <c>netcoreapp</c> &gt; <c>netstandard</c> &gt; classic framework &gt; any) and
    /// returns its declared dependencies.
    /// </summary>
    private static List<NuGetDeclaredDependency> SelectRepresentativeDependencies(IReadOnlyList<NuGetDependencyGroup>? groups)
    {
        if (groups is null or { Count: 0 })
        {
            return [];
        }

        NuGetDependencyGroup? best = null;
        var bestScore = int.MinValue;
        foreach (var group in groups)
        {
            var score = ScoreFramework(group.TargetFramework);
            if (score > bestScore)
            {
                bestScore = score;
                best = group;
            }
        }

        return best?.Dependencies?
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.Id))
            .Select(dependency => new NuGetDeclaredDependency(dependency.Id!, dependency.Range))
            .ToList() ?? [];
    }

    private static int ScoreFramework(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return 1;
        }

        var tfm = targetFramework.ToLowerInvariant();
        var number = ExtractLeadingNumber(tfm);

        // Modern net (net5.0+) is written "netX.Y"; classic framework is "net48" (no dot).
        if (ModernNetRegex().IsMatch(tfm))
        {
            return 4000 + number;
        }

        if (tfm.StartsWith("netcoreapp", StringComparison.Ordinal))
        {
            return 3000 + number;
        }

        if (tfm.StartsWith("netstandard", StringComparison.Ordinal))
        {
            return 2000 + number;
        }

        if (tfm.Contains("netframework", StringComparison.Ordinal) || ClassicNetRegex().IsMatch(tfm))
        {
            return 1000 + number;
        }

        return 500;
    }

    private static int ExtractLeadingNumber(string tfm)
    {
        var match = LeadingNumberRegex().Match(tfm);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }

    private Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken) =>
        HttpJsonCache.GetAsync<T>(cache, httpClient, $"nuget:{url}", url, cancellationToken);

    [GeneratedRegex(@"^net\d+\.\d", RegexOptions.CultureInvariant)]
    private static partial Regex ModernNetRegex();

    [GeneratedRegex(@"^net\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ClassicNetRegex();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberRegex();
}

/// <summary>All listed versions of a package with their declared dependencies.</summary>
internal sealed record NuGetPackageData(IReadOnlyList<NuGetVersionData> Versions);

/// <summary>One version with its license, deprecation flag and representative dependencies.</summary>
internal sealed record NuGetVersionData(
    NuGetVersion Version,
    string? License,
    bool IsDeprecated,
    IReadOnlyList<NuGetDeclaredDependency> Dependencies);

/// <summary>A declared dependency: target id and the requested range (null = any).</summary>
internal sealed record NuGetDeclaredDependency(string Id, string? Range);
