using System.Globalization;
using System.Text;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Projects;
using DepRadar.Application.Remediation;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Cli;

/// <summary>
/// The <c>fix</c> command: finds vulnerable <em>direct</em> dependencies in a project /
/// props file, bumps each to its minimal safe version, and either writes the file in
/// place or opens a pull request with the change.
/// </summary>
internal static class FixCommand
{
    /// <summary>The usage banner for <c>fix</c>.</summary>
    public const string Usage =
        "Usage: depradar fix <.csproj | Directory.Packages.props> [--open-pr] [--repo owner/name] [--base main] [--dry-run]";

    /// <summary>Runs <c>fix</c> with the arguments after the verb.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var openPr = args.Contains("--open-pr");
        var dryRun = args.Contains("--dry-run");
        var repo = OptionValue(args, "--repo") ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var baseBranch = OptionValue(args, "--base") ?? "main";
        var manifestPath = args.FirstOrDefault(arg => !arg.StartsWith('-'));

        if (manifestPath is null || !File.Exists(manifestPath))
        {
            await Console.Error.WriteLineAsync(manifestPath is null ? Usage : $"File not found: {manifestPath}");
            return ExitCodes.Usage;
        }

        var content = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        IReadOnlyList<ManifestReference> references;
        try
        {
            references = ProjectFileParser.ParseReferences(content);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"Could not parse '{manifestPath}': {exception.Message}");
            return ExitCodes.Usage;
        }

        await using var provider = CliHost.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var vulnerabilities = scope.ServiceProvider.GetRequiredService<IVulnerabilitySource>();

        var bumps = await ResolveBumpsAsync(references, vulnerabilities, cancellationToken);
        if (bumps.Count == 0)
        {
            Console.WriteLine("No vulnerable direct dependencies with a known fix. Nothing to do.");
            return ExitCodes.Ok;
        }

        var patch = ManifestPatcher.Apply(content, bumps);
        if (patch.Applied.Count == 0)
        {
            await Console.Error.WriteLineAsync($"Found fixes but could not locate the versions to patch in '{manifestPath}'.");
            return ExitCodes.Usage;
        }

        Console.WriteLine($"Fixable vulnerable dependencies in {manifestPath}:");
        foreach (var bump in patch.Applied)
        {
            Console.WriteLine($"  {bump.Package}: {bump.FromVersion} -> {bump.ToVersion}");
        }

        if (dryRun)
        {
            return ExitCodes.Ok;
        }

        if (openPr)
        {
            return await OpenPullRequestAsync(scope.ServiceProvider, repo, baseBranch, manifestPath, patch, cancellationToken);
        }

        await File.WriteAllTextAsync(manifestPath, patch.Content, cancellationToken);
        Console.WriteLine($"Patched {manifestPath} in place.");
        return ExitCodes.Ok;
    }

    private static async Task<Dictionary<string, string>> ResolveBumpsAsync(
        IReadOnlyList<ManifestReference> references,
        IVulnerabilitySource vulnerabilities,
        CancellationToken cancellationToken)
    {
        var bumps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            if (!SemVer.TryParse(reference.Version, out var version))
            {
                continue;
            }

            var package = PackageId.Create(reference.Id);
            var advisories = await vulnerabilities.GetAsync(package, version, cancellationToken);
            if (advisories.Count == 0)
            {
                continue;
            }

            var fixedVersions = new List<string?>();
            foreach (var advisory in advisories)
            {
                fixedVersions.Add(await vulnerabilities.GetFixedVersionAsync(advisory.AdvisoryId, package, version, cancellationToken));
            }

            if (RemediationCalculator.SafeVersion(fixedVersions) is { } safe)
            {
                bumps[reference.Id] = safe;
            }
        }

        return bumps;
    }

    private static async Task<int> OpenPullRequestAsync(
        IServiceProvider services,
        string? repo,
        string baseBranch,
        string manifestPath,
        ManifestPatch patch,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repo))
        {
            await Console.Error.WriteLineAsync("--open-pr needs a repository: pass --repo owner/name or set GITHUB_REPOSITORY.");
            return ExitCodes.Usage;
        }

        var opener = services.GetRequiredService<IPullRequestOpener>();
        var branch = string.Create(CultureInfo.InvariantCulture, $"depradar/fix-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        var (title, body) = Describe(patch.Applied);

        var url = await opener.OpenAsync(
            new PullRequestRequest(repo, baseBranch, branch, manifestPath, patch.Content, title, body),
            cancellationToken);

        if (url is null)
        {
            await Console.Error.WriteLineAsync("No GitHub token configured — set GITHUB_TOKEN to open a pull request.");
            return ExitCodes.Usage;
        }

        Console.WriteLine($"Opened pull request: {url}");
        return ExitCodes.Ok;
    }

    private static (string Title, string Body) Describe(IReadOnlyList<PackageBump> bumps)
    {
        var title = string.Create(
            CultureInfo.InvariantCulture,
            $"DepRadar: upgrade {bumps.Count} vulnerable dependenc{(bumps.Count == 1 ? "y" : "ies")}");

        var body = new StringBuilder();
        body.Append("DepRadar found and fixed ").Append(bumps.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" vulnerable direct dependenc(y/ies):").AppendLine();
        foreach (var bump in bumps)
        {
            body.Append("- **").Append(bump.Package).Append("** ").Append(bump.FromVersion).Append(" → ").AppendLine(bump.ToVersion);
        }

        body.AppendLine().AppendLine("_Opened automatically by DepRadar._");
        return (title, body.ToString());
    }

    private static string? OptionValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
