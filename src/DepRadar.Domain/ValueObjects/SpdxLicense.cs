using System.Collections.Frozen;

namespace DepRadar.Domain.ValueObjects;

/// <summary>
/// An SPDX license identifier (e.g. <c>MIT</c>, <c>Apache-2.0</c>) or expression.
/// </summary>
/// <remarks>
/// Slice 1 only captures and normalizes the identifier. The license-shift and
/// copyleft/commercial risk classification (the "MediatR case") is layered on in
/// Slice 3; <see cref="IsRecognized"/> already flags ids outside the curated set
/// so unknown or custom licenses surface instead of being silently trusted.
/// </remarks>
public readonly record struct SpdxLicense
{
    // A small curated subset of the SPDX list — enough to recognize the licenses
    // that dominate the .NET/OSS ecosystem. The full list is loaded in Slice 3.
    private static readonly FrozenSet<string> KnownIdentifiers = new[]
    {
        "MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC",
        "GPL-2.0-only", "GPL-3.0-only", "LGPL-2.1-only", "LGPL-3.0-only",
        "MPL-2.0", "MS-PL", "Unlicense", "0BSD",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private SpdxLicense(string identifier, bool isRecognized)
    {
        Identifier = identifier;
        IsRecognized = isRecognized;
    }

    /// <summary>The SPDX identifier or expression as supplied (trimmed).</summary>
    public string Identifier { get; }

    /// <summary><see langword="true"/> when the identifier matches the curated SPDX subset.</summary>
    public bool IsRecognized { get; }

    /// <summary>Creates a license value from an SPDX identifier or expression.</summary>
    /// <exception cref="ArgumentException">The identifier is empty.</exception>
    public static SpdxLicense Create(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("SPDX identifier must not be empty.", nameof(identifier));
        }

        var trimmed = identifier.Trim();
        return new SpdxLicense(trimmed, KnownIdentifiers.Contains(trimmed));
    }

    public override string ToString() => Identifier;
}
