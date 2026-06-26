using DepRadar.Application.Badges;
using DepRadar.Domain.Risk;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Badges;

public sealed class BadgeRendererTests
{
    [Fact]
    public void Renders_a_green_healthy_badge()
    {
        var svg = BadgeRenderer.RenderHealth(95, RiskLevel.None);

        svg.ShouldStartWith("<svg");
        svg.ShouldContain("DepRadar");
        svg.ShouldContain("95 healthy");
        svg.ShouldContain("#4c1"); // green
    }

    [Theory]
    [InlineData(RiskLevel.Medium, "caution")]
    [InlineData(RiskLevel.High, "risky")]
    [InlineData(RiskLevel.Critical, "critical")]
    public void Renders_the_verdict_for_each_level(RiskLevel level, string verdict)
    {
        BadgeRenderer.RenderHealth(40, level).ShouldContain($"40 {verdict}");
    }

    [Fact]
    public void Renders_a_neutral_badge_when_unscanned()
    {
        BadgeRenderer.RenderUnknown().ShouldContain("not scanned");
    }
}
