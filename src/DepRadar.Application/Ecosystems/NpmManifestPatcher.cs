using System.Text.RegularExpressions;
using DepRadar.Application.Projects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Applies version bumps to a <c>package.json</c>'s text — the npm counterpart of
/// <see cref="ManifestPatcher"/>. A targeted text edit inside the <c>dependencies</c>
/// object (rather than re-serializing the JSON) keeps formatting, ordering and the
/// untouched <c>devDependencies</c> intact. Pure.
/// </summary>
public static class NpmManifestPatcher
{
    /// <summary>
    /// Rewrites each bumped dependency's range (package name → new version), keeping the
    /// declared <c>^</c>/<c>~</c> operator so the manifest stays idiomatic. Only entries
    /// inside the <c>dependencies</c> object are touched.
    /// </summary>
    public static ManifestPatch Apply(string content, IReadOnlyDictionary<string, string> bumps)
    {
        if (!TryFindDependenciesBlock(content, out var start, out var end))
        {
            return new ManifestPatch(content, []);
        }

        var block = content[start..end];
        var applied = new List<PackageBump>();

        foreach (var (name, newVersion) in bumps)
        {
            // Groups: 1 = "name": " prefix, 2 = current range, 3 = closing quote.
            var pattern = new Regex($"""("{Regex.Escape(name)}"\s*:\s*")([^"]+)(")""");
            var match = pattern.Match(block);
            if (!match.Success)
            {
                continue;
            }

            var current = match.Groups[2].Value;
            var replacement = KeepOperator(current, newVersion);
            if (string.Equals(current, replacement, StringComparison.Ordinal))
            {
                continue;
            }

            applied.Add(new PackageBump(name, current, replacement));
            block = pattern.Replace(block, m => $"{m.Groups[1].Value}{replacement}{m.Groups[3].Value}", 1);
        }

        return new ManifestPatch(content[..start] + block + content[end..], applied);
    }

    /// <summary>A caret/tilde range stays a caret/tilde range; anything else pins exactly.</summary>
    private static string KeepOperator(string currentRange, string newVersion) =>
        currentRange.Length > 0 && currentRange[0] is '^' or '~'
            ? $"{currentRange[0]}{newVersion}"
            : newVersion;

    // Locates the span of the "dependencies": { ... } object by brace tracking.
    private static bool TryFindDependenciesBlock(string content, out int start, out int end)
    {
        start = end = 0;
        var property = Regex.Match(content, @"""dependencies""\s*:\s*\{");
        if (!property.Success)
        {
            return false;
        }

        start = property.Index + property.Length;
        var depth = 1;
        for (var i = start; i < content.Length; i++)
        {
            depth += content[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                end = i;
                return true;
            }
        }

        return false;
    }
}
