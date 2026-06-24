using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace DepRadar.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture dependency rule in CI: dependencies point
/// strictly inward (Domain ← Application ← Infrastructure), and the core layers
/// stay free of persistence and commercially-licensed libraries.
/// </summary>
public sealed class LayeringTests
{
    private const string DomainNamespace = "DepRadar.Domain";
    private const string ApplicationNamespace = "DepRadar.Application";
    private const string InfrastructureNamespace = "DepRadar.Infrastructure";

    private static readonly Assembly DomainAssembly = typeof(Domain.Packages.Package).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Domain_should_be_free_of_infrastructure_concerns()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Microsoft.Extensions.DependencyInjection")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Application_should_not_depend_on_persistence_libraries()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void No_layer_should_reference_the_commercial_MediatR_package()
    {
        // DepRadar eats its own dog food: the core uses a hand-rolled mediator, so
        // the commercially-licensed MediatR must not appear anywhere.
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("MediatR")
                .GetResult();

            AssertSuccess(result);
        }
    }

    [Fact]
    public void NuGet_Versioning_is_confined_to_infrastructure()
    {
        // ADR 0003/0004: the NuGet range library is an Infrastructure detail and must
        // not leak into the dependency-free Domain or the Application layer.
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("NuGet")
                .GetResult();

            AssertSuccess(result);
        }
    }

    private static void AssertSuccess(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            result.IsSuccessful
                ? string.Empty
                : $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
