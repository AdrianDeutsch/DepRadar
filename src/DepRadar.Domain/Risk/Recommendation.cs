namespace DepRadar.Domain.Risk;

/// <summary>The bottom-line recommendation for an upgrade.</summary>
public enum Recommendation
{
    /// <summary>The target version looks healthy; upgrading is low risk.</summary>
    Proceed,

    /// <summary>Upgrade with care — the target carries notable risk or regresses health.</summary>
    Caution,

    /// <summary>Avoid this target version — it carries critical risk.</summary>
    Avoid,
}
