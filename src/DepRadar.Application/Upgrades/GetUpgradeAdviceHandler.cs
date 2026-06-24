using DepRadar.Application.Abstractions;
using DepRadar.Application.Llm;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Upgrades;

/// <summary>
/// Handles <see cref="GetUpgradeAdviceQuery"/>: retrieves relevant changelog chunks
/// (RAG over pgvector), scores the from/to versions, builds a prompt-injection-shielded
/// prompt, and asks the language model — falling back to a deterministic templated
/// narrative when no model is configured. The recommendation itself is always
/// deterministic (never delegated to the LLM).
/// </summary>
public sealed class GetUpgradeAdviceHandler(
    IPackageRepository packageRepository,
    IRiskRepository riskRepository,
    IChangelogRepository changelogRepository,
    IEmbeddingGenerator embeddingGenerator,
    ILanguageModel languageModel)
    : IRequestHandler<GetUpgradeAdviceQuery, UpgradeAdviceDto?>
{
    private const int RetrievedChunks = 5;
    private static readonly RiskAssessment HealthyAssessment = new(HealthScore.FromFindings([]), []);

    /// <inheritdoc />
    public async Task<UpgradeAdviceDto?> Handle(GetUpgradeAdviceQuery request, CancellationToken cancellationToken)
    {
        var packageId = PackageId.Create(request.PackageId);

        var versions = await packageRepository.GetVersionsAsync(packageId, cancellationToken);
        if (versions.Count == 0)
        {
            return null;
        }

        var fromVersion = Pick(request.From, versions.Min(v => v.Version)!);
        var toVersion = Pick(request.To, versions.Max(v => v.Version)!);

        var (fromAssessment, toAssessment) = await AssessAsync(packageId, fromVersion, toVersion, cancellationToken);
        var recommendation = UpgradeRecommender.Recommend(fromAssessment, toAssessment);

        var excerpts = await RetrieveChangelogAsync(packageId, fromVersion, toVersion, cancellationToken);
        var riskSummary = BuildRiskSummary(fromVersion, fromAssessment, toVersion, toAssessment);

        var prompt = PromptShield.BuildUpgradePrompt(packageId.Original, fromVersion.ToString(), toVersion.ToString(), riskSummary, excerpts);

        var llmNarrative = await languageModel.CompleteAsync(prompt.System, prompt.User, cancellationToken);
        var narrative = llmNarrative ?? BuildTemplatedNarrative(packageId, fromVersion, toVersion, recommendation, toAssessment, excerpts);

        var keyPoints = toAssessment.Findings
            .Select(finding => $"{finding.Level} {finding.Category}: {finding.Message}")
            .ToList();

        return new UpgradeAdviceDto(
            packageId.Original,
            fromVersion.ToString(),
            toVersion.ToString(),
            recommendation.ToString(),
            narrative,
            keyPoints,
            LlmUsed: llmNarrative is not null,
            prompt.User);
    }

    private async Task<(RiskAssessment From, RiskAssessment To)> AssessAsync(
        PackageId packageId, SemVer fromVersion, SemVer toVersion, CancellationToken cancellationToken)
    {
        // from and to may be the same version (single-version packages) — request each once.
        var targets = new List<(PackageId, SemVer)> { (packageId, fromVersion) };
        if (toVersion.ToString() != fromVersion.ToString())
        {
            targets.Add((packageId, toVersion));
        }

        var inputs = await riskRepository.GetRiskInputsAsync(targets, cancellationToken);
        var byVersion = inputs.ToDictionary(input => input.Version.ToString(), PackageRiskScorer.Assess);

        return (
            byVersion.GetValueOrDefault(fromVersion.ToString(), HealthyAssessment),
            byVersion.GetValueOrDefault(toVersion.ToString(), HealthyAssessment));
    }

    private async Task<IReadOnlyList<string>> RetrieveChangelogAsync(
        PackageId packageId, SemVer fromVersion, SemVer toVersion, CancellationToken cancellationToken)
    {
        var query = $"upgrade {packageId.Original} from {fromVersion} to {toVersion}: breaking changes, security, license, deprecation";
        var queryEmbedding = await embeddingGenerator.EmbedAsync(query, cancellationToken);
        var chunks = await changelogRepository.SearchAsync(packageId, queryEmbedding, RetrievedChunks, cancellationToken);
        return chunks.Select(chunk => chunk.Text).ToList();
    }

    private static SemVer Pick(string? raw, SemVer fallback) =>
        !string.IsNullOrWhiteSpace(raw) && SemVer.TryParse(raw, out var version) ? version : fallback;

    private static string BuildRiskSummary(SemVer fromVersion, RiskAssessment from, SemVer toVersion, RiskAssessment to) =>
        $"Current {Describe(fromVersion, from)}\nTarget  {Describe(toVersion, to)}";

    private static string Describe(SemVer version, RiskAssessment assessment) =>
        assessment.Findings.Count == 0
            ? $"{version}: health {assessment.Score.Value}/100 ({assessment.Score.Level}); no findings"
            : $"{version}: health {assessment.Score.Value}/100 ({assessment.Score.Level}); " +
              string.Join("; ", assessment.Findings.Select(f => $"{f.Code}({f.Level})"));

    private static string BuildTemplatedNarrative(
        PackageId packageId, SemVer fromVersion, SemVer toVersion, Recommendation recommendation, RiskAssessment to, IReadOnlyList<string> excerpts)
    {
        var concerns = to.Findings.Count > 0
            ? "Key concerns: " + string.Join("; ", to.Findings.Select(f => f.Message)) + " "
            : "No risk findings on the target version. ";

        var notes = excerpts.Count > 0 ? "Relevant changelog notes were retrieved." : "No changelog notes were indexed.";

        return $"Upgrading {packageId.Original} {fromVersion} -> {toVersion} is rated {recommendation}. " +
               $"Target health {to.Score.Value}/100 ({to.Score.Level}). {concerns}{notes} " +
               "(Templated summary — configure an LLM key for an AI-written narrative.)";
    }
}
