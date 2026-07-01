using System.Text.Json.Serialization;

namespace DepRadar.Infrastructure.External.Cargo;

// Minimal projection of the crates.io API (https://crates.io/api/v1/crates/{name}).

/// <summary>The crate document: every published version with its risk-relevant facts.</summary>
internal sealed record CargoCrateDocument(
    [property: JsonPropertyName("versions")] IReadOnlyList<CargoVersion>? Versions);

/// <summary>One published version.</summary>
internal sealed record CargoVersion(
    [property: JsonPropertyName("num")] string? Num,
    [property: JsonPropertyName("yanked")] bool Yanked,
    [property: JsonPropertyName("license")] string? License);

/// <summary>The dependencies of one version (…/{version}/dependencies).</summary>
internal sealed record CargoDependenciesDocument(
    [property: JsonPropertyName("dependencies")] IReadOnlyList<CargoDependency>? Dependencies);

/// <summary>One declared dependency edge.</summary>
internal sealed record CargoDependency(
    [property: JsonPropertyName("crate_id")] string? CrateId,
    [property: JsonPropertyName("req")] string? Req,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("optional")] bool Optional);
