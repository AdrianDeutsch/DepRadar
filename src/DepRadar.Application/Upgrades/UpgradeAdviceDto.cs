namespace DepRadar.Application.Upgrades;

/// <summary>
/// API-facing upgrade assessment. <see cref="Prompt"/> exposes the exact (shielded)
/// prompt an LLM would receive, so the AI wiring is transparent even keyless.
/// </summary>
public sealed record UpgradeAdviceDto(
    string PackageId,
    string FromVersion,
    string ToVersion,
    string Recommendation,
    string Narrative,
    IReadOnlyList<string> KeyPoints,
    bool LlmUsed,
    string Prompt);
