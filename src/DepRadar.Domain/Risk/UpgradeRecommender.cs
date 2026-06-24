namespace DepRadar.Domain.Risk;

/// <summary>
/// Pure decision logic for an upgrade recommendation, derived from the risk of the
/// current and target versions. Deterministic and unit-tested — independent of any LLM.
/// </summary>
public static class UpgradeRecommender
{
    /// <summary>Recommends whether to upgrade from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Recommendation Recommend(RiskAssessment from, RiskAssessment to)
    {
        if (to.Score.Level >= RiskLevel.Critical)
        {
            return Recommendation.Avoid;
        }

        // The target is seriously risky, or it regresses the current health.
        if (to.Score.Level >= RiskLevel.High || to.Score.Value < from.Score.Value)
        {
            return Recommendation.Caution;
        }

        return Recommendation.Proceed;
    }
}
