using System.Globalization;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Resolves a <see href="https://peps.python.org/pep-0440/">PEP 440</see> version
/// specifier to a concrete version — the PyPI counterpart of NuGet/npm range resolution.
/// Supports the common operators (<c>==</c>, <c>!=</c>, <c>&gt;=</c>, <c>&lt;=</c>,
/// <c>&gt;</c>, <c>&lt;</c>, <c>~=</c>, <c>===</c>), <c>==X.*</c> wildcards, and
/// comma-separated clauses (AND). Pure — fully unit-testable.
/// </summary>
/// <remarks>
/// Matching runs over the project's <see cref="SemVer"/>; PEP 440 pre/post/dev/epoch
/// versions that don't parse are simply not considered, so resolution targets stable
/// releases (which is what a dependency scan wants).
/// </remarks>
public static class PyPiSpecifier
{
    /// <summary>The highest candidate satisfying <paramref name="specifier"/> (stable preferred), or null.</summary>
    public static SemVer? BestMatch(string specifier, IEnumerable<SemVer> candidates)
    {
        var matching = candidates.Where(version => Matches(version, specifier)).ToList();
        if (matching.Count == 0)
        {
            return null;
        }

        var stable = matching.Where(version => version.IsStable).ToList();
        return (stable.Count > 0 ? stable : matching).Max();
    }

    /// <summary>Whether <paramref name="version"/> satisfies the PEP 440 specifier set.</summary>
    public static bool Matches(SemVer version, string specifier)
    {
        var trimmed = (specifier ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return version.IsStable;
        }

        // All comma-separated clauses must hold (AND).
        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(clause => MatchesClause(version, clause));
    }

    private static bool MatchesClause(SemVer version, string clause)
    {
        foreach (var comparator in Expand(clause))
        {
            var comparison = version.CompareTo(comparator.Bound);
            var ok = comparator.Operator switch
            {
                ">=" => comparison >= 0,
                ">" => comparison > 0,
                "<=" => comparison <= 0,
                "<" => comparison < 0,
                "!=" => comparison != 0,
                _ => comparison == 0, // "==" / "==="
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<(string Operator, SemVer Bound)> Expand(string clause)
    {
        // Compatible release: ~=X.Y.Z := >=X.Y.Z, <X.(Y+1).0 ; ~=X.Y := >=X.Y, <(X+1).0.0
        if (clause.StartsWith("~=", StringComparison.Ordinal))
        {
            var (parts, lower) = ParsePartial(clause[2..]);
            yield return (">=", lower);
            yield return ("<", parts.Specified >= 3 ? Version(parts.Major, parts.Minor + 1, 0) : Version(parts.Major + 1, 0, 0));
            yield break;
        }

        foreach (var op in new[] { "===", "==", "!=", ">=", "<=", ">", "<" })
        {
            if (clause.StartsWith(op, StringComparison.Ordinal))
            {
                var raw = clause[op.Length..].Trim();

                // Wildcard equality: ==1.4.* := >=1.4.0, <1.5.0 (only valid with ==).
                if (op is "==" && raw.EndsWith(".*", StringComparison.Ordinal))
                {
                    var (parts, lower) = ParsePartial(raw[..^2]);
                    yield return (">=", lower);
                    yield return ("<", parts.Specified >= 2 ? Version(parts.Major, parts.Minor + 1, 0) : Version(parts.Major + 1, 0, 0));
                    yield break;
                }

                yield return (Normalize(op), ParsePartial(raw).Lower);
                yield break;
            }
        }

        // Bare version == exact.
        yield return ("==", ParsePartial(clause).Lower);
    }

    private static string Normalize(string op) => op == "===" ? "==" : op;

    private readonly record struct Parts(int Major, int Minor, int Patch, int Specified);

    private static (Parts Parts, SemVer Lower) ParsePartial(string raw)
    {
        var core = raw.Trim().TrimStart('v');
        var pieces = core.Split('.', StringSplitOptions.TrimEntries);
        var values = new int[3];
        var specified = 0;
        for (var i = 0; i < 3 && i < pieces.Length; i++)
        {
            var numeric = new string(pieces[i].TakeWhile(char.IsDigit).ToArray());
            if (numeric.Length == 0)
            {
                break;
            }

            values[i] = int.Parse(numeric, CultureInfo.InvariantCulture);
            specified++;
        }

        var parts = new Parts(values[0], values[1], values[2], specified);
        var lower = SemVer.TryParse(core, out var exact) ? exact : Version(values[0], values[1], values[2]);
        return (parts, lower);
    }

    private static SemVer Version(int major, int minor, int patch) =>
        SemVer.Parse(string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}"));
}
