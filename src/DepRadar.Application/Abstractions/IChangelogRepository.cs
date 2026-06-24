using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>Persistence port for changelog chunks and vector similarity search (pgvector).</summary>
public interface IChangelogRepository
{
    /// <summary>Idempotently stores chunks (keyed by package, version, ordinal).</summary>
    Task UpsertChunksAsync(IReadOnlyCollection<ChangelogChunk> chunks, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the <paramref name="limit"/> chunks of <paramref name="package"/> most
    /// similar to <paramref name="queryEmbedding"/> (cosine distance).
    /// </summary>
    Task<IReadOnlyList<ChangelogChunk>> SearchAsync(PackageId package, float[] queryEmbedding, int limit, CancellationToken cancellationToken);
}
