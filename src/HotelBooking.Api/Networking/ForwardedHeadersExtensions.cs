using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace HotelBooking.Api.Networking;

public static class ForwardedHeadersExtensions
{
    private const string GatewayAddressKey = "ForwardedHeaders:GatewayAddress";

    public static IServiceCollection AddGatewayForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ForwardLimit = 1;

                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();

                var gatewayAddress = configuration[GatewayAddressKey];

                if (string.IsNullOrWhiteSpace(gatewayAddress))
                {
                    options.ForwardedHeaders = ForwardedHeaders.None;

                    return;
                }

                options.KnownProxies.Add(IPAddress.Parse(gatewayAddress));
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            })
            .ValidateOnStart();

        return services;
    }
}
