using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace HotelBooking.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string ServiceNameKey = "OTEL_SERVICE_NAME";

    public static IHostApplicationBuilder AddObservability(
        this IHostApplicationBuilder builder,
        Action<TracerProviderBuilder>? tracing = null,
        Action<MeterProviderBuilder>? metrics = null)
    {
        var otlpEndpoint = OtlpEndpoint(builder.Configuration);

        builder.Services.AddSerilog(
            (provider, logger) => ConfigureSerilog(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IHostEnvironment>(),
                logger));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName(builder.Configuration, builder.Environment))
                .AddAttributes(ResourceAttributes(builder.Environment)))
            .WithTracing(tracer =>
            {
                tracing?.Invoke(tracer);

                tracer.AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(options => options.RecordException = true)
                    .AddRedisInstrumentation()
                    .AddSource(Application.Telemetry.ActivitySourceName)
                    .AddSource(MailKit.Telemetry.SmtpClient.ActivitySourceName);

                if (otlpEndpoint is not null)
                {
                    tracer.AddOtlpExporter();
                }
            })
            .WithMetrics(meter =>
            {
                metrics?.Invoke(meter);

                meter.AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddMeter(Application.Telemetry.MeterName);

                if (otlpEndpoint is not null)
                {
                    meter.AddOtlpExporter();
                }
            });

        return builder;
    }

    private static void ConfigureSerilog(
        IConfiguration configuration,
        IHostEnvironment environment,
        LoggerConfiguration logger)
    {
        logger.ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter());

        var otlpEndpoint = OtlpEndpoint(configuration);

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
                ["service.name"] = ServiceName(configuration, environment),
            };

            foreach (var (key, value) in ResourceAttributes(environment))
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
