using DepRadar.Application.Policy;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Policy;

public sealed class PolicyFileTests
{
    [Fact]
    public void Parses_a_full_policy_document()
    {
        const string Json = """
            {
              // a stricter gate for a proprietary app
              "failOn": "medium",
              "allowDeprecated": false,
              "forbiddenLicenses": ["copyleft", "unknown"],
              "ignore": ["Newtonsoft.Json", "  ", "Some.Accepted.Pkg"]
            }
            """;

        var policy = PolicyFile.Parse(Json);

        policy.FailOn.ShouldBe(RiskLevel.Medium);
        policy.AllowDeprecated.ShouldBeFalse();
        policy.ForbiddenLicenses.ShouldBe([LicenseCategory.Copyleft, LicenseCategory.Unknown], ignoreOrder: true);
        policy.IgnoredPackages.ShouldContain("newtonsoft.json"); // case-insensitive
        policy.IgnoredPackages.Count.ShouldBe(2); // blank entry dropped
    }

    [Fact]
    public void Falls_back_to_lenient_defaults_for_an_empty_document()
    {
        var policy = PolicyFile.Parse("{}");

        policy.FailOn.ShouldBe(RiskLevel.High);
        policy.AllowDeprecated.ShouldBeTrue();
        policy.ForbiddenLicenses.ShouldBeEmpty();
        policy.IgnoredPackages.ShouldBeEmpty();
    }

    [Fact]
    public void Throws_a_format_exception_on_garbage()
    {
        Should.Throw<FormatException>(() => PolicyFile.Parse("not json"));
    }
}
