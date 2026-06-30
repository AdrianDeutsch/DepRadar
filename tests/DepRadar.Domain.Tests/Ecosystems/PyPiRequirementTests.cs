using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class PyPiRequirementTests
{
    [Theory]
    [InlineData("urllib3 (>=1.21.1,<1.27)", "urllib3", ">=1.21.1,<1.27", false)]
    [InlineData("certifi>=2017.4.17", "certifi", ">=2017.4.17", false)]
    [InlineData("chardet (>=3.0.2,<5); python_version < '3'", "chardet", ">=3.0.2,<5", false)]
    [InlineData("requests[security] (>=2.0)", "requests", ">=2.0", false)]
    [InlineData("PySocks (>=1.5.6,!=1.5.7); extra == 'socks'", "PySocks", ">=1.5.6,!=1.5.7", true)]
    [InlineData("idna", "idna", "", false)]
    public void Parses_requires_dist_entries(string entry, string name, string specifier, bool optional)
    {
        PyPiRequirement.TryParse(entry, out var dependency).ShouldBeTrue();
        dependency.Name.ShouldBe(name);
        dependency.Specifier.ShouldBe(specifier);
        dependency.Optional.ShouldBe(optional);
    }

    [Fact]
    public void Rejects_blank_entries()
    {
        PyPiRequirement.TryParse("   ", out _).ShouldBeFalse();
    }
}
