using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Packages;

/// <summary>
/// A single published version of a package, identified by the (package, version)
/// pair.
/// </summary>
/// <remarks>
/// Modeled as an independent entity rather than a child collection on
/// <see cref="Package"/> so the unbounded set of versions never has to be loaded
/// to work with the package itself. The per-version <see cref="License"/> is what
/// makes license-shift detection possible in Slice 3.
/// </remarks>
public sealed class PackageVersion
{
    // Parameterless constructor for the persistence layer (EF Core) only.
    private PackageVersion()
    {
    }

    private PackageVersion(PackageId packageId, SemVer version, DateTimeOffset? publishedAt, bool isDeprecated, SpdxLicense? license)
    {
        PackageId = packageId;
        Version = version;
        PublishedAt = publishedAt;
        IsDeprecated = isDeprecated;
        License = license;
    }

    /// <summary>The owning package.</summary>
    public PackageId PackageId { get; private set; }

    /// <summary>The semantic version.</summary>
    public SemVer Version { get; private set; } = null!;

    /// <summary>Publication timestamp from the registry, if known.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Whether this specific version is deprecated.</summary>
    public bool IsDeprecated { get; private set; }

    /// <summary>The license declared for this version (may differ between versions).</summary>
    public SpdxLicense? License { get; private set; }

    /// <summary>Creates a package version.</summary>
    public static PackageVersion Create(
        PackageId packageId,
        SemVer version,
        DateTimeOffset? publishedAt = null,
        bool isDeprecated = false,
        SpdxLicense? license = null) =>
        new(packageId, version, publishedAt, isDeprecated, license);
}
