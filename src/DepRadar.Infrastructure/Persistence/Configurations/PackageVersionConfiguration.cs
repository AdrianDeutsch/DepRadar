using DepRadar.Domain.Packages;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="PackageVersion"/>. Identified by the composite
/// (package, version) key; modeled as its own table, not a child collection.
/// </summary>
internal sealed class PackageVersionConfiguration : IEntityTypeConfiguration<PackageVersion>
{
    public void Configure(EntityTypeBuilder<PackageVersion> builder)
    {
        builder.ToTable("package_versions");

        builder.HasKey(v => new { v.PackageId, v.Version });

        builder.Property(v => v.PackageId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(v => v.Version)
            .HasConversion<SemVerValueConverter>()
            .HasMaxLength(256);

        builder.Property(v => v.PublishedAt);
        builder.Property(v => v.IsDeprecated);

        builder.Property(v => v.License)
            .HasConversion<SpdxLicenseValueConverter>()
            .HasMaxLength(256);

        // Supports "all versions of package X" lookups.
        builder.HasIndex(v => v.PackageId);
    }
}
