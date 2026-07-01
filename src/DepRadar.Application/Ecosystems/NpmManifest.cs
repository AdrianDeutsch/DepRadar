using System.Text.Json;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a <c>package.json</c> manifest into its direct runtime dependencies
/// (the <c>dependencies</c> map). Pure — the npm counterpart of the
/// <c>.csproj</c> parsing in <c>ProjectFileParser</c>.
/// </summary>
/// <remarks>
/// <c>devDependencies</c> are not shipped with the package and are deliberately
/// excluded, matching what an <c>npm install --omit=dev</c> deploys. Non-registry
/// specifiers (git/file/link/workspace URLs) are kept as-is; they simply resolve
/// to nothing against the registry and surface as unresolved.
/// </remarks>
public static class NpmManifest
{
    /// <summary>Reads the <c>dependencies</c> entries as (name, range) pairs.</summary>
    /// <exception cref="FormatException">The content is not a JSON object.</exception>
    public static IReadOnlyList<ManifestDependency> ParseDependencies(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new FormatException($"Not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("A package.json manifest must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("dependencies", out var dependencies)
                || dependencies.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return dependencies.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.String)
                .Select(property => new ManifestDependency(property.Name, property.Value.GetString() ?? string.Empty))
                .ToList();
        }
    }
}

/// <summary>A direct dependency declared in a manifest: name + version range/specifier.</summary>
public readonly record struct ManifestDependency(string Name, string Specifier);
