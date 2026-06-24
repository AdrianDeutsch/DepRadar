using System.Text.Json.Serialization;

namespace DepRadar.Infrastructure.External.Osv;

// Minimal projections of the OSV.dev /v1/query request and response.

/// <summary>Query body: a package coordinate + version.</summary>
internal sealed record OsvQueryRequest(OsvPackage Package, string Version);

/// <summary>An ecosystem-qualified package name.</summary>
internal sealed record OsvPackage(string Name, string Ecosystem);

/// <summary>Query response: the advisories affecting the version.</summary>
internal sealed record OsvQueryResponse(IReadOnlyList<OsvVulnerability>? Vulns);

/// <summary>A single advisory.</summary>
internal sealed record OsvVulnerability(
    string? Id,
    string? Summary,
    IReadOnlyList<string>? Aliases,
    [property: JsonPropertyName("database_specific")] OsvDatabaseSpecific? DatabaseSpecific);

/// <summary>Advisory database-specific metadata (carries the GitHub-style severity label).</summary>
internal sealed record OsvDatabaseSpecific(string? Severity);
