using System.Globalization;
using System.Text.RegularExpressions;

namespace DepRadar.Domain.ValueObjects;

/// <summary>
/// A Semantic Versioning 2.0.0 version with correct precedence ordering.
/// </summary>
/// <remarks>
/// Deliberately hand-rolled to keep the Domain free of external dependencies
/// (Briefing constraint). For the full NuGet floating/range grammar the
/// production path is <c>NuGet.Versioning</c>; that trade-off is recorded in
/// <c>docs/adr/0003-handrolled-semver.md</c>. Build metadata is parsed but,
/// per the spec, ignored for both equality and precedence.
/// </remarks>
public sealed partial class SemVer : IEquatable<SemVer>, IComparable<SemVer>
{
    private static readonly Regex Pattern = SemVerRegex();
    private readonly string[] _prereleaseIdentifiers;

    private SemVer(int major, int minor, int patch, string? prerelease, string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        BuildMetadata = buildMetadata;
        _prereleaseIdentifiers = prerelease is null
            ? []
            : prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>The prerelease label without the leading '-', or <see langword="null"/> for a stable release.</summary>
    public string? Prerelease { get; }

    /// <summary>The build metadata without the leading '+'. Ignored for precedence.</summary>
    public string? BuildMetadata { get; }

    /// <summary><see langword="true"/> when this is a stable (non-prerelease) version.</summary>
    public bool IsStable => Prerelease is null;

    /// <summary>Parses a semantic version, throwing on malformed input.</summary>
    /// <exception cref="FormatException">The value is not a valid semantic version.</exception>
    public static SemVer Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a valid semantic version.");

    /// <summary>Attempts to parse a semantic version.</summary>
    public static bool TryParse(string? value, out SemVer version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Pattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        version = new SemVer(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null,
            match.Groups["buildmetadata"].Success ? match.Groups["buildmetadata"].Value : null);
        return true;
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        if (core != 0)
        {
            return core;
        }

        core = Minor.CompareTo(other.Minor);
        if (core != 0)
        {
            return core;
        }

        core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        // A version WITH a prerelease has lower precedence than one without.
        if (_prereleaseIdentifiers.Length == 0 && other._prereleaseIdentifiers.Length == 0)
        {
            return 0;
        }

        if (_prereleaseIdentifiers.Length == 0)
        {
            return 1;
        }

        if (other._prereleaseIdentifiers.Length == 0)
        {
            return -1;
        }

        return ComparePrerelease(_prereleaseIdentifiers, other._prereleaseIdentifiers);
    }

    public bool Equals(SemVer? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as SemVer);

    public override int GetHashCode() =>
        HashCode.Combine(Major, Minor, Patch, Prerelease is null ? 0 : string.GetHashCode(Prerelease, StringComparison.Ordinal));

    /// <summary>Returns the canonical version string (build metadata included).</summary>
    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        if (Prerelease is not null)
        {
            core += $"-{Prerelease}";
        }

        return BuildMetadata is null ? core : $"{core}+{BuildMetadata}";
    }

    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;

    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    public static bool operator ==(SemVer? left, SemVer? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(SemVer? left, SemVer? right) => !(left == right);

    /// <summary>
    /// Compares two non-empty prerelease identifier lists per SemVer rule 11:
    /// numeric identifiers rank lower than alphanumeric ones; a smaller set of
    /// fields ranks lower when all preceding fields are equal.
    /// </summary>
    private static int ComparePrerelease(string[] left, string[] right)
    {
        var shared = Math.Min(left.Length, right.Length);
        for (var i = 0; i < shared; i++)
        {
            var leftIsNumeric = int.TryParse(left[i], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumeric = int.TryParse(right[i], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

            var result = (leftIsNumeric, rightIsNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1, // numeric < alphanumeric
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i]),
            };

            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    // Official SemVer 2.0.0 regular expression (semver.org), with culture-invariant matching.
    [GeneratedRegex(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemVerRegex();
}
