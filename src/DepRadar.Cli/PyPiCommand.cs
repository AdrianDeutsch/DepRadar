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
    public const string Usage = "Usage: depradar pypi <package | requirements.txt | poetry.lock | uv.lock> [version|specifier] [--fail-on <none|low|medium|high|critical>] [--policy <file>] [--json] [--sbom <path>] [--sarif <path>]";

    private static readonly EcosystemCli Cli = new(
        RegistryLabel: "PyPI",
        ParseManifest: RequirementsFile.Parse,
        ResolveScanner: provider => provider.GetRequiredService<IPyPiScanner>().ScanAsync,
        IsLockfile: fileName => fileName.Equals("poetry.lock", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("uv.lock", StringComparison.OrdinalIgnoreCase),
        ParseLockfile: PyPiLockfile.Parse,
        ResolveLockScanner: provider => provider.GetRequiredService<IPyPiScanner>().ScanLockedAsync,
        FindLookalike: name => Lookalike.FindTarget(PyPiName.Normalize(name), KnownPackages.PyPi));

    /// <summary>Runs <c>pypi</c> with the arguments after the verb.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken) =>
        EcosystemCommand.RunAsync(Cli, Usage, args, cancellationToken);
}
