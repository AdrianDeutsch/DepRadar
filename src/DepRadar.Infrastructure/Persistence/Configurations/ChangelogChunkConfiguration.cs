using DepRadar.Domain.Packages;
using DepRadar.Infrastructure.Ai;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="ChangelogChunk"/>, including the pgvector column.</summary>
internal sealed class ChangelogChunkConfiguration : IEntityTypeConfiguration<ChangelogChunk>
{
    public void Configure(EntityTypeBuilder<ChangelogChunk> builder)
    {
        builder.ToTable("changelog_chunks");

        builder.HasKey(c => new { c.PackageId, c.Version, c.Ordinal });

        builder.Property(c => c.PackageId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(c => c.Version)
            .HasConversion<SemVerValueConverter>()
            .HasMaxLength(256);

        builder.Property(c => c.Ordinal);
        builder.Property(c => c.Text).HasMaxLength(8000);

        builder.Property(c => c.Embedding)
            .HasConversion<EmbeddingVectorConverter>()
            .HasColumnType($"vector({HashingEmbeddingGenerator.Dimensions})");
    }
}
