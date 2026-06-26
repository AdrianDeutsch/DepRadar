using System.Text.RegularExpressions;

namespace DepRadar.Application.Projects;

/// <summary>
/// Applies version bumps to a project/props file's text, preserving everything else.
/// A targeted text edit (rather than re-serializing the XML) keeps formatting, comments
/// and ordering intact — important for a clean auto-fix pull request. Pure.
/// </summary>
public static class ManifestPatcher
{
    /// <summary>
    /// Rewrites the <c>Version</c> of each <c>PackageReference</c>/<c>PackageVersion</c>
    /// named in <paramref name="bumps"/> (package id → new version), returning the patched
    /// text and the bumps that were actually applied.
    /// </summary>
    public static ManifestPatch Apply(string content, IReadOnlyDictionary<string, string> bumps)
    {
        var patched = content;
        var applied = new List<PackageBump>();

        foreach (var (id, newVersion) in bumps)
        {
            // Groups: 1 = prefix up to the opening quote, 2 = current version, 3 = closing quote.
            var pattern = new Regex(
                $"""(<Package(?:Reference|Version)\s+Include="{Regex.Escape(id)}"\s+Version=")([^"]+)(")""",
                RegexOptions.IgnoreCase);

            var match = pattern.Match(patched);
            if (!match.Success || string.Equals(match.Groups[2].Value, newVersion, StringComparison.Ordinal))
            {
                continue;
            }

            applied.Add(new PackageBump(id, match.Groups[2].Value, newVersion));
            patched = pattern.Replace(patched, m => $"{m.Groups[1].Value}{newVersion}{m.Groups[3].Value}", 1);
        }

        return new ManifestPatch(patched, applied);
    }
}

/// <summary>The result of patching a manifest: the new text and the applied bumps.</summary>
public sealed record ManifestPatch(string Content, IReadOnlyList<PackageBump> Applied);

/// <summary>A single applied version bump.</summary>
public sealed record PackageBump(string Package, string FromVersion, string ToVersion);
