namespace DepRadar.Domain.Risk;

/// <summary>The dimension of dependency health a finding belongs to.</summary>
public enum RiskCategory
{
    /// <summary>Known security vulnerability (CVE/GHSA).</summary>
    Security,

    /// <summary>License obligations (copyleft, unknown/non-OSI license).</summary>
    License,

    /// <summary>The license changed between versions (incl. the OSS→commercial pivot).</summary>
    LicenseShift,

    /// <summary>Maintenance health (deprecated, abandoned).</summary>
    Maintenance,
}
