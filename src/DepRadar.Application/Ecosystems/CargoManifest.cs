namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a <c>Cargo.toml</c> manifest into its direct runtime dependencies. Pure —
/// the Cargo counterpart of <see cref="NpmManifest"/>.
/// </summary>
/// <remarks>
/// Handled forms, line-based over the machine-friendly TOML subset crates actually use:
/// <c>[dependencies]</c> entries as <c>name = "1.0"</c> or
/// <c>name = { version = "1.0", … }</c>, plus <c>[dependencies.name]</c> sub-tables
/// with a <c>version</c> key. <c>dev-</c>/<c>build-dependencies</c> are excluded
/// (runtime focus, like every other manifest parser), and path/git/workspace
/// dependencies carry no registry version — they are skipped.
/// </remarks>
public static class CargoManifest
{
    /// <summary>Reads the runtime dependencies as (name, requirement) pairs.</summary>
    public static IReadOnlyList<ManifestDependency> ParseDependencies(string content)
    {
        var dependencies = new List<ManifestDependency>();
        var inDependenciesTable = false;
        string? subTableName = null;

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                inDependenciesTable = line is "[dependencies]";

                // [dependencies.foo] sub-table: collect its version key below.
                subTableName = line.StartsWith("[dependencies.", StringComparison.Ordinal) && line.EndsWith(']')
                    ? line["[dependencies.".Length..^1].Trim()
                    : null;
                continue;
            }

            if (subTableName is not null)
            {
                if (TryReadValue(line, "version", out var subVersion) && TryUnquote(subVersion, out var quoted))
                {
                    dependencies.Add(new ManifestDependency(subTableName, quoted));
                    subTableName = null; // one version per sub-table
                }

                continue;
            }

            if (!inDependenciesTable)
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var name = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();

            if (TryUnquote(value, out var requirement))
            {
                dependencies.Add(new ManifestDependency(name, requirement)); // name = "1.0"
            }
            else if (value.StartsWith('{') && TryReadInlineVersion(value, out var inlineRequirement))
            {
                dependencies.Add(new ManifestDependency(name, inlineRequirement)); // name = { version = "1.0", … }
            }

            // No version key (path/git/workspace): not a registry crate — skipped.
        }

        return dependencies;
    }

    private static bool TryReadInlineVersion(string inlineTable, out string requirement)
    {
        requirement = string.Empty;
        var marker = inlineTable.IndexOf("version", StringComparison.Ordinal);
        if (marker < 0)
        {
            return false;
        }

        var rest = inlineTable[(marker + "version".Length)..].TrimStart();
        return rest.StartsWith('=') && TryUnquoteLeading(rest[1..].TrimStart(), out requirement);
    }

    private static bool TryReadValue(string line, string key, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line[key.Length..].TrimStart();
        if (!rest.StartsWith('='))
        {
            return false;
        }

        value = rest[1..].Trim();
        return true;
    }

    private static bool TryUnquote(string value, out string content)
    {
        content = string.Empty;
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return false;
        }

        content = value[1..^1];
        return true;
    }

    private static bool TryUnquoteLeading(string value, out string content)
    {
        content = string.Empty;
        if (value.Length < 2 || value[0] != '"')
        {
            return false;
        }

        var close = value.IndexOf('"', 1);
        if (close < 0)
        {
            return false;
        }

        content = value[1..close];
        return true;
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf('#', StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }
}
