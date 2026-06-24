using DepRadar.Domain.Risk;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Risk;

public sealed class UpgradeRecommenderTests
{
    [Fact]
    public void Avoids_a_target_with_critical_risk()
    {
        UpgradeRecommender.Recommend(Of(), Of(RiskLevel.Critical)).ShouldBe(Recommendation.Avoid);
    }

    [Fact]
    public void Cautions_when_the_target_carries_high_risk()
    {
        UpgradeRecommender.Recommend(Of(), Of(RiskLevel.High)).ShouldBe(Recommendation.Caution);
    }

    [Fact]
    public void Cautions_when_the_target_regresses_current_health()
    {
        // Target only Medium, but it is less healthy than the (clean) current version.
        UpgradeRecommender.Recommend(Of(), Of(RiskLevel.Medium)).ShouldBe(Recommendation.Caution);
    }

    [Fact]
    public void Proceeds_for_a_healthy_upgrade()
    {
        UpgradeRecommender.Recommend(Of(), Of()).ShouldBe(Recommendation.Proceed);
    }

    private static RiskAssessment Of(params RiskLevel[] levels)
    {
        var findings = levels
            .Select(level => new RiskFinding(RiskCategory.Security, level, "X", "test"))
            .ToList();

        return new RiskAssessment(HealthScore.FromFindings(findings), findings);
    }
}
