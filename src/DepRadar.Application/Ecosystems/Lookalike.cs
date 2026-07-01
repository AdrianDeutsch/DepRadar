namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Detects package names that look like a typo of a well-known package — the
/// typosquatting attack vector (<c>requets</c> → <c>requests</c>, <c>lodahs</c> →
/// <c>lodash</c>). Pure.
/// </summary>
/// <remarks>
/// Uses the Damerau–Levenshtein distance (edits + adjacent transpositions, the classic
/// typo operations). Thresholds are deliberately conservative — distance 1 for names of
/// four-plus characters, distance 2 only for long names — because lookalike detection
/// has inherent false positives (e.g. <c>expresso</c> vs <c>express</c>); that is also
/// why callers surface it as a warning, never a gate.
/// </remarks>
public static class Lookalike
{
    /// <summary>
    /// The well-known package <paramref name="name"/> looks like a typo of, or
    /// <see langword="null"/> — including when the name IS a well-known package.
    /// Both sides are expected in the ecosystem's canonical (lower-case) form.
    /// </summary>
    public static string? FindTarget(string name, IReadOnlyList<string> knownPackages)
    {
        if (name.Length < 4 || knownPackages.Contains(name))
        {
            return null;
        }

        var allowed = name.Length >= 10 ? 2 : 1;
        string? closest = null;
        var closestDistance = int.MaxValue;

        foreach (var known in knownPackages)
        {
            // A cheap length pre-filter before the O(n·m) distance.
            if (Math.Abs(known.Length - name.Length) > allowed)
            {
                continue;
            }

            var distance = DamerauLevenshtein(name, known, allowed);
            if (distance <= allowed && distance < closestDistance)
            {
                closest = known;
                closestDistance = distance;
            }
        }

        return closest;
    }

    /// <summary>
    /// Damerau–Levenshtein distance (optimal string alignment), early-exited once the
    /// distance provably exceeds <paramref name="max"/>.
    /// </summary>
    public static int DamerauLevenshtein(string left, string right, int max)
    {
        var previousPrevious = new int[right.Length + 1];
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            var rowMinimum = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);

                // Adjacent transposition ("lodahs" -> "lodash").
                if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                {
                    current[j] = Math.Min(current[j], previousPrevious[j - 2] + 1);
                }

                rowMinimum = Math.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > max)
            {
                return max + 1; // cannot get back under the threshold
            }

            (previousPrevious, previous, current) = (previous, current, previousPrevious);
        }

        return previous[right.Length];
    }
}
