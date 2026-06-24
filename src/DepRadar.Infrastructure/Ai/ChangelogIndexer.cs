using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.Ai;

/// <summary>
/// Builds the RAG corpus for a package by synthesizing a concise note per known
/// version from DepRadar's own signals (license, deprecation, advisories) and
/// embedding it. When real changelog/release text is available (e.g. from GitHub),
/// it can be added behind the same interface.
/// </summary>
internal sealed class ChangelogIndexer(
    IPackageRepository packageRepository,
    IRiskRepository riskRepository,
    IEmbeddingGenerator embeddingGenerator,
    IChangelogRepository changelogRepository)
    : IChangelogIndexer
{
    /// <inheritdoc />
    public async Task IndexAsync(PackageId package, CancellationToken cancellationToken)
    {
        var versions = await packageRepository.GetVersionsAsync(package, cancellationToken);
        if (versions.Count == 0)
        {
            return;
        }

        var targets = versions.Select(version => (package, version.Version)).ToList();
        var inputs = await riskRepository.GetRiskInputsAsync(targets, cancellationToken);

        var chunks = new List<ChangelogChunk>(inputs.Count);
        foreach (var input in inputs)
        {
            var text = Synthesize(package, input);
            var embedding = await embeddingGenerator.EmbedAsync(text, cancellationToken);
            chunks.Add(ChangelogChunk.Create(package, input.Version, ordinal: 0, text, embedding));
        }

        if (chunks.Count > 0)
        {
            await changelogRepository.UpsertChunksAsync(chunks, cancellationToken);
        }
    }

    private static string Synthesize(PackageId package, PackageRiskInput input)
    {
        var license = input.ResolvedLicense?.Identifier ?? "unknown license";
        var deprecation = input.IsDeprecated ? ", deprecated" : string.Empty;
        var advisories = input.Vulnerabilities.Count == 0
            ? "no known advisories"
            : "advisories: " + string.Join(", ", input.Vulnerabilities.Select(v => $"{v.AdvisoryId} ({v.Severity})"));

        return $"{package.Original} {input.Version}: {license}{deprecation}; {advisories}.";
    }
}
