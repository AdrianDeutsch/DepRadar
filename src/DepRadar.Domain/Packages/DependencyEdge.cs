using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Packages;

/// <summary>
/// A directed edge of the dependency graph: a specific package version declares
/// a dependency on another package within a requested version range.
/// </summary>
/// <remarks>
/// The graph is stored as a flat set of edges (not nested object graphs), so the
/// transitive closure can be computed with recursive CTEs in Postgres without
/// materializing arbitrarily deep object trees. <see cref="VersionRange"/> is kept
/// as the raw NuGet range string because range grammar is broader than a single
/// <see cref="SemVer"/>; range resolution happens in the ingestion pipeline.
/// </remarks>
public sealed class DependencyEdge
{
    // Parameterless constructor for the persistence layer (EF Core) only.
    private DependencyEdge()
    {
    }

    private DependencyEdge(PackageId dependentId, SemVer dependentVersion, PackageId dependencyId, string versionRange, bool isDirect)
    {
        DependentId = dependentId;
        DependentVersion = dependentVersion;
        DependencyId = dependencyId;
        VersionRange = versionRange;
        IsDirect = isDirect;
    }

    /// <summary>The package that declares the dependency (the edge's source).</summary>
    public PackageId DependentId { get; private set; }

    /// <summary>The version of the dependent package in which the dependency is declared.</summary>
    public SemVer DependentVersion { get; private set; } = null!;

    /// <summary>The depended-upon package (the edge's target).</summary>
    public PackageId DependencyId { get; private set; }

    /// <summary>The raw requested NuGet version range, e.g. <c>"[6.0.0, )"</c>.</summary>
    public string VersionRange { get; private set; } = null!;

    /// <summary>
    /// <see langword="true"/> for a directly declared dependency, <see langword="false"/>
    /// for one reached transitively. Relevant from Slice 2 onward.
    /// </summary>
    public bool IsDirect { get; private set; }

    /// <summary>Creates a dependency edge.</summary>
    public static DependencyEdge Create(
        PackageId dependentId,
        SemVer dependentVersion,
        PackageId dependencyId,
        string versionRange,
        bool isDirect = true)
    {
        if (string.IsNullOrWhiteSpace(versionRange))
        {
            throw new ArgumentException("Version range must not be empty.", nameof(versionRange));
        }

        return new DependencyEdge(dependentId, dependentVersion, dependencyId, versionRange.Trim(), isDirect);
    }
}
