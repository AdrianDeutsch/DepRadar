using DepRadar.Domain.Risk;

namespace DepRadar.Domain.History;

/// <summary>The kind of change detected between two scans of the same package.</summary>
public enum DriftEventKind
{
    /// <summary>A new advisory now affects a package version.</summary>
    BecameVulnerable,

    /// <summary>A package version is now deprecated.</summary>
    BecameDeprecated,

    /// <summary>A package's source repository is now archived.</summary>
    BecameArchived,

    /// <summary>A package's source repository is now stale.</summary>
    BecameStale,

    /// <summary>An advisory that previously affected a package no longer does.</summary>
    AdvisoryCleared,

    /// <summary>A package's health score dropped.</summary>
    HealthRegressed,

    /// <summary>A package's health score improved.</summary>
    HealthImproved,

    /// <summary>A package entered the dependency graph.</summary>
    PackageAdded,

    /// <summary>A package left the dependency graph.</summary>
    PackageRemoved,
}

/// <summary>A single drift finding between two snapshots.</summary>
/// <param name="Package">The affected package id.</param>
/// <param name="Kind">What changed.</param>
/// <param name="Detail">A human-readable explanation.</param>
/// <param name="Severity">How much it should worry you (improvements are <see cref="RiskLevel.None"/>).</param>
public sealed record DriftEvent(string Package, DriftEventKind Kind, string Detail, RiskLevel Severity);
