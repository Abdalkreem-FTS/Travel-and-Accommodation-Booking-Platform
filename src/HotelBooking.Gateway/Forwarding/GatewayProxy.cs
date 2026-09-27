using Serilog;

using Yarp.ReverseProxy.Transforms;

namespace HotelBooking.Gateway.Forwarding;

public static class GatewayProxy
{
    private const string UpstreamHeader = "X-Upstream-Instance";

    public static IServiceCollection AddGatewayProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var exposeUpstream = configuration.GetValue<bool>("Gateway:ExposeUpstream");

        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transforms => transforms.AddResponseTransform(context =>
            {
                var destination = context.HttpContext.GetReverseProxyFeature().ProxiedDestination;

                if (destination is null)
                {
                    return ValueTask.CompletedTask;
                }

                context.HttpContext.RequestServices
                    .GetRequiredService<IDiagnosticContext>()
                    .Set("Upstream", destination.DestinationId);

                if (exposeUpstream)
                {
                    context.HttpContext.Response.Headers[UpstreamHeader] = destination.DestinationId;
                }

                return ValueTask.CompletedTask;
            }));

        return services;
    }
}
