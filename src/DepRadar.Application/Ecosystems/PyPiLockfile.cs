namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a Python lockfile — <c>poetry.lock</c> or <c>uv.lock</c> — into the exact
/// installed packages. Pure; the shared TOML block reading lives in
/// <see cref="TomlLockfile"/>.
/// </summary>
public static class PyPiLockfile
{
    /// <summary>Reads the distinct locked (name, version) pairs, names PEP 503 canonicalized.</summary>
    public static IReadOnlyList<LockedPackage> Parse(string content)
    {
        var locked = new List<LockedPackage>();
        var seen = new HashSet<(string, string)>();

        foreach (var entry in TomlLockfile.Parse(content))
        {
            var canonical = PyPiName.Normalize(entry.Name);
            if (seen.Add((canonical, entry.Version)))
            {
                locked.Add(new LockedPackage(canonical, entry.Version));
            }
        }

        return locked;
    }
}
