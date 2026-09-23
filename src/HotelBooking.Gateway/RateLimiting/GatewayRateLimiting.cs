using System.Globalization;
using System.Threading.RateLimiting;

using HotelBooking.Gateway.Observability;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using Yarp.ReverseProxy.Model;

namespace HotelBooking.Gateway.RateLimiting;

public static class GatewayRateLimiting
{
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .Validate(
                options => options.Auth.IsPositive && options.Global.IsPositive,
                "Every rate limit must have a permit count and a window greater than zero.")
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = RejectAsync;

            limiter.AddPolicy(
                RateLimitPolicies.Auth,
                context => Window(context, options => options.Auth));

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => Window(context, options => options.Global));
        });

        return services;
    }

    private static RateLimitPartition<string> Window(
        HttpContext context,
        Func<RateLimitOptions, WindowLimit> select)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(key);
        }

        var limit = select(options);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit.Permit,
            Window = limit.Window,
            QueueLimit = 0
        });
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var route = httpContext.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.RouteId ?? "none";

        GatewayObservability.RateLimitRejections.Add(1, new KeyValuePair<string, object?>("route", route));

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = { Status = StatusCodes.Status429TooManyRequests }
            });
    }
}
