namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Parses a <c>Cargo.lock</c> into the exact installed crates. Pure; the shared TOML
/// block reading lives in <see cref="TomlLockfile"/> — Cargo.lock uses the very same
/// <c>[[package]]</c> shape as poetry.lock/uv.lock.
/// </summary>
public static class CargoLockfile
{
    /// <summary>Reads the distinct locked (name, version) pairs, names lower-cased.</summary>
    public static IReadOnlyList<LockedPackage> Parse(string content)
    {
        var locked = new List<LockedPackage>();
        var seen = new HashSet<(string, string)>();

        foreach (var entry in TomlLockfile.Parse(content))
        {
            var name = entry.Name.Trim().ToLowerInvariant();
            if (seen.Add((name, entry.Version)))
            {
                locked.Add(new LockedPackage(name, entry.Version));
            }
        }

        return locked;
    }
}
