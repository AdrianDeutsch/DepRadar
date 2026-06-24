namespace DepRadar.Domain.Risk;

/// <summary>Severity of a risk finding, ordered so the maximum is the worst.</summary>
public enum RiskLevel
{
    /// <summary>No concern.</summary>
    None = 0,

    /// <summary>Minor concern.</summary>
    Low = 1,

    /// <summary>Notable concern.</summary>
    Medium = 2,

    /// <summary>Serious concern that should be addressed.</summary>
    High = 3,

    /// <summary>Critical concern that demands immediate action.</summary>
    Critical = 4,
}
