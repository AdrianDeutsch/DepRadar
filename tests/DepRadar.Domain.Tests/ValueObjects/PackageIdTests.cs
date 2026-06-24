using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.ValueObjects;

public sealed class PackageIdTests
{
    [Fact]
    public void Create_normalizes_value_but_preserves_original_casing()
    {
        var id = PackageId.Create("Newtonsoft.Json");

        id.Value.ShouldBe("newtonsoft.json");
        id.Original.ShouldBe("Newtonsoft.Json");
    }

    [Fact]
    public void Equality_is_case_insensitive()
    {
        PackageId.Create("Newtonsoft.Json").ShouldBe(PackageId.Create("newtonsoft.json"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/id")]
    [InlineData("white space")]
    public void Create_rejects_invalid_ids(string raw)
    {
        Should.Throw<ArgumentException>(() => PackageId.Create(raw));
    }

    [Fact]
    public void ToString_returns_original_casing()
    {
        PackageId.Create("Serilog.Sinks.Console").ToString().ShouldBe("Serilog.Sinks.Console");
    }
}
