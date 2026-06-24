namespace DepRadar.Domain.Risk;

/// <summary>
/// An aggregate health score (0–100, higher is healthier) derived from a set of
/// findings, plus the overall <see cref="RiskLevel"/> (the worst finding's level).
/// </summary>
public readonly record struct HealthScore(int Value, RiskLevel Level)
{
    private const int Max = 100;

    // How many points each finding deducts, by severity.
    private static int Penalty(RiskLevel level) => level switch
    {
        RiskLevel.Critical => 50,
        RiskLevel.High => 30,
        RiskLevel.Medium => 15,
        RiskLevel.Low => 5,
        _ => 0,
    };

    /// <summary>
    /// Computes the score from findings: start at 100, deduct each finding's penalty
    /// (floored at 0); the overall level is the worst finding's level.
    /// </summary>
    public static HealthScore FromFindings(IReadOnlyCollection<RiskFinding> findings)
    {
        var deduction = findings.Sum(finding => Penalty(finding.Level));
        var value = Math.Clamp(Max - deduction, 0, Max);
        var level = findings.Count == 0 ? RiskLevel.None : findings.Max(finding => finding.Level);
        return new HealthScore(value, level);
    }
}
