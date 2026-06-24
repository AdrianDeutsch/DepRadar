using System.Text.Json.Serialization;

namespace DepRadar.Infrastructure.External.NuGet;

// Minimal projections of the NuGet V3 registration (gz-semver2) JSON. Property
// names bind case-insensitively (Web defaults); "@id" needs an explicit name.

/// <summary>Registration index: a list of pages.</summary>
internal sealed record NuGetRegistrationIndex(IReadOnlyList<NuGetRegistrationPage>? Items);

/// <summary>
/// A registration page. <see cref="Items"/> is inlined for small packages; otherwise
/// only <see cref="PageUrl"/> is present and the page must be fetched separately.
/// </summary>
internal sealed record NuGetRegistrationPage(
    [property: JsonPropertyName("@id")] string? PageUrl,
    IReadOnlyList<NuGetRegistrationLeaf>? Items);

/// <summary>A registration leaf wrapping the catalog entry for one version.</summary>
internal sealed record NuGetRegistrationLeaf(NuGetCatalogEntry? CatalogEntry);

/// <summary>The per-version catalog entry with declared dependencies, license and deprecation.</summary>
internal sealed record NuGetCatalogEntry(
    string? Version,
    bool? Listed,
    string? LicenseExpression,
    NuGetDeprecation? Deprecation,
    IReadOnlyList<NuGetDependencyGroup>? DependencyGroups);

/// <summary>Present (non-null) only when the version is deprecated.</summary>
internal sealed record NuGetDeprecation(IReadOnlyList<string>? Reasons);

/// <summary>Declared dependencies for one target framework.</summary>
internal sealed record NuGetDependencyGroup(
    string? TargetFramework,
    IReadOnlyList<NuGetDependencyItem>? Dependencies);

/// <summary>A single declared dependency (id + requested range).</summary>
internal sealed record NuGetDependencyItem(string? Id, string? Range);
