using System.Text.Json;
using System.Text.Json.Serialization;

namespace DepRadar.Infrastructure.External.PyPi;

// Minimal projection of the PyPI JSON API (https://pypi.org/pypi/{name}[/{version}]/json).

/// <summary>The package document: the queried version's <c>info</c> + all release versions.</summary>
internal sealed record PyPiDocument(
    PyPiInfo? Info,
    IReadOnlyDictionary<string, JsonElement>? Releases);

/// <summary>The queried version's metadata fields that matter for risk.</summary>
internal sealed record PyPiInfo(
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("requires_dist")] IReadOnlyList<string>? RequiresDist,
    [property: JsonPropertyName("license")] string? License);
