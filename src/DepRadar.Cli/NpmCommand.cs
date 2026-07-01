using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>npm</c> command: scans an npm package (exact version or range) or a whole
/// <c>package.json</c> for security and maintenance risk — a thin ecosystem spec over
/// the shared <see cref="EcosystemCommand"/>.
/// </summary>
internal static class NpmCommand
{
    /// <summary>The usage banner for <c>npm</c>.</summary>
    public const string Usage = "Usage: depradar npm <package | package.json> [version|range] [--fail-on <none|low|medium|high|critical>] [--json] [--sbom <path>] [--sarif <path>]";

    private static readonly EcosystemCli Cli = new(
        RegistryLabel: "the npm registry",
        ParseManifest: NpmManifest.ParseDependencies,
        ResolveScanner: provider => provider.GetRequiredService<INpmScanner>().ScanAsync);

    /// <summary>Runs <c>npm</c> with the arguments after the verb.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken) =>
        EcosystemCommand.RunAsync(Cli, Usage, args, cancellationToken);
}
