using DepRadar.Application.Remediation;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Remediation;

public sealed class RemediationCalculatorTests
{
    [Fact]
    public void Picks_the_highest_fixed_version_so_every_advisory_is_cleared()
    {
        RemediationCalculator.SafeVersion(["12.0.1", "13.0.1", "12.3.0"]).ShouldBe("13.0.1");
    }

    [Fact]
    public void Returns_null_when_any_advisory_has_no_known_fix()
    {
        RemediationCalculator.SafeVersion(["13.0.1", null]).ShouldBeNull();
    }

    [Fact]
    public void Returns_null_for_an_empty_set()
    {
        RemediationCalculator.SafeVersion([]).ShouldBeNull();
    }
}
