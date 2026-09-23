using System.Diagnostics.Metrics;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace HotelBooking.Gateway.Observability;

public static class GatewayObservability
{
    private const string MeterName = "HotelBooking.Gateway";

    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string ServiceNameKey = "OTEL_SERVICE_NAME";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RateLimitRejections =
        Meter.CreateCounter<long>(
            "http.ratelimit.rejected",
            description: "Requests refused by a gateway rate limit, tagged by route.");

    public static IHostApplicationBuilder AddGatewayObservability(this IHostApplicationBuilder builder)
    {
        var otlpEndpoint = OtlpEndpoint(builder.Configuration);
        var serviceName = ServiceName(builder.Configuration, builder.Environment);

        builder.Services.AddSerilog((_, logger) =>
        {
            logger.ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new CompactJsonFormatter());

            if (otlpEndpoint is null)
            {
                return;
            }

            logger.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
                options.IncludedData = IncludedData.TraceIdField
                                       | IncludedData.SpanIdField
                                       | IncludedData.MessageTemplateTextAttribute
                                       | IncludedData.SpecRequiredResourceAttributes;
                options.ResourceAttributes = new Dictionary<string, object>(ResourceAttributes(builder.Environment))
                {
                    ["service.name"] = serviceName
                };
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(ResourceAttributes(builder.Environment)))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options => options.Filter = IsTraceworthy)
                    .AddHttpClientInstrumentation();

                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(MeterName);

                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }

    private static bool IsTraceworthy(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object> ResourceAttributes(IHostEnvironment environment) =>
        new(StringComparer.Ordinal)
        {
            ["deployment.environment"] = environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        };

    private static string? OtlpEndpoint(IConfiguration configuration)
    {
        var endpoint = configuration[OtlpEndpointKey];

        return string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
    }

    private static string ServiceName(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[ServiceNameKey];

        return string.IsNullOrWhiteSpace(configured) ? environment.ApplicationName : configured;
    }
}
