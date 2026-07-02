using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>go</c> command: scans a Go module (exact version or latest), a <c>go.mod</c>,
/// or a <c>go.sum</c> for security and maintenance risk — a thin ecosystem spec over
/// the shared <see cref="EcosystemCommand"/>.
/// </summary>
internal static class GoCommand
{
    /// <summary>The usage banner for <c>go</c>.</summary>
    public const string Usage = "Usage: depradar go <module | go.mod | go.sum> [version] [--fail-on <none|low|medium|high|critical>] [--policy <file>] [--json] [--sbom <path>] [--sarif <path>]";

    private static readonly EcosystemCli Cli = new(
        RegistryLabel: "the Go module proxy",
        ParseManifest: GoMod.ParseRequires,
        ResolveScanner: provider => provider.GetRequiredService<IGoScanner>().ScanAsync,
        IsLockfile: fileName => fileName.Equals("go.sum", StringComparison.OrdinalIgnoreCase),
        ParseLockfile: GoSum.Parse,
        ResolveLockScanner: provider => provider.GetRequiredService<IGoScanner>().ScanLockedAsync,
        FindLookalike: name => Lookalike.FindTarget(name.Trim().ToLowerInvariant(), KnownPackages.Go));

    /// <summary>Runs <c>go</c> with the arguments after the verb.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken) =>
        EcosystemCommand.RunAsync(Cli, Usage, args, cancellationToken);
}
