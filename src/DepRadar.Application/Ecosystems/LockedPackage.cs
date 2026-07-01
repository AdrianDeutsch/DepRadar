namespace DepRadar.Application.Ecosystems;

/// <summary>
/// One entry of a lockfile: an exact installed (name, version) pair. Unlike a manifest
/// dependency there is no range to resolve — the lockfile records what is actually
/// installed, which makes it the most precise scan target.
/// </summary>
/// <param name="Name">The package name as written in the lockfile.</param>
/// <param name="Version">The exact pinned version string.</param>
public readonly record struct LockedPackage(string Name, string Version);
