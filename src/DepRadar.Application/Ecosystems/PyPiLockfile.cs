namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a Python lockfile — <c>poetry.lock</c> or <c>uv.lock</c> — into the exact
/// installed packages. Pure.
/// </summary>
/// <remarks>
/// Both formats are TOML with the same relevant shape: repeated <c>[[package]]</c>
/// blocks carrying <c>name = "…"</c> and <c>version = "…"</c>. Because the files are
/// machine-generated with that stable layout, a line-based reader keeps the Application
/// layer dependency-free (no TOML package) — the same trade-off as the hand-rolled
/// SemVer (ADR 0003).
/// </remarks>
public static class PyPiLockfile
{
    /// <summary>Reads the distinct locked (name, version) pairs, names PEP 503 canonicalized.</summary>
    public static IReadOnlyList<LockedPackage> Parse(string content)
    {
        var locked = new List<LockedPackage>();
        var seen = new HashSet<(string, string)>();

        string? name = null;
        string? version = null;
        var inPackageBlock = false;

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();

            // A new TOML table/array-of-tables header ends the current block.
            if (line.StartsWith('['))
            {
                Flush(locked, seen, ref name, ref version);
                inPackageBlock = line is "[[package]]";
                continue;
            }

            if (!inPackageBlock)
            {
                continue;
            }

            if (TryReadString(line, "name", out var nameValue))
            {
                name = nameValue;
            }
            else if (TryReadString(line, "version", out var versionValue))
            {
                version = versionValue;
            }
        }

        Flush(locked, seen, ref name, ref version);
        return locked;
    }

    private static void Flush(List<LockedPackage> locked, HashSet<(string, string)> seen, ref string? name, ref string? version)
    {
        if (name is not null && version is not null)
        {
            var canonical = PyPiName.Normalize(name);
            if (seen.Add((canonical, version)))
            {
                locked.Add(new LockedPackage(canonical, version));
            }
        }

        name = null;
        version = null;
    }

    // Reads a simple `key = "value"` TOML line.
    private static bool TryReadString(string line, string key, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line[key.Length..].TrimStart();
        if (!rest.StartsWith('=') || rest.Length < 4)
        {
            return false;
        }

        rest = rest[1..].Trim();
        if (rest.Length < 2 || rest[0] != '"')
        {
            return false;
        }

        var close = rest.IndexOf('"', 1);
        if (close < 0)
        {
            return false;
        }

        value = rest[1..close];
        return true;
    }
}
