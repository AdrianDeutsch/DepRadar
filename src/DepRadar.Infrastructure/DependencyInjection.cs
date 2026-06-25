using System.Net;
using DepRadar.Application.Abstractions;
using DepRadar.Infrastructure.Ai;
using DepRadar.Infrastructure.External.DepsDev;
using DepRadar.Infrastructure.External.GitHub;
using DepRadar.Infrastructure.External.NuGet;
using DepRadar.Infrastructure.External.Osv;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

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
    private const string DefaultAnthropicModel = "claude-sonnet-4-6";
    private const string UserAgent = "DepRadar/0.4 (+https://github.com/AdrianDeutsch/DepRadar)";

    /// <summary>Registers persistence adapters and the resilient external API clients.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="depsDevBaseUrl">Base URL of the deps.dev API (overridable for tests).</param>
    /// <param name="nuGetBaseUrl">Base URL of the NuGet V3 API (overridable for tests).</param>
    /// <param name="osvBaseUrl">Base URL of the OSV.dev API (overridable for tests).</param>
    /// <param name="anthropicApiKey">Anthropic API key; when set, the live Claude advisor is used.</param>
    /// <param name="anthropicModel">Anthropic model id (defaults to a current Claude model).</param>
    /// <param name="gitHubToken">GitHub token (optional) to raise the repo-health API rate limit.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? depsDevBaseUrl = null,
        string? nuGetBaseUrl = null,
        string? osvBaseUrl = null,
        string? anthropicApiKey = null,
        string? anthropicModel = null,
        string? gitHubToken = null)
    {
        // Caches external API responses (NuGet/OSV/deps.dev) so repeated scans don't
        // burn quota; an idempotent re-scan hits the cache, not the network.
        services.AddHybridCache();

        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IScanRepository, ScanRepository>();
        services.AddScoped<IGraphRepository, GraphRepository>();
        services.AddScoped<IRiskRepository, RiskRepository>();
        services.AddScoped<IChangelogRepository, ChangelogRepository>();
        services.AddScoped<IChangelogIndexer, ChangelogIndexer>();
        services.AddScoped<IRepositoryHealthEnricher, RepositoryHealthEnricher>();
        services.AddScoped<IDependencyGraphResolver, DependencyGraphResolver>();
        services.AddSingleton<IEmbeddingGenerator, HashingEmbeddingGenerator>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DepRadarDbContext>());

        AddLanguageModel(services, anthropicApiKey, anthropicModel);

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

        services.AddHttpClient<IRepositoryHealthSource, GitHubRepositoryHealthSource>(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                if (!string.IsNullOrWhiteSpace(gitHubToken))
                {
                    client.DefaultRequestHeaders.Authorization = new("Bearer", gitHubToken);
                }
            })
            .AddStandardResilienceHandler();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="DepRadarDbContext"/> against PostgreSQL with the
    /// pgvector type mapping enabled. Used by the hosts; the connection string is
    /// supplied by Aspire. (Tests register the context themselves.)
    /// </summary>
    public static IServiceCollection AddDepRadarDbContext(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<DepRadarDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.EnableRetryOnFailure();
            }));

        return services;
    }

    // Wires Claude when an API key is present; otherwise a null model so the upgrade
    // advisor falls back to a deterministic templated narrative (works keyless).
    private static void AddLanguageModel(IServiceCollection services, string? apiKey, string? model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<ILanguageModel, NullLanguageModel>();
            return;
        }

        services.AddSingleton(new AnthropicOptions(string.IsNullOrWhiteSpace(model) ? DefaultAnthropicModel : model));
        services.AddHttpClient<ILanguageModel, AnthropicLanguageModel>(client =>
            {
                client.BaseAddress = new Uri("https://api.anthropic.com/");
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            })
            .AddStandardResilienceHandler();
    }
}
