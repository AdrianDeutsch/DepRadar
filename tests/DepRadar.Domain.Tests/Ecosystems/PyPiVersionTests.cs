using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class PyPiVersionTests
{
    [Theory]
    // pad short releases to major.minor.patch
    [InlineData("1", "1.0.0")]
    [InlineData("1.4", "1.4.0")]
    [InlineData("2.31.0", "2.31.0")]
    // fourth segment maps onto NuGet's revision
    [InlineData("1.4.2.3", "1.4.2.3")]
    // zero epoch is dropped
    [InlineData("0!1.2.3", "1.2.3")]
    public void Parses_final_releases(string input, string expected)
    {
        PyPiVersion.TryParse(input, out var version).ShouldBeTrue();
        version.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("2.0b1")]      // pre-release
    [InlineData("1.0rc1")]     // release candidate
    [InlineData("1.0.post1")]  // post-release
    [InlineData("1.0.dev0")]   // dev-release
    [InlineData("1.0+local")]  // local version
    [InlineData("1!2.0")]      // non-zero epoch
    [InlineData("")]
    public void Rejects_non_final_releases(string input)
    {
        PyPiVersion.TryParse(input, out _).ShouldBeFalse();
    }
}

public sealed class PyPiNameTests
{
    [Theory]
    [InlineData("Flask", "flask")]
    [InlineData("python_dateutil", "python-dateutil")]
    [InlineData("python.dateutil", "python-dateutil")]
    [InlineData("Jinja2", "jinja2")]
    [InlineData("zope.interface", "zope-interface")]
    [InlineData("  Requests  ", "requests")]
    public void Normalizes_to_pep503_canonical_form(string input, string expected)
    {
        PyPiName.Normalize(input).ShouldBe(expected);
    }
}
