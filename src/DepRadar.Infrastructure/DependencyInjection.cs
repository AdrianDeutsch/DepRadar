using System.Net;
using DepRadar.Application.Abstractions;
using DepRadar.Infrastructure.External.DepsDev;
using DepRadar.Infrastructure.External.NuGet;
using DepRadar.Infrastructure.External.Osv;
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
    private const string DefaultNuGetBaseUrl = "https://api.nuget.org/";
    private const string DefaultOsvBaseUrl = "https://api.osv.dev/";
    private const string UserAgent = "DepRadar/0.3 (+https://github.com/AdrianDeutsch/DepRadar)";

    /// <summary>Registers persistence adapters and the resilient external API clients.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="depsDevBaseUrl">Base URL of the deps.dev API (overridable for tests).</param>
    /// <param name="nuGetBaseUrl">Base URL of the NuGet V3 API (overridable for tests).</param>
    /// <param name="osvBaseUrl">Base URL of the OSV.dev API (overridable for tests).</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? depsDevBaseUrl = null,
        string? nuGetBaseUrl = null,
        string? osvBaseUrl = null)
    {
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IScanRepository, ScanRepository>();
        services.AddScoped<IGraphRepository, GraphRepository>();
        services.AddScoped<IRiskRepository, RiskRepository>();
        services.AddScoped<IDependencyGraphResolver, DependencyGraphResolver>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DepRadarDbContext>());

        services.AddHttpClient<IPackageMetadataSource, DepsDevPackageMetadataSource>(client =>
            {
                client.BaseAddress = new Uri(depsDevBaseUrl ?? DefaultDepsDevBaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            // Bundled retry + circuit breaker + total/attempt timeout + rate limiter.
            // Every external call goes through this — no naive HttpClient usage.
            .AddStandardResilienceHandler();

        services.AddHttpClient<NuGetClient>(client =>
            {
                client.BaseAddress = new Uri(nuGetBaseUrl ?? DefaultNuGetBaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            // The registration API is gzip-only (registration5-gz-semver2).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IVulnerabilitySource, OsvVulnerabilitySource>(client =>
            {
                client.BaseAddress = new Uri(osvBaseUrl ?? DefaultOsvBaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .AddStandardResilienceHandler();

        return services;
    }
}
