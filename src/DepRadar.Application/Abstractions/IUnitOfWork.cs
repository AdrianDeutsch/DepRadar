namespace DepRadar.Application.Abstractions;

/// <summary>
/// Transactional boundary. Handlers mutate aggregates through repositories, then
/// commit once via this port — keeping the persistence technology out of the
/// application layer.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Commits all pending changes and returns the number of state entries written.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
