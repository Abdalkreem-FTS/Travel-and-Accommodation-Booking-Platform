using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelBooking.Api.Endpoints;

public static class EndpointExtensions
{
    public const string RoutePrefix = "/api";

    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        ServiceDescriptor[] endpoints = [.. AssemblyReference.Assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                && type.IsAssignableTo(typeof(IEndpoint)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))];

        services.TryAddEnumerable(endpoints);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(RoutePrefix);

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }
}
