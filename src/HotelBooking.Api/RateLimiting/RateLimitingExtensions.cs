using System.Globalization;
using System.Threading.RateLimiting;
using HotelBooking.Api.Authentication;
using HotelBooking.Api.Errors;
using HotelBooking.Application;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace HotelBooking.Api.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(
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
        var key = PartitionKey(context);

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

    private static string PartitionKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? $"user:{context.User.GetUserId()}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        Telemetry.RateLimitRejections.Add(
            1, new KeyValuePair<string, object?>("endpoint", context.HttpContext.Request.Path.Value ?? "/"));

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return new ValueTask(
            CustomResults.Problem(RequestErrors.TooManyRequests).ExecuteAsync(context.HttpContext));
    }
}
