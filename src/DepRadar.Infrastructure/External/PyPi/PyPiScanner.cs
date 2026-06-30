using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.External.Npm;

namespace DepRadar.Infrastructure.External.PyPi;

/// <summary>
/// Wires the PyPI resolver + PyPI vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="IPyPiScanner"/>.
/// </summary>
internal sealed class PyPiScanner(
    PyPiDependencyGraphResolver resolver,
    PyPiVulnerabilitySource vulnerabilities,
    TimeProvider timeProvider) : IPyPiScanner
{
    /// <inheritdoc />
    public Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken)
    {
        var pinned = version is not null && PyPiVersion.TryParse(version, out var parsed) ? parsed : null;
        var analyzer = new ProjectAnalyzer(resolver, vulnerabilities, NullMetadataSource.Instance, NullRepositoryHealthSource.Instance, timeProvider);
        return analyzer.AnalyzeAsync(PackageId.FromNormalized(PyPiName.Normalize(package)), pinned, cancellationToken);
    }
}
