using System.Text.Json;
using System.Text.Json.Serialization;

namespace DepRadar.Infrastructure.External.Npm;

// Minimal projection of the npm registry document at https://registry.npmjs.org/{name}.

/// <summary>The registry document: all published versions plus dist-tags.</summary>
internal sealed record NpmPackageDocument(
    IReadOnlyDictionary<string, NpmVersionDocument>? Versions,
    [property: JsonPropertyName("dist-tags")] NpmDistTags? DistTags);

/// <summary>One published version's manifest fields that matter for risk.</summary>
/// <remarks>
/// <c>license</c> and <c>deprecated</c> are polymorphic in the wild (string vs object vs
/// bool), so they are read as raw <see cref="JsonElement"/> and interpreted by the resolver.
/// </remarks>
internal sealed record NpmVersionDocument(
    IReadOnlyDictionary<string, string>? Dependencies,
    JsonElement License,
    JsonElement Deprecated);

/// <summary>The dist-tags map; only <c>latest</c> is used.</summary>
internal sealed record NpmDistTags([property: JsonPropertyName("latest")] string? Latest);
