using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class LookalikeTests
{
    [Theory]
    // classic typo classes: substitution, omission, transposition — per ecosystem list
    [InlineData("requets", "pypi", "requests")]
    [InlineData("lodahs", "npm", "lodash")]
    [InlineData("expres", "npm", "express")]
    [InlineData("nunpy", "pypi", "numpy")]
    [InlineData("serd", "cargo", "serde")]
    // distance 2 only for long names
    [InlineData("beatifulsoup4", "pypi", "beautifulsoup4")]
    public void Flags_close_neighbors_of_well_known_packages(string name, string ecosystem, string expectedTarget)
    {
        var known = ecosystem switch
        {
            "npm" => KnownPackages.Npm,
            "cargo" => KnownPackages.Cargo,
            _ => KnownPackages.PyPi,
        };

        Lookalike.FindTarget(name, known).ShouldBe(expectedTarget);
    }

    [Theory]
    [InlineData("requests")] // IS the well-known package
    [InlineData("lodash")]
    [InlineData("left-pad")] // unrelated, distance > threshold
    [InlineData("abc")]      // too short to judge
    public void Does_not_flag_exact_known_names_short_names_or_distant_ones(string name)
    {
        Lookalike.FindTarget(name, Merge(KnownPackages.Npm, KnownPackages.PyPi)).ShouldBeNull();
    }

    [Fact]
    public void Distance_two_requires_a_long_name()
    {
        // "reqests" (D1) flags; "rqests" is D2 at 6 chars — too short for the looser threshold.
        Lookalike.FindTarget("reqests", KnownPackages.PyPi).ShouldBe("requests");
        Lookalike.FindTarget("rqests", KnownPackages.PyPi).ShouldBeNull();
    }

    [Theory]
    [InlineData("abcd", "abdc", 1)]  // transposition counts as one edit
    [InlineData("kitten", "sitting", 3)]
    [InlineData("same", "same", 0)]
    public void Damerau_levenshtein_computes_edit_distances(string left, string right, int expected)
    {
        Lookalike.DamerauLevenshtein(left, right, max: 5).ShouldBe(expected);
    }

    private static IReadOnlyList<string> Merge(IReadOnlyList<string> first, IReadOnlyList<string> second) =>
        [.. first, .. second];
}
