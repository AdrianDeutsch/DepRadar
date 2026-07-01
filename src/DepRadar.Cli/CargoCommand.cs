using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>cargo</c> command: scans a crates.io crate (exact version or Cargo
/// requirement), a <c>Cargo.toml</c>, or a <c>Cargo.lock</c> for security and
/// maintenance risk — a thin ecosystem spec over the shared <see cref="EcosystemCommand"/>.
/// </summary>
internal static class CargoCommand
{
    /// <summary>The usage banner for <c>cargo</c>.</summary>
    public const string Usage = "Usage: depradar cargo <crate | Cargo.toml | Cargo.lock> [version|req] [--fail-on <none|low|medium|high|critical>] [--json] [--sbom <path>] [--sarif <path>]";

    private static readonly EcosystemCli Cli = new(
        RegistryLabel: "crates.io",
        ParseManifest: CargoManifest.ParseDependencies,
        ResolveScanner: provider => provider.GetRequiredService<ICargoScanner>().ScanAsync,
        IsLockfile: fileName => fileName.Equals("Cargo.lock", StringComparison.OrdinalIgnoreCase),
        ParseLockfile: CargoLockfile.Parse,
        ResolveLockScanner: provider => provider.GetRequiredService<ICargoScanner>().ScanLockedAsync,
        FindLookalike: name => Lookalike.FindTarget(name.Trim().ToLowerInvariant(), KnownPackages.Cargo));

    /// <summary>Runs <c>cargo</c> with the arguments after the verb.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken) =>
        EcosystemCommand.RunAsync(Cli, Usage, args, cancellationToken);
}
