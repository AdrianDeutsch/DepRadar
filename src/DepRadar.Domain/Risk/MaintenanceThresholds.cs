namespace DepRadar.Domain.Risk;

/// <summary>
/// Shared maintenance-scoring thresholds. Centralized so the persisted-scan path
/// (risk repository) and the stateless analysis path (CLI) classify staleness
/// identically.
/// </summary>
public static class MaintenanceThresholds
{
    /// <summary>A repository with no commits for this long is treated as stale/abandoned (~18 months).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(548);
}
