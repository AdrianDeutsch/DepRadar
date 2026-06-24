using DepRadar.Application.Abstractions;
using DepRadar.Infrastructure.External.DepsDev;
using DepRadar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer.
/// </summary>
/// <remarks>
/// The <see cref="DepRadarDbContext"/> itself is registered by the host
/// (via the Aspire <c>AddNpgsqlDbContext</c> integration) or by the integration
/// test fixture, so the connection string, health checks and telemetry are owned
/// where the runtime lives. This method wires everything that depends on it.
/// </remarks>
public static class DependencyInjection
{
    private const string DefaultDepsDevBaseUrl = "https://api.deps.dev/";

    /// <summary>Registers persistence adapters and the resilient deps.dev metadata source.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="depsDevBaseUrl">Base URL of the deps.dev API (overridable for tests).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? depsDevBaseUrl = null)
    {
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DepRadarDbContext>());

        services.AddHttpClient<IPackageMetadataSource, DepsDevPackageMetadataSource>(client =>
            {
                client.BaseAddress = new Uri(depsDevBaseUrl ?? DefaultDepsDevBaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DepRadar/0.1 (+https://github.com/AdrianDeutsch/DepRadar)");
            })
            // Bundled retry + circuit breaker + total/attempt timeout + rate limiter.
            // Every external call goes through this — no naive HttpClient usage.
            .AddStandardResilienceHandler();

        return services;
    }
}
