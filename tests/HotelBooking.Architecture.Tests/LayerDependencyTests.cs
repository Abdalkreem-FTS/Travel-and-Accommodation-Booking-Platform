using System.Reflection;

using NetArchTest.Rules;

namespace HotelBooking.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule
/// </summary>
public sealed class LayerDependencyTests
{
    private const string ApplicationNamespace = "HotelBooking.Application";
    private const string InfrastructureNamespace = "HotelBooking.Infrastructure";
    private const string ApiNamespace = "HotelBooking.Api";
    private const string WorkersNamespace = "HotelBooking.Workers";
    private const string DomainNamespace = "HotelBooking.Domain";
    private const string GatewayNamespace = "HotelBooking.Gateway";

    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string Redis = "StackExchange.Redis";
    private const string AspNetCore = "Microsoft.AspNetCore";
    private const string RateLimiting = "Microsoft.AspNetCore.RateLimiting";

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

    public static TheoryData<string> InfrastructureForbidden => [ApiNamespace, WorkersNamespace];

    public static TheoryData<string> WorkersForbidden => [ApiNamespace, AspNetCore];

    public static TheoryData<string> GatewayForbidden =>
    [
        DomainNamespace, ApplicationNamespace, InfrastructureNamespace, ApiNamespace, WorkersNamespace,
    ];

    public static TheoryData<string> ApiForbidden => [WorkersNamespace, GatewayNamespace, RateLimiting];

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

    [Theory]
    [MemberData(nameof(WorkersForbidden))]
    public void Workers_ForEachWebTarget_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Workers.AssemblyReference.Assembly, forbidden);

    [Theory]
    [MemberData(nameof(ApiForbidden))]
    public void Api_ForEachOtherHostOrASecondRateLimiter_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Api.AssemblyReference.Assembly, forbidden);

    [Theory]
    [MemberData(nameof(GatewayForbidden))]
    public void Gateway_ForEachSolutionLayer_DoesNotDependOnIt(string forbidden) =>
        AssertNoDependency(Gateway.AssemblyReference.Assembly, forbidden);

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
