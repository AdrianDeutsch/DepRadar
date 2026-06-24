namespace DepRadar.Application.Abstractions;

/// <summary>
/// Raw package metadata as returned by an external source (deps.dev / NuGet),
/// before it is parsed into domain value objects.
/// </summary>
/// <remarks>
/// Versions and license are kept as <see cref="string"/> on purpose: this is
/// untrusted external data, so parsing/validation into <c>SemVer</c> and
/// <c>SpdxLicense</c> is the use-case handler's job, where malformed entries can
/// be skipped without failing the whole ingest.
/// </remarks>
public sealed record PackageMetadata(
    string PackageId,
    string? Description,
    Uri? ProjectUrl,
    Uri? SourceRepositoryUrl,
    string? License,
    bool IsDeprecated,
    string? LatestStableVersion,
    IReadOnlyList<PackageVersionMetadata> Versions);

/// <summary>Raw metadata for a single published version.</summary>
public sealed record PackageVersionMetadata(
    string Version,
    DateTimeOffset? PublishedAt,
    bool IsDeprecated,
    string? License);
