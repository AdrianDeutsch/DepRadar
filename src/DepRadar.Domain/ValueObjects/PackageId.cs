using System.Text.RegularExpressions;

namespace DepRadar.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a NuGet package.
/// </summary>
/// <remarks>
/// NuGet package ids are compared case-insensitively, so the value is normalized
/// to lower-invariant for identity and persistence while the original casing is
/// preserved for display. Modeling this as a value object (instead of a raw
/// <see cref="string"/>) prevents mixing up package ids with arbitrary strings
/// and centralizes validation.
/// </remarks>
public readonly partial record struct PackageId
{
    // NuGet id grammar: alphanumeric segments separated by '.', '-' or '_'.
    private static readonly Regex IdPattern = NuGetIdRegex();

    private PackageId(string value, string original)
    {
        Value = value;
        Original = original;
    }

    /// <summary>The normalized (lower-invariant) id used for equality and storage.</summary>
    public string Value { get; }

    /// <summary>The original casing as supplied, kept for display purposes.</summary>
    public string Original { get; }

    /// <summary>
    /// Creates a validated <see cref="PackageId"/> from a raw package id.
    /// </summary>
    /// <param name="raw">The package id, e.g. <c>"Newtonsoft.Json"</c>.</param>
    /// <exception cref="ArgumentException">The id is empty or not a valid NuGet id.</exception>
    public static PackageId Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Package id must not be empty.", nameof(raw));
        }

        var trimmed = raw.Trim();
        if (!IdPattern.IsMatch(trimmed))
        {
            throw new ArgumentException($"'{raw}' is not a valid NuGet package id.", nameof(raw));
        }

        return new PackageId(trimmed.ToLowerInvariant(), trimmed);
    }

    /// <summary>Rehydrates an id from already-normalized storage without re-validating.</summary>
    public static PackageId FromNormalized(string normalized) => new(normalized, normalized);

    public bool Equals(PackageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Original;

    [GeneratedRegex(@"^\w+([_.-]\w+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NuGetIdRegex();
}
