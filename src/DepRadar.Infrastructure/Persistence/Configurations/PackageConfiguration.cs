using DepRadar.Domain.Packages;
using DepRadar.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DepRadar.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Package"/> aggregate root.</summary>
internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("packages");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion<PackageIdValueConverter>()
            .HasMaxLength(256);

        builder.Property(p => p.Description)
            .HasMaxLength(4000);

        builder.Property(p => p.ProjectUrl)
            .HasMaxLength(2048);

        builder.Property(p => p.SourceRepositoryUrl)
            .HasMaxLength(2048);

        builder.Property(p => p.License)
            .HasConversion<SpdxLicenseValueConverter>()
            .HasMaxLength(256);

        builder.Property(p => p.LatestStableVersion)
            .HasConversion<SemVerValueConverter>()
            .HasMaxLength(256);

        builder.Property(p => p.IsDeprecated);
        builder.Property(p => p.FirstSeenAt);
        builder.Property(p => p.LastRefreshedAt);
    }
}
