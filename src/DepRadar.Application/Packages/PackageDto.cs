using DepRadar.Domain.Packages;

namespace DepRadar.Application.Packages;

/// <summary>API-facing read model for a package and its known versions.</summary>
public sealed record PackageDto(
    string Id,
    string? Description,
    string? ProjectUrl,
    string? SourceRepositoryUrl,
    string? License,
    bool IsDeprecated,
    string? LatestStableVersion,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastRefreshedAt,
    IReadOnlyList<PackageVersionDto> Versions)
{
    /// <summary>Projects a domain <see cref="Package"/> and its versions into a DTO.</summary>
    public static PackageDto FromDomain(Package package, IReadOnlyCollection<PackageVersion> versions) => new(
        package.Id.Original,
        package.Description,
        package.ProjectUrl?.ToString(),
        package.SourceRepositoryUrl?.ToString(),
        package.License?.Identifier,
        package.IsDeprecated,
        package.LatestStableVersion?.ToString(),
        package.FirstSeenAt,
        package.LastRefreshedAt,
        versions
            .OrderByDescending(v => v.Version)
            .Select(PackageVersionDto.FromDomain)
            .ToList());
}

/// <summary>API-facing read model for a single package version.</summary>
public sealed record PackageVersionDto(
    string Version,
    DateTimeOffset? PublishedAt,
    bool IsDeprecated,
    string? License)
{
    /// <summary>Projects a domain <see cref="PackageVersion"/> into a DTO.</summary>
    public static PackageVersionDto FromDomain(PackageVersion version) => new(
        version.Version.ToString(),
        version.PublishedAt,
        version.IsDeprecated,
        version.License?.Identifier);
}
