using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace HotelBooking.Api.Observability;

public static class ObservabilityExtensions
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string ServiceNameKey = "OTEL_SERVICE_NAME";

    public static IHostApplicationBuilder AddApiObservability(this IHostApplicationBuilder builder)
    {
        var otlpEndpoint = OtlpEndpoint(builder.Configuration);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName(builder))
                .AddAttributes(ResourceAttributes(builder.Environment)))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options => options.Filter = IsTraceworthy)
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options => options.RecordException = true)
                    .AddRedisInstrumentation()
                    .AddSource(Application.Telemetry.ActivitySourceName);

                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddMeter(Application.Telemetry.MeterName);

                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }

    public static void ConfigureSerilog(HostBuilderContext context, LoggerConfiguration logger)
    {
        logger.ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter());

        var otlpEndpoint = OtlpEndpoint(context.Configuration);

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

            options.ResourceAttributes = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["service.name"] = ServiceName(context.Configuration, context.HostingEnvironment),
            };

            foreach (var (key, value) in ResourceAttributes(context.HostingEnvironment))
            {
                options.ResourceAttributes[key] = value;
            }
        });
    }

    private static Dictionary<string, object> ResourceAttributes(IHostEnvironment environment) =>
        new(StringComparer.Ordinal)
        {
            ["deployment.environment"] = environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        };

    private static bool IsTraceworthy(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static string? OtlpEndpoint(IConfiguration configuration)
    {
        var endpoint = configuration[OtlpEndpointKey];

        return string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
    }

    private static string ServiceName(IHostApplicationBuilder builder) =>
        ServiceName(builder.Configuration, builder.Environment);

    private static string ServiceName(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[ServiceNameKey];

        return string.IsNullOrWhiteSpace(configured) ? environment.ApplicationName : configured;
    }
}
