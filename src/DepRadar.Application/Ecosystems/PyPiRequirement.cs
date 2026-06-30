using System.Text.RegularExpressions;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses one <c>requires_dist</c> entry from a PyPI package (PEP 508), e.g.
/// <c>"requests[security] (&gt;=2.0,&lt;3) ; python_version &gt;= '3.7' and extra == 'socks'"</c>,
/// into the package name, its PEP 440 specifier, and whether it is an optional
/// (extra-gated) dependency. Pure.
/// </summary>
public static partial class PyPiRequirement
{
    /// <summary>Parses a requirement; <paramref name="result"/> is meaningful only when it returns true.</summary>
    public static bool TryParse(string entry, out PyPiDependency result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        // Split the environment marker off (after the first ';').
        var semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        var requirement = (semicolon >= 0 ? entry[..semicolon] : entry).Trim();
        var marker = semicolon >= 0 ? entry[(semicolon + 1)..] : string.Empty;

        var match = NameRegex().Match(requirement);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var rest = requirement[match.Length..].Trim();

        // Drop a leading "[extras]" group, then unwrap an optional "(...)" around the specifier.
        if (rest.StartsWith('['))
        {
            var close = rest.IndexOf(']', StringComparison.Ordinal);
            rest = close >= 0 ? rest[(close + 1)..].Trim() : rest;
        }

        if (rest.StartsWith('(') && rest.EndsWith(')'))
        {
            rest = rest[1..^1].Trim();
        }

        // An "extra == ..." marker means the dependency is optional (installed only with that extra).
        var optional = marker.Contains("extra", StringComparison.OrdinalIgnoreCase);

        result = new PyPiDependency(name, rest, optional);
        return true;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9._-]*)", RegexOptions.Compiled)]
    private static partial Regex NameRegex();
}

/// <summary>A parsed PyPI dependency: name, PEP 440 specifier, and whether it is optional.</summary>
/// <param name="Name">The dependency package name.</param>
/// <param name="Specifier">The PEP 440 version specifier (may be empty = any).</param>
/// <param name="Optional">True when gated behind an <c>extra</c> (not a runtime dependency).</param>
public readonly record struct PyPiDependency(string Name, string Specifier, bool Optional);
