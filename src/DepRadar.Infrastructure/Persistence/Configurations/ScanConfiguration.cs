using DepRadar.Domain.Packages;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Scan"/> aggregate root.</summary>
internal sealed class ScanConfiguration : IEntityTypeConfiguration<Scan>
{
    public void Configure(EntityTypeBuilder<Scan> builder)
    {
        builder.ToTable("scans");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion<ScanIdValueConverter>();

        builder.Property(s => s.RootPackageId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        // Stored as text for readable rows and forward-compatible enum evolution.
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(s => s.RequestedAt);
        builder.Property(s => s.StartedAt);
        builder.Property(s => s.CompletedAt);
        builder.Property(s => s.PackagesDiscovered);
        builder.Property(s => s.EdgesWritten);
        builder.Property(s => s.Error).HasMaxLength(2048);

        // Supports the worker's "next queued scans" poll.
        builder.HasIndex(s => new { s.Status, s.RequestedAt });
    }
}
