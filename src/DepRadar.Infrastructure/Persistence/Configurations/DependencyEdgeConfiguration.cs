using DepRadar.Domain.Packages;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="DependencyEdge"/>, the flat representation of the
/// dependency graph. The composite key is the (dependent, version, dependency)
/// triple; an index on the dependency id supports reverse ("who depends on X?")
/// traversal in Slice 2.
/// </summary>
internal sealed class DependencyEdgeConfiguration : IEntityTypeConfiguration<DependencyEdge>
{
    public void Configure(EntityTypeBuilder<DependencyEdge> builder)
    {
        builder.ToTable("dependency_edges");

        builder.HasKey(e => new { e.DependentId, e.DependentVersion, e.DependencyId });

        builder.Property(e => e.DependentId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(e => e.DependentVersion)
            .HasConversion<SemVerValueConverter>()
            .HasMaxLength(256);

        builder.Property(e => e.DependencyId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(e => e.DependencyVersion)
            .HasConversion<SemVerValueConverter>()
            .HasMaxLength(256);

        builder.Property(e => e.VersionRange)
            .HasMaxLength(256);

        builder.Property(e => e.IsDirect);

        builder.HasIndex(e => e.DependencyId);
    }
}
