using HotelBooking.Infrastructure.Observability;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace HotelBooking.Api.Observability;

public static class ApiObservability
{
    public static IHostApplicationBuilder AddApiObservability(this IHostApplicationBuilder builder) =>
        builder.AddObservability(
            tracing => tracing.AddAspNetCoreInstrumentation(options => options.Filter = IsTraceworthy),
            metrics => metrics.AddAspNetCoreInstrumentation());

    private static bool IsTraceworthy(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
