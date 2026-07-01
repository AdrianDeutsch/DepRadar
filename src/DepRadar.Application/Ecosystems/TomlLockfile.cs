namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Reads the repeated <c>[[package]]</c> blocks (with <c>name</c>/<c>version</c> string
/// keys) that <c>poetry.lock</c>, <c>uv.lock</c> and <c>Cargo.lock</c> all share. Pure.
/// </summary>
/// <remarks>
/// The files are machine-generated with a stable layout, so a line-based reader keeps
/// the Application layer dependency-free (no TOML package) — the same trade-off as the
/// hand-rolled SemVer (ADR 0003). Ecosystem-specific name canonicalization is applied
/// by the callers (<see cref="PyPiLockfile"/>, <see cref="CargoLockfile"/>).
/// </remarks>
public static class TomlLockfile
{
    /// <summary>Reads the raw (name, version) pairs of every <c>[[package]]</c> block.</summary>
    public static IReadOnlyList<LockedPackage> Parse(string content)
    {
        var locked = new List<LockedPackage>();

        string? name = null;
        string? version = null;
        var inPackageBlock = false;

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();

            // A new TOML table/array-of-tables header ends the current block.
            if (line.StartsWith('['))
            {
                Flush(locked, ref name, ref version);
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

        Flush(locked, ref name, ref version);
        return locked;
    }

    private static void Flush(List<LockedPackage> locked, ref string? name, ref string? version)
    {
        if (name is not null && version is not null)
        {
            locked.Add(new LockedPackage(name, version));
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
