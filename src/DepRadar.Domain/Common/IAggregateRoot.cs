namespace DepRadar.Domain.Common;

/// <summary>
/// Marker interface for aggregate roots. Repositories are only ever exposed for
/// aggregate roots, never for entities living inside an aggregate boundary.
/// Architecture tests rely on this marker to verify persistence boundaries.
/// </summary>
public interface IAggregateRoot;
