namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a pip <c>requirements.txt</c> into direct dependencies. Pure — the PyPI
/// counterpart of the <c>.csproj</c> parsing in <c>ProjectFileParser</c>.
/// </summary>
/// <remarks>
/// Each requirement line is PEP 508, so the heavy lifting is delegated to
/// <see cref="PyPiRequirement"/>. File-level syntax handled here: blank lines,
/// <c>#</c> comments (full-line and inline), backslash line continuations, and
/// option lines (<c>-r</c>, <c>-e</c>, <c>--hash</c>, …) which are skipped —
/// nested/editable/URL requirements are not registry packages to score.
/// </remarks>
public static class RequirementsFile
{
    /// <summary>Reads the requirement lines as (name, specifier) pairs.</summary>
    public static IReadOnlyList<ManifestDependency> Parse(string content)
    {
        var dependencies = new List<ManifestDependency>();

        foreach (var logicalLine in JoinContinuations(content))
        {
            var line = StripInlineComment(logicalLine).Trim();
            if (line.Length == 0 || line.StartsWith('-'))
            {
                continue;
            }

            if (PyPiRequirement.TryParse(line, out var requirement) && !requirement.Optional)
            {
                dependencies.Add(new ManifestDependency(requirement.Name, requirement.Specifier));
            }
        }

        return dependencies;
    }

    /// <summary>Merges physical lines ending in <c>\</c> into one logical line.</summary>
    private static IEnumerable<string> JoinContinuations(string content)
    {
        var pending = string.Empty;
        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.EndsWith('\\'))
            {
                pending += line[..^1];
                continue;
            }

            yield return pending + line;
            pending = string.Empty;
        }

        if (pending.Length > 0)
        {
            yield return pending;
        }
    }

    // pip treats " #" (or a line-leading '#') as a comment start.
    private static string StripInlineComment(string line)
    {
        if (line.StartsWith('#'))
        {
            return string.Empty;
        }

        var index = line.IndexOf(" #", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }
}
