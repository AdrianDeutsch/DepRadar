using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the model (including the pgvector
/// mapping) without running a host. The connection string is a placeholder — EF never
/// connects at design time for scaffolding.
/// </summary>
internal sealed class DepRadarDbContextFactory : IDesignTimeDbContextFactory<DepRadarDbContext>
{
    public DepRadarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DepRadarDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=depradar_design;Username=postgres;Password=postgres",
                npgsql => npgsql.UseVector())
            .Options;

        return new DepRadarDbContext(options);
    }
}
