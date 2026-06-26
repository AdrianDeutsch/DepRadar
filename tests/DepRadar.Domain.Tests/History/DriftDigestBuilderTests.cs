using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.History;

public sealed class DriftDigestBuilderTests
{
    [Fact]
    public void Renders_drifted_roots_worst_first_with_their_events()
    {
        var mild = Report("alpha", -20, new DriftEvent("alpha", DriftEventKind.BecameDeprecated, "1.0.0 is now deprecated", RiskLevel.High));
        var severe = Report("beta", -60, new DriftEvent("dep", DriftEventKind.BecameVulnerable, "new advisory GHSA-x", RiskLevel.High));

        var markdown = DriftDigestBuilder.Render([mild, severe], DateTimeOffset.UnixEpoch);

        markdown.ShouldContain("# DepRadar drift digest");
        markdown.ShouldContain("## beta");
        markdown.ShouldContain("## alpha");
        markdown.ShouldContain("GHSA-x");
        markdown.IndexOf("## beta", StringComparison.Ordinal)
            .ShouldBeLessThan(markdown.IndexOf("## alpha", StringComparison.Ordinal)); // -60 before -20
    }

    [Fact]
    public void Reports_no_drift_when_nothing_changed()
    {
        var clean = Report("clean", 0);

        DriftDigestBuilder.Render([clean], DateTimeOffset.UnixEpoch).ShouldContain("No drift detected");
    }

    private static DriftReport Report(string root, int delta, params DriftEvent[] events) =>
        new(PackageId.Create(root), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), delta, events);
}
