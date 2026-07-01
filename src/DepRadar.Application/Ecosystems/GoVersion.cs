using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Normalizes a Go module version onto the Domain <see cref="SemVer"/>. Pure.
/// </summary>
/// <remarks>
/// Go versions are strict semver with a mandatory <c>v</c> prefix. Pseudo-versions
/// (<c>v0.0.0-20190101000000-abcdef123456</c>) parse as pre-releases and are therefore
/// naturally ranked below tagged releases; a <c>+incompatible</c> suffix is valid
/// build metadata. Callers keep the ORIGINAL string for proxy paths (the proxy is
/// exact about it) and use the parsed value for ordering.
/// </remarks>
public static class GoVersion
{
    /// <summary>Attempts to parse a Go version; <paramref name="version"/> is meaningful only when true.</summary>
    public static bool TryParse(string? value, out SemVer version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v'))
        {
            text = text[1..];
        }

        return SemVer.TryParse(text, out version);
    }
}
