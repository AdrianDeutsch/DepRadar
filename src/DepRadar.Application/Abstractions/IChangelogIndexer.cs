using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Builds and stores the changelog/RAG corpus for a package (synthesizing notes from
/// the package's known versions, license changes and advisories), embedding each chunk.
/// </summary>
public interface IChangelogIndexer
{
    /// <summary>Indexes the changelog corpus for the given package.</summary>
    Task IndexAsync(PackageId package, CancellationToken cancellationToken);
}
