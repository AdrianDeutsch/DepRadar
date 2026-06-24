namespace DepRadar.Infrastructure.External.DepsDev;

// Minimal projections of the deps.dev v3 JSON responses. Property names are matched
// case-insensitively (JsonSerializerDefaults.Web), so camelCase keys bind directly.

/// <summary>Response of <c>GET /v3/systems/nuget/packages/{name}</c>.</summary>
internal sealed record DepsDevPackageResponse(IReadOnlyList<DepsDevVersionEntry>? Versions);

/// <summary>A single entry in a package's version list.</summary>
internal sealed record DepsDevVersionEntry(
    DepsDevVersionKey? VersionKey,
    DateTimeOffset? PublishedAt,
    bool IsDefault,
    bool IsDeprecated);

/// <summary>The (system, name, version) coordinate of a version.</summary>
internal sealed record DepsDevVersionKey(string? Version);

/// <summary>Response of <c>GET /v3/systems/nuget/packages/{name}/versions/{version}</c>.</summary>
internal sealed record DepsDevVersionResponse(
    IReadOnlyList<string>? Licenses,
    IReadOnlyList<DepsDevLink>? Links,
    bool IsDeprecated,
    DateTimeOffset? PublishedAt);

/// <summary>A labeled external link (e.g. <c>SOURCE_REPO</c>, <c>HOMEPAGE</c>).</summary>
internal sealed record DepsDevLink(string? Label, string? Url);
