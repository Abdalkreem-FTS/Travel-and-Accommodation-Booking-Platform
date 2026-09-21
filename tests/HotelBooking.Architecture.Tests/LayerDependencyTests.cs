using System.Reflection;

using NetArchTest.Rules;

namespace HotelBooking.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule
/// </summary>
public sealed class LayerDependencyTests
{
    // private const string DomainNamespace = "HotelBooking.Domain";
    private const string ApplicationNamespace = "HotelBooking.Application";
    private const string InfrastructureNamespace = "HotelBooking.Infrastructure";
    private const string ApiNamespace = "HotelBooking.Api";

    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string Redis = "StackExchange.Redis";
    private const string AspNetCore = "Microsoft.AspNetCore";

    public static TheoryData<string> DomainForbidden =>
    [
        ApplicationNamespace, InfrastructureNamespace, ApiNamespace,
        EfCore, Redis, AspNetCore,
    ];

    public static TheoryData<string> ApplicationForbidden =>
    [
        InfrastructureNamespace, ApiNamespace,
        EfCore, Redis, AspNetCore,
    ];

    public static TheoryData<string> InfrastructureForbidden => [ApiNamespace];

    [Theory]
    [MemberData(nameof(DomainForbidden))]
    public void Domain_ForEachForbiddenTarget_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Domain.AssemblyReference.Assembly, forbidden);

    [Theory]
    [MemberData(nameof(ApplicationForbidden))]
    public void Application_ForEachInfrastructureOrDeliveryTarget_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Application.AssemblyReference.Assembly, forbidden);

    [Theory]
    [MemberData(nameof(InfrastructureForbidden))]
    public void Infrastructure_ForEachHostProject_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Infrastructure.AssemblyReference.Assembly, forbidden);

    private static void AssertNoDependency(Assembly assembly, string forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"{assembly.GetName().Name} must not depend on {forbidden}. Offending types: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
