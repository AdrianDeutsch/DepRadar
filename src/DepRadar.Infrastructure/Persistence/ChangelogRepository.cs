using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IChangelogRepository"/> backed by pgvector.
/// Similarity search uses the cosine-distance operator (<c>&lt;=&gt;</c>) on the
/// vector column, evaluated in PostgreSQL.
/// </summary>
internal sealed class ChangelogRepository(DepRadarDbContext dbContext) : IChangelogRepository
{
    /// <inheritdoc />
    public async Task UpsertChunksAsync(IReadOnlyCollection<ChangelogChunk> chunks, CancellationToken cancellationToken)
    {
        var packageIds = chunks.Select(c => c.PackageId).Distinct().ToList();

        var existing = (await dbContext.ChangelogChunks
            .AsNoTracking()
            .Where(c => packageIds.Contains(c.PackageId))
            .Select(c => new { c.PackageId, c.Version, c.Ordinal })
            .ToListAsync(cancellationToken))
            .Select(c => (c.PackageId.Value, c.Version.ToString(), c.Ordinal))
            .ToHashSet();

        foreach (var chunk in chunks)
        {
            if (existing.Add((chunk.PackageId.Value, chunk.Version.ToString(), chunk.Ordinal)))
            {
                await dbContext.ChangelogChunks.AddAsync(chunk, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChangelogChunk>> SearchAsync(PackageId package, float[] queryEmbedding, int limit, CancellationToken cancellationToken)
    {
        var packageValue = package.Value;
        var query = new Vector(queryEmbedding);

        return await dbContext.ChangelogChunks
            .FromSql($"""
                SELECT * FROM depradar."changelog_chunks"
                WHERE "PackageId" = {packageValue}
                ORDER BY "Embedding" <=> {query}
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
