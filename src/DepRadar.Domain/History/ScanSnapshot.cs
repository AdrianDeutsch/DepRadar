using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.History;

/// <summary>
/// An append-only record of a package's assessed graph at the moment a scan completed.
/// Comparing successive snapshots is what surfaces <em>drift</em> — a dependency that
/// newly became vulnerable, deprecated or archived since you last looked.
/// </summary>
public sealed class ScanSnapshot
{
    private readonly List<PackageRiskState> _packages = [];

    // Parameterless constructor for the persistence layer (EF Core) only.
    private ScanSnapshot()
    {
    }

    private ScanSnapshot(
        Guid id,
        PackageId rootPackageId,
        DateTimeOffset createdAt,
        int overallScore,
        RiskLevel overallLevel,
        IEnumerable<PackageRiskState> packages)
    {
        Id = id;
        RootPackageId = rootPackageId;
        CreatedAt = createdAt;
        OverallScore = overallScore;
        OverallLevel = overallLevel;
        _packages.AddRange(packages);
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private set; }

    /// <summary>The scanned root package.</summary>
    public PackageId RootPackageId { get; private set; }

    /// <summary>When the snapshot was taken (scan completion).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The worst package's score across the graph (0–100).</summary>
    public int OverallScore { get; private set; }

    /// <summary>The worst package's level across the graph.</summary>
    public RiskLevel OverallLevel { get; private set; }

    /// <summary>The per-package risk state captured at snapshot time.</summary>
    public IReadOnlyList<PackageRiskState> Packages => _packages;

    /// <summary>Creates a snapshot for the given root at <paramref name="createdAt"/>.</summary>
    public static ScanSnapshot Create(
        PackageId rootPackageId,
        DateTimeOffset createdAt,
        int overallScore,
        RiskLevel overallLevel,
        IEnumerable<PackageRiskState> packages) =>
        new(Guid.NewGuid(), rootPackageId, createdAt, overallScore, overallLevel, packages);
}
