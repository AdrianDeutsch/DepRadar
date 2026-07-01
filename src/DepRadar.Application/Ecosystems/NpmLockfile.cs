using System.Text.Json;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses an npm <c>package-lock.json</c> / <c>npm-shrinkwrap.json</c> (lockfile
/// version 2/3) into the exact installed packages. Pure.
/// </summary>
/// <remarks>
/// Entries live under <c>packages</c>, keyed by their install path
/// (<c>node_modules/express</c>, nested <c>node_modules/a/node_modules/b</c>, scoped
/// <c>node_modules/@scope/name</c>); the package name is everything after the last
/// <c>node_modules/</c>. Dev-only (<c>"dev": true</c>), linked and versionless entries
/// are skipped — mirroring the manifest scan, which covers runtime dependencies.
/// </remarks>
public static class NpmLockfile
{
    private const string PathMarker = "node_modules/";

    /// <summary>Reads the distinct installed (name, version) pairs.</summary>
    /// <exception cref="FormatException">The content is not a JSON object.</exception>
    public static IReadOnlyList<LockedPackage> Parse(string json)
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
                throw new FormatException("A package-lock.json must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var locked = new List<LockedPackage>();
            var seen = new HashSet<(string, string)>();

            foreach (var entry in packages.EnumerateObject())
            {
                var marker = entry.Name.LastIndexOf(PathMarker, StringComparison.Ordinal);
                if (marker < 0 || entry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue; // the "" root entry, or a malformed key
                }

                if (entry.Value.TryGetProperty("dev", out var dev) && dev.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (!entry.Value.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String)
                {
                    continue; // links and workspace references carry no version
                }

                var name = entry.Name[(marker + PathMarker.Length)..];
                if (name.Length > 0 && seen.Add((name, version.GetString()!)))
                {
                    locked.Add(new LockedPackage(name, version.GetString()!));
                }
            }

            return locked;
        }
    }
}
