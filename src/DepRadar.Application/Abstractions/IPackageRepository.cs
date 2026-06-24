using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Persistence port for the <see cref="Package"/> aggregate and its versions.
/// Writes are idempotent so repeated scans never create duplicates.
/// </summary>
public interface IPackageRepository
{
    /// <summary>Loads a package by id, or <see langword="null"/> if not stored yet.</summary>
    Task<Package?> GetAsync(PackageId id, CancellationToken cancellationToken);

    /// <summary>Loads the stored versions of a package (empty if none/unknown).</summary>
    Task<IReadOnlyList<PackageVersion>> GetVersionsAsync(PackageId id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates the package together with the given versions. Does not
    /// persist on its own — call <see cref="IUnitOfWork.SaveChangesAsync"/> to commit.
    /// </summary>
    Task UpsertAsync(Package package, IReadOnlyCollection<PackageVersion> versions, CancellationToken cancellationToken);
}
