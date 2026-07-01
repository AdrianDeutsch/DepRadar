using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Resolves a Cargo version requirement to a concrete version. Pure.
/// </summary>
/// <remarks>
/// Cargo's grammar is npm's with two twists: a bare requirement (<c>"1.2.3"</c>)
/// means <em>caret</em> (npm: exact), and multiple comparators are comma-separated
/// (npm: space). Both normalize onto <see cref="NpmRange"/> — caret/tilde/wildcard
/// and comparator semantics (including the <c>0.x</c> caret rules) are identical.
/// </remarks>
public static class CargoReq
{
    /// <summary>The highest candidate satisfying <paramref name="req"/> (stable preferred), or null.</summary>
    public static SemVer? BestMatch(string req, IEnumerable<SemVer> candidates) =>
        NpmRange.BestMatch(Normalize(req), candidates);

    /// <summary>Whether <paramref name="version"/> satisfies the Cargo requirement.</summary>
    public static bool Satisfies(SemVer version, string req) =>
        NpmRange.Satisfies(version, Normalize(req));

    /// <summary>Rewrites a Cargo requirement into the equivalent npm range.</summary>
    internal static string Normalize(string req)
    {
        var clauses = (req ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(clause => clause[0] is '^' or '~' or '=' or '<' or '>' or '*' ? clause : $"^{clause}");

        return string.Join(' ', clauses);
    }
}
