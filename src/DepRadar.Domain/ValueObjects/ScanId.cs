namespace DepRadar.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a scan run, backed by a GUID. Prevents mixing scan
/// ids up with other identifiers and gives the API a stable handle to poll.
/// </summary>
public readonly record struct ScanId(Guid Value)
{
    /// <summary>Creates a brand-new, unique scan id.</summary>
    public static ScanId New() => new(Guid.NewGuid());

    /// <summary>Rehydrates a scan id from storage.</summary>
    public static ScanId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
