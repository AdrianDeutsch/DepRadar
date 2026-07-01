namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a <c>go.sum</c> checksum ledger into the distinct (module, version) pairs it
/// records. Pure.
/// </summary>
/// <remarks>
/// Lines have the shape <c>module version[/go.mod] h1:hash</c>; the <c>/go.mod</c>
/// suffix marks the manifest hash of the same version and is folded away. Note the
/// honest caveat: go.sum is a checksum LEDGER, not a lockfile — it may contain a
/// superset of the versions the build actually selects (documented in ADR 0026), but
/// scanning it covers everything the module graph could resolve to.
/// </remarks>
public static class GoSum
{
    /// <summary>Reads the distinct recorded (module, version) pairs.</summary>
    public static IReadOnlyList<LockedPackage> Parse(string content)
    {
        var locked = new List<LockedPackage>();
        var seen = new HashSet<(string, string)>();

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[1].StartsWith('v'))
            {
                continue;
            }

            var version = parts[1].EndsWith("/go.mod", StringComparison.Ordinal)
                ? parts[1][..^"/go.mod".Length]
                : parts[1];

            if (seen.Add((parts[0], version)))
            {
                locked.Add(new LockedPackage(parts[0], version));
            }
        }

        return locked;
    }
}
