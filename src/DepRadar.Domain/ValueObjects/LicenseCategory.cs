namespace DepRadar.Domain.ValueObjects;

/// <summary>
/// Risk classification of a license. Values are ordered by restrictiveness so a
/// shift to a higher category (e.g. Permissive → Unknown, the commercialization
/// signal) is a simple comparison.
/// </summary>
public enum LicenseCategory
{
    /// <summary>MIT, Apache-2.0, BSD, ISC, … — lowest obligation.</summary>
    Permissive = 0,

    /// <summary>LGPL, MPL, EPL — file/library-level copyleft.</summary>
    WeakCopyleft = 1,

    /// <summary>GPL, AGPL — strong copyleft with viral obligations.</summary>
    Copyleft = 2,

    /// <summary>No recognized SPDX OSS license — unclear or potentially commercial.</summary>
    Unknown = 3,
}
