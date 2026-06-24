using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRiskRepository"/>. Stores advisories
/// idempotently and assembles scoring inputs with batched reads.
/// </summary>
internal sealed class RiskRepository(DepRadarDbContext dbContext) : IRiskRepository
{
    /// <inheritdoc />
    public async Task UpsertVulnerabilitiesAsync(IReadOnlyCollection<PackageVulnerability> vulnerabilities, CancellationToken cancellationToken)
    {
        var packageIds = vulnerabilities.Select(v => v.PackageId).Distinct().ToList();

        var existing = (await dbContext.PackageVulnerabilities
            .AsNoTracking()
            .Where(v => packageIds.Contains(v.PackageId))
            .Select(v => new { v.PackageId, v.Version, v.AdvisoryId })
            .ToListAsync(cancellationToken))
            .Select(v => (v.PackageId.Value, v.Version.ToString(), v.AdvisoryId))
            .ToHashSet();

        foreach (var vulnerability in vulnerabilities)
        {
            if (existing.Add((vulnerability.PackageId.Value, vulnerability.Version.ToString(), vulnerability.AdvisoryId)))
            {
                await dbContext.PackageVulnerabilities.AddAsync(vulnerability, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<PackageRiskInput?> GetRiskInputAsync(PackageId package, SemVer version, CancellationToken cancellationToken)
    {
        var inputs = await GetRiskInputsAsync([(package, version)], cancellationToken);
        return inputs.Count > 0 ? inputs[0] : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageRiskInput>> GetRiskInputsAsync(
        IReadOnlyCollection<(PackageId Package, SemVer Version)> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var packageIds = targets.Select(t => t.Package).Distinct().ToList();

        var packages = (await dbContext.Packages
                .AsNoTracking()
                .Where(p => packageIds.Contains(p.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id.Value, p => p, StringComparer.Ordinal);

        var versions = (await dbContext.PackageVersions
                .AsNoTracking()
                .Where(v => packageIds.Contains(v.PackageId))
                .ToListAsync(cancellationToken))
            .ToDictionary(v => (v.PackageId.Value, v.Version.ToString()), v => v);

        var vulnerabilities = (await dbContext.PackageVulnerabilities
                .AsNoTracking()
                .Where(v => packageIds.Contains(v.PackageId))
                .ToListAsync(cancellationToken))
            .GroupBy(v => (v.PackageId.Value, v.Version.ToString()))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PackageVulnerability>)g.ToList());

        var inputs = new List<PackageRiskInput>(targets.Count);
        foreach (var (package, version) in targets)
        {
            var key = (package.Value, version.ToString());
            if (!versions.TryGetValue(key, out var packageVersion))
            {
                continue;
            }

            packages.TryGetValue(package.Value, out var packageEntity);
            var advisories = vulnerabilities.GetValueOrDefault(key, []);

            inputs.Add(new PackageRiskInput(
                package,
                version,
                packageVersion.License,
                packageEntity?.License,
                packageVersion.IsDeprecated,
                advisories));
        }

        return inputs;
    }
}
