using System.Text.RegularExpressions;
using DepRadar.Application.Projects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Applies version bumps to a <c>requirements.txt</c>'s text — the PyPI counterpart of
/// <see cref="ManifestPatcher"/>. Only exact <c>==</c> pins are rewritten (any other
/// specifier has no single unambiguous version to replace); trailing comments and
/// environment markers on the line are preserved. Pure.
/// </summary>
public static partial class RequirementsPatcher
{
    /// <summary>
    /// Rewrites each bumped requirement's <c>==</c> pin (package name → new version),
    /// matching names in their PEP 503 canonical form.
    /// </summary>
    public static ManifestPatch Apply(string content, IReadOnlyDictionary<string, string> bumps)
    {
        var canonical = bumps.ToDictionary(pair => PyPiName.Normalize(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        var applied = new List<PackageBump>();

        var lines = content.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            // Groups: 1 = name (+extras), 2 = pinned version.
            var match = PinRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var name = PyPiName.Normalize(match.Groups[1].Value);
            if (!canonical.TryGetValue(name, out var newVersion)
                || string.Equals(match.Groups[2].Value, newVersion, StringComparison.Ordinal))
            {
                continue;
            }

            applied.Add(new PackageBump(name, match.Groups[2].Value, newVersion));
            lines[i] = lines[i][..match.Groups[2].Index] + newVersion + lines[i][(match.Groups[2].Index + match.Groups[2].Length)..];
        }

        return new ManifestPatch(string.Join('\n', lines), applied);
    }

    [GeneratedRegex(@"^\s*([A-Za-z0-9][A-Za-z0-9._-]*(?:\s*\[[^\]]*\])?)\s*==\s*([A-Za-z0-9.!+*]+)")]
    private static partial Regex PinRegex();
}
