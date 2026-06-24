namespace DepRadar.Domain.Risk;

/// <summary>The result of scoring a package: an aggregate score and the findings behind it.</summary>
/// <param name="Score">The aggregate health score and overall level.</param>
/// <param name="Findings">The explainable findings that produced the score.</param>
public sealed record RiskAssessment(HealthScore Score, IReadOnlyList<RiskFinding> Findings);
