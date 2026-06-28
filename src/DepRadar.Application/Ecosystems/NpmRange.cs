using System.Globalization;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Resolves an npm version range to a concrete version — the npm counterpart of NuGet's
/// range resolution. Supports the common grammar: <c>^</c>, <c>~</c>, x-ranges
/// (<c>1.2.x</c>, <c>1.*</c>), hyphen ranges, comparators (<c>&gt;=</c>, <c>&lt;</c>, …),
/// exact versions, and <c>||</c> unions. Pure — fully unit-testable without a registry.
/// </summary>
public static class NpmRange
{
    /// <summary>The highest candidate satisfying <paramref name="range"/> (stable preferred), or null.</summary>
    public static SemVer? BestMatch(string range, IEnumerable<SemVer> candidates)
    {
        var matching = candidates.Where(version => Satisfies(version, range)).ToList();
        if (matching.Count == 0)
        {
            return null;
        }

        var stable = matching.Where(version => version.IsStable).ToList();
        return (stable.Count > 0 ? stable : matching).Max();
    }

    /// <summary>Whether <paramref name="version"/> satisfies the npm range string.</summary>
    public static bool Satisfies(SemVer version, string range)
    {
        var trimmed = (range ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return version.IsStable; // empty range == "*"
        }

        // A range is an OR of comparator sets separated by "||".
        return trimmed
            .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(set => SatisfiesSet(version, set));
    }

    private static bool SatisfiesSet(SemVer version, string set)
    {
        foreach (var comparator in Expand(set))
        {
            var comparison = version.CompareTo(comparator.Bound);
            var ok = comparator.Operator switch
            {
                ">=" => comparison >= 0,
                ">" => comparison > 0,
                "<=" => comparison <= 0,
                "<" => comparison < 0,
                _ => comparison == 0, // "="
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Expands a comparator set into primitive (operator, bound) comparators.</summary>
    private static IEnumerable<(string Operator, SemVer Bound)> Expand(string set)
    {
        // Hyphen range: "1.2.3 - 2.3.4" (a partial upper bound is exclusive of the next).
        var hyphen = set.Split(" - ", StringSplitOptions.TrimEntries);
        if (hyphen.Length == 2)
        {
            yield return (">=", Floor(hyphen[0]));
            var (upper, upperLower) = ParsePartial(hyphen[1]);
            yield return upper.Specified == 3
                ? ("<=", upperLower)
                : ("<", upper.Specified == 2 ? Version(upper.Major, upper.Minor + 1, 0) : Version(upper.Major + 1, 0, 0));
            yield break;
        }

        foreach (var token in set.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var comparator in ExpandToken(token))
            {
                yield return comparator;
            }
        }
    }

    private static IEnumerable<(string Operator, SemVer Bound)> ExpandToken(string token)
    {
        if (token is "*" or "x" or "X" or "latest")
        {
            yield break; // matches anything
        }

        if (token[0] is '^')
        {
            var (parts, lower) = ParsePartial(token[1..]);
            yield return (">=", lower);
            yield return ("<", CaretUpper(parts));
            yield break;
        }

        if (token[0] is '~')
        {
            var (parts, lower) = ParsePartial(token[1..]);
            yield return (">=", lower);
            yield return ("<", TildeUpper(parts));
            yield break;
        }

        foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
        {
            if (token.StartsWith(op, StringComparison.Ordinal))
            {
                yield return (op, Floor(token[op.Length..]));
                yield break;
            }
        }

        // A bare version: exact if fully specified, else an x-range (e.g. "1.2" == ">=1.2.0 <1.3.0").
        var (components, floor) = ParsePartial(token);
        if (components.Specified == 3)
        {
            yield return ("=", floor);
        }
        else
        {
            yield return (">=", floor);
            yield return ("<", components.Specified == 2 ? TildeUpper(components) : CaretUpperMajor(components));
        }
    }

    private readonly record struct Parts(int Major, int Minor, int Patch, int Specified);

    private static (Parts Parts, SemVer Lower) ParsePartial(string raw)
    {
        var pieces = raw.Split('.', StringSplitOptions.TrimEntries);
        var specified = 0;
        var values = new int[3];
        for (var i = 0; i < 3 && i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (piece is "x" or "X" or "*" or "")
            {
                break;
            }

            // Drop any prerelease/build suffix on the lower bound's patch for range math.
            var numeric = new string(piece.TakeWhile(char.IsDigit).ToArray());
            values[i] = numeric.Length > 0 ? int.Parse(numeric, CultureInfo.InvariantCulture) : 0;
            specified++;
        }

        var parts = new Parts(values[0], values[1], values[2], specified);
        var lower = specified == 3 ? SemVer.Parse(raw) : Version(values[0], values[1], values[2]);
        return (parts, lower);
    }

    private static SemVer CaretUpper(Parts p) =>
        p.Major > 0 ? Version(p.Major + 1, 0, 0)
        : p.Minor > 0 ? Version(0, p.Minor + 1, 0)
        : Version(0, 0, p.Patch + 1);

    private static SemVer CaretUpperMajor(Parts p) => Version(p.Major + 1, 0, 0);

    private static SemVer TildeUpper(Parts p) =>
        p.Specified >= 2 ? Version(p.Major, p.Minor + 1, 0) : Version(p.Major + 1, 0, 0);

    private static SemVer Floor(string raw) => ParsePartial(raw).Lower;

    private static SemVer Version(int major, int minor, int patch) =>
        SemVer.Parse(string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}"));
}
