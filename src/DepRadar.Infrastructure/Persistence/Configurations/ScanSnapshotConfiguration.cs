using DepRadar.Domain.History;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ScanSnapshot"/>. An append-only history row; the
/// per-package states ride along as a single JSON column, indexed by (root, time) so
/// the two most recent snapshots are a cheap read.
/// </summary>
internal sealed class ScanSnapshotConfiguration : IEntityTypeConfiguration<ScanSnapshot>
{
    public void Configure(EntityTypeBuilder<ScanSnapshot> builder)
    {
        builder.ToTable("scan_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RootPackageId)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(s => s.OverallLevel)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Immutable, append-only: the states serialize to one JSON document rather
        // than a child table. A value comparer keeps EF's change tracking happy.
        var comparer = new ValueComparer<IReadOnlyList<PackageRiskState>>(
            (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
            states => states.Aggregate(0, (hash, state) => HashCode.Combine(hash, state.Package, state.Version, state.Score)),
            states => states.ToList());

        builder.Property(s => s.Packages)
            .HasConversion(new PackageRiskStatesConverter(), comparer)
            .HasColumnType("jsonb")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.RootPackageId, s.CreatedAt });
    }
}
