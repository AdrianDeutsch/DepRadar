using DepRadar.Application;
using DepRadar.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DepRadar.Cli;

/// <summary>
/// Builds the DI container for a standalone run: application + infrastructure
/// services without the EF Core DbContext, since the stateless analyzer never
/// touches the database. A GitHub token (env <c>GITHUB_TOKEN</c>) lifts the
/// repo-health rate limit.
/// </summary>
internal static class CliHost
{
    /// <summary>Creates a configured provider; callers own its lifetime.</summary>
    public static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddApplication();
        services.AddInfrastructure(gitHubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
        return services.BuildServiceProvider();
    }
}
