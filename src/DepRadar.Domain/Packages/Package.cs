using DepRadar.Domain.Common;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Packages;

/// <summary>
/// Aggregate root for a single NuGet package and its current health-relevant
/// metadata.
/// </summary>
/// <remarks>
/// The aggregate is intentionally small: it does NOT hold navigation collections
/// of every version or graph edge (those can grow without bound and are modeled
/// as independent entities — <see cref="PackageVersion"/>, <see cref="DependencyEdge"/>).
/// This keeps loads cheap and write boundaries crisp.
/// </remarks>
public sealed class Package : IAggregateRoot
{
    // Parameterless constructor for the persistence layer (EF Core) only.
    private Package()
    {
    }

    private Package(PackageId id, DateTimeOffset timestamp)
    {
        Id = id;
        FirstSeenAt = timestamp;
        LastRefreshedAt = timestamp;
    }

    /// <summary>The package identity.</summary>
    public PackageId Id { get; private set; }

    /// <summary>Short package description, if known.</summary>
    public string? Description { get; private set; }

    /// <summary>The package's project/home URL.</summary>
    public Uri? ProjectUrl { get; private set; }

    /// <summary>The resolved source repository (used later for repo-health scoring).</summary>
    public Uri? SourceRepositoryUrl { get; private set; }

    /// <summary>The currently declared license, if any.</summary>
    public SpdxLicense? License { get; private set; }

    /// <summary>Whether the package is flagged deprecated on NuGet.</summary>
    public bool IsDeprecated { get; private set; }

    /// <summary>The latest stable version observed for the package.</summary>
    public SemVer? LatestStableVersion { get; private set; }

    /// <summary>Whether the source repository is archived (read-only / unmaintained).</summary>
    public bool IsArchived { get; private set; }

    /// <summary>When the source repository was last pushed to, if known.</summary>
    public DateTimeOffset? LastCommitAt { get; private set; }

    /// <summary>When the package was first ingested.</summary>
    public DateTimeOffset FirstSeenAt { get; private set; }

    /// <summary>When the metadata was last refreshed. Updated on every ingest.</summary>
    public DateTimeOffset LastRefreshedAt { get; private set; }

    /// <summary>Creates a new package aggregate for the given id.</summary>
    public static Package Create(PackageId id, DateTimeOffset timestamp) => new(id, timestamp);

    /// <summary>
    /// Idempotently updates the mutable metadata. Re-running ingestion with the
    /// same data is a no-op apart from <see cref="LastRefreshedAt"/>, which keeps
    /// repeated scans safe (Briefing: idempotent ingestion).
    /// </summary>
    public void Refresh(
        string? description,
        Uri? projectUrl,
        Uri? sourceRepositoryUrl,
        SpdxLicense? license,
        bool isDeprecated,
        SemVer? latestStableVersion,
        DateTimeOffset timestamp)
    {
        Description = description;
        ProjectUrl = projectUrl;
        SourceRepositoryUrl = sourceRepositoryUrl;
        License = license;
        IsDeprecated = isDeprecated;
        LatestStableVersion = latestStableVersion;
        LastRefreshedAt = timestamp;
    }

    /// <summary>Records source-repository health (archived flag, last push) for maintenance scoring.</summary>
    public void RecordRepositoryHealth(Uri? sourceRepositoryUrl, bool isArchived, DateTimeOffset? lastCommitAt, DateTimeOffset timestamp)
    {
        SourceRepositoryUrl = sourceRepositoryUrl ?? SourceRepositoryUrl;
        IsArchived = isArchived;
        LastCommitAt = lastCommitAt;
        LastRefreshedAt = timestamp;
    }
}
