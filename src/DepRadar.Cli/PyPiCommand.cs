using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>pypi</c> command: scans a PyPI package (exact version or PEP 440 specifier)
/// or a whole <c>requirements.txt</c> for security and maintenance risk — a thin
/// ecosystem spec over the shared <see cref="EcosystemCommand"/>.
/// </summary>
internal static class PyPiCommand
{
    /// <summary>The usage banner for <c>pypi</c>.</summary>
    public const string Usage = "Usage: depradar pypi <package | requirements.txt> [version|specifier] [--fail-on <none|low|medium|high|critical>] [--json] [--sbom <path>] [--sarif <path>]";

    private static readonly EcosystemCli Cli = new(
        RegistryLabel: "PyPI",
        ParseManifest: RequirementsFile.Parse,
        ResolveScanner: provider => provider.GetRequiredService<IPyPiScanner>().ScanAsync);

    /// <summary>Runs <c>pypi</c> with the arguments after the verb.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken) =>
        EcosystemCommand.RunAsync(Cli, Usage, args, cancellationToken);
}
