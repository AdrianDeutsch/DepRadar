namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a <c>go.mod</c> file into its direct requirements. Pure — the Go counterpart
/// of <see cref="NpmManifest"/>.
/// </summary>
/// <remarks>
/// Handles single-line (<c>require module v1.2.3</c>) and block
/// (<c>require ( … )</c>) forms. Requirements marked <c>// indirect</c> are skipped —
/// they are transitive pins, not something a human declared (Go ≥1.17 lists them
/// separately). <c>replace</c>/<c>exclude</c> directives are out of scope; go.mod
/// versions are exact (minimal version selection), so the specifier IS the version.
/// </remarks>
public static class GoMod
{
    /// <summary>Reads the direct (non-indirect) requirements as (module, version) pairs.</summary>
    public static IReadOnlyList<ManifestDependency> ParseRequires(string content)
    {
        var dependencies = new List<ManifestDependency>();
        var inRequireBlock = false;

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var isIndirect = raw.Contains("// indirect", StringComparison.Ordinal);
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (inRequireBlock)
            {
                if (line is ")")
                {
                    inRequireBlock = false;
                }
                else if (!isIndirect)
                {
                    AddRequirement(dependencies, line);
                }

                continue;
            }

            if (line is "require (")
            {
                inRequireBlock = true;
            }
            else if (line.StartsWith("require ", StringComparison.Ordinal) && !isIndirect)
            {
                AddRequirement(dependencies, line["require ".Length..]);
            }
        }

        return dependencies;
    }

    // A requirement line: "<module-path> <version>".
    private static void AddRequirement(List<ManifestDependency> dependencies, string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[1].StartsWith('v'))
        {
            dependencies.Add(new ManifestDependency(parts[0], parts[1]));
        }
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }
}
