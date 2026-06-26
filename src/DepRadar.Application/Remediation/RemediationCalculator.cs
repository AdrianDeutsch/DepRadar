using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Remediation;

/// <summary>
/// Picks the version that clears <em>every</em> advisory on a package: the highest of
/// the per-advisory patched versions. Pure.
/// </summary>
public static class RemediationCalculator
{
    /// <summary>
    /// The minimal version that resolves all advisories, or <see langword="null"/> when
    /// any advisory has no known fix (so the package cannot be fully patched).
    /// </summary>
    public static string? SafeVersion(IReadOnlyList<string?> perAdvisoryFixedVersions)
    {
        if (perAdvisoryFixedVersions.Count == 0 || perAdvisoryFixedVersions.Any(version => version is null))
        {
            return null;
        }

        return perAdvisoryFixedVersions
            .Where(version => SemVer.TryParse(version, out _))
            .Select(version => SemVer.Parse(version!))
            .OrderByDescending(version => version)
            .FirstOrDefault()?
            .ToString();
    }
}
